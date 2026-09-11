using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace Omnom.Tests;

public class ComplexPromptDiagnostics
{
    private readonly ITestOutputHelper _output;

    public ComplexPromptDiagnostics(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DiagnoseComplexMealPrompt()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("diag_" + Guid.NewGuid())
            .Options;

        using var db = new AppDbContext(options);
        DbInitializer.Initialize(db);

        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var usdaService = new UsdaFoodService(httpClient, db, config, NullLogger<UsdaFoodService>.Instance);
        var openRouterService = new OpenRouterService(httpClient, config, db, NullLogger<OpenRouterService>.Instance);
        var parser = new MealParserService(openRouterService, usdaService, NullLogger<MealParserService>.Instance);

        var prompt = "a medium size plate of chicken breast, 2 slices aldi boule bread, few teaspoons of jam, a medium light smoothie from orange julius, a 8oz decaf latte w/ dairy milk, 2 more slices of boule, maybe 150g ground lamb, 120g rice, a few tablespoons of hummus, a sprinkle of feta, and maybe 2 handfuls of green grapes";

        var result = await parser.ParseMealAsync(prompt);

        _output.WriteLine($"Items parsed ({result.Items.Count}):");
        foreach (var item in result.Items)
        {
            _output.WriteLine($"  -> Food: '{item.FoodName}', Qty: {item.Quantity} {item.Unit}, Grams: {item.Grams}g, Cal: {item.Calories}, P: {item.Protein}, C: {item.Carbs}, F: {item.Fat}, UsdaStatus: {item.UsdaMatchStatus}");
        }
    }
}
