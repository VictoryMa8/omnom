using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Controllers;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Models;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class LifterTrackerTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        DbInitializer.Initialize(context);
        return context;
    }

    [Fact]
    public void DbInitializer_SeedsBodybuildingStaples()
    {
        using var db = CreateInMemoryDbContext();

        var chicken = db.FoodReferences.FirstOrDefault(f => f.Name.Contains("Chicken Breast (Cooked"));
        Assert.NotNull(chicken);
        Assert.True(chicken.IsStaple);
        Assert.Equal(165, chicken.CaloriesPer100g);
        Assert.Equal(31.0, chicken.ProteinPer100g);

        var rice = db.FoodReferences.FirstOrDefault(f => f.Name.Contains("Jasmine Rice"));
        Assert.NotNull(rice);
        Assert.Equal(130, rice.CaloriesPer100g);

        var target = db.DailyTargets.FirstOrDefault();
        Assert.NotNull(target);
        Assert.Equal(2500, target.TargetCalories);
        Assert.Equal(180, target.TargetProtein);
    }

    [Fact]
    public async Task UsdaFoodService_FindsBestStapleMatch()
    {
        using var db = CreateInMemoryDbContext();
        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new UsdaFoodService(httpClient, db, config, NullLogger<UsdaFoodService>.Instance);

        var match = await service.FindBestMatchAsync("chicken breast cooked");
        Assert.NotNull(match);
        Assert.Contains("Chicken Breast", match.Name);
        Assert.Contains("Cooked", match.Name);
    }

    [Fact]
    public async Task MealParserService_ParsesAndGeneratesClarificationChips()
    {
        using var db = CreateInMemoryDbContext();
        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var usdaService = new UsdaFoodService(httpClient, db, config, NullLogger<UsdaFoodService>.Instance);
        var openRouterService = new OpenRouterService(httpClient, config, db, NullLogger<OpenRouterService>.Instance);
        var parser = new MealParserService(openRouterService, usdaService, NullLogger<MealParserService>.Instance);

        var result = await parser.ParseMealAsync("8oz chicken breast and 1.5 cups jasmine rice");

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);

        var chickenItem = result.Items.FirstOrDefault(i => i.FoodName.Contains("Chicken Breast"));
        Assert.NotNull(chickenItem);
        Assert.True(chickenItem.Protein > 50); // 8oz cooked chicken is ~70g protein

        // Should have clarification chips for Meat State and Cooking Fat
        Assert.NotEmpty(result.ClarificationChips);
        Assert.Contains(result.ClarificationChips, c => c.Label.Contains("State"));
        Assert.Contains(result.ClarificationChips, c => c.Label.Contains("Cooking Fat"));
    }

    [Fact]
    public async Task DiaryController_CalculatesTimelineAndRunningTotals()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new DiaryController(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Add Meal 1: Breakfast at 08:00
        await controller.CreateMeal(new CreateMealRequest(
            today,
            "08:00",
            "Breakfast",
            "4 eggs and 2 slices sourdough",
            null,
            new List<CreateMealItemRequest>
            {
                new("Whole Eggs", 4, "egg", 200, 286, 25.2, 1.4, 19.0, 0, null, null),
                new("Sourdough Bread", 2, "slice", 90, 234, 8.1, 45.0, 2.2, 2.0, null, null)
            }
        ));

        // Add Meal 2: Lunch at 12:30
        await controller.CreateMeal(new CreateMealRequest(
            today,
            "12:30",
            "Lunch",
            "8oz chicken and rice",
            null,
            new List<CreateMealItemRequest>
            {
                new("Chicken Breast", 8, "oz", 226, 373, 70.0, 0, 8.1, 0, null, null),
                new("Jasmine Rice", 1.5, "cup", 237, 308, 6.4, 66.8, 0.7, 0.9, null, null)
            }
        ));

        var actionResult = await controller.GetTimeline(today.ToString("yyyy-MM-dd"));
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var timeline = Assert.IsType<DayTimelineDto>(okResult.Value);

        Assert.Equal(2, timeline.Meals.Count);
        Assert.Equal("Breakfast", timeline.Meals[0].Name);
        Assert.Equal("Lunch", timeline.Meals[1].Name);

        // Meal 1 running calories: 286 + 234 = 520
        Assert.Equal(520, timeline.Meals[0].RunningCalories);

        // Meal 2 running calories: 520 + 373 + 308 = 1201
        Assert.Equal(1201, timeline.Meals[1].RunningCalories);

        // Total consumed
        Assert.Equal(1201, timeline.ConsumedCalories);
        Assert.Equal(109.7, timeline.ConsumedProtein); // 33.3 + 76.4

        // Remaining from 2500 kcal target
        Assert.Equal(1299, timeline.RemainingCalories);
    }

    [Fact]
    public async Task TargetsController_UpdatesTargetForDate()
    {
        using var db = CreateInMemoryDbContext();
        var targetsController = new TargetsController(db);
        var diaryController = new DiaryController(db);
        var date = DateOnly.Parse("2026-09-06");

        // Update target for this date
        await targetsController.UpdateTarget(new UpdateTargetRequest(
            "Lean Bulk (2,800 kcal)",
            2800,
            200,
            340,
            70,
            35,
            date
        ));

        var actionResult = await diaryController.GetTimeline(date.ToString("yyyy-MM-dd"));
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var timeline = Assert.IsType<DayTimelineDto>(okResult.Value);

        Assert.Equal("Lean Bulk (2,800 kcal)", timeline.Target.Name);
        Assert.Equal(2800, timeline.Target.TargetCalories);
        Assert.Equal(200, timeline.Target.TargetProtein);
    }
}
