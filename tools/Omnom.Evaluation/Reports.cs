using System.Text;
using System.Text.Json;
namespace Omnom.Evaluation;

public static class Reports
{
    public static string Markdown(Corpus corpus, string mode, List<CaseRun> runs, string? stop)
    {
        var metrics = JsonSerializer.SerializeToElement(Scoring.Aggregate(runs), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var text = new StringBuilder($"# Meal evaluation: {mode}\n\nDataset {corpus.Version}; {runs.Count} attempted; stopped: {stop ?? "no"}.\n\n");
        text.AppendLine("| Metric | Numerator / denominator | Rate |\n|---|---|---|");
        foreach (var key in new[] { "itemPrecision", "itemRecall", "fullyCorrectMeals", "explicitMassAccuracy", "portionAccuracy", "quantityAccuracy", "fallback" })
        {
            var value = metrics.GetProperty(key); var rate = value.GetProperty("rate");
            text.AppendLine($"| {key} | {value.GetProperty("numerator")} / {value.GetProperty("denominator")} | {(rate.ValueKind == JsonValueKind.Null ? "n/a" : rate.ToString())} |");
        }
        text.AppendLine($"\nQualifier errors: {metrics.GetProperty("qualifierErrors")}; forbidden substitutions: {metrics.GetProperty("forbiddenSubstitutions")}; excluded-food violations: {metrics.GetProperty("exclusionViolations")}.");
        text.AppendLine("\n| Case | Category | Outcome | Violations |\n|---|---|---|---|");
        foreach (var run in runs)
        {
            var failures = run.Score == null ? run.Failure : string.Join("; ", run.Score.Foods.Where(f => !f.Correct).Select(f => f.Key + ": " + string.Join(", ", f.Failures)));
            if (run.Score is { FalsePositives: > 0 }) failures += "; extra foods";
            if (run.Score is { ExclusionViolations: > 0 }) failures += "; excluded foods returned";
            text.AppendLine($"| {run.Id} | {run.Category} | {(run.Score?.FullyCorrect == true ? "pass" : run.Outcome == "scored" ? "needs work" : run.Outcome)} | {failures?.Replace("|", "/")} |");
        }
        text.AppendLine("\nRates are identity/constraint checks on synthetic prompts, not independent nutrition or live-model accuracy. See the JSON report for version hashes, execution context, actual foods, category metrics, unknown/estimated outcomes, and live request accounting.");
        return text.ToString();
    }
}
