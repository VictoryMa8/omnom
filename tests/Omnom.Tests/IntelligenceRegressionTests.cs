using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class IntelligenceRegressionTests
{
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        DbInitializer.Initialize(db);
        return db;
    }

    private static MealParserService Parser(AppDbContext db, string response) => new(
        new Router(response),
        new LocalFoods(new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance)),
        NullLogger<MealParserService>.Instance);

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"items\":[]}")]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":[null]}")]
    [InlineData("{\"items\":[{\"foodName\":\"Chicken\",\"sourceText\":\"chocolate\"}]}")]
    [InlineData("{\"items\":[{\"foodName\":\"Chicken\",\"sourceText\":\"150g chicken breast\",\"quantity\":-1}]}")]
    [InlineData("{\"items\":[{\"foodName\":\"Chicken\",\"sourceText\":\"150g chicken breast\",\"calories\":-1}]}")]
    public async Task InvalidAiResultsUseTheVisibleLocalFallback(string response)
    {
        using var db = CreateDb();
        var result = await Parser(db, response).ParseMealAsync("150g chicken breast");
        var item = Assert.Single(result.Items);
        Assert.Equal(150, item.Grams);
        Assert.Equal(247.5, item.Calories);
        Assert.Contains("local parser", result.AiSummary);
    }

    [Theory]
    [InlineData("150g chicken breast", "g", 150, 150)]
    [InlineData(".5 kg chicken breast", "kg", .5, 500)]
    [InlineData("8oz chicken breast", "oz", 8, 226.8)]
    public async Task ExplicitWeightsOverrideIncorrectAiArithmetic(string prompt, string unit, double quantity, double grams)
    {
        using var db = CreateDb();
        var response = JsonSerializer.Serialize(new { suggestedMealName = "Meal", items = new[] {
            new { sourceText = prompt, foodName = "Chicken breast", searchQuery = "chicken breast cooked",
                quantity = 1, unit = "serving", estimatedGrams = 999 }
        }});
        // Fenced responses still work if a provider wraps the requested JSON.
        var result = await Parser(db, "```json\n" + response + "\n```").ParseMealAsync(prompt);
        var item = Assert.Single(result.Items);
        Assert.Equal(grams, item.Grams);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal(unit, item.Unit);
        Assert.Null(result.AiSummary);
    }

    [Theory]
    [InlineData("100g dry rice", "rice", "White Rice (Dry / Raw)", 365)]
    [InlineData("100g raw chicken breast", "chicken breast cooked", "Chicken Breast (Raw, boneless skinless)", 120)]
    public async Task SourcePreparationSurvivesAnOverGenericAiQuery(string prompt, string query, string foodName, double calories)
    {
        using var db = CreateDb();
        var response = JsonSerializer.Serialize(new { items = new[] {
            new { sourceText = prompt, foodName = "Food", searchQuery = query, quantity = 100, unit = "g", estimatedGrams = 100 }
        }});
        var item = Assert.Single((await Parser(db, response).ParseMealAsync(prompt)).Items);
        Assert.Equal(foodName, item.FoodName);
        Assert.Equal(calories, item.Calories);
    }

    [Theory]
    [InlineData("1 can diet coke")]
    [InlineData("1 can coke zero")]
    public async Task ZeroCalorieColaNeverUsesRegularColaNutrition(string prompt)
    {
        using var db = CreateDb();
        var result = await Parser(db, "invalid").ParseMealAsync(prompt);
        var item = Assert.Single(result.Items);
        Assert.Equal(0, item.Calories);
        Assert.Equal(0, item.Carbs);
        Assert.Equal("Estimated", item.UsdaMatchStatus);
    }

    [Fact]
    public async Task UnknownFoodDoesNotReceiveGenericCalories()
    {
        using var db = CreateDb();
        var result = await Parser(db, "invalid").ParseMealAsync("100g mysteryfood");
        var item = Assert.Single(result.Items);
        Assert.Equal("Unparsed", item.UsdaMatchStatus);
        Assert.Equal(0, item.Calories);
        Assert.Contains("No nutrition match; enter values from the label", item.Assumptions);
        Assert.Contains("no nutrition match", result.AiSummary);
    }

    [Theory]
    [InlineData("raw ground lamb")]
    [InlineData("diet cola")]
    public async Task MatchingRejectsIncompatibleStaplePreparationOrSugar(string query)
    {
        using var db = CreateDb();
        var foods = new UsdaFoodService(new HttpClient(), db, new ConfigurationBuilder().Build(), NullLogger<UsdaFoodService>.Instance);
        Assert.Null(await foods.FindBestMatchAsync(query, allowRemote: false));
    }

    [Fact]
    public async Task FallbackPreservesNamedMealWeightsEggCountsAndExcludesNegations()
    {
        using var db = CreateDb();
        var result = await Parser(db, "invalid").ParseMealAsync("Lunch: 150g raw chicken breast, 100g dry rice and 2 eggs. No butter.");
        Assert.Equal("Lunch", result.SuggestedMealName);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(new double[] { 150, 100, 100 }, result.Items.Select(i => i.Grams));
        Assert.Equal(new double[] { 180, 365, 143 }, result.Items.Select(i => i.Calories));
    }

    [Fact]
    public async Task FallbackUsesFoodSpecificCupPortions()
    {
        using var db = CreateDb();
        var item = Assert.Single((await Parser(db, "invalid").ParseMealAsync("1.5 cups jasmine rice")).Items);
        Assert.Equal(237, item.Grams);
        Assert.Equal(308.1, item.Calories);
    }

    [Theory]
    [InlineData("length", "{}")]
    [InlineData("content_filter", "{}")]
    [InlineData("stop", "")]
    public async Task RouterRejectsIncompleteAnswers(string finishReason, string content)
    {
        using var db = CreateDb();
        using var client = new HttpClient(new ResponseHandler(JsonSerializer.Serialize(new {
            choices = new[] { new { finish_reason = finishReason, message = new { content } } }
        })));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["OPENROUTER_API_KEY"] = "test-key"
        }).Build();
        var router = new OpenRouterService(client, config, db, NullLogger<OpenRouterService>.Instance);
        await Assert.ThrowsAnyAsync<Exception>(() => router.GenerateCompletionAsync("system", "meal"));
    }

    private sealed class Router(string response) : IOpenRouterService
    {
        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
            => Task.FromResult(response);
        public List<string> GetAvailableFreeModels() => [];
    }

    private sealed class LocalFoods(IUsdaFoodService foods) : IUsdaFoodService
    {
        public Task<FoodReference?> FindBestMatchAsync(string query, bool allowRemote = true, CancellationToken cancellationToken = default)
            => foods.FindBestMatchAsync(query, false, cancellationToken);
        public Task<List<FoodReference>> SearchFoodsAsync(string query, int limit = 10) => foods.SearchFoodsAsync(query, limit);
    }

    private sealed class ResponseHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
    }
}
