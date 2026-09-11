using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using Omnom.Api.Controllers;
using Omnom.Api.Data;

namespace Omnom.Api.Services;

public sealed class PasscodeAccess(AppDbContext db, IConfiguration configuration)
{
    // Without a configured secret, sessions expire when the server restarts.
    private static readonly byte[] ProcessSecret = RandomNumberGenerator.GetBytes(64);

    public string? ConfiguredPasscode => configuration.GetValue<bool>("REQUIRE_PASSCODE")
        ? configuration["APP_PASSCODE"]?.Trim()
        :
        (db.UserSettings.FirstOrDefault(s => s.Key == "AppPasscode")?.Value
        ?? Environment.GetEnvironmentVariable("APP_PASSCODE")
        ?? configuration["AppPasscode"])?.Trim();

    private SymmetricSecurityKey SigningKey
    {
        get
        {
            var secret = configuration["Jwt:Secret"];
            var bytes = string.IsNullOrWhiteSpace(secret) ? ProcessSecret : Encoding.UTF8.GetBytes(secret);
            // Changing the passcode invalidates previously issued sessions.
            return new SymmetricSecurityKey(HMACSHA256.HashData(bytes,
                Encoding.UTF8.GetBytes(ConfiguredPasscode ?? "")));
        }
    }

    public string GenerateToken() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: "omnom", audience: "omnom", expires: DateTime.UtcNow.AddDays(365),
        signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)));

    public bool HasAccess(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(ConfiguredPasscode)) return !configuration.GetValue<bool>("REQUIRE_PASSCODE");
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(header[7..].Trim(), new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = SigningKey,
                ValidateIssuer = true, ValidIssuer = "omnom",
                ValidateAudience = true, ValidAudience = "omnom",
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
            }, out _);
            return true;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return false;
        }
    }
}

public sealed class PasscodeAccessFilter(PasscodeAccess access, IConfiguration configuration) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action)
        {
            if (configuration.GetValue<bool>("PUBLIC_DIARY"))
            {
                // Public users only need parsing. Never expose the legacy shared diary or settings.
                if (action.ControllerTypeInfo.AsType() == typeof(AiController)) return;
                context.Result = new NotFoundResult();
                return;
            }
            if (action.ControllerTypeInfo.AsType() == typeof(AuthController)) return;
        }
        if (!access.HasAccess(context.HttpContext.Request))
            context.Result = new UnauthorizedObjectResult(new { message = "Unlock your diary to continue." });
    }
}
