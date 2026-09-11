using System;

namespace Omnom.Api.Data.Entities;

public class DailyTarget
{
    public int Id { get; set; }
    public DateOnly EffectiveDate { get; set; } // The date from which this target applies
    public string Name { get; set; } = "Current Target"; // e.g. "Cut (2200 kcal)", "Bulk (3200 kcal)", "Training Day"
    
    public double TargetCalories { get; set; } = 2500;
    public double TargetProtein { get; set; } = 180;
    public double TargetCarbs { get; set; } = 275;
    public double TargetFat { get; set; } = 75;
    public double? TargetFiber { get; set; } = 35;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
