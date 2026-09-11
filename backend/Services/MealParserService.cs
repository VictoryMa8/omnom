using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Omnom.Api.Data.Entities;
using Omnom.Api.Models;

namespace Omnom.Api.Services;

public class MealParserService : IMealParserService
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IUsdaFoodService _usdaFoodService;
    private readonly ILogger<MealParserService> _logger;
    private readonly TimeSpan _aiWait;

    public MealParserService(
        IOpenRouterService openRouterService,
        IUsdaFoodService usdaFoodService,
        ILogger<MealParserService> logger,
        TimeSpan? aiWait = null)
    {
        _openRouterService = openRouterService;
        _usdaFoodService = usdaFoodService;
        _logger = logger;
        _aiWait = aiWait ?? ParseLimits.AiWait;
    }

    public async Task<AiParsedMealResult> ParseMealAsync(string prompt, string? mealTypeHint = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new AiParsedMealResult { SuggestedMealName = mealTypeHint ?? "Meal" };
        }

        LlmExtractionResult? extraction = null;

        // 1. Try LLM parsing first, but never wait unbounded if the model hangs.
        try
        {
            var systemPrompt = BuildSystemPrompt(mealTypeHint);
            using var aiTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            aiTimeout.CancelAfter(_aiWait);
            var jsonResponse = await _openRouterService
                .GenerateCompletionAsync(systemPrompt, prompt, cancellationToken: aiTimeout.Token)
                .WaitAsync(_aiWait, cancellationToken);
            extraction = ParseLlmJson(jsonResponse);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM parsing failed or no API key set; falling back to heuristic parser.");
        }

        // 2. Fallback to deterministic regex parser if LLM failed
        var usedFallback = extraction == null;
        extraction ??= FallbackHeuristicParse(prompt, mealTypeHint);

        // 3. Cross-reference extracted items against USDA / staples database
        var result = new AiParsedMealResult
        {
            SuggestedMealName = SanitizeMealName(extraction.SuggestedMealName, prompt, mealTypeHint)
        };

        result.AiSummary = usedFallback ? "AI took too long or was unavailable. Used the local parser; review quantities and estimates." : null;
        var nutritionTime = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < extraction.Items.Count; i++)
        {
            var item = extraction.Items[i];
            var lookupQuery = CanonicalSearchQuery(!string.IsNullOrWhiteSpace(item.SearchQuery) ? item.SearchQuery : item.FoodName);
            
            var foodRef = await _usdaFoodService.FindBestMatchAsync(lookupQuery, nutritionTime.Elapsed < ParseLimits.NutritionRemoteWait, cancellationToken);
            var grams = item.EstimatedGrams > 0
                ? item.EstimatedGrams
                : EstimateGrams(item.Quantity, item.Unit, foodRef?.DefaultServingGrams ?? 100, $"{item.FoodName} {lookupQuery}");

            AiParsedItem parsedItem;
            if (foodRef != null)
            {
                var factor = grams / 100.0;
                parsedItem = new AiParsedItem
                {
                    FoodName = ChooseDisplayName(item.FoodName, foodRef),
                    Quantity = item.Quantity > 0 ? item.Quantity : Math.Round(grams / (foodRef.DefaultServingGrams > 0 ? foodRef.DefaultServingGrams : 100), 2),
                    Unit = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : foodRef.DefaultServingUnit,
                    Grams = Math.Round(grams, 1),
                    Calories = Math.Round(foodRef.CaloriesPer100g * factor, 1),
                    Protein = Math.Round(foodRef.ProteinPer100g * factor, 1),
                    Carbs = Math.Round(foodRef.CarbsPer100g * factor, 1),
                    Fat = Math.Round(foodRef.FatPer100g * factor, 1),
                    Fiber = Math.Round(foodRef.FiberPer100g * factor, 1),
                    UsdaFdcId = foodRef.FdcId,
                    UsdaMatchStatus = foodRef.IsStaple ? "VerifiedStaple" : "UsdaApiMatch"
                };
            }
            else
            {
                // Sensible fallback macro estimation if no USDA match
                var defaultCal = item.Calories > 0 ? item.Calories : Math.Round(grams * 1.5, 1);
                parsedItem = new AiParsedItem
                {
                    FoodName = item.FoodName,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                    Unit = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "serving",
                    Grams = Math.Round(grams, 1),
                    Calories = Math.Round(defaultCal, 1),
                    Protein = Math.Round(item.Protein, 1),
                    Carbs = Math.Round(item.Carbs, 1),
                    Fat = Math.Round(item.Fat, 1),
                    Fiber = Math.Round(item.Fiber, 1),
                    UsdaMatchStatus = "Estimated"
                };
            }

            result.Items.Add(parsedItem);

            // Generate lifter clarification chips for this item
            GenerateClarificationChipsForItem(result, parsedItem, i, item);
        }

        // Add overall cooking oil / butter chip if any fried or pan-cooked foods without explicit oil
        AddGeneralCookingFatChip(result, prompt);

        return result;
    }

    private void GenerateClarificationChipsForItem(AiParsedMealResult result, AiParsedItem parsedItem, int index, LlmItem item)
    {
        var nameLower = parsedItem.FoodName.ToLowerInvariant();

        // 1. Meat Cooked vs Raw
        if ((nameLower.Contains("chicken") || nameLower.Contains("steak") || nameLower.Contains("beef") || nameLower.Contains("turkey") || nameLower.Contains("lamb"))
            && !nameLower.Contains("deli") && (item.MeatStateAmbiguous || (!nameLower.Contains("raw") && !nameLower.Contains("cooked"))))
        {
            var isCurrentlyCooked = !nameLower.Contains("raw");
            result.ClarificationChips.Add(new AiClarificationChip
            {
                Id = $"chip-prep-{index}",
                Label = $"{parsedItem.FoodName.Split('(')[0].Trim()} State",
                TargetItemIndex = index,
                SelectedOptionId = isCurrentlyCooked ? "cooked" : "raw",
                Options = new List<ClarificationOption>
                {
                    new()
                    {
                        Id = "cooked",
                        Label = "Cooked (Default)",
                        ReplacementFoodName = parsedItem.FoodName.Replace("Raw", "Cooked"),
                        ReplacementCalories = Math.Round(parsedItem.Calories, 1),
                        ReplacementProtein = Math.Round(parsedItem.Protein, 1),
                        ReplacementFat = Math.Round(parsedItem.Fat, 1),
                        ReplacementCarbs = Math.Round(parsedItem.Carbs, 1),
                        ReplacementGrams = parsedItem.Grams
                    },
                    new()
                    {
                        Id = "raw",
                        Label = "Raw",
                        ReplacementFoodName = parsedItem.FoodName.Replace("Cooked", "Raw"),
                        ReplacementCalories = Math.Round(parsedItem.Calories * 0.75, 1),
                        ReplacementProtein = Math.Round(parsedItem.Protein * 0.75, 1),
                        ReplacementFat = Math.Round(parsedItem.Fat * 0.75, 1),
                        ReplacementCarbs = 0,
                        ReplacementGrams = parsedItem.Grams
                    }
                }
            });
        }

        // 2. Ground Beef Lean Percentage
        if (nameLower.Contains("ground beef") && (item.BeefFatAmbiguous || !nameLower.Contains("%") && !nameLower.Contains("/")))
        {
            result.ClarificationChips.Add(new AiClarificationChip
            {
                Id = $"chip-beef-lean-{index}",
                Label = "Beef Lean %",
                TargetItemIndex = index,
                SelectedOptionId = "93_7",
                Options = new List<ClarificationOption>
                {
                    new()
                    {
                        Id = "93_7",
                        Label = "93/7 Lean (Default)",
                        ReplacementFoodName = "Ground Beef 93/7 Lean (Cooked)",
                        ReplacementCalories = Math.Round(parsedItem.Grams * 2.18, 1),
                        ReplacementProtein = Math.Round(parsedItem.Grams * 0.265, 1),
                        ReplacementFat = Math.Round(parsedItem.Grams * 0.115, 1),
                        ReplacementCarbs = 0,
                        ReplacementGrams = parsedItem.Grams
                    },
                    new()
                    {
                        Id = "80_20",
                        Label = "80/20 Standard",
                        ReplacementFoodName = "Ground Beef 80/20 Lean (Cooked)",
                        ReplacementCalories = Math.Round(parsedItem.Grams * 2.70, 1),
                        ReplacementProtein = Math.Round(parsedItem.Grams * 0.24, 1),
                        ReplacementFat = Math.Round(parsedItem.Grams * 0.19, 1),
                        ReplacementCarbs = 0,
                        ReplacementGrams = parsedItem.Grams
                    }
                }
            });
        }
    }

    private void AddGeneralCookingFatChip(AiParsedMealResult result, string prompt)
    {
        var lowerPrompt = prompt.ToLowerInvariant();
        bool hasFatExplicit = lowerPrompt.Contains("oil") || lowerPrompt.Contains("butter") || lowerPrompt.Contains("spray") || lowerPrompt.Contains("ghee");
        bool hasPanCooked = result.Items.Any(i => 
        {
            var name = i.FoodName.ToLowerInvariant();
            return name.Contains("egg") || name.Contains("steak") || name.Contains("chicken") || name.Contains("beef") || name.Contains("lamb") || name.Contains("sauteed");
        });

        if (hasPanCooked && !hasFatExplicit)
        {
            result.ClarificationChips.Add(new AiClarificationChip
            {
                Id = "chip-cooking-oil",
                Label = "Cooking Fat",
                TargetItemIndex = null,
                SelectedOptionId = "none",
                Options = new List<ClarificationOption>
                {
                    new() { Id = "none", Label = "0-Cal Spray / Airfry" },
                    new()
                    {
                        Id = "olive_oil_1tbsp",
                        Label = "+1 tbsp Olive Oil (+124 kcal)",
                        IsAdditiveItem = true,
                        AdditiveItemName = "Extra Virgin Olive Oil",
                        AdditiveGrams = 14,
                        AdditiveCalories = 124,
                        AdditiveProtein = 0,
                        AdditiveCarbs = 0,
                        AdditiveFat = 14
                    },
                    new()
                    {
                        Id = "butter_1tbsp",
                        Label = "+1 tbsp Butter (+100 kcal)",
                        IsAdditiveItem = true,
                        AdditiveItemName = "Butter",
                        AdditiveGrams = 14,
                        AdditiveCalories = 100,
                        AdditiveProtein = 0.1,
                        AdditiveCarbs = 0,
                        AdditiveFat = 11.5
                    }
                }
            });
        }
    }

    private static string SanitizeMealName(string? suggested, string prompt, string? mealTypeHint)
    {
        var fallback = DetectMealName(prompt, mealTypeHint);
        if (string.IsNullOrWhiteSpace(suggested)) return fallback;

        var name = Regex.Replace(suggested.Trim(), @"\s+", " ");
        name = Regex.Replace(name, @"(\s+(with|and|or|&)\s+[A-Za-z])$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s+(with|and|or|&)$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s+[A-Za-z]$", "");

        if (name.Length > 48)
        {
            var cut = name.LastIndexOf(' ', 48);
            name = cut > 10 ? name[..cut] : name[..48];
        }

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static string ChooseDisplayName(string parsedName, FoodReference foodRef)
    {
        if (foodRef.IsStaple) return foodRef.Name;

        var usda = foodRef.Name?.Trim() ?? "";
        var parsed = (parsedName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(parsed)) return usda;

        if (usda.Contains(',') ||
            usda.StartsWith("Beverages", StringComparison.OrdinalIgnoreCase) ||
            usda.Length > parsed.Length + 18)
        {
            return CleanFoodTitle(parsed);
        }

        return usda;
    }

    private static string DetectMealName(string? prompt, string? mealTypeHint)
    {
        if (!string.IsNullOrWhiteSpace(mealTypeHint)) return mealTypeHint;
        if (string.IsNullOrWhiteSpace(prompt)) return "Meal";

        var lower = prompt.ToLowerInvariant();
        if (lower.Contains("breakfast")) return "Breakfast";
        if (lower.Contains("lunch")) return "Lunch";
        if (lower.Contains("dinner")) return "Dinner";
        if (lower.Contains("snack")) return "Snack";
        if (lower.Contains("post-workout") || lower.Contains("post workout")) return "Post-Workout";
        if (lower.Contains("pre-workout") || lower.Contains("pre workout")) return "Pre-Workout";

        return "Meal";
    }

    private static readonly HashSet<string> SizeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "large", "medium", "small", "mini", "big", "huge"
    };

    private static double EstimateGrams(double quantity, string unit, double defaultServingGrams, string? foodName = null)
    {
        var u = unit.ToLowerInvariant().Trim();
        var q = quantity > 0 ? quantity : 1.0;
        var food = (foodName ?? "").ToLowerInvariant();

        if (food.Contains("pizza") && (u is "slice" or "slices" || string.IsNullOrWhiteSpace(u) || u == "serving"))
            return q * 107;
        if ((food.Contains("coke") || food.Contains("cola") || food.Contains("soda")) &&
            (food.Contains("mini") || u == "mini"))
            return q * 222;
        if (food.Contains("tortilla") && (food.Contains("large") || u == "large"))
            return q * 70;
        if (food.Contains("tortilla"))
            return q * 45;
        if (u is "pack" or "packs" or "bag" or "bags")
            return q * 28;

        return u switch
        {
            "g" or "gram" or "grams" => q,
            "oz" or "ounce" or "ounces" => q * 28.3495,
            "lb" or "lbs" or "pound" or "pounds" => q * 453.592,
            "tbsp" or "tablespoon" or "tablespoons" => q * 15.0,
            "tsp" or "teaspoon" or "teaspoons" => q * 5.0,
            "cup" or "cups" => q * (defaultServingGrams > 0 ? defaultServingGrams : 150.0),
            "slice" or "slices" => q * 45.0, // Standard bread slice is 45g
            "scoop" or "scoops" => q * 30.0,
            "can" or "cans" => food.Contains("coke") || food.Contains("cola") || food.Contains("soda") ? q * 355.0 : q * 140.0,
            "egg" or "eggs" => q * 50.0,
            "handful" or "handfuls" => q * 80.0,
            "plate" or "plates" => q * 220.0,
            "sprinkle" or "sprinkles" => q * 20.0,
            _ => q * (defaultServingGrams > 0 ? defaultServingGrams : 100.0)
        };
    }

    private static string BuildSystemPrompt(string? mealTypeHint)
    {
        return $$"""
You are an expert sports nutritionist and food extractor for the macro tracking app 'omnom'.
Your job is to parse the user's natural language meal description into a structured JSON list of food items and realistic portion estimates.

CRITICAL RULES:
1. Extract every distinct food item.
2. Clean `searchQuery` to 1-3 generic canonical food keywords. NEVER include brand names, restaurant names, or fluff words like "medium plate", "few", "maybe", "aldi", "orange julius".
   - "aldi boule bread" -> "boule bread"
   - "medium light smoothie from orange julius" -> "fruit smoothie"
   - "a 8oz decaf latte w/ dairy milk" -> "latte"
   - "few teaspoons of jam" -> "jam"
   - "few tablespoons of hummus" -> "hummus"
   - "a sprinkle of feta" -> "feta cheese"
   - "2 handfuls of green grapes" -> "grapes"
   - "maybe 150g ground lamb" -> "ground lamb"
   - "medium size plate of chicken breast" -> "chicken breast cooked"
   - "2 slices of pizza" -> "cheese pizza"
   - "1 mini coke" -> "cola"
   - "1 large mission tortilla" -> "flour tortilla"
   - "1 pack of cheetos" -> "cheetos"
4. Keep `suggestedMealName` to 2-6 complete words. Never truncate mid-word (bad: "Ground Lamb Wrap with I").
5. Convert colloquial and vague quantities to realistic grams (`estimatedGrams`):
   - "medium plate of meat" = 220g
   - "1 slice bread" = 45g (2 slices = 90g)
   - "2 slices of pizza" = 214g (107g each)
   - "1 mini coke" / "mini can" = 222g
   - "1 pack of cheetos" / chips = 28g
   - "1 large tortilla" = 70g
   - ".75 lb ground lamb" = 340g
   - "few teaspoons" = 3 tsp = 15g
   - "few tablespoons" = 3 tbsp = 45g
   - "sprinkle" = 20g
   - "handful" = 80g (2 handfuls = 160g)
   - "8oz latte" = 240g
   - "medium smoothie" = 350g
   - "maybe 150g" = 150g
6. Return ONLY valid JSON matching this schema:
{
  "suggestedMealName": "{{mealTypeHint ?? "Meal"}}",
  "items": [
    {
      "foodName": "Chicken Breast",
      "searchQuery": "chicken breast cooked",
      "quantity": 1,
      "unit": "plate",
      "estimatedGrams": 220,
      "meatStateAmbiguous": true,
      "beefFatAmbiguous": false,
      "calories": 363,
      "protein": 68,
      "carbs": 0,
      "fat": 8,
      "fiber": 0
    }
  ]
}
""";
    }

    private static LlmExtractionResult? ParseLlmJson(string rawJson)
    {
        try
        {
            var firstOpen = rawJson.IndexOf('{');
            var lastClose = rawJson.LastIndexOf('}');
            if (firstOpen >= 0 && lastClose > firstOpen)
            {
                var jsonSubstring = rawJson.Substring(firstOpen, lastClose - firstOpen + 1);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<LlmExtractionResult>(jsonSubstring, options);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    // High performance fallback parser for colloquial lifter phrases
    public static LlmExtractionResult FallbackHeuristicParse(string prompt, string? mealTypeHint)
    {
        var result = new LlmExtractionResult
        {
            SuggestedMealName = DetectMealName(prompt, mealTypeHint),
            Items = new List<LlmItem>()
        };

        // Split on commas, "and", or newlines
        var segments = Regex.Split(prompt, @",|\band\b|\n|\+", RegexOptions.IgnoreCase)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));

        foreach (var rawSeg in segments)
        {
            var seg = rawSeg;

            // Strip leading filler words: "maybe", "a", "an", "and", "or"
            seg = Regex.Replace(seg, @"^(and\s+|maybe\s+|a\s+|an\s+|some\s+)+", "", RegexOptions.IgnoreCase).Trim();

            // Check colloquial patterns:
            // 1. "medium size plate of [food]"
            if (Regex.IsMatch(seg, @"^(medium|large|small)?\s*(size\s+)?plate\s+(of\s+)?(.+)$", RegexOptions.IgnoreCase))
            {
                var food = Regex.Replace(seg, @"^(medium|large|small)?\s*(size\s+)?plate\s+(of\s+)?", "", RegexOptions.IgnoreCase).Trim();
                result.Items.Add(new LlmItem
                {
                    FoodName = CleanFoodTitle(food),
                    SearchQuery = CanonicalSearchQuery(food),
                    Quantity = 1,
                    Unit = "plate",
                    EstimatedGrams = 220,
                    MeatStateAmbiguous = true
                });
                continue;
            }

            // 2. "few teaspoons/tablespoons of [food]"
            var fewMatch = Regex.Match(seg, @"^(a\s+)?few\s+(tea|table)?spoons?\s+(of\s+)?(.+)$", RegexOptions.IgnoreCase);
            if (fewMatch.Success)
            {
                bool isTbsp = fewMatch.Groups[2].Value.StartsWith("table", StringComparison.OrdinalIgnoreCase);
                var food = fewMatch.Groups[4].Value.Trim();
                result.Items.Add(new LlmItem
                {
                    FoodName = CleanFoodTitle(food),
                    SearchQuery = CanonicalSearchQuery(food),
                    Quantity = 3,
                    Unit = isTbsp ? "tbsp" : "tsp",
                    EstimatedGrams = isTbsp ? 45 : 15
                });
                continue;
            }

            // 3. "sprinkle of [food]"
            if (Regex.IsMatch(seg, @"^sprinkle\s+(of\s+)?(.+)$", RegexOptions.IgnoreCase))
            {
                var food = Regex.Replace(seg, @"^sprinkle\s+(of\s+)?", "", RegexOptions.IgnoreCase).Trim();
                result.Items.Add(new LlmItem
                {
                    FoodName = CleanFoodTitle(food),
                    SearchQuery = CanonicalSearchQuery(food),
                    Quantity = 1,
                    Unit = "tbsp",
                    EstimatedGrams = 20
                });
                continue;
            }

            // 4. "[X] handfuls? of [food]"
            var handfulMatch = Regex.Match(seg, @"^([0-9]+)?\s*handfuls?\s+(of\s+)?(.+)$", RegexOptions.IgnoreCase);
            if (handfulMatch.Success)
            {
                double count = double.TryParse(handfulMatch.Groups[1].Value, out var c) ? c : 1;
                var food = handfulMatch.Groups[3].Value.Trim();
                result.Items.Add(new LlmItem
                {
                    FoodName = CleanFoodTitle(food),
                    SearchQuery = CanonicalSearchQuery(food),
                    Quantity = count,
                    Unit = "handful",
                    EstimatedGrams = count * 80
                });
                continue;
            }

            // 5. "[X] more slices of [food]"
            var moreSlicesMatch = Regex.Match(seg, @"^([0-9]+)\s+more\s+slices?\s+(of\s+)?(.+)$", RegexOptions.IgnoreCase);
            if (moreSlicesMatch.Success)
            {
                double count = double.TryParse(moreSlicesMatch.Groups[1].Value, out var c) ? c : 2;
                var food = moreSlicesMatch.Groups[3].Value.Trim();
                result.Items.Add(new LlmItem
                {
                    FoodName = CleanFoodTitle(food),
                    SearchQuery = CanonicalSearchQuery(food),
                    Quantity = count,
                    Unit = "slice",
                    EstimatedGrams = EstimateGrams(count, "slice", 45, food)
                });
                continue;
            }

            // 6. Standard numbers with units: e.g. ".75 lb ground lamb", "2 slices pizza", "1 mini coke"
            var numMatch = Regex.Match(seg, @"^([0-9]*\.?[0-9]+(?:\/[0-9]+)?)\s*([a-zA-Z]+)?\s+(.+)$");
            if (numMatch.Success)
            {
                var qtyStr = numMatch.Groups[1].Value;
                var unit = numMatch.Groups[2].Value;
                var food = Regex.Replace(numMatch.Groups[3].Value.Trim(), @"^(of\s+|more\s+)", "", RegexOptions.IgnoreCase).Trim();

                double qty = 1;
                if (qtyStr.Contains('/'))
                {
                    var parts = qtyStr.Split('/');
                    if (parts.Length == 2 && double.TryParse(parts[0], out var n) && double.TryParse(parts[1], out var d) && d > 0)
                        qty = n / d;
                }
                else
                {
                    double.TryParse(qtyStr, out qty);
                }

                if (SizeWords.Contains(unit))
                {
                    food = $"{unit} {food}".Trim();
                    unit = "serving";
                }

                result.Items.Add(new LlmItem
                {
                    FoodName = CleanFoodTitle(food),
                    SearchQuery = CanonicalSearchQuery(food),
                    Quantity = qty > 0 ? qty : 1,
                    Unit = !string.IsNullOrWhiteSpace(unit) ? unit : "serving",
                    EstimatedGrams = EstimateGrams(qty, unit, 100, food),
                    MeatStateAmbiguous = (food.Contains("chicken", StringComparison.OrdinalIgnoreCase) || food.Contains("beef", StringComparison.OrdinalIgnoreCase) || food.Contains("lamb", StringComparison.OrdinalIgnoreCase))
                });
                continue;
            }

            // Fallback general food
            result.Items.Add(new LlmItem
            {
                FoodName = CleanFoodTitle(seg),
                SearchQuery = CanonicalSearchQuery(seg),
                Quantity = 1,
                Unit = "serving",
                EstimatedGrams = EstimateGrams(1, "serving", 100, seg)
            });
        }

        return result;
    }

    private static string CleanFoodTitle(string raw)
    {
        var cleaned = Regex.Replace(raw, @"\b(aldi|from orange julius|w/ dairy milk|decaf)\b", "", RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"^(of\s+|more\s+)", "", RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = raw;
        return char.ToUpper(cleaned[0]) + cleaned[1..];
    }

    private static string CanonicalSearchQuery(string raw)
    {
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("smoothie")) return "smoothie";
        if (lower.Contains("boule")) return "boule bread";
        if (lower.Contains("latte")) return "latte";
        if (lower.Contains("lamb")) return "ground lamb";
        if (lower.Contains("chicken")) return "chicken breast cooked";
        if (lower.Contains("feta")) return "feta cheese";
        if (lower.Contains("hummus")) return "hummus";
        if (lower.Contains("jam")) return "jam";
        if (lower.Contains("grape")) return "green grapes";
        if (lower.Contains("rice")) return "jasmine rice cooked";
        if (lower.Contains("dessert") && lower.Contains("pizza")) return "dessert pizza";
        if (lower.Contains("pizza")) return "cheese pizza";
        if (lower.Contains("coke") || lower.Contains("cola") || Regex.IsMatch(lower, @"\bsoda\b")) return "cola";
        if (lower.Contains("tortilla")) return "flour tortilla";
        if (lower.Contains("cheeto")) return "cheetos";
        return raw;
    }

    public class LlmExtractionResult
    {
        public string? SuggestedMealName { get; set; }
        public List<LlmItem> Items { get; set; } = new();
    }

    public class LlmItem
    {
        public string FoodName { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;
        public double Quantity { get; set; } = 1.0;
        public string Unit { get; set; } = "serving";
        public double EstimatedGrams { get; set; }
        public bool MeatStateAmbiguous { get; set; }
        public bool BeefFatAmbiguous { get; set; }
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Fiber { get; set; }
    }
}
