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
        // AI configuration belongs to the server; saved client settings cannot override it.
        var apiKey = _configuration["OPENROUTER_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Set OPENROUTER_API_KEY in the server environment or .env file.");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ParseLimits.AiWait);
        var rateLimited = false;
        foreach (var model in FreeModels)
        {
            budget.Token.ThrowIfCancellationRequested();
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var content = await TryModelCompletionAsync(apiKey.Trim(), model, systemPrompt, userPrompt, budget.Token);
                if (MealParserService.ParseLlmJson(content, userPrompt) == null)
                    throw new InvalidOperationException("Free model returned an invalid meal extraction.");
                return content;
            }
            catch (OperationCanceledException) when (budget.IsCancellationRequested) { throw; }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
            catch (Exception ex)
            {
                rateLimited |= ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };
                _logger.LogWarning("Free AI model {Model} failed after {ElapsedMs} ms ({Failure}, HTTP {Status}); trying the next free option.",
                    model.Id, elapsed.ElapsedMilliseconds, SafeFailure(ex), (ex as HttpRequestException)?.StatusCode);
            }
        }
        throw new FreeModelsUnavailableException(rateLimited);
    }

    private async Task<string> TryModelCompletionAsync(string apiKey, FreeModel model, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
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
            throw new HttpRequestException($"OpenRouter request failed ({(int)response.StatusCode}, {ClassifyProviderError(responseBody)}).", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("cost", out var cost)
            && cost.ValueKind == JsonValueKind.Number && cost.GetDouble() > 0)
            throw new InvalidOperationException("OpenRouter reported a nonzero cost despite the free-only price limit.");
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("finish_reason", out var finishReason) &&
                finishReason.GetString() is "length" or "content_filter" or "error")
                throw new InvalidOperationException("OpenRouter did not complete the meal extraction.");
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(content.GetString()))
            {
                var actualModel = root.TryGetProperty("model", out var modelName) ? modelName.GetString() : model.Id;
                _logger.LogInformation("Free AI completion received from {Model}.", actualModel);
                return content.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Unexpected response structure from OpenRouter.");
    }

    // Never log response bodies, credentials, or meal text. These fixed categories
    // still distinguish account settings from capacity and request failures.
    private static string ClassifyProviderError(string body)
    {
        if (body.Contains("data policy", StringComparison.OrdinalIgnoreCase) || body.Contains("privacy", StringComparison.OrdinalIgnoreCase))
            return "account privacy policy";
        if (body.Contains("credit", StringComparison.OrdinalIgnoreCase) || body.Contains("balance", StringComparison.OrdinalIgnoreCase))
            return "account credits";
        if (body.Contains("rate limit", StringComparison.OrdinalIgnoreCase)) return "rate limit";
        if (body.Contains("parameter", StringComparison.OrdinalIgnoreCase)) return "unsupported parameters";
        if (body.Contains("endpoint", StringComparison.OrdinalIgnoreCase)) return "no eligible endpoint";
        return "provider rejection";
    }

    private static string SafeFailure(Exception exception) => exception switch
    {
        HttpRequestException http when http.StatusCode.HasValue => http.Message,
        OperationCanceledException => "model timeout",
        _ => exception.GetType().Name
    };
}

public sealed class FreeModelsUnavailableException(bool rateLimited) : InvalidOperationException("Free AI models are unavailable. Use the local parser.")
{
    public bool RateLimited { get; } = rateLimited;
}
