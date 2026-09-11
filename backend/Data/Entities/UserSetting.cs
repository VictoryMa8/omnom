using System;

namespace Omnom.Api.Data.Entities;

public class UserSetting
{
    public string Key { get; set; } = string.Empty; // e.g., "OpenRouterApiKey", "OpenRouterModel", "UsdaApiKey", "AppPasscodeHash"
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
