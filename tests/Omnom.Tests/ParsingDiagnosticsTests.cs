using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Omnom.Api.Controllers;
using Omnom.Api.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omnom.Api.Data;
using Omnom.Api.Services;

namespace Omnom.Tests;

public class ParsingDiagnosticsTests
{
    private const string PrivateText = "private-meal-marker";
    private const string Secret = "secret-key-marker";
    private const string Prompt = "150g chicken breast " + PrivateText;
    private const string Completion = """{"items":[{"sourceText":"150g chicken breast","foodName":"Chicken Breast","searchQuery":"chicken breast cooked","quantity":150,"unit":"g","estimatedGrams":150}]}""";

    [Theory]
    [InlineData("success", "success", false, 1)]
    [InlineData("primary-failure", "http-failure", false, 2)]
    [InlineData("rate-limited", "rate-limited", true, 2)]
    [InlineData("total-failure", "http-failure", true, 2)]
    [InlineData("invalid", "invalid-output", false, 2)]
    [InlineData("truncated", "truncated-output", false, 2)]
    [InlineData("filtered", "filtered-output", false, 2)]
    [InlineData("timeout", "timeout", false, 2)]
    [InlineData("authentication", "authentication", true, 1)]
    [InlineData("missing", "missing-configuration", true, 0)]
    public async Task PipelineHasCorrelatedSafeStageDiagnostics(string scenario, string expected, bool fallback, int attempts)
    {
        using var db = Db();
        var logs = new Logs();
        var sent = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            sent++;
            if (scenario == "rate-limited") return Response(HttpStatusCode.TooManyRequests, PrivateText + Secret);
            if (scenario == "authentication") return Response(HttpStatusCode.Unauthorized, PrivateText + Secret);
            if (scenario == "total-failure" || scenario == "primary-failure" && sent == 1)
                return Response(HttpStatusCode.ServiceUnavailable, PrivateText + Secret);
            if (scenario == "timeout" && sent == 1) throw new TaskCanceledException(PrivateText + Secret);
            return Reply(scenario == "invalid" && sent == 1 ? "{}" : Completion,
                scenario == "truncated" && sent == 1 ? "length" : scenario == "filtered" && sent == 1 ? "content_filter" : "stop");
        }));
        var router = new OpenRouterService(client, Config(scenario != "missing"), db, logs.For<OpenRouterService>());
        var foods = new UsdaFoodService(client, db, Config(), logs.For<UsdaFoodService>());
        var parser = new MealParserService(router, foods, logs.For<MealParserService>());
        var result = await parser.ParseMealAsync(Prompt);
        Assert.Equal(attempts, sent);
        Assert.Equal(fallback, result.AiSummary?.Contains("local parser") == true);
        Assert.Contains(logs.Events, e => e.Stage == "extraction" && e.Outcome == (fallback ? "fallback" : "success"));
        Assert.Contains(logs.Events, e => e.Stage == "matching" && e.References >= 1);
        Assert.Contains(logs.Events, e => e.Stage == "request" && e.Outcome == "success");
        if (scenario == "missing") Assert.Contains(logs.Events, e => e.FallbackCategory == expected);
        else Assert.Contains(logs.Events, e => e.Stage == "model-attempt" && e.Outcome == expected);
        if (!fallback)
        {
            var success = Assert.Single(logs.Events.Where(e => e.Stage == "model-attempt" && e.Outcome == "success"));
            Assert.Equal(23, success.PromptTokens);
            Assert.Equal(42, success.CompletionTokens);
            Assert.Equal(0, success.Cost);
            Assert.Equal(OpenRouterService.Model, success.ActualModel);
        }
        Assert.Single(logs.Events.Select(e => e.CorrelationId).Distinct());
        Assert.All(logs.Events, e => { Assert.NotEqual("unscoped", e.CorrelationId); Assert.True(e.ElapsedMs >= 0); });
        logs.AssertSafe();
    }

    [Fact]
    public async Task CancellationIsRecordedAndNeverConvertedToFallback()
    {
        using var db = Db(); var logs = new Logs();
        using var cancel = new CancellationTokenSource();
        using var client = new HttpClient(new Handler(_ => { cancel.Cancel(); throw new OperationCanceledException(PrivateText + Secret, cancel.Token); }));
        var parser = new MealParserService(new OpenRouterService(client, Config(), db, logs.For<OpenRouterService>()),
            new UsdaFoodService(client, db, Config(), logs.For<UsdaFoodService>()), logs.For<MealParserService>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parser.ParseMealAsync(Prompt, cancellationToken: cancel.Token));
        Assert.Contains(logs.Events, e => e.Stage == "model-attempt" && e.Outcome == "cancelled");
        Assert.Contains(logs.Events, e => e.Stage == "request" && e.Outcome == "cancelled");
        Assert.DoesNotContain(logs.Events, e => e.Outcome == "fallback"); logs.AssertSafe();
    }

    [Fact]
    public async Task UntrustedModelNamesAndMissingUsageStaySafeAndUnknown()
    {
        using var db = Db(); var logs = new Logs();
        using var client = new HttpClient(new Handler(_ => Response(HttpStatusCode.OK, JsonSerializer.Serialize(new {
            model = PrivateText + Secret, choices = new[] { new { message = new { content = Completion } } }
        }))));
        var router = new OpenRouterService(client, Config(), db, logs.For<OpenRouterService>());
        await router.GenerateCompletionAsync("system", Prompt);
        var attempt = Assert.Single(logs.Events); Assert.Equal("other", attempt.ActualModel);
        Assert.Null(attempt.Cost); Assert.Null(attempt.PromptTokens); Assert.Null(attempt.CompletionTokens); logs.AssertSafe();
    }

    [Theory]
    [InlineData("exception", "http-failure")]
    [InlineData("http", "http-failure")]
    [InlineData("json", "invalid-output")]
    [InlineData("timeout", "timeout")]
    public async Task UsdaFailuresNeverLogQueriesCredentialsOrExceptionDetails(string failure, string category)
    {
        using var db = Db(); var logs = new Logs();
        using var client = new HttpClient(new Handler(_ => failure switch {
            "exception" => throw new HttpRequestException("https://example/?api_key=" + Secret + "&query=" + PrivateText),
            "timeout" => throw new TaskCanceledException(PrivateText + Secret),
            "http" => Response(HttpStatusCode.TooManyRequests, PrivateText + Secret),
            _ => Response(HttpStatusCode.OK, PrivateText + Secret)
        }));
        var service = new UsdaFoodService(client, db, Config(), logs.For<UsdaFoodService>());
        Assert.Null(await service.FindBestMatchAsync(PrivateText));
        var entry = Assert.Single(logs.Events); Assert.Equal("usda", entry.Stage); Assert.Equal(category, entry.Outcome); logs.AssertSafe();
    }

    [Fact]
    public async Task ConcurrentParsesHaveSeparateCorrelationIds()
    {
        using var db = Db(); var logs = new Logs();
        using var client = new HttpClient(new Handler(_ => Reply(Completion)));
        var router = new OpenRouterService(client, Config(), db, logs.For<OpenRouterService>());
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => {
            await Task.Yield(); await router.GenerateCompletionAsync("system", Prompt);
        }));
        Assert.Equal(4, logs.Events.Select(e => e.CorrelationId).Distinct().Count());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"choices\":[null]}")]
    public async Task MalformedProviderEnvelopeIsInvalidOutput(string envelope)
    {
        using var db = Db(); var logs = new Logs();
        using var client = new HttpClient(new Handler(_ => Response(HttpStatusCode.OK, envelope)));
        var router = new OpenRouterService(client, Config(), db, logs.For<OpenRouterService>());
        await Assert.ThrowsAsync<FreeModelsUnavailableException>(() => router.GenerateCompletionAsync("system", Prompt));
        Assert.Equal(2, logs.Events.Count); Assert.All(logs.Events, e => Assert.Equal("invalid-output", e.Outcome)); logs.AssertSafe();
    }

    [Fact]
    public async Task AiDeadlineIsTimeoutWhileTheCallerRemainsConnected()
    {
        using var db = Db(); var logs = new Logs();
        using var deadline = new CancellationTokenSource();
        using var client = new HttpClient(new Handler(_ => { deadline.Cancel(); throw new OperationCanceledException(PrivateText + Secret, deadline.Token); }));
        var router = new OpenRouterService(client, Config(), db, logs.For<OpenRouterService>());
        using (ParseDiagnostics.Begin(CancellationToken.None))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => router.GenerateCompletionAsync("system", Prompt, cancellationToken: deadline.Token));
        Assert.Equal("timeout", Assert.Single(logs.Events).Outcome); logs.AssertSafe();
    }

    [Theory]
    [InlineData(false, "request-deadline")]
    [InlineData(true, "cancelled")]
    public async Task ApiDeadlineAndClientCancellationHaveDistinctSafeCategories(bool aborted, string category)
    {
        var logs = new Logs();
        using var cancel = new CancellationTokenSource();
        if (aborted) cancel.Cancel();
        var controller = new AiController(new CancelledParser(), logs.For<AiController>()) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestAborted = cancel.Token } }
        };
        var result = await controller.ParseMeal(new(Prompt, null));
        Assert.Equal(504, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Contains(logs.Events, e => e.Stage == "api" && e.Outcome == category); logs.AssertSafe();
    }

    [Fact]
    public async Task SharedApplicationRateLimitHasSafeDiagnosticsAndSkipsParser()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync(); DbInitializer.Initialize(db);
        var budget = new RequestBudget(db); var logs = new Logs();
        // Filling the current hour takes milliseconds and uses the real atomic counter.
        for (var i = 0; i < 30; i++) Assert.True(await budget.Take("ai", 30, 3600));
        var action = new ControllerActionDescriptor { ControllerTypeInfo = typeof(AiController).GetTypeInfo() };
        var context = new ActionExecutingContext(new ActionContext(new DefaultHttpContext(), new RouteData(), action),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        await new RequestBudgetFilter(budget, logs.For<RequestBudgetFilter>()).OnActionExecutionAsync(context,
            () => throw new InvalidOperationException("Limited requests must not reach parsing."));
        Assert.Equal(429, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        Assert.Contains(logs.Events, e => e.Outcome == "application-rate-limited" && e.HttpStatus == 429); logs.AssertSafe();
    }

    private sealed class CancelledParser : IMealParserService
    {
        public Task<AiParsedMealResult> ParseMealAsync(string prompt, string? hint = null, CancellationToken cancellationToken = default, string? mode = null)
            => throw new OperationCanceledException(PrivateText + Secret);
    }

    private static HttpResponseMessage Reply(string content, string finish = "stop") => Response(HttpStatusCode.OK,
        JsonSerializer.Serialize(new { model = OpenRouterService.Model, choices = new[] { new { finish_reason = finish, message = new { content, reasoning = PrivateText + Secret } } },
            usage = new { cost = 0, prompt_tokens = 23, completion_tokens = 42 } }));
    private static HttpResponseMessage Response(HttpStatusCode status, string content) => new(status) { Content = new StringContent(content) };
    private static IConfiguration Config(bool key = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["OPENROUTER_API_KEY"] = key ? Secret : null, ["USDA:ApiKey"] = Secret }).Build();
    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        DbInitializer.Initialize(db); return db;
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(reply(request));
    }
    private sealed class Logs
    {
        public List<ParseDiagnostic> Events { get; } = [];
        public List<string> Text { get; } = [];
        public ILogger<T> For<T>() => new Logger<T>(this);
        public void AssertSafe()
        {
            var all = string.Join("\n", Text);
            Assert.DoesNotContain(PrivateText, all); Assert.DoesNotContain(Secret, all); Assert.DoesNotContain("api_key", all);
        }
        private sealed class Logger<T>(Logs logs) : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            {
                lock (logs)
                {
                    Assert.Null(error); logs.Text.Add(formatter(state, error));
                    if (state is DiagnosticLogState value)
                    {
                        logs.Events.Add(value.Diagnostic);
                        logs.Text.Add(JsonSerializer.Serialize(value.ToDictionary(k => k.Key, k => k.Value)));
                    }
                }
            }
        }
    }
}
