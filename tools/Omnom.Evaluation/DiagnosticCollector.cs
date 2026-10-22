using Microsoft.Extensions.Logging;
using Omnom.Api.Services;

namespace Omnom.Evaluation;

// Collect only the application's allowlisted diagnostics, never provider content.
public sealed class DiagnosticCollector
{
    public List<ParseDiagnostic> Events { get; } = [];
    public ILogger<T> For<T>() => new CollectorLogger<T>(this);
    private sealed class CollectorLogger<T>(DiagnosticCollector collector) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is DiagnosticLogState diagnostic) collector.Events.Add(diagnostic.Diagnostic);
        }
    }
}
