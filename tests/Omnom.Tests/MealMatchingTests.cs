using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class MealMatchingTests
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

    private sealed class FailingOpenRouter : IOpenRouterService
    {
        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
            => Task.FromException<string>(new InvalidOperationException("LLM disabled for test"));

        public List<string> GetAvailableFreeModels() => new();
    }

    [Fact]
    public void HeuristicParser_HandlesLambWrapPromptPortions()
    {
        var parsed = MealParserService.FallbackHeuristicParse(
            ".75 lb of ground lamb, 1 large mission tortilla, 1 mini coke, 2 slices of pizza, 1 pack of cheetos",
            null);

        Assert.Equal(5, parsed.Items.Count);

        var lamb = parsed.Items.First(i => i.SearchQuery.Contains("lamb"));
        Assert.InRange(lamb.EstimatedGrams, 339, 341);

        var tortilla = parsed.Items.First(i => i.SearchQuery.Contains("tortilla"));
        Assert.Equal("flour tortilla", tortilla.SearchQuery);
        Assert.Equal(70, tortilla.EstimatedGrams);

        var cola = parsed.Items.First(i => i.SearchQuery == "cola");
        Assert.Equal(222, cola.EstimatedGrams);

        var pizza = parsed.Items.First(i => i.SearchQuery.Contains("pizza"));
        Assert.Equal("cheese pizza", pizza.SearchQuery);
        Assert.Equal(214, pizza.EstimatedGrams);

        var cheetos = parsed.Items.First(i => i.SearchQuery.Contains("cheeto"));
        Assert.Equal(28, cheetos.EstimatedGrams);
    }

    [Fact]
    public async Task UsdaFoodService_PrefersCheesePizzaStapleOverDessertPizzaCache()
    {
        using var db = CreateDb();
        db.FoodReferences.Add(new FoodReference
        {
            Name = "Dessert pizza",
            Category = "USDA Cached",
            NormalizedQuery = "dessert pizza pizza",
            DefaultServingGrams = 100,
            DefaultServingUnit = "g",
            CaloriesPer100g = 204,
            ProteinPer100g = 1.9,
            CarbsPer100g = 32.4,
            FatPer100g = 7.5,
            IsStaple = false
        });
        await db.SaveChangesAsync();

        var service = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);
        var match = await service.FindBestMatchAsync("pizza", allowRemote: false);

        Assert.NotNull(match);
        Assert.Equal("Cheese Pizza", match.Name);
        Assert.True(match.IsStaple);
    }

    [Fact]
    public async Task UsdaFoodService_MatchesColaAndCheetosAndTortillaStaples()
    {
        using var db = CreateDb();
        var service = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);

        var cola = await service.FindBestMatchAsync("mini coke", allowRemote: false);
        Assert.NotNull(cola);
        Assert.Contains("Cola", cola.Name);

        var chips = await service.FindBestMatchAsync("cheetos", allowRemote: false);
        Assert.NotNull(chips);
        Assert.Equal("Cheetos", chips.Name);

        var tortilla = await service.FindBestMatchAsync("mission tortilla", allowRemote: false);
        Assert.NotNull(tortilla);
        Assert.Contains("Tortilla", tortilla.Name);
    }

    [Fact]
    public async Task Parser_MapsLambWrapPromptToStaplesNotUsdaCatalogDump()
    {
        using var db = CreateDb();
        var usda = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);
        var parser = new MealParserService(new FailingOpenRouter(), usda, NullLogger<MealParserService>.Instance);

        var result = await parser.ParseMealAsync(
            ".75 lb of ground lamb, 1 large mission tortilla, 1 mini coke, 2 slices of pizza, 1 pack of cheetos");

        Assert.Equal(5, result.Items.Count);
        Assert.Contains(result.Items, i => i.FoodName.Contains("Ground Lamb") && i.Grams >= 339 && i.Grams <= 341);
        Assert.Contains(result.Items, i => i.FoodName.Contains("Tortilla") && i.Grams == 70);
        Assert.Contains(result.Items, i => i.FoodName.Contains("Cola") && i.Grams == 222);
        Assert.Contains(result.Items, i => i.FoodName == "Cheese Pizza" && i.Grams == 214);
        Assert.DoesNotContain(result.Items, i => i.FoodName.Contains("Dessert", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Items, i => i.FoodName == "Cheetos" && i.Grams == 28);
        Assert.DoesNotContain(result.Items, i => i.FoodName.Contains("Beverages", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => i.FoodName.Contains("Mission Foods", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Parser_SanitizesTruncatedMealTitle()
    {
        using var db = CreateDb();
        var usda = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);
        var parser = new MealParserService(new CompletingOpenRouter("""{"suggestedMealName":"Ground Lamb Wrap with I","items":[{"foodName":"Ground Lamb","searchQuery":"ground lamb","quantity":0.75,"unit":"lb","estimatedGrams":340}]}"""), usda, NullLogger<MealParserService>.Instance);

        var result = await parser.ParseMealAsync(".75 lb of ground lamb");
        Assert.Equal("Ground Lamb Wrap", result.SuggestedMealName);
    }

    [Fact]
    public void DbInitializer_BackfillsNewStaplesOnExistingDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppDbContext(options);
        db.FoodReferences.Add(new FoodReference
        {
            Name = "Chicken Breast (Cooked, roasted/grilled)",
            Category = "Protein",
            NormalizedQuery = "chicken breast cooked",
            IsStaple = true,
            CaloriesPer100g = 165,
            ProteinPer100g = 31
        });
        db.SaveChanges();

        Assert.DoesNotContain(db.FoodReferences, f => f.Name == "Cheese Pizza");
        DbInitializer.Initialize(db);
        Assert.Contains(db.FoodReferences, f => f.Name == "Cheese Pizza" && f.IsStaple);
        Assert.Contains(db.FoodReferences, f => f.Name == "Cheetos" && f.IsStaple);
    }

    [Theory]
    [InlineData("cheese", "Cheddar Cheese")]
    [InlineData("cheddar cheese", "Cheddar Cheese")]
    [InlineData("cottage cheese", "Cottage Cheese (Lowfat 2%)")]
    [InlineData("feta cheese", "Feta Cheese (Crumbled)")]
    [InlineData("cheese pizza", "Cheese Pizza")]
    public async Task UsdaFoodService_DistinguishesCheeseVarieties(string query, string expectedName)
    {
        using var db = CreateDb();
        var service = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);

        var match = await service.FindBestMatchAsync(query, allowRemote: false);

        Assert.NotNull(match);
        Assert.Equal(expectedName, match.Name);
    }

    [Theory]
    [InlineData("150g raw chicken thigh", "Chicken Thigh (Raw, skinless)")]
    [InlineData("150g chicken thigh cooked", "Chicken Thigh (Cooked, skinless)")]
    [InlineData("150g raw chicken breast", "Chicken Breast (Raw, boneless skinless)")]
    [InlineData("150g brown rice", "Brown Rice (Cooked)")]
    [InlineData("150g white rice dry raw", "White Rice (Dry / Raw)")]
    public async Task Parser_PreservesExplicitFoodAndPreparation(string prompt, string expectedName)
    {
        using var db = CreateDb();
        var service = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);
        var parser = new MealParserService(new FailingOpenRouter(), service, NullLogger<MealParserService>.Instance);

        var result = await parser.ParseMealAsync(prompt);

        var item = Assert.Single(result.Items);
        Assert.Equal(expectedName, item.FoodName);
        Assert.Equal(150, item.Grams);
    }

    [Fact]
    public async Task UsdaFoodService_IgnoresSearchWordsInLegacyCache()
    {
        using var db = CreateDb();
        db.FoodReferences.Add(new FoodReference
        {
            Name = "Unrelated food",
            NormalizedQuery = "unrelated food dragonfruit",
            IsStaple = false
        });
        await db.SaveChangesAsync();
        var service = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);

        Assert.Null(await service.FindBestMatchAsync("dragonfruit", allowRemote: false));
        Assert.Empty(await service.SearchFoodsAsync("dragonfruit"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UsdaFoodService_ScoresRemoteDescriptionsWithoutSearchWords(bool includeMatch)
    {
        using var db = CreateDb();
        var foods = new List<object> { new { fdcId = 1, description = "Pear", foodNutrients = new object[0] } };
        if (includeMatch)
            foods.Add(new { fdcId = 2, description = "Dragonfruit, raw", foodNutrients = new object[0] });
        var json = System.Text.Json.JsonSerializer.Serialize(new { foods });
        using var client = new HttpClient(new UsdaResponseHandler(json));
        var service = new UsdaFoodService(client, db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);

        var match = await service.FindBestMatchAsync("dragonfruit");

        if (includeMatch)
        {
            Assert.NotNull(match);
            Assert.Equal(2, match.FdcId);
            Assert.Equal("dragonfruit raw", match.NormalizedQuery);
        }
        else
        {
            Assert.Null(match);
            Assert.DoesNotContain(db.FoodReferences, f => f.FdcId == 1);
        }
    }

    private sealed class UsdaResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private sealed class CompletingOpenRouter : IOpenRouterService
    {
        private readonly string _json;
        public CompletingOpenRouter(string json) => _json = json;
        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_json);
        public List<string> GetAvailableFreeModels() => new();
    }
}
