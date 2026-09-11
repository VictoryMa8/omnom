using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Omnom.Api.Models;
using Omnom.Api.Services;

namespace Omnom.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController : ControllerBase
{
    private readonly IUsdaFoodService _usdaFoodService;

    public FoodsController(IUsdaFoodService usdaFoodService)
    {
        _usdaFoodService = usdaFoodService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<FoodSearchItemDto>>> Search([FromQuery] string? q, [FromQuery] int limit = 15)
    {
        var foods = await _usdaFoodService.SearchFoodsAsync(q ?? string.Empty, limit);
        var dtos = foods.Select(f => new FoodSearchItemDto(
            f.Id,
            f.FdcId,
            f.Name,
            f.Category,
            f.CaloriesPer100g,
            f.ProteinPer100g,
            f.CarbsPer100g,
            f.FatPer100g,
            f.FiberPer100g,
            f.DefaultServingGrams,
            f.DefaultServingUnit,
            f.IsStaple
        )).ToList();

        return Ok(dtos);
    }
}
