using System.Text.Json;

namespace Omnom.Evaluation;

public static class CorpusValidation
{
    public static readonly string[] Categories = ["mass", "preparation", "qualifiers", "brands", "vague", "mixed", "corrections", "unknown"];
    public static void Validate(Corpus corpus)
    {
        void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        Require(corpus.Version == "1.0.0", "Unsupported dataset version.");
        Require(corpus.Cases.Count >= 60, "Corpus requires at least 60 cases.");
        Require(corpus.Cases.Select(c => c.Id).Distinct().Count() == corpus.Cases.Count, "Duplicate case IDs.");
        foreach (var category in Categories) Require(corpus.Cases.Count(c => c.Category == category) >= 6, $"Need six {category} cases.");
        foreach (var fixture in corpus.Cases)
        {
            Require(fixture.Id.Length > 0 && fixture.Prompt.Length > 0 && fixture.Intent.Length > 0, "Missing case metadata.");
            Require(fixture.Expected.Count > 0 && fixture.Expected.Select(f => f.Key).Distinct().Count() == fixture.Expected.Count, $"{fixture.Id}: invalid expected keys.");
            Require(fixture.StubFormat is "json" or "fenced" or "invalid", $"{fixture.Id}: invalid stub format.");
            Require(fixture.StubResponse.ValueKind == JsonValueKind.Object, $"{fixture.Id}: missing canned response.");
            foreach (var food in fixture.Expected)
            {
                Require(food.Aliases.Count > 0 && !string.IsNullOrWhiteSpace(food.SourceText) &&
                    fixture.Prompt.Contains(food.SourceText, StringComparison.OrdinalIgnoreCase), $"{fixture.Id}: source excerpt not in prompt.");
                Require(!string.IsNullOrWhiteSpace(food.Evidence), $"{fixture.Id}: missing evidence.");
                Require(food.Resolution is "reference" or "unresolved" or "uncertain", $"{fixture.Id}: invalid resolution.");
                Require(food.QuantityKind is "explicit-mass" or "reference-serving" or "unspecified", $"{fixture.Id}: invalid quantity kind.");
                Require(food.Grams == null || food.Grams > 0 && double.IsFinite(food.Grams.Value), $"{fixture.Id}: invalid expected grams.");
                Require(food.QuantityKind != "explicit-mass" || food.Grams != null, $"{fixture.Id}: missing explicit mass.");
                if (food.Nutrition != null)
                {
                    Require(food.Reference != null && food.NutritionTolerance != null, $"{fixture.Id}: nutrition needs provenance/tolerances.");
                    foreach (var pair in food.Nutrition)
                        Require(new[] { "calories", "protein", "carbs", "fat", "fiber" }.Contains(pair.Key) &&
                            double.IsFinite(pair.Value) && pair.Value >= 0 && food.NutritionTolerance!.TryGetValue(pair.Key, out var t) && double.IsFinite(t) && t >= 0,
                            $"{fixture.Id}: invalid nutrient or tolerance.");
                }
                if (food.UncertainFields.Count > 0)
                    Require(food.Nutrition == null || food.Nutrition.Values.All(v => v == 0), $"{fixture.Id}: uncertain portions/dishes cannot have exact nutrition expectations.");
            }
        }
    }
}
