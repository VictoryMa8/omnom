using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Omnom.Api.Data;
using Omnom.Api.Services;
using Xunit;

namespace Omnom.Tests;

public class FreeModelRoutingTests
{
    private const string Meal = "150g chicken breast";
    private const string Completion = """{"items":[{"sourceText":"150g chicken breast","foodName":"Chicken Breast","searchQuery":"chicken breast cooked","quantity":150,"unit":"g","estimatedGrams":150}]}""";

    [Theory]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task UnavailablePrimaryUsesOnlyTheFreeBackupWithItsSupportedJsonMode(int status)
    {
        using var db = CreateDb();
        var handler = new Handler((attempt, _) => attempt == 1
            ? new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("unavailable") }
            : Reply(Completion));
        using var client = new HttpClient(handler);
        var router = CreateRouter(db, client);
        Assert.Equal(Completion, await router.GenerateCompletionAsync("system", Meal, "z-ai/glm-5.2"));
        Assert.Equal(new[] { OpenRouterService.Model, "apodex/apodex-1.1-mini:free" }, handler.Payloads.Select(p => p.GetProperty("model").GetString()));
        AssertAllPricesZero(handler.Payloads);
        Assert.False(handler.Payloads[0].TryGetProperty("response_format", out _));
        var format = handler.Payloads[1].GetProperty("response_format");
        Assert.Equal("json_object", format.GetProperty("type").GetString());
        Assert.False(format.TryGetProperty("json_schema", out _));
    }

    [Theory]
    [InlineData("{}")] // valid JSON is not necessarily a meal
    [InlineData("not JSON")]
    [InlineData("{\"items\":[{\"foodName\":\"Rice\",\"sourceText\":\"a different meal\"}]}")]
    public async Task InvalidOrUngroundedPrimaryAnswerAlsoTriesTheFreeBackup(string invalid)
    {
        using var db = CreateDb();
        var handler = new Handler((attempt, _) => Reply(attempt == 1 ? invalid : Completion));
        using var client = new HttpClient(handler);
        Assert.Equal(Completion, await CreateRouter(db, client).GenerateCompletionAsync("system", Meal));
        Assert.Equal(2, handler.Payloads.Count);
        AssertAllPricesZero(handler.Payloads);
    }

    [Fact]
    public async Task ExhaustedFreeProvidersNeverTryAPaidModel()
    {
        using var db = CreateDb();
        var handler = new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("limited") });
        using var client = new HttpClient(handler);
        var router = CreateRouter(db, client);
        var error = await Assert.ThrowsAsync<FreeModelsUnavailableException>(() => router.GenerateCompletionAsync("system", Meal));
        Assert.True(error.RateLimited);
        Assert.Equal(router.GetAvailableFreeModels(), handler.Payloads.Select(p => p.GetProperty("model").GetString()));
        AssertAllPricesZero(handler.Payloads);
    }

    [Fact]
    public async Task NonzeroReportedCostIsRejected()
    {
        using var db = CreateDb();
        var handler = new Handler((_, _) => Reply(Completion, .01));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<FreeModelsUnavailableException>(() => CreateRouter(db, client).GenerateCompletionAsync("system", Meal));
        AssertAllPricesZero(handler.Payloads);
    }

    [Fact]
    public async Task InvalidKeyIsNotRetriedAgainstEveryProvider()
    {
        using var db = CreateDb();
        var handler = new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("invalid key") });
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateRouter(db, client).GenerateCompletionAsync("system", Meal));
        Assert.Single(handler.Payloads);
    }

    [Fact]
    public async Task CancellationStopsBeforeSendingRequests()
    {
        using var db = CreateDb();
        var handler = new Handler((_, _) => Reply(Completion));
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateRouter(db, client)
            .GenerateCompletionAsync("system", Meal, cancellationToken: cancellation.Token));
        Assert.Empty(handler.Payloads);
    }

    private static void AssertAllPricesZero(IEnumerable<JsonElement> payloads)
    {
        foreach (var payload in payloads)
        {
            var prices = payload.GetProperty("provider").GetProperty("max_price");
            foreach (var field in new[] { "prompt", "completion", "request", "image" })
                Assert.Equal(0, prices.GetProperty(field).GetDouble());
            Assert.False(payload.TryGetProperty("plugins", out _));
            Assert.False(payload.TryGetProperty("tools", out _));
        }
    }

    private static HttpResponseMessage Reply(string completion, double cost = 0) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new {
            choices = new[] { new { finish_reason = "stop", message = new { content = completion } } },
            usage = new { cost }
        }))
    };

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static OpenRouterService CreateRouter(AppDbContext db, HttpClient client) => new(client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OPENROUTER_API_KEY"] = "test-key" }).Build(),
        db, NullLogger<OpenRouterService>.Instance);

    private sealed class Handler(Func<int, JsonElement, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<JsonElement> Payloads { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Payloads.Add(payload.RootElement.Clone());
            return reply(Payloads.Count, Payloads[^1]);
        }
    }
}
