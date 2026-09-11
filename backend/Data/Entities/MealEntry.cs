using System;
using System.Collections.Generic;

namespace Omnom.Api.Data.Entities;

public class MealEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Time { get; set; } // Optional time string e.g. "12:30", or null if user logged everything at once
    public string Name { get; set; } = "Meal"; // e.g. "Meal", "Lunch", "Daily Log"
    public string? RawDescription { get; set; } // The original natural language prompt
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MealItem> Items { get; set; } = new();

    // Calculated totals for fast indexing
    public double TotalCalories { get; set; }
    public double TotalProtein { get; set; }
    public double TotalCarbs { get; set; }
    public double TotalFat { get; set; }
    public double TotalFiber { get; set; }
}
