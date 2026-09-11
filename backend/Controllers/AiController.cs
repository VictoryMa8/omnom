using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Omnom.Api.Models;
using Omnom.Api.Services;

namespace Omnom.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly IMealParserService _mealParserService;

    public AiController(IMealParserService mealParserService)
    {
        _mealParserService = mealParserService;
    }

    [HttpPost("parse")]
    public async Task<ActionResult<AiParsedMealResult>> ParseMeal([FromBody] AiParseMealRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { message = "Prompt cannot be empty." });
        }

        if (request.Prompt.Length > 4000)
            return BadRequest(new { message = "Please keep each meal description under 4,000 characters." });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeout.CancelAfter(ParseLimits.RequestWait);
        try
        {
            var result = await _mealParserService.ParseMealAsync(request.Prompt, request.MealTypeHint, timeout.Token);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { message = "Parsing timed out. Try a shorter meal description or tap Log Fuel again to use the local parser." });
        }
    }
}
