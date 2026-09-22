using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class DeterministicParserTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        DbInitializer.Initialize(context);
        return context;
    }

    private sealed class CountingOpenRouter : IOpenRouterService
    {
        public int Calls { get; private set; }

        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("not json");
        }

        public System.Collections.Generic.List<string> GetAvailableFreeModels() => new();
    }

    private static MealParserService CreateParser(AppDbContext db, CountingOpenRouter router)
    {
        var usda = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);
        return new MealParserService(router, usda, NullLogger<MealParserService>.Instance);
    }

    [Fact]
    public async Task ExactMode_CookedChicken100g_UsesStapleMacrosAndSkipsModel()
    {
        using var db = CreateDb();
        var router = new CountingOpenRouter();
        var parser = CreateParser(db, router);

        var result = await parser.ParseMealAsync("cooked chicken breast 100g", mode: "exact");

        Assert.Equal(0, router.Calls);
        var chicken = Assert.Single(result.Items);
        Assert.Equal("VerifiedStaple", chicken.UsdaMatchStatus);
        Assert.Contains("Chicken Breast", chicken.FoodName);
        Assert.Contains("Cooked", chicken.FoodName);
        Assert.Equal(100, chicken.Grams);
        Assert.Equal(165, chicken.Calories);
        Assert.Equal(31, chicken.Protein);
        Assert.DoesNotContain(chicken.Assumptions, a => a.Contains("Cooked assumed"));
    }

    [Fact]
    public async Task ExactMode_AcceptsWeightFirstAndTwoItems()
    {
        using var db = CreateDb();
        var parser = CreateParser(db, new CountingOpenRouter());

        var result = await parser.ParseMealAsync("100g cooked chicken breast, jasmine rice 150g", mode: "exact");

        Assert.Equal(2, result.Items.Count);
        var chicken = result.Items[0];
        Assert.Equal(100, chicken.Grams);
        Assert.Equal(165, chicken.Calories);
        var rice = result.Items[1];
        Assert.Contains("Rice", rice.FoodName);
        Assert.Equal(150, rice.Grams);
        Assert.Equal(195, rice.Calories);
    }

    [Theory]
    [InlineData("ground beef 93/7 200g", "93/7", 436)]
    [InlineData("200g ground beef 80/20", "80/20", 540)]
    public async Task ExactMode_LeanRatioSelectsGroundBeefStaple(string prompt, string lean, double calories)
    {
        using var db = CreateDb();
        var parser = CreateParser(db, new CountingOpenRouter());

        var result = await parser.ParseMealAsync(prompt, mode: "exact");

        var beef = Assert.Single(result.Items);
        Assert.Contains(lean, beef.FoodName);
        Assert.Equal(200, beef.Grams);
        Assert.Equal(calories, beef.Calories);
    }

    [Fact]
    public async Task ExactMode_VagueLineIsUnparsedAndDoesNotInventCalories()
    {
        using var db = CreateDb();
        var parser = CreateParser(db, new CountingOpenRouter());

        var result = await parser.ParseMealAsync("some chicken, cooked chicken breast 100g", mode: "exact");

        Assert.Equal(2, result.Items.Count);
        var unread = result.Items[0];
        Assert.Equal("Unparsed", unread.UsdaMatchStatus);
        Assert.Equal("some chicken", unread.FoodName);
        Assert.Equal(0, unread.Calories);
        Assert.Equal(0, unread.Grams);
        Assert.Contains("Could not read: some chicken", result.AiSummary);
        Assert.Equal(165, result.Items[1].Calories);
    }

    [Fact]
    public async Task ExactMode_ParsesEggsOilAndGrilledSalmon()
    {
        using var db = CreateDb();
        var parser = CreateParser(db, new CountingOpenRouter());

        var eggs = await parser.ParseMealAsync("2 large eggs + 1 tbsp olive oil", mode: "exact");
        Assert.Equal(2, eggs.Items.Count);
        Assert.Contains("Egg", eggs.Items[0].FoodName);
        Assert.Equal(100, eggs.Items[0].Grams);
        Assert.Equal(143, eggs.Items[0].Calories);
        Assert.Contains("Olive Oil", eggs.Items[1].FoodName);
        Assert.Equal(15, eggs.Items[1].Grams);

        var salmon = await parser.ParseMealAsync("6 oz grilled salmon", mode: "exact");
        var fillet = Assert.Single(salmon.Items);
        Assert.Contains("Salmon", fillet.FoodName);
        Assert.InRange(fillet.Grams, 170, 171);
        Assert.Equal("VerifiedStaple", fillet.UsdaMatchStatus);
    }
}
