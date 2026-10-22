using System.Net;
using System.Text.Json;
using Omnom.Api.Data.Entities;
using Omnom.Api.Services;

namespace Omnom.Evaluation;

public sealed class NetworkForbidden : HttpMessageHandler
{
    public int Attempts { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { Attempts++; throw new InvalidOperationException("Offline evaluation attempted network access."); }
}
public sealed class LocalReferences(IUsdaFoodService inner) : IUsdaFoodService
{
    public Task<FoodReference?> FindBestMatchAsync(string query, bool allowRemote = true, CancellationToken cancellationToken = default)
        => inner.FindBestMatchAsync(query, false, cancellationToken);
    public Task<List<FoodReference>> SearchFoodsAsync(string query, int limit = 10) => inner.SearchFoodsAsync(query, limit);
}
public sealed class CannedRouter(MealCase fixture, bool heuristic) : IOpenRouterService
{
    public List<string> GetAvailableFreeModels() => [];
    public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (heuristic) throw new InvalidOperationException("Deliberate offline heuristic path.");
        var response = fixture.StubFormat == "invalid" ? "{}" : fixture.StubResponse.GetRawText();
        return Task.FromResult(fixture.StubFormat == "fenced" ? "```json\n" + response + "\n```" : response);
    }
}

// Caps actual HTTP attempts (including backup attempts), not merely case count.
public sealed class CappedRouterHandler(int cap, HashSet<string> models, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    public int Sent { get; private set; }
    public int WithCost { get; private set; }
    public double CostSum { get; private set; }
    public int MissingCost { get; private set; }
    public string? StopReason { get; private set; }
    public List<string> RequestedModels { get; } = [];
    public List<string> ActualModels { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (StopReason != null) throw new InvalidOperationException("Evaluation stopped: " + StopReason);
        if (Sent >= cap) { StopReason = "request-cap"; throw new InvalidOperationException("Evaluation request cap reached."); }
        if (request.RequestUri?.AbsoluteUri != "https://openrouter.ai/api/v1/chat/completions" || request.Method != HttpMethod.Post)
            throw new InvalidOperationException("Evaluation permits only the configured OpenRouter completion endpoint.");
        using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var model = payload.RootElement.GetProperty("model").GetString()!;
        var prices = payload.RootElement.GetProperty("provider").GetProperty("max_price");
        foreach (var field in new[] { "prompt", "completion", "request", "image" })
            if (prices.GetProperty(field).GetDouble() != 0) throw new InvalidOperationException("Nonzero price limit refused.");
        if (!models.Contains(model)) throw new InvalidOperationException("Unapproved model refused.");
        Sent++; RequestedModels.Add(model);
        HttpResponseMessage response;
        try { response = await base.SendAsync(request, cancellationToken); }
        catch { MissingCost++; throw; }
        if (response.StatusCode == HttpStatusCode.TooManyRequests) StopReason = "provider-quota";
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) StopReason = "provider-auth";
        string body;
        try { body = await response.Content.ReadAsStringAsync(cancellationToken); }
        catch { MissingCost++; response.Dispose(); throw; }
        try
        {
            using var parsed = JsonDocument.Parse(body);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) { MissingCost++; return response; }
            if (parsed.RootElement.TryGetProperty("model", out var actual) && actual.ValueKind == JsonValueKind.String)
                ActualModels.Add(models.Contains(actual.GetString()!) ? actual.GetString()! : "other");
            if (parsed.RootElement.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("cost", out var cost)
                && cost.ValueKind == JsonValueKind.Number && cost.TryGetDouble(out var value) && double.IsFinite(value) && value >= 0)
            { WithCost++; CostSum += value; if (value > 0) StopReason = "nonzero-reported-cost"; }
            else MissingCost++;
        }
        catch (JsonException) { MissingCost++; }
        return response;
    }
}
