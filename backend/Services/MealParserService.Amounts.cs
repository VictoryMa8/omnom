using System.Text.RegularExpressions;
using Omnom.Api.Data.Entities;

namespace Omnom.Api.Services;

public partial class MealParserService
{
    private const string AmountNumber = @"\d*\.?\d+(?:/\d+)?";
    private const string AmountUnit = @"fluid\s+ounces?|fl\.?\s*oz|millilit(?:er|re)s?|ml|lit(?:er|re)s?|kg|kilograms?|grams?|g|ounces?|oz|pounds?|lbs?|l|slices?|scoops?|cans?|bottles?|cartons?|glass(?:es)?";
    private static readonly Regex StatedAmounts = new($@"(?<![\d./-])(?<qty>{AmountNumber})\s*(?:more\s+)?(?<unit>{AmountUnit})\b", RegexOptions.IgnoreCase);
    private sealed record MealFragment(string Source, string Text, int Start, int End, bool Corrected = false, string Excluded = "");
    private sealed record StatedAmount(double Quantity, string Unit, int Index, int Length);

    private static List<StatedAmount> Amounts(string source) => StatedAmounts.Matches(source).Select(m => new StatedAmount(
        ParseExactQuantity(m.Groups["qty"].Value), NormalizeAmountUnit(m.Groups["unit"].Value), m.Index, m.Length)).ToList();

    private static string NormalizeAmountUnit(string unit) => Regex.Replace(unit.ToLowerInvariant(), @"[.\s]+", "") switch
    {
        "g" or "gram" or "grams" => "g", "kg" or "kilogram" or "kilograms" => "kg",
        "oz" or "ounce" or "ounces" => "oz", "lb" or "lbs" or "pound" or "pounds" => "lb",
        "floz" or "fluidounce" or "fluidounces" => "fl oz",
        "ml" or "milliliter" or "milliliters" or "millilitre" or "millilitres" => "ml",
        "l" or "liter" or "liters" or "litre" or "litres" => "l",
        "slices" => "slice", "scoops" => "scoop", "cans" => "can", "bottles" => "bottle",
        "cartons" => "carton", "glasses" or "glass" => "glass", _ => unit.ToLowerInvariant()
    };

    private static bool IsBeverage(string text) => Regex.IsMatch(text,
        @"\b(milk|latte|coffee|juice|smoothie|cola|coke|soda|water|tea|shake)\b", RegexOptions.IgnoreCase)
        && !Regex.IsMatch(text, @"\b(chicken|beef|lamb|pork|fish|tuna|rice|bread|butter|powder)\b", RegexOptions.IgnoreCase);

    private static string PositiveText(string text) => Regex.Replace(text,
        @"\s+\b(without|not|no(?!\s+(?:added\s+sugar|sugar\s+added)))\s+.*$", "", RegexOptions.IgnoreCase).Trim();

    // A deliberately small grammar: an immediately following "actually / make
    // that" amount replaces one earlier amount; "more" adds another portion.
    // Keep the entire literal source span for review and model grounding.
    private static List<MealFragment> MealFragments(string prompt)
    {
        var fragments = new List<MealFragment>();
        var prefix = Regex.Match(prompt, @"^\s*(breakfast|lunch|dinner|snack|post[\s-]?workout|pre[\s-]?workout)\s*:\s*", RegexOptions.IgnoreCase);
        var start = prefix.Length;
        var priorSeparator = "";
        var excluding = false;
        foreach (var delimiter in Regex.Matches(prompt, @",|;|\+|[\r\n]+|\band\b|(?<=[a-z])\.\s+", RegexOptions.IgnoreCase).Cast<Match>().Where(m => m.Index >= start)
            .Select(m => (Index: m.Index, Length: m.Length, Value: m.Value)).Append((Index: prompt.Length, Length: 0, Value: "")))
        {
            var raw = prompt[start..delimiter.Index].Trim().TrimEnd('.');
            var end = delimiter.Index;
            if (raw.Length > 0)
            {
                var negative = Regex.IsMatch(raw, @"^(no(?!\s+(?:added\s+sugar|sugar\s+added))|not|without|skipped)\b|^ignore\s+the\s+earlier\b", RegexOptions.IgnoreCase);
                var continuation = excluding && priorSeparator.Equals("and", StringComparison.OrdinalIgnoreCase)
                    && !Regex.IsMatch(raw, $@"^{AmountNumber}\s*", RegexOptions.IgnoreCase);
                if (!negative && !continuation)
                {
                    var text = PositiveText(raw);
                    var correction = Regex.Match(text, $@"^(?:actually|make\s+that|rather)\s+(?<amount>{AmountNumber}\s*(?:{AmountUnit}))\s*$", RegexOptions.IgnoreCase);
                    if (correction.Success && fragments.Count > 0 && Amounts(fragments[^1].Text) is [var earlier])
                    {
                        var previous = fragments[^1];
                        var updated = previous.Text.Remove(earlier.Index, earlier.Length).Insert(earlier.Index, correction.Groups["amount"].Value);
                        fragments[^1] = previous with { Source = prompt[previous.Start..end].Trim(), Text = updated, End = end, Corrected = true };
                    }
                    else
                    {
                        if (Regex.IsMatch(text, @"^(actually|make\s+that|rather)\b", RegexOptions.IgnoreCase) && fragments.Count > 0)
                        {
                            var previous = fragments[^1];
                            fragments[^1] = previous with { Source = prompt[previous.Start..end].Trim(), Text = previous.Text + " " + text, End = end };
                            priorSeparator = delimiter.Value; start = delimiter.Index + delimiter.Length;
                            continue;
                        }
                        var inline = Regex.Match(text, $@"\s+(?:actually|make\s+that|rather)\s+(?<amount>{AmountNumber}\s*(?:{AmountUnit}))\s*$", RegexOptions.IgnoreCase);
                        var corrected = false;
                        if (inline.Success && Amounts(text[..inline.Index]) is [var initial])
                        {
                            text = text[..inline.Index].Remove(initial.Index, initial.Length).Insert(initial.Index, inline.Groups["amount"].Value);
                            corrected = true;
                        }
                        // A bare additive count inherits food only from the immediately
                        // preceding portion with the same unit; otherwise it stays unread.
                        var more = Regex.Match(text, $@"^(?<qty>{AmountNumber})\s+more\s+(?<unit>slices?|scoops?)\s*$", RegexOptions.IgnoreCase);
                        if (more.Success && fragments.Count > 0 && Amounts(fragments[^1].Text) is [var previousAmount]
                            && previousAmount.Unit == NormalizeAmountUnit(more.Groups["unit"].Value))
                        {
                            var food = fragments[^1].Text.Remove(previousAmount.Index, previousAmount.Length).Trim();
                            text = $"{more.Groups["qty"].Value} {more.Groups["unit"].Value} {food}";
                        }
                        text = Regex.Replace(text, $@"^(?<qty>{AmountNumber})\s+more\s+", "${qty} ", RegexOptions.IgnoreCase);
                        fragments.Add(new(raw, text, start, end, corrected, raw.Length > PositiveText(raw).Length ? raw[PositiveText(raw).Length..] : ""));
                    }
                    excluding = raw != PositiveText(raw);
                }
                else excluding = true;
            }
            priorSeparator = delimiter.Value;
            start = delimiter.Index + delimiter.Length;
        }
        return fragments;
    }

    private static void SetMass(LlmItem item, double quantity, string unit)
    {
        var previous = item.EstimatedGrams;
        item.Quantity = quantity; item.Unit = unit;
        item.EstimatedGrams = EstimateGrams(quantity, unit, 100);
        if (previous > 0)
        {
            var ratio = item.EstimatedGrams / previous;
            item.Calories *= ratio; item.Protein *= ratio; item.Carbs *= ratio; item.Fat *= ratio; item.Fiber *= ratio;
        }
    }

    private static void RequireWeightReview(LlmItem item, string reason)
    {
        item.NeedsWeightReview = true;
        item.Unparsed = true;
        item.EstimatedGrams = 0;
        item.ExactAssumption = reason;
    }

    private static void EnforceStatedAmount(LlmItem item, string text)
    {
        var amounts = Amounts(text);
        if (Regex.IsMatch(text, @"^(actually|make\s+that|rather)\b|\b\d+\s*[x×]\s*\d", RegexOptions.IgnoreCase))
        {
            RequireWeightReview(item, "Correction or container arithmetic needs review; enter total grams");
            return;
        }
        if (Regex.IsMatch(text, $@"^{AmountNumber}\s+(?:more\s+)?(?:slices?|scoops?)\s*$", RegexOptions.IgnoreCase))
        {
            RequireWeightReview(item, "Additional portion needs a food identity; specify the food and amount");
            return;
        }
        if (amounts.Any(a => !double.IsFinite(a.Quantity) || a.Quantity <= 0))
        {
            RequireWeightReview(item, "Invalid amount; enter a positive finite weight");
            return;
        }
        var explicitOunceWeight = Regex.IsMatch(text, @"\b(by\s+weight|weighed|weight\s+ounces?|ounces?\s+of\s+mass)\b", RegexOptions.IgnoreCase);
        var beverage = IsBeverage(item.FoodName) || IsBeverage(text);
        bool Volume(StatedAmount amount) => amount.Unit is "ml" or "l" or "fl oz" || amount.Unit == "oz" && beverage && !explicitOunceWeight;
        var masses = amounts.Where(a => a.Unit is "g" or "kg" or "lb" || a.Unit == "oz" && !Volume(a)).ToList();
        if (masses.Count > 1)
        {
            RequireWeightReview(item, "Multiple weights need review; enter the intended total grams");
            return;
        }
        if (masses is [var mass]) { SetMass(item, mass.Quantity, mass.Unit); return; }
        var volumes = amounts.Where(Volume).ToList();
        if (volumes.Count > 0 && amounts.Any(a => a.Unit is "can" or "bottle" or "carton" or "glass"))
        {
            RequireWeightReview(item, "Container count and volume need review; enter total volume or grams");
            return;
        }
        if (volumes is [var volume])
        {
            item.Quantity = volume.Quantity;
            item.Unit = volume.Unit == "oz" ? "fl oz" : volume.Unit;
            item.VolumeMl = volume.Quantity * (item.Unit == "l" ? 1000 : item.Unit == "fl oz" ? 29.5735295625 : 1);
            item.EstimatedGrams = 0;
            item.ExactAssumption = volume.Unit == "oz" ? "Drink oz treated as US fluid ounces; confirm volume or enter weighed grams" : null;
            if (item.Unparsed) RequireWeightReview(item, "Volume has no checked food match; enter weighed grams and nutrition");
        }
        else if (volumes.Count > 1) RequireWeightReview(item, "Multiple volumes need review; enter the intended total grams");
        else if (amounts is [var count] && count.Unit is "slice" or "scoop" or "can" or "bottle" or "carton" or "glass")
        {
            item.Quantity = count.Quantity; item.Unit = count.Unit;
            if (item.Unparsed) RequireWeightReview(item, "No checked serving weight; enter grams or a matching checked serving");
        }
    }

    private static bool ResolveStatedPortion(LlmItem item, FoodReference? food)
    {
        if (item.VolumeMl is double ml)
        {
            if ((food != null && IsBeverage(food.Name)) || IsZeroCalorieDrink(item.FoodName + " " + item.SourceText))
            {
                item.EstimatedGrams = ml;
                item.ExactAssumption = string.Join("; ", new[] { item.ExactAssumption, "Volume estimate: assumed 1 g/ml; US fl oz = 29.5735 ml. Edit grams if weighed." }.Where(s => s != null));
            }
            else RequireWeightReview(item, "Volume is not weight; enter weighed grams or a checked serving weight");
        }
        else if (NormalizeAmountUnit(item.Unit) is "can" or "bottle" or "carton" or "glass")
        {
            if (food != null && NormalizeAmountUnit(food.DefaultServingUnit) == NormalizeAmountUnit(item.Unit))
                item.EstimatedGrams = item.Quantity * food.DefaultServingGrams;
            else if (!IsZeroCalorieDrink(item.FoodName + " " + item.SourceText))
                RequireWeightReview(item, "Container size is unknown; enter grams or an explicit volume");
        }
        else if (NormalizeAmountUnit(item.Unit) is "slice" or "scoop" && food != null
            && NormalizeAmountUnit(food.DefaultServingUnit) == NormalizeAmountUnit(item.Unit))
            item.EstimatedGrams = item.Quantity * food.DefaultServingGrams;
        if (!double.IsFinite(item.EstimatedGrams)) RequireWeightReview(item, "Amount is too large; enter a finite weight in grams");
        return !item.NeedsWeightReview;
    }
}
