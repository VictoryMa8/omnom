using System.Text.Json;
using Omnom.Api.Models;

namespace Omnom.Evaluation;

public sealed record Corpus(string Version, string Description, List<MealCase> Cases);
public sealed record MealCase(string Id, string Category, string Prompt, string Intent,
    List<ExpectedFood> Expected, List<string> ExcludedFoods, JsonElement StubResponse,
    string StubFormat = "json");
public sealed record ExpectedFood(string Key, List<string> Aliases, string SourceText,
    List<string> Qualifiers, List<string> ForbiddenQualifiers, double? Grams,
    string QuantityKind, double? Quantity, string? Unit, string Resolution,
    List<string> UncertainFields, string? Reference, Dictionary<string, double>? Nutrition,
    Dictionary<string, double>? NutritionTolerance, string Evidence);
public sealed record FoodScore(string Key, int? ActualIndex, bool IdentityCorrect, bool QualifiersCorrect,
    bool? ExplicitMassCorrect, bool? PortionCorrect, bool? CountCorrect, bool ResolutionCorrect,
    bool UncertaintyVisible, bool? NutritionCorrect, bool Correct, List<string> Failures);
public sealed record CaseScore(string Id, string Category, int ExpectedItems, int ActualItems,
    int TruePositives, int FalsePositives, int FalseNegatives, int QualifierErrors,
    int ExclusionViolations, int ForbiddenSubstitutions, int ExplicitMassChecks, int ExplicitMassCorrect,
    bool FullyCorrect, List<FoodScore> Foods, List<AiParsedItem> Actual);
public sealed record CaseRun(string Id, string Category, string Outcome, double ElapsedMs,
    bool Fallback, CaseScore? Score, string? Failure);
public sealed record Counts(int Numerator, int Denominator, double? Rate);
