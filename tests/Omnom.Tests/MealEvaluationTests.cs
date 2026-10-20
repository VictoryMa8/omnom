using System.Net;
using System.Text;
using System.Text.Json;
using Omnom.Api.Models;
using Omnom.Evaluation;

namespace Omnom.Tests;

public class MealEvaluationTests
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    static Corpus Corpus() => JsonSerializer.Deserialize<Corpus>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MealEvaluation/cases.json")), Json)!;
    static AiParsedItem Correct(ExpectedFood food) => new() {
        FoodName = food.Reference ?? food.Aliases[0], SourceText = food.SourceText, Grams = food.Grams ?? 100,
        Quantity = food.Quantity ?? 1, Unit = food.Unit ?? "serving", UsdaMatchStatus = food.Reference == null ? "Unparsed" : "VerifiedStaple",
        Calories = food.Nutrition?.GetValueOrDefault("calories") ?? 0, Protein = food.Nutrition?.GetValueOrDefault("protein") ?? 0,
        Carbs = food.Nutrition?.GetValueOrDefault("carbs") ?? 0, Fat = food.Nutrition?.GetValueOrDefault("fat") ?? 0,
        Fiber = food.Nutrition?.GetValueOrDefault("fiber") ?? 0 };

    [Fact]
    public void CorpusHasReviewedCategoriesStableIdsSourcesAndHonestUncertainty()
    {
        var corpus = Corpus(); CorpusValidation.Validate(corpus);
        Assert.Equal(66, corpus.Cases.Count);
        foreach (var category in CorpusValidation.Categories) Assert.Equal(8, corpus.Cases.Count(c => c.Category == category));
        Assert.Equal(2, corpus.Cases.Count(c => c.Category == "full-day"));
        Assert.Contains(corpus.Cases, c => c.Prompt.Contains("orange julius"));
        Assert.All(corpus.Cases.Where(c => c.Category == "vague").SelectMany(c => c.Expected), f => {
            Assert.Null(f.Grams); Assert.Null(f.Nutrition); Assert.NotEmpty(f.UncertainFields);
        });
        Assert.Throws<InvalidDataException>(() => CorpusValidation.Validate(corpus with { Cases = [corpus.Cases[0]] }));
    }

    [Fact]
    public void MarkdownReportRendersTheSameRatiosAndCaseFailures()
    {
        var corpus = Corpus(); var fixture = corpus.Cases[0];
        var score = Scoring.Score(fixture, new() { Items = [Correct(fixture.Expected[0])] });
        var text = Reports.Markdown(corpus, "stubbed-ai", [new(fixture.Id, fixture.Category, "scored", 1, false, score, null)], null);
        Assert.Contains("explicitMassAccuracy | 1 / 1", text); Assert.Contains(fixture.Id + " | mass | pass", text);
    }

    [Fact]
    public void FrozenNutritionAndServingExpectationsAgreeWithTheirCitedReferenceRows()
    {
        using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MealEvaluation/references.json")));
        var rows = snapshot.RootElement.GetProperty("foods").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!);
        foreach (var food in Corpus().Cases.SelectMany(c => c.Expected).Where(f => f.Nutrition != null))
        {
            if (food.Reference == "MealParserService.IsZeroCalorieDrink semantic contract")
            { Assert.All(food.Nutrition!, pair => Assert.Equal(0, pair.Value)); continue; }
            Assert.True(rows.TryGetValue(food.Reference!, out var row)); Assert.NotNull(food.Grams);
            foreach (var pair in food.Nutrition!)
                Assert.InRange(Math.Abs(pair.Value - row.GetProperty(pair.Key + "Per100g").GetDouble() * food.Grams!.Value / 100), 0, food.NutritionTolerance![pair.Key]);
            if (food.QuantityKind == "reference-serving")
                Assert.Equal(row.GetProperty("defaultServingGrams").GetDouble() * food.Quantity, food.Grams);
        }
    }

    [Fact]
    public void MissingAndDuplicateFoodsCannotInflateAccuracyOrReuseOnePrediction()
    {
        var fixture = Corpus().Cases[0]; var food = fixture.Expected[0]; var actual = Correct(food);
        var missing = Scoring.Score(fixture, new());
        Assert.Equal(1, missing.FalseNegatives); Assert.Equal(1, missing.ExplicitMassChecks); Assert.Equal(0, missing.ExplicitMassCorrect);
        var duplicate = Scoring.Score(fixture, new() { Items = [actual, Correct(food)] });
        Assert.Equal(1, duplicate.TruePositives); Assert.Equal(1, duplicate.FalsePositives); Assert.False(duplicate.FullyCorrect);
        var two = fixture with { Expected = [food, food with { Key = "second" }] };
        Assert.Equal(1, Scoring.Score(two, new() { Items = [actual] }).FalseNegatives);
        Assert.Null(Scoring.Ratio(0, 0).Rate);
    }

    [Fact]
    public void QualifiersAreJudgedFromActualFoodNotTheOriginalSourceExcerpt()
    {
        var fixture = Corpus().Cases.Single(c => c.Id == "PREPARATION-001");
        var item = Correct(fixture.Expected[0]); item.FoodName = "Chicken Breast Cooked";
        var score = Scoring.Score(fixture, new() { Items = [item] });
        Assert.Equal(1, score.QualifierErrors); Assert.Equal(1, score.ForbiddenSubstitutions); Assert.False(score.FullyCorrect);
    }

    [Fact]
    public void ExplicitMassAndEachNutritionToleranceHaveBoundaryChecks()
    {
        var fixture = Corpus().Cases[0]; var item = Correct(fixture.Expected[0]);
        item.Grams += .1; item.Calories += .2; item.Protein += .15;
        Assert.True(Scoring.Score(fixture, new() { Items = [item] }).FullyCorrect);
        item.Grams += .01; item.Protein += .01;
        var score = Scoring.Score(fixture, new() { Items = [item] });
        Assert.Equal(0, score.ExplicitMassCorrect); Assert.False(score.Foods[0].NutritionCorrect); Assert.False(score.FullyCorrect);
    }

    [Fact]
    public void UnknownNutritionAndNegatedIngredientsAreNotAcceptedAsSuccess()
    {
        var fixture = Corpus().Cases.Single(c => c.Id == "UNKNOWN-001"); var item = Correct(fixture.Expected[0]);
        Assert.True(Scoring.Score(fixture, new() { Items = [item] }).FullyCorrect);
        item.UsdaMatchStatus = "Estimated"; item.Calories = 150;
        Assert.Equal(1, Scoring.Score(fixture, new() { Items = [item] }).ForbiddenSubstitutions);
        var excluded = fixture with { ExcludedFoods = ["butter"] };
        Assert.Equal(1, Scoring.Score(excluded, new() { Items = [item, new() { FoodName = "Butter" }] }).ExclusionViolations);
    }

    [Fact]
    public void UncertainRecipeCannotPassAsUnqualifiedVerifiedNutrition()
    {
        var fixture = Corpus().Cases.First(c => c.Category == "mixed" && c.Expected[0].UncertainFields.Count > 0);
        var item = Correct(fixture.Expected[0]); item.UsdaMatchStatus = "VerifiedStaple";
        Assert.False(Scoring.Score(fixture, new() { Items = [item] }).FullyCorrect);
        item.Assumptions = ["Estimated recipe"];
        Assert.True(Scoring.Score(fixture, new() { Items = [item] }).FullyCorrect);
    }

    static HttpRequestMessage Request(double maximumPrice = 0) => new(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions") {
        Content = new StringContent(JsonSerializer.Serialize(new { model = "approved", provider = new { max_price = new {
            prompt = maximumPrice, completion = 0, request = 0, image = 0 } } }), Encoding.UTF8, "application/json") };
    sealed class Reply(HttpStatusCode status = HttpStatusCode.OK, string body = "{}") : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }); }
    }
    [Fact]
    public async Task HttpAttemptCapAndZeroPriceCheckApplyBeforeSendingAnyBackup()
    {
        var inner = new Reply(); using var capped = new CappedRouterHandler(1, ["approved"], inner); using var client = new HttpClient(capped);
        using (var reply = await client.SendAsync(Request())) { }
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Request()));
        Assert.Equal(1, inner.Calls); Assert.Equal("request-cap", capped.StopReason); Assert.Equal(1, capped.MissingCost);
        Assert.Equal(0, capped.WithCost);
        var second = new Reply(); using var guard = new CappedRouterHandler(3, ["approved"], second); using var other = new HttpClient(guard);
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SendAsync(Request(.001)));
        Assert.Equal(0, second.Calls);
    }
    [Fact]
    public async Task QuotaResponseStopsBeforeAnotherNetworkAttempt()
    {
        var inner = new Reply(HttpStatusCode.TooManyRequests); using var handler = new CappedRouterHandler(10, ["approved"], inner); using var client = new HttpClient(handler);
        using (var response = await client.SendAsync(Request())) Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Request()));
        Assert.Equal("provider-quota", handler.StopReason); Assert.Equal(1, inner.Calls);
    }
    [Fact]
    public async Task UnexpectedReportedCostStopsAndOfflineHandlerCannotSend()
    {
        var inner = new Reply(body: "{\"usage\":{\"cost\":0.01},\"model\":\"actual\"}");
        using var handler = new CappedRouterHandler(5, ["approved"], inner); using var client = new HttpClient(handler);
        using (var response = await client.SendAsync(Request())) { }
        Assert.Equal("nonzero-reported-cost", handler.StopReason); Assert.Equal(.01, handler.CostSum);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Request()));
        var blocked = new NetworkForbidden(); using var offline = new HttpClient(blocked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => offline.SendAsync(Request())); Assert.Equal(1, blocked.Attempts);
    }
}
