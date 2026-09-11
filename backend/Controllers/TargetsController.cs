using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;
using Omnom.Api.Models;

namespace Omnom.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TargetsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public TargetsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("current")]
    public async Task<ActionResult<DailyTargetDto>> GetCurrentTarget()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var target = await _dbContext.DailyTargets
            .Where(t => t.EffectiveDate <= today)
            .OrderByDescending(t => t.EffectiveDate)
            .ThenByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        target ??= await _dbContext.DailyTargets.FirstOrDefaultAsync() ?? new DailyTarget();

        return Ok(new DailyTargetDto(
            target.Id,
            target.EffectiveDate,
            target.Name,
            target.TargetCalories,
            target.TargetProtein,
            target.TargetCarbs,
            target.TargetFat,
            target.TargetFiber
        ));
    }

    [HttpPost]
    public async Task<ActionResult<DailyTargetDto>> UpdateTarget([FromBody] UpdateTargetRequest request)
    {
        var effectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var target = new DailyTarget
        {
            EffectiveDate = effectiveDate,
            Name = !string.IsNullOrWhiteSpace(request.Name) ? request.Name : "Custom Target",
            TargetCalories = request.TargetCalories,
            TargetProtein = request.TargetProtein,
            TargetCarbs = request.TargetCarbs,
            TargetFat = request.TargetFat,
            TargetFiber = request.TargetFiber,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.DailyTargets.Add(target);
        await _dbContext.SaveChangesAsync();

        return Ok(new DailyTargetDto(
            target.Id,
            target.EffectiveDate,
            target.Name,
            target.TargetCalories,
            target.TargetProtein,
            target.TargetCarbs,
            target.TargetFat,
            target.TargetFiber
        ));
    }
}
