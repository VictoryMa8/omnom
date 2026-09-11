using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Models;
using Omnom.Api.Services;

namespace Omnom.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IOpenRouterService _openRouterService;
    private readonly IConfiguration? _configuration;

    public SettingsController(AppDbContext dbContext, IOpenRouterService openRouterService, IConfiguration? configuration = null)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _openRouterService = openRouterService;
    }

    [HttpGet]
    public async Task<ActionResult<SettingsDto>> GetSettings()
    {
        var settings = await _dbContext.UserSettings.ToListAsync();

        var usdaKey = settings.FirstOrDefault(s => s.Key == "UsdaApiKey")?.Value;
        var appPasscode = settings.FirstOrDefault(s => s.Key == "AppPasscode")?.Value ?? Environment.GetEnvironmentVariable("APP_PASSCODE");

        var maskedUsda = MaskKey(usdaKey ?? Environment.GetEnvironmentVariable("USDA_API_KEY"));

        return Ok(new SettingsDto(
            string.Empty,
            OpenRouterService.Model,
            maskedUsda,
            !string.IsNullOrWhiteSpace(appPasscode),
            _openRouterService.GetAvailableFreeModels(),
            _configuration?.GetValue<bool>("REQUIRE_PASSCODE") ?? false
        ));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        if (request.OpenRouterApiKey != null || request.OpenRouterModel != null)
            return BadRequest(new { message = "OpenRouter is managed by the server and cannot be changed in Settings." });

        if (request.UsdaApiKey != null)
        {
            await UpsertSetting("UsdaApiKey", request.UsdaApiKey.Trim());
        }

        if (request.NewPasscode != null)
        {
            if (_configuration?.GetValue<bool>("REQUIRE_PASSCODE") == true)
                return BadRequest(new { message = "The hosted access PIN is managed by the server." });
            await UpsertSetting("AppPasscode", request.NewPasscode.Trim());
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private async Task UpsertSetting(string key, string value)
    {
        var existing = await _dbContext.UserSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.UserSettings.Add(new UserSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        if (key.Length <= 8) return "••••••••";
        return key.Substring(0, 4) + "••••••••" + key.Substring(key.Length - 4);
    }
}
