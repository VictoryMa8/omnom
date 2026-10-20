using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Controllers;
using Omnom.Api.Data;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class ReferenceClarificationTests
{
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        DbInitializer.Initialize(db); return db;
    }

    private static UsdaFoodService Foods(AppDbContext db) => new(new HttpClient(), db,
        new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);

    [Theory]
    [InlineData("150g chicken breast", "raw", 180, 33.8)]
    [InlineData("100g rice", "dry", 365, 7.1)]
    public async Task PrepOptionsUseActualReferencesAtTheLoggedWeight(string prompt, string alternative, double calories, double protein)
    {
        using var db = CreateDb();
        var parser = new MealParserService(new OfflineAi(), Foods(db), NullLogger<MealParserService>.Instance);
        var result = await parser.ParseMealAsync(prompt);
        var option = Assert.Single(result.ClarificationChips.Single(c => c.Id == "chip-prep-0").Options, o => o.Id == alternative);
        var reference = db.FoodReferences.Single(f => f.Id == option.ReplacementReferenceFoodId);
        Assert.Equal(reference.Name, option.ReplacementFoodName);
        Assert.Equal(reference.FdcId, option.ReplacementFdcId);
        Assert.Equal("VerifiedStaple", option.ReplacementMatchStatus);
        Assert.Equal(calories, option.ReplacementCalories!.Value, 1);
        Assert.Equal(protein, option.ReplacementProtein!.Value, 1);
        Assert.Equal(Math.Round(reference.FiberPer100g * result.Items[0].Grams / 100, 1), option.ReplacementFiber);
        Assert.Equal(prompt, result.Items[0].SourceText);
    }

    [Fact]
    public async Task UnsupportedRawAlternativeDoesNotInventAScaledOption()
    {
        using var db = CreateDb();
        var parser = new MealParserService(new OfflineAi(), Foods(db), NullLogger<MealParserService>.Instance);
        var result = await parser.ParseMealAsync("150g ground lamb");
        Assert.DoesNotContain(result.ClarificationChips, c => c.Id.StartsWith("chip-prep"));
    }

    [Fact]
    public async Task BeefChoicesUseTheirReferenceRecordsAndPreserveTheWeight()
    {
        using var db = CreateDb();
        var parser = new MealParserService(new OfflineAi(), Foods(db), NullLogger<MealParserService>.Instance);
        var result = await parser.ParseMealAsync("200g ground beef");
        var chip = result.ClarificationChips.Single(c => c.Id == "chip-beef-lean-0");
        Assert.Equal("93_7", chip.SelectedOptionId);
        foreach (var option in chip.Options)
        {
            var food = db.FoodReferences.Single(f => f.Id == option.ReplacementReferenceFoodId);
            Assert.Equal(food.CaloriesPer100g * 2, option.ReplacementCalories);
            Assert.Equal(200, option.ReplacementGrams);
        }
    }

    [Theory]
    [InlineData("100g ground beef 85/15")]
    [InlineData("100g raw ground beef 93/7")]
    public async Task ExactModeDoesNotSubstituteAnotherLeanRatioOrCookedState(string prompt)
    {
        using var db = CreateDb();
        var parser = new MealParserService(new OfflineAi(), Foods(db), NullLogger<MealParserService>.Instance);
        var item = Assert.Single((await parser.ParseMealAsync(prompt, mode: "exact")).Items);
        Assert.Equal("Unparsed", item.UsdaMatchStatus);
        Assert.Equal(0, item.Calories);
    }

    [Fact]
    public async Task CookingFatValuesComeFromReferencesIncludingTraceMacros()
    {
        using var db = CreateDb();
        var parser = new MealParserService(new OfflineAi(), Foods(db), NullLogger<MealParserService>.Instance);
        var result = await parser.ParseMealAsync("2 eggs");
        var butter = result.ClarificationChips.Single(c => c.Id == "chip-cooking-oil").Options.Single(o => o.Id == "butter_1tbsp");
        var reference = db.FoodReferences.Single(f => f.Id == butter.AdditiveReferenceFoodId);
        Assert.Equal(reference.Name, butter.AdditiveItemName);
        Assert.Equal(100.4, butter.AdditiveCalories, 1);
        Assert.Equal(11.3, butter.AdditiveFat, 1);
        Assert.Equal(.1, butter.AdditiveProtein, 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task FoodSearchRejectsUnboundedLimits(int limit)
    {
        using var db = CreateDb();
        var controller = new FoodsController(Foods(db));
        Assert.IsType<BadRequestObjectResult>((await controller.Search("rice", limit)).Result);
    }

    private sealed class OfflineAi : IOpenRouterService
    {
        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
            => Task.FromException<string>(new InvalidOperationException("offline"));
        public List<string> GetAvailableFreeModels() => [];
    }
}
