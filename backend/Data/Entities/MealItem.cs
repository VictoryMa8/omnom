namespace Omnom.Api.Data.Entities;

public class MealItem
{
    public int Id { get; set; }
    public int MealEntryId { get; set; }
    public MealEntry? MealEntry { get; set; }

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
    public string? SelectedClarification { get; set; } // e.g., "Cooked", "Raw", "1 tbsp Olive Oil"
}
