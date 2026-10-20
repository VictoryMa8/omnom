import type { AiParsedMealResult, FoodReference, MealItem, RememberedFood } from '../services/api';

const nutrients = ['calories', 'protein', 'carbs', 'fat', 'fiber'] as const;
const round = (value: number) => Math.round(value * 10) / 10;
const key = (value: string) => value.trim().toLowerCase().replace(/\s+/g, ' ');
const quantity = '(?:\\d*\\.?\\d+(?:/\\d+)?|one|two|three|four|five|six|seven|eight|nine|ten|a|an)';
const units = '(?:kg|kilograms?|g|grams?|oz|ounces?|lb|lbs|pounds?|cups?|tbsp|tablespoons?|tsp|teaspoons?|slices?|scoops?|servings?|cans?|pieces?|plates?|handfuls?|large|medium|small)';

export function foodAliasFromSource(source: string): string {
  return source.replace(/^(?:breakfast|lunch|dinner|snack)\s*:\s*/i, '')
    .replace(new RegExp(`^(?:maybe\\s+)?(?:${quantity}\\s*)?(?:${units}\\s+)+(?:of\\s+)?`, 'i'), '')
    .replace(new RegExp(`\\s+${quantity}\\s*${units}$`, 'i'), '').trim();
}

export function replaceWithReference(item: MealItem, food: FoodReference): MealItem {
  const grams = item.grams > 0 ? item.grams : food.defaultServingGrams > 0 ? food.defaultServingGrams : 100;
  return {
    foodName: food.name, sourceText: item.sourceText, quantity: grams, unit: 'g', grams,
    calories: round(food.caloriesPer100g * grams / 100), protein: round(food.proteinPer100g * grams / 100),
    carbs: round(food.carbsPer100g * grams / 100), fat: round(food.fatPer100g * grams / 100), fiber: round(food.fiberPer100g * grams / 100),
    referenceFoodId: food.id, usdaFdcId: food.fdcId, usdaMatchStatus: food.isStaple ? 'VerifiedStaple' : 'UsdaApiMatch',
    assumptions: [],
  };
}

function statedMass(source: string): number | undefined {
  const matches = [...source.matchAll(/(?<![\d./])([\d]*\.?\d+(?:\/\d+)?)\s*(kg|kilograms?|g|grams?|oz|ounces?|lb|lbs|pounds?)\b/gi)];
  if (matches.length !== 1 || /\b(fluid|fl)\b/i.test(source)) return;
  const match = matches[0]!;
  const [numerator, denominator] = match[1]!.split('/').map(Number);
  const amount = numerator! / (denominator ?? 1);
  if (!Number.isFinite(amount) || amount <= 0) return;
  const unit = match[2]!.toLowerCase();
  return amount * (/^kg|^kilogram/.test(unit) ? 1000 : /^oz|^ounce/.test(unit) ? 28.3495 : /^lb|^pound/.test(unit) ? 453.592 : 1);
}

export function applyFoodDefaults(result: AiParsedMealResult, foods: RememberedFood[]): AiParsedMealResult {
  const copy: AiParsedMealResult = JSON.parse(JSON.stringify(result));
  const replaced = new Set<number>();
  copy.items = copy.items.map((item, index) => {
    const alias = foodAliasFromSource(item.sourceText || item.foodName);
    const saved = foods.find(food => key(food.alias) === key(alias));
    if (!saved) return item;
    const source = item.sourceText || item.foodName;
    const mass = statedMass(source);
    const hasAmount = mass != null || new RegExp(`(?:^|\\s)${quantity}\\s*(?:${units}\\b|eggs?\\b)`, 'i').test(source);
    let grams = item.grams > 0 ? item.grams : saved.item.grams;
    let count = item.quantity, unit = item.unit;
    if (mass != null) { grams = mass; count = mass; unit = 'g'; }
    else if (!hasAmount && saved.usePortion) { grams = saved.item.grams; count = saved.item.quantity; unit = saved.item.unit; }
    else if (hasAmount && item.quantity > 0 && key(item.unit).replace(/s$/, '') === key(saved.item.unit).replace(/s$/, '') && saved.item.quantity > 0) {
      grams = saved.item.grams * item.quantity / saved.item.quantity;
    }
    const updated: MealItem = { ...JSON.parse(JSON.stringify(saved.item)), sourceText: source, grams: round(grams), quantity: count, unit,
      assumptions: [...(saved.item.assumptions ?? []).filter(a => !a.startsWith('Remembered')),
        `Remembered food: ${saved.alias}`, ...(!hasAmount ? [saved.usePortion ? 'Remembered portion' : 'Estimated portion'] : [])] };
    for (const field of nutrients) updated[field] = round(saved.item[field] * grams / saved.item.grams);
    replaced.add(index); return updated;
  });
  copy.clarificationChips = copy.clarificationChips.filter(chip => chip.targetItemIndex == null || !replaced.has(chip.targetItemIndex));
  // Keep an AI fallback notice, but do not claim a resolved personal food is still unmatched.
  if (copy.items.every(item => item.usdaMatchStatus !== 'Unparsed') && copy.aiSummary)
    copy.aiSummary = copy.aiSummary.startsWith('Could not read:') ? undefined : copy.aiSummary.replace(/Some foods have no nutrition match\..*$/, '').trim() || undefined;
  return copy;
}
