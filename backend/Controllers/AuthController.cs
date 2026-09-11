using Microsoft.AspNetCore.Mvc;
using Omnom.Api.Models;
using Omnom.Api.Services;

namespace Omnom.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(PasscodeAccess access) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new
    {
        passcodeRequired = !string.IsNullOrWhiteSpace(access.ConfiguredPasscode),
        authenticated = access.HasAccess(Request)
    });

    [HttpPost("verify")]
    public IActionResult Verify([FromBody] VerifyPasscodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(access.ConfiguredPasscode)
            || string.Equals(request.Passcode?.Trim(), access.ConfiguredPasscode, StringComparison.Ordinal))
            return Ok(new AuthResponse(true, access.GenerateToken(), "Access granted."));
        return Unauthorized(new AuthResponse(false, string.Empty, "Incorrect passcode."));
    }
}
