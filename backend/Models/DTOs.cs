using System;
using System.Collections.Generic;

namespace Omnom.Api.Models;

public record MealItemDto(
    int Id,
    string FoodName,
    double Quantity,
    string Unit,
    double Grams,
    double Calories,
    double Protein,
    double Carbs,
    double Fat,
    double Fiber,
    int? UsdaFdcId,
    string? SelectedClarification
);

public record MealEntryDto(
    int Id,
    DateOnly Date,
    string? Time,
    string Name,
    string? RawDescription,
    string? Notes,
    double TotalCalories,
    double TotalProtein,
    double TotalCarbs,
    double TotalFat,
    double TotalFiber,
    // Running cumulative totals after this meal
    double RunningCalories,
    double RunningProtein,
    double RunningCarbs,
    double RunningFat,
    List<MealItemDto> Items
);

public record DayTimelineDto(
    DateOnly Date,
    DailyTargetDto Target,
    double ConsumedCalories,
    double ConsumedProtein,
    double ConsumedCarbs,
    double ConsumedFat,
    double ConsumedFiber,
    double RemainingCalories,
    double RemainingProtein,
    double RemainingCarbs,
    double RemainingFat,
    List<MealEntryDto> Meals
);

public record CreateMealRequest(
    DateOnly Date,
    string? Time,
    string Name,
    string? RawDescription,
    string? Notes,
    List<CreateMealItemRequest> Items
);

public record CreateMealItemRequest(
    string? FoodName,
    double? Quantity,
    string? Unit,
    double? Grams,
    double? Calories,
    double? Protein,
    double? Carbs,
    double? Fat,
    double? Fiber,
    int? UsdaFdcId,
    string? SelectedClarification
);

public record UpdateMealRequest(
    string? Time,
    string Name,
    string? Notes,
    List<CreateMealItemRequest> Items
);

public record DailyTargetDto(
    int Id,
    DateOnly EffectiveDate,
    string Name,
    double TargetCalories,
    double TargetProtein,
    double TargetCarbs,
    double TargetFat,
    double? TargetFiber
);

public record UpdateTargetRequest(
    string Name,
    double TargetCalories,
    double TargetProtein,
    double TargetCarbs,
    double TargetFat,
    double? TargetFiber,
    DateOnly? EffectiveDate = null
);

public record VerifyPasscodeRequest(string Passcode);
public record AuthResponse(bool Success, string Token, string Message);

public record SettingsDto(
    string OpenRouterApiKeyMasked,
    string OpenRouterModel,
    string UsdaApiKeyMasked,
    bool HasPasscodeConfigured,
    List<string> AvailableFreeModels,
    bool PasscodeManaged = false
);

public record UpdateSettingsRequest(
    string? OpenRouterApiKey,
    string? OpenRouterModel,
    string? UsdaApiKey,
    string? NewPasscode
);

public record FoodSearchItemDto(
    int? Id,
    int? FdcId,
    string Name,
    string Category,
    double CaloriesPer100g,
    double ProteinPer100g,
    double CarbsPer100g,
    double FatPer100g,
    double FiberPer100g,
    double DefaultServingGrams,
    string DefaultServingUnit,
    bool IsStaple
);
