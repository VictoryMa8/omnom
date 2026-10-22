using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omnom.Api.Data;

namespace Omnom.Api.Services;

public class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterService> _logger;
    public const string Model = "inclusionai/ling-3.1-flash";
    private sealed record FreeModel(string Id, bool SupportsJsonObject);
    private static readonly FreeModel[] FreeModels =
    [
        new(Model, false),
        new("apodex/apodex-1.1-mini:free", true)
    ];

    public OpenRouterService(
        HttpClient httpClient,
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public List<string> GetAvailableFreeModels() => FreeModels.Select(m => m.Id).ToList();

    public async Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? requestedModel = null, CancellationToken cancellationToken = default)
    {
        using var diagnosticScope = ParseDiagnostics.Begin(cancellationToken);
        // AI configuration belongs to the server; saved client settings cannot override it.
        var apiKey = _configuration["OPENROUTER_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ParseStageException("missing-configuration");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ParseLimits.AiWait);
        var rateLimited = false;
        foreach (var model in FreeModels)
        {
            budget.Token.ThrowIfCancellationRequested();
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            ModelCompletion? completion = null;
            try
            {
                completion = await TryModelCompletionAsync(apiKey.Trim(), model, systemPrompt, userPrompt, budget.Token);
                if (MealParserService.ParseLlmJson(completion.Content, userPrompt) == null)
                    throw new ParseStageException("invalid-output");
                RecordAttempt("success");
                return completion.Content;
            }
            catch (Exception ex)
            {
                if (ex is CompletionFailure failure) completion = failure.Completion;
                var category = ex switch
                {
                    CompletionFailure classified => classified.Category,
                    ProviderRejection rejected when rejected.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) => rejected.Category,
                    _ => ParseDiagnostics.Failure(ex, ParseDiagnostics.CallerToken)
                };
                RecordAttempt(category, (ex as HttpRequestException)?.StatusCode is { } status ? (int)status : null);
                if (ex is OperationCanceledException && budget.IsCancellationRequested
                    || ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized }) throw;
                rateLimited |= ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };
            }

            void RecordAttempt(string outcome, int? status = null) => ParseDiagnostics.Write(_logger, new(
                ParseDiagnostics.CorrelationId, "model-attempt", outcome, elapsed.Elapsed.TotalMilliseconds,
                model.Id, completion?.ActualModel, status ?? completion?.HttpStatus,
                completion?.PromptTokens, completion?.CompletionTokens, completion?.Cost));
        }
        throw new FreeModelsUnavailableException(rateLimited);
    }

    private sealed record ModelCompletion(string Content, string? ActualModel, int HttpStatus, int? PromptTokens, int? CompletionTokens, double? Cost);

    private async Task<ModelCompletion> TryModelCompletionAsync(string apiKey, FreeModel model, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Keep the primary bounded, but give the backup the remaining shared
        // budget. A quick rate-limit rejection should leave time for its answer.
        if (model.Id == Model) timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://github.com/victoryma/omnom");
        request.Headers.Add("X-Title", "Omnom Lifter Macro Tracker");

        var payload = new Dictionary<string, object>
        {
            ["model"] = model.Id,
            ["messages"] = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            ["temperature"] = 0.1,
            // Reasoning and the JSON answer share this budget. Keep reasoning private.
            ["max_tokens"] = 6000,
            ["reasoning"] = new { enabled = true, exclude = true },
            // Fail closed if today's free preview gains a price. Applies to every backup too.
            ["provider"] = new { require_parameters = true, max_price = new { prompt = 0, completion = 0, request = 0, image = 0 } }
        };
        // Apodex advertises structured outputs in the catalog, but its provider
        // rejects json_schema. Use its verified JSON-object mode and validate locally.
        if (model.SupportsJsonObject)
            payload["response_format"] = new { type = "json_object" };

        var jsonPayload = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new HttpRequestException("OpenRouter request failed.", null, response.StatusCode);
            throw new ProviderRejection(response.StatusCode, ClassifyProviderError(responseBody));
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ParseStageException("invalid-output");
        double? reportedCost = null;
        int? promptTokens = null, completionTokens = null;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            if (usage.TryGetProperty("cost", out var cost) && cost.ValueKind == JsonValueKind.Number
                && cost.TryGetDouble(out var value) && double.IsFinite(value) && value >= 0) reportedCost = value;
            promptTokens = TokenCount(usage, "prompt_tokens");
            completionTokens = TokenCount(usage, "completion_tokens");
        }
        // Only known server-owned model IDs may appear in logs; arbitrary provider
        // strings could echo private input. Missing model metadata stays unknown.
        var actualModel = root.TryGetProperty("model", out var modelName) && modelName.ValueKind == JsonValueKind.String
            ? FreeModels.FirstOrDefault(m => m.Id == modelName.GetString())?.Id ?? "other" : null;
        var completion = new ModelCompletion("", actualModel, (int)response.StatusCode, promptTokens, completionTokens, reportedCost);
        if (reportedCost > 0) throw new CompletionFailure("nonzero-cost", completion);
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.ValueKind != JsonValueKind.Object) throw new CompletionFailure("invalid-output", completion);
            if (firstChoice.TryGetProperty("finish_reason", out var finishReason) && finishReason.ValueKind == JsonValueKind.String)
            {
                var failure = finishReason.GetString() switch
                {
                    "length" => "truncated-output", "content_filter" => "filtered-output", "error" => "provider-error", _ => null
                };
                if (failure != null) throw new CompletionFailure(failure, completion);
            }
            if (firstChoice.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(content.GetString()))
                return completion with { Content = content.GetString()! };
        }
        throw new CompletionFailure("invalid-output", completion);
    }

    private static int? TokenCount(JsonElement usage, string field) => usage.TryGetProperty(field, out var count)
        && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var value) && value >= 0 ? value : null;

    private sealed class CompletionFailure(string category, ModelCompletion completion) : Exception
    {
        public string Category { get; } = category;
        public ModelCompletion Completion { get; } = completion;
    }

    private sealed class ProviderRejection(HttpStatusCode status, string category)
        : HttpRequestException("OpenRouter request failed.", null, status)
    {
        public string Category { get; } = category;
    }

    // Never log response bodies, credentials, or meal text. These fixed categories
    // still distinguish account settings from capacity and request failures.
    private static string ClassifyProviderError(string body)
    {
        if (body.Contains("data policy", StringComparison.OrdinalIgnoreCase) || body.Contains("privacy", StringComparison.OrdinalIgnoreCase))
            return "account-privacy-policy";
        if (body.Contains("credit", StringComparison.OrdinalIgnoreCase) || body.Contains("balance", StringComparison.OrdinalIgnoreCase))
            return "account-credits";
        if (body.Contains("rate limit", StringComparison.OrdinalIgnoreCase)) return "rate-limited";
        if (body.Contains("parameter", StringComparison.OrdinalIgnoreCase)) return "unsupported-parameters";
        if (body.Contains("endpoint", StringComparison.OrdinalIgnoreCase)) return "no-eligible-endpoint";
        return "http-failure";
    }
}

public sealed class FreeModelsUnavailableException(bool rateLimited) : InvalidOperationException("Free AI models are unavailable. Use the local parser.")
{
    public bool RateLimited { get; } = rateLimited;
}
