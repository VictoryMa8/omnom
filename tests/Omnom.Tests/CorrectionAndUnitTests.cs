using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Models;
using Omnom.Api.Services;

namespace Omnom.Tests;

public class CorrectionAndUnitTests
{
    [Theory]
    [InlineData("200g cooked chicken breast, actually 150g", 150)]
    [InlineData("200g cooked chicken breast actually 150g", 150)]
    [InlineData("200g cooked chicken breast; make that 0.1kg", 100)]
    [InlineData("200g cooked chicken breast, actually 150g, actually 100g", 100)]
    public async Task CorrectionsReplaceEarlierAmountInEveryMode(string prompt, double expected)
    {
        foreach (var mode in new[] { "exact", "fallback", "ai" })
        {
            using var scope = new Fixture();
            // AI deliberately keeps the old amount AND omits the correction from
            // its source excerpt. Literal correction context must still win.
            var result = await scope.Parse(prompt, mode, Source("200g cooked chicken breast", "chicken breast", "chicken breast cooked", 999));
            var item = Assert.Single(result.Items);
            Assert.Equal(expected, item.Grams);
            Assert.Equal(prompt.Contains("0.1kg") ? .1 : expected, item.Quantity);
            Assert.Equal(prompt.Contains("0.1kg") ? "kg" : "g", item.Unit);
            Assert.Equal(expected * 1.65, item.Calories, 1);
            Assert.Equal(prompt, item.SourceText);
        }
    }

    [Theory]
    [InlineData("150g cooked rice without oil", "rice", 150)]
    [InlineData("150g cooked rice, no oil and butter", "rice", 150)]
    [InlineData("100g raw chicken breast, not cooked", "chicken", 100)]
    [InlineData("100g dry rice; ignore the earlier 150g estimate", "rice", 100)]
    [InlineData("100g cooked ground lamb, not chicken", "lamb", 100)]
    public async Task NegationsAndDiscardedEstimatesAreNeverFoods(string prompt, string identity, double grams)
    {
        foreach (var mode in new[] { "exact", "fallback" })
        {
            using var scope = new Fixture();
            var result = await scope.Parse(prompt, mode);
            var item = Assert.Single(result.Items);
            Assert.Contains(identity, item.FoodName, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(grams, item.Grams); Assert.NotEqual("Unparsed", item.UsdaMatchStatus);
            if (prompt.Contains("raw chicken")) Assert.Contains("Raw", item.FoodName);
            Assert.DoesNotContain(result.ClarificationChips, c => c.Id == "chip-cooking-oil" && prompt.Contains("oil"));
        }
    }

    [Theory]
    [InlineData("2 slices sourdough and 2 more slices sourdough")]
    [InlineData("2 slices sourdough, 2 more slices")]
    [InlineData("2 slices of sourdough and 2 more slices of sourdough")]
    public async Task MoreSlicesAddASeparateReferenceSizedPortion(string prompt)
    {
        foreach (var mode in new[] { "exact", "fallback" })
        {
            using var scope = new Fixture();
            var result = await scope.Parse(prompt, mode);
            Assert.Equal(2, result.Items.Count);
            Assert.All(result.Items, item => { Assert.Equal(100, item.Grams); Assert.Equal(2, item.Quantity); Assert.Equal(260, item.Calories); });
            Assert.Equal(200, result.Items.Sum(i => i.Grams));
            Assert.Contains("more", result.Items[1].SourceText);
        }
    }

    [Theory]
    [InlineData("2 more slices")]
    [InlineData("100g cooked chicken breast 150g")]
    [InlineData("200g cooked chicken breast, actually 150g cooked rice")]
    [InlineData("2 bottles 250ml whole milk")]
    public async Task AmbiguousAmountsRemainUnresolved(string prompt)
    {
        foreach (var mode in new[] { "exact", "fallback" })
        {
            using var scope = new Fixture(); var result = await scope.Parse(prompt, mode);
            Assert.All(result.Items, item => { Assert.Equal("Unparsed", item.UsdaMatchStatus); Assert.Equal(0, item.Grams); Assert.Equal(0, item.Calories); });
        }
    }

    [Theory]
    [InlineData("8 fl oz whole milk", 236.6, "fl oz")]
    [InlineData("8 fluid ounces whole milk", 236.6, "fl oz")]
    [InlineData("8oz whole milk", 236.6, "fl oz")]
    [InlineData("250ml whole milk", 250, "ml")]
    [InlineData("0.5 l whole milk", 500, "l")]
    public async Task BeverageVolumeUsesADisclosedDensityEstimateInsteadOfMassOunces(string prompt, double grams, string unit)
    {
        foreach (var mode in new[] { "exact", "fallback", "ai" })
        {
            using var scope = new Fixture();
            var item = Assert.Single((await scope.Parse(prompt, mode, Source(prompt, "whole milk", "whole milk", 999))).Items);
            Assert.Equal(grams, item.Grams); Assert.Equal(unit, item.Unit);
            Assert.Contains(item.Assumptions, a => a.Contains("assumed 1 g/ml"));
            Assert.NotEqual("Unparsed", item.UsdaMatchStatus);
            if (prompt.StartsWith("8")) Assert.NotEqual(226.8, item.Grams);
        }
    }

    [Theory]
    [InlineData("8oz cooked chicken breast", "chicken breast", "chicken breast cooked", 226.8)]
    [InlineData("8oz whole milk by weight", "whole milk", "whole milk", 226.8)]
    [InlineData("250g whole milk", "whole milk", "whole milk", 250)]
    public async Task ExplicitMassStillOverridesBadModelArithmetic(string prompt, string food, string query, double grams)
    {
        foreach (var mode in new[] { "exact", "fallback", "ai" })
        {
            using var scope = new Fixture();
            var item = Assert.Single((await scope.Parse(prompt, mode, Source(prompt, food, query, 999))).Items);
            Assert.Equal(grams, item.Grams); Assert.NotEqual("Unparsed", item.UsdaMatchStatus);
            Assert.DoesNotContain(item.Assumptions, a => a.Contains("1 g/ml"));
        }
    }

    [Theory]
    [InlineData("100 ml olive oil", "olive oil")]
    [InlineData("250 ml nebula juice", "nebula juice")]
    [InlineData("1 bottle whole milk", "whole milk")]
    public async Task UnsupportedDensitiesAndContainerSizesRequireReview(string prompt, string food)
    {
        foreach (var mode in new[] { "exact", "fallback", "ai" })
        {
            using var scope = new Fixture();
            var item = Assert.Single((await scope.Parse(prompt, mode, Source(prompt, food, food, 999))).Items);
            Assert.Equal("Unparsed", item.UsdaMatchStatus); Assert.Equal(0, item.Grams); Assert.Equal(0, item.Calories);
            Assert.Contains(item.Assumptions, a => a.Contains("weight") || a.Contains("grams") || a.Contains("volume"));
        }
    }

    [Fact]
    public async Task MaliciousNegatedModelItemUsesSafeFallback()
    {
        using var scope = new Fixture();
        var result = await scope.Parse("150g cooked rice without oil", "ai", Source("150g cooked rice without oil", "oil", "olive oil", 150));
        var item = Assert.Single(result.Items); Assert.Contains("Rice", item.FoodName); Assert.Equal(150, item.Grams);
        Assert.Contains("local parser", result.AiSummary);
    }

    [Fact]
    public async Task WeighedGramsWinOverVolumeInTheSameAiSource()
    {
        using var scope = new Fixture();
        const string prompt = "8 fl oz whole milk (250g weighed)";
        var item = Assert.Single((await scope.Parse(prompt, "ai", Source(prompt, "whole milk", "whole milk", 999))).Items);
        Assert.Equal(250, item.Grams); Assert.Equal("g", item.Unit);
    }

    [Theory]
    [InlineData("100g raw chicken breast, not cooked", "not cooked", "chicken breast", "chicken breast cooked")]
    [InlineData("150g cooked rice without oil", "oil", "oil", "olive oil")]
    public async Task ASourceExcerptInsideAnExclusionCannotGroundAModelFood(string prompt, string source, string food, string query)
    {
        using var scope = new Fixture();
        var result = await scope.Parse(prompt, "ai", Source(source, food, query, 100));
        var item = Assert.Single(result.Items);
        Assert.Contains("local parser", result.AiSummary);
        if (prompt.Contains("raw chicken")) Assert.Contains("Raw", item.FoodName);
        else Assert.Contains("Rice", item.FoodName);
    }

    [Theory]
    [InlineData("100g cooked chicken breast", "chicken breast raw", "Cooked")]
    [InlineData("100g cooked ground beef 93/7", "ground beef 80/20 cooked", "93/7")]
    [InlineData("100g dry rice", "rice cooked", "Dry")]
    public async Task ExplicitIdentitySurvivesAnIncorrectModelQuery(string prompt, string query, string expected)
    {
        using var scope = new Fixture();
        var item = Assert.Single((await scope.Parse(prompt, "ai", Source(prompt, "Food", query, 999))).Items);
        Assert.Contains(expected, item.FoodName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100, item.Grams);
    }

    [Fact]
    public async Task UnknownExactContainerRetainsItsUnitSoDefaultsCannotSubstituteAnUnrelatedPortion()
    {
        using var scope = new Fixture();
        var item = Assert.Single((await scope.Parse("2 bottles nebula fuel", "exact")).Items);
        Assert.Equal("Unparsed", item.UsdaMatchStatus); Assert.Equal(0, item.Grams);
        Assert.Equal(2, item.Quantity); Assert.Equal("bottle", item.Unit);
    }

    private static string Source(string source, string name, string query, double grams) => JsonSerializer.Serialize(new {
        items = new[] { new { sourceText = source, foodName = name, searchQuery = query, quantity = 1, unit = "serving", estimatedGrams = grams } }
    });
    private sealed class Fixture : IDisposable
    {
        private readonly AppDbContext db;
        private readonly HttpClient http = new(new RejectNetwork());
        public Fixture()
        {
            db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            DbInitializer.Initialize(db);
        }
        public Task<AiParsedMealResult> Parse(string prompt, string mode, string? response = null)
        {
            var foods = new LocalFoods(new UsdaFoodService(http, db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance));
            var parser = new MealParserService(new Router(mode == "ai" ? response ?? "{}" : "{}"), foods, NullLogger<MealParserService>.Instance);
            return parser.ParseMealAsync(prompt, mode: mode == "exact" ? "exact" : null);
        }
        public void Dispose() { db.Dispose(); http.Dispose(); }
    }
    private sealed class Router(string response) : IOpenRouterService
    {
        public List<string> GetAvailableFreeModels() => [];
        public Task<string> GenerateCompletionAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default) => Task.FromResult(response);
    }
    private sealed class LocalFoods(IUsdaFoodService inner) : IUsdaFoodService
    {
        public Task<FoodReference?> FindBestMatchAsync(string query, bool allowRemote = true, CancellationToken cancellationToken = default) => inner.FindBestMatchAsync(query, false, cancellationToken);
        public Task<List<FoodReference>> SearchFoodsAsync(string query, int limit = 10) => inner.SearchFoodsAsync(query, limit);
    }
    private sealed class RejectNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new InvalidOperationException("Offline tests must not use the network.");
    }
}
