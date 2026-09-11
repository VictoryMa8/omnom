using System;
using System.Collections.Generic;
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
public class DiaryController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public DiaryController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<DayTimelineDto>> GetTimeline([FromQuery] string? date)
    {
        DateOnly queryDate;
        if (string.IsNullOrWhiteSpace(date) || !DateOnly.TryParse(date, out queryDate))
        {
            queryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        // Fetch active target for this date (or latest prior target)
        var targetEntity = await _dbContext.DailyTargets
            .Where(t => t.EffectiveDate <= queryDate)
            .OrderByDescending(t => t.EffectiveDate)
            .ThenByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        targetEntity ??= await _dbContext.DailyTargets.OrderBy(t => t.Id).FirstOrDefaultAsync()
            ?? new DailyTarget
            {
                Name = "Maintenance (2,500 kcal)",
                TargetCalories = 2500,
                TargetProtein = 180,
                TargetCarbs = 275,
                TargetFat = 75,
                TargetFiber = 35
            };

        var targetDto = new DailyTargetDto(
            targetEntity.Id,
            targetEntity.EffectiveDate,
            targetEntity.Name,
            targetEntity.TargetCalories,
            targetEntity.TargetProtein,
            targetEntity.TargetCarbs,
            targetEntity.TargetFat,
            targetEntity.TargetFiber
        );

        // Fetch all meals for this date in order of creation
        var meals = await _dbContext.MealEntries
            .Where(m => m.Date == queryDate)
            .Include(m => m.Items)
            .OrderBy(m => m.Id)
            .ToListAsync();

        double runningCal = 0;
        double runningP = 0;
        double runningC = 0;
        double runningF = 0;
        double runningFib = 0;

        var mealDtos = new List<MealEntryDto>();

        foreach (var meal in meals)
        {
            runningCal += meal.TotalCalories;
            runningP += meal.TotalProtein;
            runningC += meal.TotalCarbs;
            runningF += meal.TotalFat;
            runningFib += meal.TotalFiber;

            var itemDtos = meal.Items.Select(i => new MealItemDto(
                i.Id,
                i.FoodName,
                i.Quantity,
                i.Unit,
                i.Grams,
                i.Calories,
                i.Protein,
                i.Carbs,
                i.Fat,
                i.Fiber,
                i.UsdaFdcId,
                i.SelectedClarification
            )).ToList();

            mealDtos.Add(new MealEntryDto(
                meal.Id,
                meal.Date,
                meal.Time,
                meal.Name,
                meal.RawDescription,
                meal.Notes,
                Math.Round(meal.TotalCalories, 1),
                Math.Round(meal.TotalProtein, 1),
                Math.Round(meal.TotalCarbs, 1),
                Math.Round(meal.TotalFat, 1),
                Math.Round(meal.TotalFiber, 1),
                Math.Round(runningCal, 1),
                Math.Round(runningP, 1),
                Math.Round(runningC, 1),
                Math.Round(runningF, 1),
                itemDtos
            ));
        }

        var consumedCal = Math.Round(runningCal, 1);
        var consumedP = Math.Round(runningP, 1);
        var consumedC = Math.Round(runningC, 1);
        var consumedF = Math.Round(runningF, 1);
        var consumedFib = Math.Round(runningFib, 1);

        var remainingCal = Math.Round(targetDto.TargetCalories - consumedCal, 1);
        var remainingP = Math.Round(targetDto.TargetProtein - consumedP, 1);
        var remainingC = Math.Round(targetDto.TargetCarbs - consumedC, 1);
        var remainingF = Math.Round(targetDto.TargetFat - consumedF, 1);

        return Ok(new DayTimelineDto(
            queryDate,
            targetDto,
            consumedCal,
            consumedP,
            consumedC,
            consumedF,
            consumedFib,
            remainingCal,
            remainingP,
            remainingC,
            remainingF,
            mealDtos
        ));
    }

    [HttpPost("meal")]
    public async Task<IActionResult> CreateMeal([FromBody] CreateMealRequest request)
    {
        var meal = new MealEntry
        {
            Date = request.Date,
            Time = !string.IsNullOrWhiteSpace(request.Time) ? request.Time.Trim() : null,
            Name = !string.IsNullOrWhiteSpace(request.Name) ? request.Name : "Meal",
            RawDescription = request.RawDescription,
            Notes = request.Notes
        };

        foreach (var i in request.Items ?? new())
        {
            meal.Items.Add(new MealItem
            {
                FoodName = !string.IsNullOrWhiteSpace(i.FoodName) ? i.FoodName : "Food Item",
                Quantity = i.Quantity ?? 1.0,
                Unit = !string.IsNullOrWhiteSpace(i.Unit) ? i.Unit : "serving",
                Grams = i.Grams ?? 100.0,
                Calories = i.Calories ?? 0.0,
                Protein = i.Protein ?? 0.0,
                Carbs = i.Carbs ?? 0.0,
                Fat = i.Fat ?? 0.0,
                Fiber = i.Fiber ?? 0.0,
                UsdaFdcId = i.UsdaFdcId,
                SelectedClarification = i.SelectedClarification
            });
        }

        // Recalculate totals
        meal.TotalCalories = Math.Round(meal.Items.Sum(i => i.Calories), 1);
        meal.TotalProtein = Math.Round(meal.Items.Sum(i => i.Protein), 1);
        meal.TotalCarbs = Math.Round(meal.Items.Sum(i => i.Carbs), 1);
        meal.TotalFat = Math.Round(meal.Items.Sum(i => i.Fat), 1);
        meal.TotalFiber = Math.Round(meal.Items.Sum(i => i.Fiber), 1);

        _dbContext.MealEntries.Add(meal);
        await _dbContext.SaveChangesAsync();

        return Ok(new { id = meal.Id, date = meal.Date.ToString("yyyy-MM-dd"), message = "Meal added successfully" });
    }

    [HttpPut("meal/{id}")]
    public async Task<IActionResult> UpdateMeal(int id, [FromBody] UpdateMealRequest request)
    {
        var meal = await _dbContext.MealEntries
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (meal == null) return NotFound();

        meal.Time = !string.IsNullOrWhiteSpace(request.Time) ? request.Time.Trim() : null;
        meal.Name = !string.IsNullOrWhiteSpace(request.Name) ? request.Name : "Meal";
        meal.Notes = request.Notes;

        // Replace items
        _dbContext.MealItems.RemoveRange(meal.Items);
        meal.Items.Clear();

        foreach (var i in request.Items ?? new())
        {
            meal.Items.Add(new MealItem
            {
                MealEntryId = meal.Id,
                FoodName = !string.IsNullOrWhiteSpace(i.FoodName) ? i.FoodName : "Food Item",
                Quantity = i.Quantity ?? 1.0,
                Unit = !string.IsNullOrWhiteSpace(i.Unit) ? i.Unit : "serving",
                Grams = i.Grams ?? 100.0,
                Calories = i.Calories ?? 0.0,
                Protein = i.Protein ?? 0.0,
                Carbs = i.Carbs ?? 0.0,
                Fat = i.Fat ?? 0.0,
                Fiber = i.Fiber ?? 0.0,
                UsdaFdcId = i.UsdaFdcId,
                SelectedClarification = i.SelectedClarification
            });
        }

        meal.TotalCalories = Math.Round(meal.Items.Sum(i => i.Calories), 1);
        meal.TotalProtein = Math.Round(meal.Items.Sum(i => i.Protein), 1);
        meal.TotalCarbs = Math.Round(meal.Items.Sum(i => i.Carbs), 1);
        meal.TotalFat = Math.Round(meal.Items.Sum(i => i.Fat), 1);
        meal.TotalFiber = Math.Round(meal.Items.Sum(i => i.Fiber), 1);

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("meal/{id}")]
    public async Task<IActionResult> DeleteMeal(int id)
    {
        var meal = await _dbContext.MealEntries.FindAsync(id);
        if (meal == null) return NotFound();

        _dbContext.MealEntries.Remove(meal);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
}
