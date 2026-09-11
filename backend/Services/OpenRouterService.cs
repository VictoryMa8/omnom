using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    public const string Model = "z-ai/glm-5.2";

    public OpenRouterService(
        HttpClient httpClient,
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public List<string> GetAvailableFreeModels() => new() { Model };

    public async Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? requestedModel = null, CancellationToken cancellationToken = default)
    {
        // AI configuration belongs to the server; saved client settings cannot override it.
        var apiKey = _configuration["OPENROUTER_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Set OPENROUTER_API_KEY in the server environment or .env file.");

        return await TryModelCompletionAsync(apiKey.Trim(), Model, systemPrompt, userPrompt, cancellationToken);
    }

    private async Task<string> TryModelCompletionAsync(string apiKey, string model, string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ParseLimits.AiWait);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://github.com/victoryma/omnom");
        request.Headers.Add("X-Title", "Omnom Lifter Macro Tracker");

        var payload = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1,
            max_tokens = 2500,
            reasoning = new { enabled = false }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenRouter request failed ({(int)response.StatusCode}): {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        throw new Exception("Unexpected response structure from OpenRouter: " + responseBody);
    }
}
