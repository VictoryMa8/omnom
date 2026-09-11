using System;

namespace Omnom.Api.Data.Entities;

public class FoodReference
{
    public int Id { get; set; }
    public int? FdcId { get; set; } // USDA FoodData Central ID if from USDA
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "Protein", "Carb", "Fat", "Produce", "Dairy"
    public string NormalizedQuery { get; set; } = string.Empty; // lower-cased keywords for fast matching
    
    // Default serving size
    public double DefaultServingGrams { get; set; } = 100;
    public string DefaultServingUnit { get; set; } = "g";

    // Macro values per 100g
    public double CaloriesPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }
    public double FiberPer100g { get; set; }

    public bool IsStaple { get; set; } = false; // True if one of the curated lifter staple foods
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
