using System.Text.RegularExpressions;
using Omnom.Api.Models;

namespace Omnom.Evaluation;

public static class Scoring
{
    public const double MassTolerance = 0.1;
    public static bool Matches(string text, string phrase)
    {
        static string[] Tokens(string value) => Regex.Matches(value.ToLowerInvariant(), @"[a-z0-9]+")
            .Select(m => m.Value.Length > 3 && m.Value.EndsWith('s') ? m.Value[..^1] : m.Value).ToArray();
        var words = Tokens(text).ToHashSet();
        return phrase.Split('|').Any(alternative => Tokens(alternative).Length > 0 && Tokens(alternative).All(words.Contains));
    }
    static bool Identity(ExpectedFood food, AiParsedItem item) => food.Aliases.Any(a => Matches(item.FoodName, a));
    static bool Qualifiers(ExpectedFood food, AiParsedItem item) => food.Qualifiers.All(q => Matches(item.FoodName, q))
        && food.ForbiddenQualifiers.All(q => !Matches(item.FoodName, q));
    static bool Near(double actual, double expected, double tolerance) => double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance + 1e-8;

    public static CaseScore Score(MealCase fixture, AiParsedMealResult actual)
    {
        // Maximum-cardinality one-to-one matching. Prefer preparation/amount agreement for duplicate identities.
        var owner = Enumerable.Repeat(-1, actual.Items.Count).ToArray();
        bool Assign(int expected, HashSet<int> visited)
        {
            var food = fixture.Expected[expected];
            var candidates = Enumerable.Range(0, actual.Items.Count).Where(i => Identity(food, actual.Items[i]))
                .OrderByDescending(i => (Qualifiers(food, actual.Items[i]) ? 2 : 0)
                    + (food.Grams is double g && Near(actual.Items[i].Grams, g, MassTolerance) ? 1 : 0));
            foreach (var i in candidates)
                if (visited.Add(i) && (owner[i] < 0 || Assign(owner[i], visited))) { owner[i] = expected; return true; }
            return false;
        }
        for (var i = 0; i < fixture.Expected.Count; i++) Assign(i, []);
        var rows = new List<FoodScore>();
        var substitutions = 0;
        for (var e = 0; e < fixture.Expected.Count; e++)
        {
            var food = fixture.Expected[e]; var index = Array.IndexOf(owner, e);
            if (index < 0)
            {
                rows.Add(new(food.Key, null, false, false, food.QuantityKind == "explicit-mass" ? false : null,
                    food.Grams != null ? false : null, food.Quantity != null ? false : null,
                    false, false, food.Nutrition != null ? false : null, false, ["missing food"])); continue;
            }
            var item = actual.Items[index]; var failures = new List<string>();
            var qualifier = Qualifiers(food, item);
            if (!qualifier) failures.Add("preparation/brand/diet/lean qualifier mismatch");
            bool? portion = food.Grams is double grams ? Near(item.Grams, grams, MassTolerance) : null;
            if (portion == false) failures.Add("portion grams differ");
            bool? mass = food.QuantityKind == "explicit-mass" ? portion : null;
            bool? count = food.Quantity is double q ? Near(item.Quantity, q, 0.001) &&
                (food.Unit == null || Matches(item.Unit, food.Unit)) : null;
            if (count == false) failures.Add("quantity/unit differ");
            var resolved = item.UsdaMatchStatus != "Unparsed";
            var resolution = food.Resolution switch {
                "unresolved" => !resolved && item.Calories == 0 && item.Protein == 0 && item.Carbs == 0 && item.Fat == 0 && item.Fiber == 0,
                "reference" => item.UsdaMatchStatus is "VerifiedStaple" or "UsdaApiMatch",
                "uncertain" => item.UsdaMatchStatus is "Unparsed" or "Estimated" || item.Assumptions.Count > 0,
                _ => false
            };
            if (!resolution) failures.Add("resolution/source expectation differs");
            var uncertain = food.UncertainFields.Count == 0 || item.UsdaMatchStatus is "Unparsed" or "Estimated"
                || item.Assumptions.Count > 0;
            if (!uncertain) failures.Add("uncertainty is not visible");
            if (resolved && (!qualifier || food.Resolution == "unresolved")) substitutions++;
            var nutrients = new Dictionary<string, double> { ["calories"] = item.Calories, ["protein"] = item.Protein,
                ["carbs"] = item.Carbs, ["fat"] = item.Fat, ["fiber"] = item.Fiber };
            bool? nutrition = food.Nutrition == null ? null : food.Nutrition.All(pair =>
                Near(nutrients[pair.Key], pair.Value, food.NutritionTolerance![pair.Key]));
            if (nutrition == false) failures.Add("reference nutrition outside fixture tolerances");
            rows.Add(new(food.Key, index, true, qualifier, mass, portion, count, resolution, uncertain, nutrition,
                failures.Count == 0, failures));
        }
        var matched = owner.Count(i => i >= 0);
        substitutions += actual.Items.Where((item, i) => owner[i] < 0 && item.UsdaMatchStatus != "Unparsed")
            .Count(item => fixture.Expected.Any(food => food.Resolution == "unresolved" &&
                food.Aliases.Any(alias => Matches(item.SourceText ?? "", alias))));
        var exclusions = actual.Items.Count(item => fixture.ExcludedFoods.Any(alias => Matches(item.FoodName, alias)));
        var fp = actual.Items.Count - matched;
        return new(fixture.Id, fixture.Category, fixture.Expected.Count, actual.Items.Count, matched, fp,
            fixture.Expected.Count - matched, rows.Count(r => r.IdentityCorrect && !r.QualifiersCorrect), exclusions,
            substitutions, rows.Count(r => r.ExplicitMassCorrect != null), rows.Count(r => r.ExplicitMassCorrect == true),
            rows.All(r => r.Correct) && fp == 0 && exclusions == 0, rows, actual.Items);
    }

    public static Counts Ratio(int numerator, int denominator) => new(numerator, denominator,
        denominator == 0 ? null : Math.Round((double)numerator / denominator, 4));
    public static object Aggregate(IEnumerable<CaseRun> runs)
    {
        var all = runs.ToList(); var scores = all.Where(r => r.Score != null).Select(r => r.Score!).ToList();
        var tp = scores.Sum(s => s.TruePositives); var fp = scores.Sum(s => s.FalsePositives); var fn = scores.Sum(s => s.FalseNegatives);
        var foods = scores.SelectMany(s => s.Foods).ToList(); var predicted = scores.SelectMany(s => s.Actual).ToList();
        return new { attempted = all.Count, scored = scores.Count, notScored = all.Count - scores.Count,
            itemPrecision = Ratio(tp, tp + fp), itemRecall = Ratio(tp, tp + fn),
            fullyCorrectMeals = Ratio(scores.Count(s => s.FullyCorrect), scores.Count),
            explicitMassAccuracy = Ratio(scores.Sum(s => s.ExplicitMassCorrect), scores.Sum(s => s.ExplicitMassChecks)),
            portionAccuracy = Ratio(foods.Count(f => f.PortionCorrect == true), foods.Count(f => f.PortionCorrect != null)),
            quantityAccuracy = Ratio(foods.Count(f => f.CountCorrect == true), foods.Count(f => f.CountCorrect != null)),
            qualifierErrors = scores.Sum(s => s.QualifierErrors), forbiddenSubstitutions = scores.Sum(s => s.ForbiddenSubstitutions),
            exclusionViolations = scores.Sum(s => s.ExclusionViolations),
            unresolvedItems = predicted.Count(i => i.UsdaMatchStatus == "Unparsed"),
            estimatedItems = predicted.Count(i => i.UsdaMatchStatus == "Estimated"),
            fallback = Ratio(all.Count(r => r.Score != null && r.Fallback), scores.Count),
            failures = all.Count(r => r.Outcome != "scored") };
    }
}
