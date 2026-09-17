import type { MealItem } from '../services/api';

// Broad review thresholds, not dietary advice or a reason to block logging.
export function nutritionWarnings(item: MealItem): string[] {
  const values = [item.grams, item.calories, item.protein, item.carbs, item.fat, item.fiber];
  if (values.some(v => !Number.isFinite(v) || v < 0) || item.grams <= 0)
    return ['Enter a positive weight and non-negative nutrition values.'];
  const warnings: string[] = [];
  if (item.grams > 1500) warnings.push('This is a large portion. Check the weight and units.');
  const zeroCalorieDrink = /\b(water|diet|zero|unsweetened|black coffee|plain tea)\b/i.test(item.foodName);
  if (((item.grams >= 200 && item.calories < 10) || (item.calories === 0 && item.protein + item.carbs + item.fat === 0)) && !zeroCalorieDrink)
    warnings.push('Very few calories for this portion. Check that nutrition is complete.');
  // Fiber can contribute less energy than other carbohydrate. Allow rounding and
  // normal variation before flagging a mismatch; zero-carb alcohol is excluded.
  const macroCalories = item.protein * 4 + Math.max(0, item.carbs - item.fiber) * 4 + item.fiber * 2 + item.fat * 9;
  if (!/\b(beer|wine|vodka|whiskey|whisky|rum|gin|tequila|alcohol)\b/i.test(item.foodName)
    && Math.abs(item.calories - macroCalories) > Math.max(60, item.calories * 0.3))
    warnings.push('Calories and macros look different. Compare them with the label.');
  if (item.calories / item.grams > 9.5 || item.protein + item.carbs + item.fat > item.grams * 1.1)
    warnings.push('Nutrition looks too high for this weight. Check the serving size.');
  if (item.fiber > item.carbs + 1) warnings.push('Fiber is higher than total carbs. Check these values.');
  return warnings;
}
