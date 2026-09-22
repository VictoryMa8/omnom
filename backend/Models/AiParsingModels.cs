using System.Collections.Generic;

namespace Omnom.Api.Models;

public record AiParseMealRequest(
    string Prompt,
    string? MealTypeHint, // e.g. "Breakfast", "Lunch", "Dinner", "Snack", or auto
    string? Mode = null // "ai" (default) or "exact"
);

public class AiParsedMealResult
{
    public string SuggestedMealName { get; set; } = "Meal";
    public List<AiParsedItem> Items { get; set; } = new();
    public List<AiClarificationChip> ClarificationChips { get; set; } = new();
    public string? AiSummary { get; set; }
}

public class AiParsedItem
{
    public string FoodName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1.0;
    public string Unit { get; set; } = "serving"; // "g", "oz", "scoop", "slice", "cup", etc.
    public double Grams { get; set; }

    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Carbs { get; set; }
    public double Fat { get; set; }
    public double Fiber { get; set; }

    public int? UsdaFdcId { get; set; }
    public string UsdaMatchStatus { get; set; } = "Estimated"; // "VerifiedStaple", "UsdaApiMatch", "Estimated"
    public List<string> Assumptions { get; set; } = new();
}

public class AiClarificationChip
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty; // e.g. "Chicken State", "Cooking Fat", "Beef Lean %"
    public int? TargetItemIndex { get; set; } // If modifies a specific item
    public string SelectedOptionId { get; set; } = string.Empty;
    public List<ClarificationOption> Options { get; set; } = new();
}

public class ClarificationOption
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty; // e.g. "Cooked (Default)", "Raw"
    
    // Direct replacement nutrition (per 100g or serving) OR delta adjustments
    public double? ReplacementCalories { get; set; }
    public double? ReplacementProtein { get; set; }
    public double? ReplacementCarbs { get; set; }
    public double? ReplacementFat { get; set; }
    public double? ReplacementGrams { get; set; }
    public string? ReplacementFoodName { get; set; }

    // Or additive food (e.g. adding 1 tbsp olive oil as a new item)
    public bool IsAdditiveItem { get; set; } = false;
    public string? AdditiveItemName { get; set; }
    public double AdditiveGrams { get; set; }
    public double AdditiveCalories { get; set; }
    public double AdditiveFat { get; set; }
    public double AdditiveProtein { get; set; }
    public double AdditiveCarbs { get; set; }
}
