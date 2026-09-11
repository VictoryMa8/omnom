using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Omnom.Api.Controllers;
using Omnom.Api.Data;
using Omnom.Api.Models;
using Xunit;
using Xunit.Abstractions;

namespace Omnom.Tests;

public class ApiSerializationTests
{
    private readonly ITestOutputHelper _output;

    public ApiSerializationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestTimeDeserializationFromHHmm()
    {
        var json = "{\"date\":\"2026-09-06\",\"time\":\"01:25\",\"name\":\"Meal\",\"items\":[]}";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        
        var request = JsonSerializer.Deserialize<CreateMealRequest>(json, options);
        Assert.NotNull(request);
        Assert.Equal("01:25", request.Time);
    }

    [Fact]
    public void TestTimeNullWhenLoggingAtEndOfDay()
    {
        var json = "{\"date\":\"2026-09-06\",\"name\":\"Full Day Food Log\",\"items\":[]}";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        
        var request = JsonSerializer.Deserialize<CreateMealRequest>(json, options);
        Assert.NotNull(request);
        Assert.Null(request.Time);
    }

    [Fact]
    public async Task TestCreateMealControllerDirectly()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("test_" + Guid.NewGuid())
            .Options;

        using var db = new AppDbContext(options);
        var controller = new DiaryController(db);

        var request = new CreateMealRequest(
            DateOnly.Parse("2026-09-06"),
            "01:25",
            "Meal",
            "desc",
            null,
            new List<CreateMealItemRequest>
            {
                new("Chicken", 1, "plate", 220, 363, 68, 0, 8, 0, null, null)
            }
        );

        var result = await controller.CreateMeal(request);
        _output.WriteLine($"Result type: {result?.GetType()?.Name}");
    }

    [Fact]
    public async Task TestCreateMealWithNullTimeAndNullableFields()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("test_" + Guid.NewGuid())
            .Options;

        using var db = new AppDbContext(options);
        var controller = new DiaryController(db);

        var request = new CreateMealRequest(
            DateOnly.Parse("2026-09-06"),
            null, // Optional Time is null
            "Midnight Log",
            "End of day eating",
            null,
            new List<CreateMealItemRequest>
            {
                new(null, null, null, null, null, null, null, null, null, null, null),
                new("Ground Lamb", 150, "g", 150, 420, 26, 0, 35, 0, null, null)
            }
        );

        var result = await controller.CreateMeal(request);
        Assert.NotNull(result);
        var entry = await db.MealEntries.Include(m => m.Items).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Null(entry.Time);
        Assert.Equal("Midnight Log", entry.Name);
        Assert.Equal(2, entry.Items.Count);
        Assert.Equal(420, entry.TotalCalories);
    }
}
