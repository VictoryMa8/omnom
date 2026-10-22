using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Omnom.Api.Services;

// Only fixed categories, counts and numeric metadata belong here. Never pass user
// text, response bodies, exception messages, reasoning, URLs or credentials.
public sealed record ParseDiagnostic(
    string CorrelationId, string Stage, string Outcome, double ElapsedMs,
    string? RequestedModel = null, string? ActualModel = null, int? HttpStatus = null,
    int? PromptTokens = null, int? CompletionTokens = null, double? Cost = null,
    int? ExtractedItems = null, int? References = null, int? Estimates = null,
    int? Unresolved = null, string? FallbackCategory = null);

public static class ParseDiagnostics
{
    private sealed record Context(string CorrelationId, CancellationToken CallerToken);
    private static readonly AsyncLocal<Context?> Current = new();
    public static string CorrelationId => Current.Value?.CorrelationId ?? "unscoped";
    public static CancellationToken CallerToken => Current.Value?.CallerToken ?? default;
    public static IDisposable Begin(CancellationToken caller = default)
    {
        var previous = Current.Value;
        Current.Value = previous ?? new Context(Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"), caller);
        return new Scope(previous);
    }
    private sealed class Scope(Context? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
    public static void Write(ILogger logger, ParseDiagnostic diagnostic) => logger.Log(
        LogLevel.Information, new EventId(4004, "ParseDiagnostic"), new DiagnosticLogState(diagnostic), null,
        (state, _) => JsonSerializer.Serialize(state.Diagnostic));

    public static string Failure(Exception error, CancellationToken caller = default) => error switch
    {
        OperationCanceledException when caller.IsCancellationRequested => "cancelled",
        OperationCanceledException or TimeoutException => "timeout",
        ParseStageException stage => stage.Category,
        FreeModelsUnavailableException { RateLimited: true } => "rate-limited",
        FreeModelsUnavailableException => "models-unavailable",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "rate-limited",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } => "authentication",
        HttpRequestException => "http-failure",
        JsonException => "invalid-output",
        _ => "internal-failure"
    };
}

public sealed class ParseStageException(string category) : InvalidOperationException("Parsing stage failed.")
{
    public string Category { get; } = category;
}

public sealed class DiagnosticLogState : List<KeyValuePair<string, object?>>
{
    public ParseDiagnostic Diagnostic { get; }
    public DiagnosticLogState(ParseDiagnostic diagnostic)
    {
        Diagnostic = diagnostic;
        foreach (var property in typeof(ParseDiagnostic).GetProperties())
            Add(new(property.Name, property.GetValue(diagnostic)));
    }
}
