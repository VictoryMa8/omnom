using System.Text;
using System.Text.Json;
namespace Omnom.Evaluation;

public static class Reports
{
    public sealed record Latency(int Samples, double? P50Ms, double? P95Ms);
    // Nearest-rank percentiles. Keep completed/scored and failed samples separate.
    public static Latency Percentiles(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        double? At(double p) => sorted.Length == 0 ? null : sorted[(int)Math.Ceiling(p * sorted.Length) - 1];
        return new(sorted.Length, At(.5), At(.95));
    }
    public static object DiagnosticSummary(List<CaseRun> runs)
    {
        var events = runs.SelectMany(r => r.Diagnostics).ToList();
        var attempts = events.Where(e => e.Stage == "model-attempt").ToList();
        return new {
            scoredEndToEnd = Percentiles(runs.Where(r => r.Outcome == "scored").Select(r => r.ElapsedMs)),
            failedEndToEnd = Percentiles(runs.Where(r => r.Outcome != "scored").Select(r => r.ElapsedMs)),
            stageLatency = events.GroupBy(e => e.Stage).ToDictionary(g => g.Key, g => Percentiles(g.Select(e => e.ElapsedMs))),
            attemptOutcomes = attempts.GroupBy(e => e.Outcome).ToDictionary(g => g.Key, g => g.Count()),
            extractionOutcomes = events.Where(e => e.Stage == "extraction").GroupBy(e => e.Outcome).ToDictionary(g => g.Key, g => g.Count()),
            fallbackCategories = events.Where(e => e.Stage == "extraction" && e.FallbackCategory != null)
                .GroupBy(e => e.FallbackCategory!).ToDictionary(g => g.Key, g => g.Count()),
            matching = new { references = events.Sum(e => e.References ?? 0), estimates = events.Sum(e => e.Estimates ?? 0), unresolved = events.Sum(e => e.Unresolved ?? 0) },
            usage = new { attempts = attempts.Count, withCost = attempts.Count(e => e.Cost.HasValue),
                missingCost = attempts.Count(e => !e.Cost.HasValue),
                reportedCostSum = attempts.Any(e => e.Cost.HasValue) ? (double?)attempts.Sum(e => e.Cost ?? 0) : null,
                withPromptTokens = attempts.Count(e => e.PromptTokens.HasValue), withCompletionTokens = attempts.Count(e => e.CompletionTokens.HasValue),
                promptTokenSum = attempts.Any(e => e.PromptTokens.HasValue) ? (long?)attempts.Sum(e => (long)(e.PromptTokens ?? 0)) : null,
                completionTokenSum = attempts.Any(e => e.CompletionTokens.HasValue) ? (long?)attempts.Sum(e => (long)(e.CompletionTokens ?? 0)) : null }
        };
    }

    public static string Markdown(Corpus corpus, string mode, List<CaseRun> runs, string? stop)
    {
        var metrics = JsonSerializer.SerializeToElement(Scoring.Aggregate(runs), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var text = new StringBuilder($"# Meal evaluation: {mode}\n\nDataset {corpus.Version}; {runs.Count} attempted; stopped: {stop ?? "no"}.\n\n");
        var latency = Percentiles(runs.Where(r => r.Outcome == "scored").Select(r => r.ElapsedMs));
        text.AppendLine($"Completed/scored latency (nearest rank): n={latency.Samples}, p50={latency.P50Ms?.ToString("F1") ?? "unknown"} ms, p95={latency.P95Ms?.ToString("F1") ?? "unknown"} ms. Includes local fallbacks; failed runs are separate in JSON.\n");
        text.AppendLine("Stage timings, extraction outcomes, reference/estimate/unresolved counts, per-attempt outcomes and nullable usage/cost are in the JSON diagnostics summary.\n");
        text.AppendLine("| Metric | Numerator / denominator | Rate |\n|---|---|---|");
        foreach (var key in new[] { "itemPrecision", "itemRecall", "fullyCorrectMeals", "explicitMassAccuracy", "portionAccuracy", "quantityAccuracy", "fallback" })
        {
            var value = metrics.GetProperty(key); var rate = value.GetProperty("rate");
            text.AppendLine($"| {key} | {value.GetProperty("numerator")} / {value.GetProperty("denominator")} | {(rate.ValueKind == JsonValueKind.Null ? "n/a" : rate.ToString())} |");
        }
        text.AppendLine($"\nQualifier errors: {metrics.GetProperty("qualifierErrors")}; forbidden substitutions: {metrics.GetProperty("forbiddenSubstitutions")}; excluded-food violations: {metrics.GetProperty("exclusionViolations")}.");
        text.AppendLine("\n| Case | Category | Outcome | Repetition | Violations |\n|---|---|---|---|---|");
        foreach (var run in runs)
        {
            var failures = run.Score == null ? run.Failure : string.Join("; ", run.Score.Foods.Where(f => !f.Correct).Select(f => f.Key + ": " + string.Join(", ", f.Failures)));
            if (run.Score is { FalsePositives: > 0 }) failures += "; extra foods";
            if (run.Score is { ExclusionViolations: > 0 }) failures += "; excluded foods returned";
            text.AppendLine($"| {run.Id} | {run.Category} | {(run.Score?.FullyCorrect == true ? "pass" : run.Outcome == "scored" ? "needs work" : run.Outcome)} | {run.Repetition} | {failures?.Replace("|", "/")} |");
        }
        text.AppendLine("\nRates are identity/constraint checks on synthetic prompts, not independent nutrition or live-model accuracy. See the JSON report for version hashes, execution context, actual foods, category metrics, unknown/estimated outcomes, and live request accounting.");
        return text.ToString();
    }
}
