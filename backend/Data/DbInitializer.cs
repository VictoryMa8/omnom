using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Omnom.Api.Data.Entities;

namespace Omnom.Api.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();
        // Also upgrades databases created before request limiting was introduced.
        if (context.Database.IsRelational()) context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "RequestBudgets" (
                "Key" TEXT PRIMARY KEY, "Window" BIGINT NOT NULL, "Count" INTEGER NOT NULL
            )
            """);

        // Migrate SQLite MealEntries if Time column was previously marked NOT NULL
        try
        {
            if (context.Database.IsSqlite())
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(MealEntries);";
                using var reader = cmd.ExecuteReader();
                bool isTimeNotNull = false;
                while (reader.Read())
                {
                    var colName = reader.GetString(1);
                    var notNull = reader.GetInt32(3);
                    if (string.Equals(colName, "Time", StringComparison.OrdinalIgnoreCase) && notNull == 1)
                    {
                        isTimeNotNull = true;
                        break;
                    }
                }
                reader.Close();

                if (isTimeNotNull)
                {
                    using var migrateCmd = conn.CreateCommand();
                    migrateCmd.CommandText = @"
                        PRAGMA foreign_keys=off;
                        BEGIN TRANSACTION;
                        CREATE TABLE ""MealEntries_new"" (
                            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_MealEntries"" PRIMARY KEY AUTOINCREMENT,
                            ""Date"" TEXT NOT NULL,
                            ""Time"" TEXT NULL,
                            ""Name"" TEXT NOT NULL,
                            ""RawDescription"" TEXT NULL,
                            ""Notes"" TEXT NULL,
                            ""CreatedAt"" TEXT NOT NULL,
                            ""TotalCalories"" REAL NOT NULL,
                            ""TotalProtein"" REAL NOT NULL,
                            ""TotalCarbs"" REAL NOT NULL,
                            ""TotalFat"" REAL NOT NULL,
                            ""TotalFiber"" REAL NOT NULL
                        );
                        INSERT INTO ""MealEntries_new"" SELECT ""Id"", ""Date"", ""Time"", ""Name"", ""RawDescription"", ""Notes"", ""CreatedAt"", ""TotalCalories"", ""TotalProtein"", ""TotalCarbs"", ""TotalFat"", ""TotalFiber"" FROM ""MealEntries"";
                        DROP TABLE ""MealEntries"";
                        ALTER TABLE ""MealEntries_new"" RENAME TO ""MealEntries"";
                        CREATE INDEX ""IX_MealEntries_Date"" ON ""MealEntries"" (""Date"");
                        COMMIT;
                        PRAGMA foreign_keys=on;
                    ";
                    migrateCmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration warning: {ex.Message}");
        }

        // Seed default daily target if none exists
        if (!context.DailyTargets.Any())
        {
            context.DailyTargets.Add(new DailyTarget
            {
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Name = "Maintenance (2,500 kcal)",
                TargetCalories = 2500,
                TargetProtein = 180,
                TargetCarbs = 275,
                TargetFat = 75,
                TargetFiber = 35
            });
        }

        // Seed default settings if empty
        if (!context.UserSettings.Any(s => s.Key == "OpenRouterModel"))
        {
            context.UserSettings.Add(new UserSetting
            {
                Key = "OpenRouterModel",
                Value = "meta-llama/llama-3.3-70b-instruct:free"
            });
        }

        // Seed (and backfill) bodybuilding staples so existing databases pick up new foods.
        var existingStapleNames = context.FoodReferences
            .Where(f => f.IsStaple)
            .Select(f => f.Name)
            .ToHashSet();
        foreach (var staple in GetStaples())
        {
            if (!existingStapleNames.Contains(staple.Name))
            {
                context.FoodReferences.Add(staple);
            }
        }

        context.SaveChanges();
    }

    private static List<FoodReference> GetStaples()
    {
        return new List<FoodReference>
        {
            // --- POULTRY & MEATS ---
            new() {
                Name = "Chicken Breast (Cooked, roasted/grilled)",
                Category = "Protein",
                NormalizedQuery = "chicken breast cooked grilled roasted",
                DefaultServingGrams = 140,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 165,
                ProteinPer100g = 31.0,
                CarbsPer100g = 0.0,
                FatPer100g = 3.6,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Chicken Breast (Raw, boneless skinless)",
                Category = "Protein",
                NormalizedQuery = "chicken breast raw boneless skinless",
                DefaultServingGrams = 140,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 120,
                ProteinPer100g = 22.5,
                CarbsPer100g = 0.0,
                FatPer100g = 2.6,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Chicken Thigh (Cooked, skinless)",
                Category = "Protein",
                NormalizedQuery = "chicken thigh cooked skinless",
                DefaultServingGrams = 120,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 209,
                ProteinPer100g = 26.0,
                CarbsPer100g = 0.0,
                FatPer100g = 10.9,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Chicken Thigh (Raw, skinless)",
                Category = "Protein",
                NormalizedQuery = "chicken thigh raw skinless",
                DefaultServingGrams = 120,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 121,
                ProteinPer100g = 20.0,
                CarbsPer100g = 0.0,
                FatPer100g = 4.5,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Ground Beef 93/7 Lean (Cooked)",
                Category = "Protein",
                NormalizedQuery = "ground beef 93 7 93/7 cooked lean",
                DefaultServingGrams = 112,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 218,
                ProteinPer100g = 26.5,
                CarbsPer100g = 0.0,
                FatPer100g = 11.5,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Ground Beef 80/20 Lean (Cooked)",
                Category = "Protein",
                NormalizedQuery = "ground beef 80 20 80/20 cooked",
                DefaultServingGrams = 112,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 270,
                ProteinPer100g = 24.0,
                CarbsPer100g = 0.0,
                FatPer100g = 19.0,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Ground Turkey 93/7 Lean (Cooked)",
                Category = "Protein",
                NormalizedQuery = "ground turkey 93 7 93/7 cooked",
                DefaultServingGrams = 112,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 197,
                ProteinPer100g = 27.0,
                CarbsPer100g = 0.0,
                FatPer100g = 9.8,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Sirloin Steak (Cooked, trimmed)",
                Category = "Protein",
                NormalizedQuery = "sirloin steak beef cooked",
                DefaultServingGrams = 170,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 206,
                ProteinPer100g = 30.0,
                CarbsPer100g = 0.0,
                FatPer100g = 8.5,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Ribeye Steak (Cooked)",
                Category = "Protein",
                NormalizedQuery = "ribeye steak beef cooked",
                DefaultServingGrams = 200,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 291,
                ProteinPer100g = 24.0,
                CarbsPer100g = 0.0,
                FatPer100g = 21.0,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Pork Tenderloin (Cooked)",
                Category = "Protein",
                NormalizedQuery = "pork tenderloin cooked",
                DefaultServingGrams = 140,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 143,
                ProteinPer100g = 26.0,
                CarbsPer100g = 0.0,
                FatPer100g = 3.5,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Canned Tuna in Water (Drained)",
                Category = "Protein",
                NormalizedQuery = "canned tuna in water fish",
                DefaultServingGrams = 140,
                DefaultServingUnit = "can",
                CaloriesPer100g = 116,
                ProteinPer100g = 25.5,
                CarbsPer100g = 0.0,
                FatPer100g = 0.8,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Atlantic Salmon (Cooked)",
                Category = "Protein",
                NormalizedQuery = "salmon cooked fish atlantic",
                DefaultServingGrams = 150,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 206,
                ProteinPer100g = 22.1,
                CarbsPer100g = 0.0,
                FatPer100g = 12.3,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Shrimp (Cooked)",
                Category = "Protein",
                NormalizedQuery = "shrimp cooked prawns seafood",
                DefaultServingGrams = 100,
                DefaultServingUnit = "g",
                CaloriesPer100g = 99,
                ProteinPer100g = 24.0,
                CarbsPer100g = 0.2,
                FatPer100g = 0.3,
                FiberPer100g = 0.0,
                IsStaple = true
            },

            // --- EGGS & DAIRY ---
            new() {
                Name = "Whole Egg (Large, Grade A)",
                Category = "Protein",
                NormalizedQuery = "egg whole egg large eggs",
                DefaultServingGrams = 50,
                DefaultServingUnit = "egg",
                CaloriesPer100g = 143,
                ProteinPer100g = 12.6,
                CarbsPer100g = 0.7,
                FatPer100g = 9.5,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Egg Whites (Liquid / Raw)",
                Category = "Protein",
                NormalizedQuery = "egg whites liquid egg white",
                DefaultServingGrams = 100,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 52,
                ProteinPer100g = 10.9,
                CarbsPer100g = 0.7,
                FatPer100g = 0.2,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Greek Yogurt (Nonfat, Plain 0%)",
                Category = "Dairy",
                NormalizedQuery = "greek yogurt nonfat plain 0%",
                DefaultServingGrams = 170,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 59,
                ProteinPer100g = 10.2,
                CarbsPer100g = 3.6,
                FatPer100g = 0.4,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                // USDA Agriculture Handbook 8-1, item 01-009 (per 100 g):
                // https://www.govinfo.gov/content/pkg/GOVPUB-A-PURL-gpo17007/pdf/GOVPUB-A-PURL-gpo17007.pdf
                Name = "Cheddar Cheese",
                Category = "Dairy",
                NormalizedQuery = "cheddar cheese",
                DefaultServingGrams = 28,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 403,
                ProteinPer100g = 24.9,
                CarbsPer100g = 1.28,
                FatPer100g = 33.14,
                FiberPer100g = 0,
                IsStaple = true
            },
            new() {
                Name = "Cottage Cheese (Lowfat 2%)",
                Category = "Dairy",
                NormalizedQuery = "cottage cheese 2% lowfat",
                DefaultServingGrams = 113,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 81,
                ProteinPer100g = 11.0,
                CarbsPer100g = 4.8,
                FatPer100g = 2.3,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Whey Protein Powder (Isolate / Concentrate)",
                Category = "Protein",
                NormalizedQuery = "whey protein powder scoop isolate",
                DefaultServingGrams = 30,
                DefaultServingUnit = "scoop",
                CaloriesPer100g = 380,
                ProteinPer100g = 80.0,
                CarbsPer100g = 6.0,
                FatPer100g = 3.5,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Casein Protein Powder",
                Category = "Protein",
                NormalizedQuery = "casein protein powder scoop",
                DefaultServingGrams = 32,
                DefaultServingUnit = "scoop",
                CaloriesPer100g = 360,
                ProteinPer100g = 78.0,
                CarbsPer100g = 6.0,
                FatPer100g = 2.0,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Whole Milk",
                Category = "Dairy",
                NormalizedQuery = "whole milk cows milk",
                DefaultServingGrams = 244,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 61,
                ProteinPer100g = 3.2,
                CarbsPer100g = 4.8,
                FatPer100g = 3.3,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Skim Milk (Nonfat)",
                Category = "Dairy",
                NormalizedQuery = "skim milk fat free nonfat milk",
                DefaultServingGrams = 244,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 34,
                ProteinPer100g = 3.4,
                CarbsPer100g = 5.0,
                FatPer100g = 0.1,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Fairlife Core Power / Ultra-Filtered Milk 2%",
                Category = "Dairy",
                NormalizedQuery = "fairlife ultra-filtered milk core power",
                DefaultServingGrams = 240,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 58,
                ProteinPer100g = 5.4,
                CarbsPer100g = 2.5,
                FatPer100g = 1.9,
                FiberPer100g = 0.0,
                IsStaple = true
            },

            // --- GRAINS & COMPLEX CARBS ---
            new() {
                Name = "Jasmine Rice / White Rice (Cooked)",
                Category = "Carb",
                NormalizedQuery = "white rice jasmine rice cooked",
                DefaultServingGrams = 158,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 130,
                ProteinPer100g = 2.7,
                CarbsPer100g = 28.2,
                FatPer100g = 0.3,
                FiberPer100g = 0.4,
                IsStaple = true
            },
            new() {
                Name = "White Rice (Dry / Raw)",
                Category = "Carb",
                NormalizedQuery = "white rice jasmine rice dry raw",
                DefaultServingGrams = 50,
                DefaultServingUnit = "g",
                CaloriesPer100g = 365,
                ProteinPer100g = 7.1,
                CarbsPer100g = 80.0,
                FatPer100g = 0.7,
                FiberPer100g = 1.3,
                IsStaple = true
            },
            new() {
                Name = "Brown Rice (Cooked)",
                Category = "Carb",
                NormalizedQuery = "brown rice cooked",
                DefaultServingGrams = 195,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 111,
                ProteinPer100g = 2.6,
                CarbsPer100g = 23.0,
                FatPer100g = 0.9,
                FiberPer100g = 1.8,
                IsStaple = true
            },
            new() {
                Name = "Rolled Oats / Oatmeal (Dry)",
                Category = "Carb",
                NormalizedQuery = "oats oatmeal rolled oats dry",
                DefaultServingGrams = 40,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 389,
                ProteinPer100g = 16.9,
                CarbsPer100g = 66.3,
                FatPer100g = 6.9,
                FiberPer100g = 10.6,
                IsStaple = true
            },
            new() {
                Name = "Cream of Rice (Dry)",
                Category = "Carb",
                NormalizedQuery = "cream of rice hot cereal dry",
                DefaultServingGrams = 45,
                DefaultServingUnit = "g",
                CaloriesPer100g = 360,
                ProteinPer100g = 7.0,
                CarbsPer100g = 81.0,
                FatPer100g = 0.5,
                FiberPer100g = 1.0,
                IsStaple = true
            },
            new() {
                Name = "Sourdough Bread",
                Category = "Carb",
                NormalizedQuery = "sourdough bread slice",
                DefaultServingGrams = 50,
                DefaultServingUnit = "slice",
                CaloriesPer100g = 260,
                ProteinPer100g = 9.0,
                CarbsPer100g = 50.0,
                FatPer100g = 2.5,
                FiberPer100g = 2.2,
                IsStaple = true
            },
            new() {
                Name = "Whole Wheat Bread",
                Category = "Carb",
                NormalizedQuery = "whole wheat bread slice",
                DefaultServingGrams = 40,
                DefaultServingUnit = "slice",
                CaloriesPer100g = 247,
                ProteinPer100g = 13.0,
                CarbsPer100g = 41.0,
                FatPer100g = 3.4,
                FiberPer100g = 7.0,
                IsStaple = true
            },
            new() {
                Name = "Sweet Potato (Cooked/Baked)",
                Category = "Carb",
                NormalizedQuery = "sweet potato baked cooked",
                DefaultServingGrams = 150,
                DefaultServingUnit = "medium",
                CaloriesPer100g = 90,
                ProteinPer100g = 2.0,
                CarbsPer100g = 20.7,
                FatPer100g = 0.15,
                FiberPer100g = 3.3,
                IsStaple = true
            },
            new() {
                Name = "White Potato / Russet (Baked)",
                Category = "Carb",
                NormalizedQuery = "russet potato white potato baked",
                DefaultServingGrams = 170,
                DefaultServingUnit = "medium",
                CaloriesPer100g = 93,
                ProteinPer100g = 2.5,
                CarbsPer100g = 21.2,
                FatPer100g = 0.1,
                FiberPer100g = 2.2,
                IsStaple = true
            },
            new() {
                Name = "Pasta / Spaghetti (Cooked)",
                Category = "Carb",
                NormalizedQuery = "pasta spaghetti noodles cooked",
                DefaultServingGrams = 140,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 158,
                ProteinPer100g = 5.8,
                CarbsPer100g = 30.9,
                FatPer100g = 0.9,
                FiberPer100g = 1.8,
                IsStaple = true
            },
            new() {
                Name = "Plain Bagel",
                Category = "Carb",
                NormalizedQuery = "bagel plain bagel",
                DefaultServingGrams = 100,
                DefaultServingUnit = "bagel",
                CaloriesPer100g = 250,
                ProteinPer100g = 10.0,
                CarbsPer100g = 48.0,
                FatPer100g = 1.5,
                FiberPer100g = 2.3,
                IsStaple = true
            },

            // --- HEALTHY FATS & OILS ---
            new() {
                Name = "Extra Virgin Olive Oil",
                Category = "Fat",
                NormalizedQuery = "olive oil evoo extra virgin",
                DefaultServingGrams = 14,
                DefaultServingUnit = "tbsp",
                CaloriesPer100g = 884,
                ProteinPer100g = 0.0,
                CarbsPer100g = 0.0,
                FatPer100g = 100.0,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Butter",
                Category = "Fat",
                NormalizedQuery = "butter unsalted salted",
                DefaultServingGrams = 14,
                DefaultServingUnit = "tbsp",
                CaloriesPer100g = 717,
                ProteinPer100g = 0.8,
                CarbsPer100g = 0.1,
                FatPer100g = 81.0,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Peanut Butter",
                Category = "Fat",
                NormalizedQuery = "peanut butter pb creamy crunchy",
                DefaultServingGrams = 32,
                DefaultServingUnit = "tbsp",
                CaloriesPer100g = 588,
                ProteinPer100g = 25.0,
                CarbsPer100g = 20.0,
                FatPer100g = 50.0,
                FiberPer100g = 6.0,
                IsStaple = true
            },
            new() {
                Name = "Almond Butter",
                Category = "Fat",
                NormalizedQuery = "almond butter",
                DefaultServingGrams = 32,
                DefaultServingUnit = "tbsp",
                CaloriesPer100g = 614,
                ProteinPer100g = 21.0,
                CarbsPer100g = 19.0,
                FatPer100g = 56.0,
                FiberPer100g = 10.0,
                IsStaple = true
            },
            new() {
                Name = "Avocado (Raw)",
                Category = "Fat",
                NormalizedQuery = "avocado haas avocado",
                DefaultServingGrams = 100,
                DefaultServingUnit = "medium",
                CaloriesPer100g = 160,
                ProteinPer100g = 2.0,
                CarbsPer100g = 8.5,
                FatPer100g = 14.7,
                FiberPer100g = 6.7,
                IsStaple = true
            },
            new() {
                Name = "Almonds (Raw / Roasted)",
                Category = "Fat",
                NormalizedQuery = "almonds raw roasted nuts",
                DefaultServingGrams = 28,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 579,
                ProteinPer100g = 21.0,
                CarbsPer100g = 22.0,
                FatPer100g = 50.0,
                FiberPer100g = 12.5,
                IsStaple = true
            },

            // --- PRODUCE & FRUITS ---
            new() {
                Name = "Banana (Raw)",
                Category = "Fruit",
                NormalizedQuery = "banana raw bananas",
                DefaultServingGrams = 118,
                DefaultServingUnit = "medium",
                CaloriesPer100g = 89,
                ProteinPer100g = 1.1,
                CarbsPer100g = 22.8,
                FatPer100g = 0.3,
                FiberPer100g = 2.6,
                IsStaple = true
            },
            new() {
                Name = "Blueberries (Fresh)",
                Category = "Fruit",
                NormalizedQuery = "blueberries fresh blueberry",
                DefaultServingGrams = 100,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 57,
                ProteinPer100g = 0.7,
                CarbsPer100g = 14.5,
                FatPer100g = 0.3,
                FiberPer100g = 2.4,
                IsStaple = true
            },
            new() {
                Name = "Strawberries (Fresh)",
                Category = "Fruit",
                NormalizedQuery = "strawberries fresh strawberry",
                DefaultServingGrams = 150,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 32,
                ProteinPer100g = 0.7,
                CarbsPer100g = 7.7,
                FatPer100g = 0.3,
                FiberPer100g = 2.0,
                IsStaple = true
            },
            new() {
                Name = "Apple (With skin)",
                Category = "Fruit",
                NormalizedQuery = "apple with skin honeycrisp fuji",
                DefaultServingGrams = 180,
                DefaultServingUnit = "medium",
                CaloriesPer100g = 52,
                ProteinPer100g = 0.3,
                CarbsPer100g = 13.8,
                FatPer100g = 0.2,
                FiberPer100g = 2.4,
                IsStaple = true
            },
            new() {
                Name = "Broccoli (Cooked / Steamed)",
                Category = "Vegetable",
                NormalizedQuery = "broccoli steamed cooked raw",
                DefaultServingGrams = 100,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 35,
                ProteinPer100g = 2.4,
                CarbsPer100g = 7.0,
                FatPer100g = 0.4,
                FiberPer100g = 2.6,
                IsStaple = true
            },
            new() {
                Name = "Baby Spinach (Raw)",
                Category = "Vegetable",
                NormalizedQuery = "spinach baby spinach raw",
                DefaultServingGrams = 50,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 23,
                ProteinPer100g = 2.9,
                CarbsPer100g = 3.6,
                FatPer100g = 0.4,
                FiberPer100g = 2.2,
                IsStaple = true
            },
            new() {
                Name = "Asparagus (Cooked)",
                Category = "Vegetable",
                NormalizedQuery = "asparagus cooked spears",
                DefaultServingGrams = 100,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 20,
                ProteinPer100g = 2.2,
                CarbsPer100g = 3.9,
                FatPer100g = 0.1,
                FiberPer100g = 2.1,
                IsStaple = true
            },
            new() {
                Name = "Ground Lamb (Cooked)",
                Category = "Protein",
                NormalizedQuery = "lamb ground lamb cooked",
                DefaultServingGrams = 112,
                DefaultServingUnit = "g",
                CaloriesPer100g = 283,
                ProteinPer100g = 24.8,
                CarbsPer100g = 0.0,
                FatPer100g = 19.7,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Feta Cheese (Crumbled)",
                Category = "Dairy",
                NormalizedQuery = "feta cheese crumbled feta",
                DefaultServingGrams = 28,
                DefaultServingUnit = "tbsp",
                CaloriesPer100g = 265,
                ProteinPer100g = 14.2,
                CarbsPer100g = 3.9,
                FatPer100g = 21.3,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Hummus",
                Category = "Fat",
                NormalizedQuery = "hummus chickpea dip",
                DefaultServingGrams = 30,
                DefaultServingUnit = "tbsp",
                CaloriesPer100g = 166,
                ProteinPer100g = 7.9,
                CarbsPer100g = 14.3,
                FatPer100g = 9.6,
                FiberPer100g = 6.0,
                IsStaple = true
            },
            new() {
                Name = "Fruit Jam / Preserves",
                Category = "Carb",
                NormalizedQuery = "jam fruit jam jelly preserves",
                DefaultServingGrams = 20,
                DefaultServingUnit = "tsp",
                CaloriesPer100g = 240,
                ProteinPer100g = 0.4,
                CarbsPer100g = 65.0,
                FatPer100g = 0.1,
                FiberPer100g = 1.1,
                IsStaple = true
            },
            new() {
                Name = "Green Grapes (Raw)",
                Category = "Fruit",
                NormalizedQuery = "grapes green grapes seedless grapes",
                DefaultServingGrams = 100,
                DefaultServingUnit = "cup",
                CaloriesPer100g = 69,
                ProteinPer100g = 0.7,
                CarbsPer100g = 18.1,
                FatPer100g = 0.2,
                FiberPer100g = 0.9,
                IsStaple = true
            },
            new() {
                Name = "Caffe Latte (Whole Milk)",
                Category = "Dairy",
                NormalizedQuery = "latte caffe latte decaf latte dairy milk",
                DefaultServingGrams = 240,
                DefaultServingUnit = "oz",
                CaloriesPer100g = 65,
                ProteinPer100g = 3.3,
                CarbsPer100g = 5.0,
                FatPer100g = 3.6,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Fruit Smoothie",
                Category = "Carb",
                NormalizedQuery = "smoothie fruit smoothie orange julius",
                DefaultServingGrams = 350,
                DefaultServingUnit = "serving",
                CaloriesPer100g = 60,
                ProteinPer100g = 1.0,
                CarbsPer100g = 14.0,
                FatPer100g = 0.2,
                FiberPer100g = 1.0,
                IsStaple = true
            },
            new() {
                Name = "Boule Bread (Artisan Sourdough)",
                Category = "Carb",
                NormalizedQuery = "boule aldi boule bread artisan loaf",
                DefaultServingGrams = 45,
                DefaultServingUnit = "slice",
                CaloriesPer100g = 260,
                ProteinPer100g = 9.0,
                CarbsPer100g = 50.0,
                FatPer100g = 2.5,
                FiberPer100g = 2.2,
                IsStaple = true
            },
            new() {
                Name = "Cheese Pizza",
                Category = "Carb",
                NormalizedQuery = "pizza cheese pizza slice pepperoni pizza",
                DefaultServingGrams = 107,
                DefaultServingUnit = "slice",
                CaloriesPer100g = 266,
                ProteinPer100g = 11.4,
                CarbsPer100g = 33.3,
                FatPer100g = 9.8,
                FiberPer100g = 2.3,
                IsStaple = true
            },
            new() {
                Name = "Flour Tortilla (Large)",
                Category = "Carb",
                NormalizedQuery = "flour tortilla wrap burrito mission tortilla large tortilla",
                DefaultServingGrams = 70,
                DefaultServingUnit = "tortilla",
                CaloriesPer100g = 306,
                ProteinPer100g = 8.2,
                CarbsPer100g = 49.6,
                FatPer100g = 7.5,
                FiberPer100g = 2.4,
                IsStaple = true
            },
            new() {
                Name = "Cola (Regular)",
                Category = "Carb",
                NormalizedQuery = "cola coke coca cola soda pop mini coke mini cola",
                DefaultServingGrams = 355,
                DefaultServingUnit = "can",
                CaloriesPer100g = 42,
                ProteinPer100g = 0.0,
                CarbsPer100g = 10.4,
                FatPer100g = 0.0,
                FiberPer100g = 0.0,
                IsStaple = true
            },
            new() {
                Name = "Cheetos",
                Category = "Carb",
                NormalizedQuery = "cheetos cheeto cheese puffs chips pack",
                DefaultServingGrams = 28,
                DefaultServingUnit = "pack",
                CaloriesPer100g = 536,
                ProteinPer100g = 7.2,
                CarbsPer100g = 53.6,
                FatPer100g = 33.6,
                FiberPer100g = 2.4,
                IsStaple = true
            }
        };
    }
}
