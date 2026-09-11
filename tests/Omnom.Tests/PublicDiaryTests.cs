using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Controllers;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class PublicDiaryTests
{
    [Fact]
    public void PublicModeAllowsParsingButNeverExposesLegacyDiariesOrSettings()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["PUBLIC_DIARY"] = "true",
            ["REQUIRE_PASSCODE"] = "true",
            ["APP_PASSCODE"] = "should-not-be-needed"
        }).Build();
        foreach (var controller in new[] { typeof(AiController), typeof(DiaryController), typeof(TargetsController), typeof(SettingsController), typeof(AuthController) })
        {
            var context = new AuthorizationFilterContext(new ActionContext(new DefaultHttpContext(), new RouteData(),
                new ControllerActionDescriptor { ControllerTypeInfo = controller.GetTypeInfo() }), new List<IFilterMetadata>());
            new PasscodeAccessFilter(new PasscodeAccess(db, config), config).OnAuthorization(context);
            if (controller == typeof(AiController)) Assert.Null(context.Result);
            else Assert.IsType<NotFoundResult>(context.Result);
        }
    }

    [Fact]
    public async Task SlowAiUsesLabeledLocalFallbackAndCallerCancellationStopsWork()
    {
        var timedOut = new MealParserService(new TimedOutAi(), new InstantUsda(), NullLogger<MealParserService>.Instance);
        var timedOutMeal = await timedOut.ParseMealAsync("200g chicken breast");
        Assert.NotEmpty(timedOutMeal.Items);
        Assert.Contains("local parser", timedOutMeal.AiSummary);

        var hanging = new MealParserService(new HangingAi(), new InstantUsda(), NullLogger<MealParserService>.Instance,
            aiWait: TimeSpan.FromMilliseconds(200));
        var elapsed = Stopwatch.StartNew();
        var hungMeal = await hanging.ParseMealAsync("200g chicken breast");
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), $"Local fallback waited {elapsed.Elapsed}.");
        Assert.NotEmpty(hungMeal.Items);
        Assert.Contains("local parser", hungMeal.AiSummary);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            hanging.ParseMealAsync("200g chicken breast", cancellationToken: cancellation.Token));
    }

    private sealed class TimedOutAi : IOpenRouterService
    {
        public List<string> GetAvailableFreeModels() => new();
        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
            => throw new TaskCanceledException("AI deadline exceeded");
    }

    private sealed class HangingAi : IOpenRouterService
    {
        public List<string> GetAvailableFreeModels() => new();
        public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, string? model = null, CancellationToken cancellationToken = default)
            => new TaskCompletionSource<string>().Task;
    }

    private sealed class InstantUsda : IUsdaFoodService
    {
        public Task<FoodReference?> FindBestMatchAsync(string query, bool allowRemote = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<FoodReference?>(new FoodReference
            {
                Name = "Chicken Breast",
                NormalizedQuery = "chicken breast",
                CaloriesPer100g = 165,
                ProteinPer100g = 31,
                DefaultServingGrams = 100,
                DefaultServingUnit = "g",
                IsStaple = true
            });
        }

        public Task<List<FoodReference>> SearchFoodsAsync(string query, int limit = 10) => Task.FromResult(new List<FoodReference>());
    }
}
