using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Omnom.Api.Data;
using Omnom.Api.Data.Entities;

namespace Omnom.Api.Services;

public class UsdaFoodService : IUsdaFoodService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsdaFoodService> _logger;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "few", "of", "and", "or", "maybe", "from", "with", "w", "w/", "more",
        "plate", "size", "handful", "handfuls", "sprinkle", "tablespoon", "tablespoons", "tbsp",
        "teaspoon", "teaspoons", "tsp", "serving", "servings", "cup", "cups", "slice", "slices",
        "medium", "small", "large", "mini", "oz", "ounce", "ounces", "g", "gram", "grams", "light",
        "some", "piece", "pieces", "bowl", "plate", "glass", "pack", "packs", "bag", "bags"
    };

    // USDA catalog rows that steal generic queries (e.g. "pizza" → "Dessert pizza").
    private static readonly HashSet<string> JunkDescriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        "dessert", "baby", "infant", "toddler", "imitation", "formula",
        "nfs", "nfsmw", "recipe", "candied", "filling", "mixes"
    };

    private static readonly Dictionary<string, string[]> TokenAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["coke"] = ["cola", "coca"],
        ["coca"] = ["cola", "coke"],
        ["cola"] = ["coke", "coca"],
        ["cheeto"] = ["cheetos"],
        ["cheetos"] = ["cheeto"],
        ["soda"] = ["cola", "coke"],
        ["tortilla"] = ["wrap"],
    };

    public UsdaFoodService(
        HttpClient httpClient,
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger<UsdaFoodService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetUsdaApiKey()
    {
        var dbKey = _dbContext.UserSettings.FirstOrDefault(s => s.Key == "UsdaApiKey")?.Value;
        if (!string.IsNullOrWhiteSpace(dbKey))
        {
            return dbKey.Trim();
        }

        var envKey = Environment.GetEnvironmentVariable("USDA_API_KEY") ?? _configuration["USDA:ApiKey"];
        return !string.IsNullOrWhiteSpace(envKey) ? envKey.Trim() : "DEMO_KEY";
    }

    public async Task<FoodReference?> FindBestMatchAsync(string query, bool allowRemote = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var cleanTokens = ExtractCleanTokens(query);
        if (cleanTokens.Length == 0)
        {
            cleanTokens = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        // 1. Check local database (staples + cached USDA foods)
        var firstSignificantToken = cleanTokens.FirstOrDefault(t => t.Length > 2) ?? cleanTokens.FirstOrDefault() ?? "";
        
        var localCandidates = await _dbContext.FoodReferences
            .Where(f => f.IsStaple || EF.Functions.Like(f.NormalizedQuery, $"%{firstSignificantToken}%"))
            .ToListAsync(cancellationToken);

        if (localCandidates.Any())
        {
            var bestLocal = localCandidates
                .Select(c => new
                {
                    Food = c,
                    Score = CalculateMatchScore(cleanTokens, c.NormalizedQuery, c.IsStaple, c.Name)
                })
                .Where(x => x.Score >= 0.4)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Food.IsStaple)
                .ThenBy(x => x.Food.Id)
                .FirstOrDefault();

            if (bestLocal != null)
            {
                return bestLocal.Food;
            }
        }

        if (!allowRemote) return null;

        // 2. Query USDA FoodData Central API with clean keywords
        var usdaCleanQuery = string.Join(" ", cleanTokens);
        if (string.IsNullOrWhiteSpace(usdaCleanQuery)) usdaCleanQuery = query;

        try
        {
            var apiKey = GetUsdaApiKey();
            var encodedQuery = Uri.EscapeDataString(usdaCleanQuery);
            var url = $"https://api.nal.usda.gov/fdc/v1/foods/search?query={encodedQuery}&pageSize=10&dataType=Foundation,SR%20Legacy,Survey%20(FNDDS)&api_key={apiKey}";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ParseLimits.UsdaLookup);
            using var response = await _httpClient.GetAsync(url, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(timeout.Token);
                var usdaFood = ParseUsdaSearchResponse(content, usdaCleanQuery, cleanTokens);
                if (usdaFood != null)
                {
                    if (usdaFood.FdcId is int fdcId)
                    {
                        var existing = await _dbContext.FoodReferences
                            .FirstOrDefaultAsync(f => f.FdcId == fdcId, cancellationToken);
                        if (existing != null) return existing;
                    }

                    _dbContext.FoodReferences.Add(usdaFood);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return usdaFood;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query USDA FoodData Central for {Query}", usdaCleanQuery);
        }

        // NEVER return arbitrary first candidate! Return null so caller can handle gracefully.
        return null;
    }

    public async Task<List<FoodReference>> SearchFoodsAsync(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await _dbContext.FoodReferences
                .Where(f => f.IsStaple)
                .Take(limit)
                .ToListAsync();
        }

        var cleanTokens = ExtractCleanTokens(query);
        if (cleanTokens.Length == 0) cleanTokens = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var localMatches = await _dbContext.FoodReferences.ToListAsync();

        var scored = localMatches
            .Select(f => new
            {
                Food = f,
                Score = CalculateMatchScore(cleanTokens, f.NormalizedQuery, f.IsStaple, f.Name)
            })
            .Where(x => x.Score > 0.2)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.Food)
            .ToList();

        return scored;
    }

    private static string[] ExtractCleanTokens(string query)
    {
        var normalized = Normalize(query);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !StopWords.Contains(t) && t.Length > 1)
            .ToArray();
        // Use a visible, conventional default only when no cheese variety was specified.
        return tokens.SequenceEqual(new[] { "cheese" }) ? ["cheddar", "cheese"] : tokens;
    }

    private static double CalculateMatchScore(string[] cleanTokens, string targetNormalized, bool isStaple, string? displayName = null)
    {
        if (cleanTokens.Length == 0) return 0.0;

        // Older cached rows include the original search query. Those words are not
        // evidence that the USDA food actually matches a subsequent search.
        var targetTokens = Normalize(!isStaple && displayName != null ? displayName : targetNormalized)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int matched = 0;

        foreach (var token in cleanTokens)
        {
            var variants = GetTokenVariants(token);
            if (targetTokens.Any(t => variants.Any(v => t.Equals(v, StringComparison.OrdinalIgnoreCase))))
            {
                matched += 2;
            }
            else if (targetTokens.Any(t => variants.Any(v =>
                         t.Equals(v + "s", StringComparison.OrdinalIgnoreCase) ||
                         v.Equals(t + "s", StringComparison.OrdinalIgnoreCase))))
            {
                matched += 1;
            }
        }

        if (matched == 0) return 0.0;

        double maxPossible = cleanTokens.Length * 2.0;
        double score = matched / maxPossible;

        if (isStaple) score += 0.2;

        var nameForJunk = displayName ?? targetNormalized;
        if (ContainsUnrequestedJunk(nameForJunk, cleanTokens))
        {
            score -= 0.7;
        }

        // Preserve the distinction between complete and partial staple matches.
        return Math.Max(score, 0);
    }

    private static string[] GetTokenVariants(string token)
    {
        if (TokenAliases.TryGetValue(token, out var aliases))
        {
            var variants = new string[aliases.Length + 1];
            variants[0] = token;
            Array.Copy(aliases, 0, variants, 1, aliases.Length);
            return variants;
        }

        return [token];
    }

    private static bool ContainsUnrequestedJunk(string name, string[] queryTokens)
    {
        var nameTokens = Normalize(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var junk in JunkDescriptors)
        {
            if (queryTokens.Any(t => t.Equals(junk, StringComparison.OrdinalIgnoreCase))) continue;
            if (nameTokens.Any(t => t.Equals(junk, StringComparison.OrdinalIgnoreCase))) return true;
        }

        return false;
    }

    private static string Normalize(string input)
    {
        var chars = input.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray();
        return new string(chars);
    }

    private static FoodReference? ParseUsdaSearchResponse(string json, string originalQuery, string[] cleanTokens)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("foods", out var foods) || foods.GetArrayLength() == 0)
        {
            return null;
        }

        FoodReference? best = null;
        double bestScore = -1;

        foreach (var food in foods.EnumerateArray())
        {
            var parsed = ParseUsdaFood(food, originalQuery);
            if (parsed == null) continue;

            var score = CalculateMatchScore(cleanTokens, parsed.NormalizedQuery, false, parsed.Name);
            if (score > bestScore)
            {
                bestScore = score;
                best = parsed;
            }
            else if (Math.Abs(score - bestScore) < 0.001 && best != null && parsed.Name.Length < best.Name.Length)
            {
                best = parsed;
            }
        }

        return bestScore >= 0.4 ? best : null;
    }

    private static FoodReference? ParseUsdaFood(JsonElement food, string originalQuery)
    {
        int fdcId = food.TryGetProperty("fdcId", out var idProp) ? idProp.GetInt32() : 0;
        string description = food.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? originalQuery : originalQuery;

        double calories = 0, protein = 0, carbs = 0, fat = 0, fiber = 0;

        if (food.TryGetProperty("foodNutrients", out var nutrients))
        {
            foreach (var n in nutrients.EnumerateArray())
            {
                int nutrientId = 0;
                if (n.TryGetProperty("nutrientId", out var nId)) nutrientId = nId.GetInt32();

                double value = 0;
                if (n.TryGetProperty("value", out var v)) value = v.GetDouble();

                switch (nutrientId)
                {
                    case 1008: // Energy KCAL
                    case 2047: // Energy (Atwater General Factors)
                    case 2048: // Energy (Atwater Specific Factors)
                        if (calories == 0 && value > 0) calories = value;
                        break;
                    case 1003: protein = value; break;
                    case 1005: carbs = value; break;
                    case 1004: fat = value; break;
                    case 1079: fiber = value; break;
                }
            }
        }

        if (calories == 0 && (protein > 0 || carbs > 0 || fat > 0))
        {
            calories = Math.Round(protein * 4 + carbs * 4 + fat * 9, 1);
        }

        return new FoodReference
        {
            FdcId = fdcId > 0 ? fdcId : null,
            Name = description,
            Category = "USDA Cached",
            NormalizedQuery = Normalize(description),
            DefaultServingGrams = 100,
            DefaultServingUnit = "g",
            CaloriesPer100g = Math.Round(calories, 1),
            ProteinPer100g = Math.Round(protein, 1),
            CarbsPer100g = Math.Round(carbs, 1),
            FatPer100g = Math.Round(fat, 1),
            FiberPer100g = Math.Round(fiber, 1),
            IsStaple = false
        };
    }
}
