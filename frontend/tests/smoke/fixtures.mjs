import { test as base, expect } from '@playwright/test';

export const KEY = 'omnom_diary_v1';
// Synthetic API contract fixtures: IDs are intentionally test-only, not USDA claims.
export const chicken = { foodName: 'Fixture chicken', sourceText: '100g fixture chicken',
  grams: 100, quantity: 100, unit: 'g', calories: 165, protein: 31, carbs: 0, fat: 3.6, fiber: 0,
  referenceFoodId: 901, usdaFdcId: 999001, usdaMatchStatus: 'VerifiedStaple', assumptions: [] };
export const lentils = { id: 902, fdcId: 999002, name: 'Fixture lentils', category: 'Test references',
  caloriesPer100g: 116, proteinPer100g: 9, carbsPer100g: 20.1, fatPer100g: 0.4, fiberPer100g: 7.9,
  defaultServingGrams: 100, defaultServingUnit: 'g', isStaple: false };
export const lentilItem = { foodName: lentils.name, sourceText: 'my lentils', grams: 150, quantity: 150, unit: 'g',
  calories: 174, protein: 13.5, carbs: 30.2, fat: 0.6, fiber: 11.9, referenceFoodId: 902,
  usdaFdcId: 999002, usdaMatchStatus: 'UsdaApiMatch', assumptions: [] };
export const meal = (id = 1, date = '2026-10-20', name = 'Training meal') => ({
  id, date, name, items: [structuredClone(chicken)], rawDescription: '100g fixture chicken' });
export const backup = (extra = {}) => ({ version: 1, meals: [], targets: [], nextMealId: 10, ...extra });
export const parsed = (items = [chicken]) => ({ suggestedMealName: 'Fixture meal', items: structuredClone(items), clarificationChips: [] });

export const test = base.extend({
  page: async ({ page, browser }, use, info) => {
    info.annotations.push({ type: 'browser', description: browser.version() });
    await page.clock.setFixedTime(new Date('2026-10-20T12:00:00-05:00'));
    const failures = [];
    page.on('pageerror', error => failures.push(error.message));
    await page.route('**/*', async route => {
      const url = new URL(route.request().url());
      if (url.hostname !== '127.0.0.1' && url.protocol !== 'data:' && url.protocol !== 'blob:')
        return route.abort(); // No fonts, analytics, or provider network calls from tests.
      return route.continue();
    });
    await use(page);
    expect(failures, 'No uncaught browser exceptions').toEqual([]);
  },
});
export { expect };
export async function seed(page, data) {
  await page.addInitScript(({ key, data }) => {
    // Seed once: reload must read the app's persisted mutations, not reset the fixture.
    if (!sessionStorage.getItem('omnom-smoke-seeded')) {
      localStorage.setItem(key, JSON.stringify(data)); sessionStorage.setItem('omnom-smoke-seeded', '1');
    }
  }, { key: KEY, data });
}
export async function stored(page) { return page.evaluate(key => JSON.parse(localStorage.getItem(key)), KEY); }
export async function stubApis(page, response = parsed()) {
  await page.route('**/api/ai/parse', route => route.fulfill({ json: response }));
  await page.route('**/api/foods/search?*', route => route.fulfill({ json: [lentils] }));
}
export async function open(page) {
  await page.goto('/'); await expect(page.getByRole('heading', { name: 'Daily overview' })).toBeVisible();
}
export async function date(page, value) {
  await page.getByLabel('Choose diary date').fill(value);
  await page.getByLabel('Choose diary date').press('Tab');
  await expect(page.getByLabel('Choose diary date')).toHaveValue(value);
}
export async function review(page, prompt = '100g fixture chicken') {
  await page.getByLabel('Describe your meal').fill(prompt);
  await page.getByRole('button', { name: 'Review meal', exact: true }).click();
  await expect(page.getByLabel('Meal name', { exact: true })).toBeVisible();
}
export async function save(page, name = 'Fixture meal') {
  await page.getByLabel('Meal name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Save meal', exact: true }).click();
  await expect(page.locator('.saved-meal').getByRole('heading', { name, exact: true })).toBeVisible();
}
export async function clearToasts(page) {
  for (const button of await page.getByRole('button', { name: 'Dismiss notification' }).all()) await button.click();
}
