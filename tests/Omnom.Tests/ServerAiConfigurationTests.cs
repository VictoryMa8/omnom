using System.Net;
using System.Text;
using System.Text.Json;
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

public class ServerAiConfigurationTests
{
    [Fact]
    public async Task ServerKeyAndPinnedModelOverrideOldSettingsAndRequestedModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.UserSettings.AddRange(new UserSetting { Key = "OpenRouterApiKey", Value = "old-key" },
            new UserSetting { Key = "OpenRouterModel", Value = "old-model" });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["OPENROUTER_API_KEY"] = "server-test-key" }).Build();
        var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var service = new OpenRouterService(client, config, db, NullLogger<OpenRouterService>.Instance);
        Assert.Equal("{}", await service.GenerateCompletionAsync("system", "meal", "client-model"));
        Assert.Equal("server-test-key", handler.Key);
        Assert.Equal(OpenRouterService.Model, handler.Model);
        var controller = new SettingsController(db, service);
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateSettings(
            new UpdateSettingsRequest("replacement", null, null, null)));
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateSettings(
            new UpdateSettingsRequest(null, "replacement-model", null, null)));
        var settings = Assert.IsType<SettingsDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetSettings()).Result).Value);
        Assert.Empty(settings.OpenRouterApiKeyMasked);
        Assert.Equal(OpenRouterService.Model, settings.OpenRouterModel);
    }

    [Fact]
    public void EnvironmentFileLoadsFromBackendAndPreservesExistingEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), "omnom-env-" + Guid.NewGuid());
        var key = "OMNOM_TEST_" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(root, "backend"));
        try
        {
            File.WriteAllText(Path.Combine(root, ".env"), $"# comment\n{key}=\"file-value\"\n");
            EnvironmentFile.Load(Path.Combine(root, "backend"));
            Assert.Equal("file-value", Environment.GetEnvironmentVariable(key));
            Environment.SetEnvironmentVariable(key, "external-value");
            EnvironmentFile.Load(root);
            Assert.Equal("external-value", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            Directory.Delete(root, true);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Key { get; private set; }
        public string? Model { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Key = request.Headers.Authorization?.Parameter;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Model = payload.RootElement.GetProperty("model").GetString();
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}", Encoding.UTF8, "application/json")
            };
        }
    }
}
