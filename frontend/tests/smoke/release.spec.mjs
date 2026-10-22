import { readFile } from 'node:fs/promises';
import { test, expect, KEY, chicken, lentils, lentilItem, meal, backup, parsed,
  seed, stored, stubApis, open, date, review, save, clearToasts } from './fixtures.mjs';

const savedCard = (page, name) => page.locator('.saved-meal').filter({ has: page.getByRole('heading', { name, exact: true }) });
const total = (page, calories) => expect(page.locator('.calorie-caption')).toContainText(`${calories} kcal logged`);

test('packaged app crosses the real API boundary with Exact references and public restrictions', async ({ page, request }, info) => {
  // No routes are stubbed in this scenario: actual controllers, parser, and isolated SQLite.
  for (const path of ['/api/diary?date=2026-10-20', '/api/targets/current', '/api/settings', '/api/auth/status'])
    expect((await request.get(path)).status(), path).toBe(404);
  for (const path of ['/api/diary/meal', '/api/targets', '/api/settings', '/api/auth/verify'])
    expect((await request.post(path, { data: {} })).status(), path).toBe(404);
  const search = await request.get('/api/foods/search?q=chicken');
  expect(search.status()).toBe(200); expect((await search.json()).length).toBeGreaterThan(0);
  await open(page); await date(page, '2026-10-18');
  await page.getByRole('button', { name: 'Exact', exact: true }).click();
  const response = page.waitForResponse(r => r.url().endsWith('/api/ai/parse'));
  await review(page, 'cooked chicken breast 100g, jasmine rice 150g');
  const api = await (await response).json();
  expect(api.items).toHaveLength(2); expect(api.items.map(item => item.grams)).toEqual([100, 150]);
  expect(api.items.every(item => item.referenceFoodId > 0 && item.usdaMatchStatus === 'VerifiedStaple')).toBe(true);
  await expect(page.getByLabel('Optional meal time')).toHaveValue('');
  await save(page, 'Real API meal'); await page.reload();
  await date(page, '2026-10-18'); await total(page, 360);
  const actual = (await stored(page)).meals[0];
  expect(actual.date).toBe('2026-10-18'); expect(actual.time).toBeUndefined();
  expect(actual.items).toEqual(api.items);
  await page.getByRole('button', { name: 'Expand Real API meal' }).click();
  await expect(savedCard(page, 'Real API meal')).toContainText('100g');
  await page.screenshot({ path: `.smoke/real-api-${info.project.name}.png`, fullPage: true });
});

test('stubbed review replacement persists every nutrient, source, date, and remembered default', async ({ page }) => {
  await stubApis(page); await open(page); await date(page, '2026-10-18');
  await review(page); await page.getByRole('button', { name: 'Edit details', exact: true }).click();
  await page.getByRole('button', { name: /Fixture lentils/ }).click();
  await expect(page.locator('.source-label')).toHaveText('USDA match');
  await page.getByText('Remember this food / brand', { exact: true }).click();
  await page.getByLabel('Remembered name for Fixture lentils').fill('my lentils');
  await page.getByRole('button', { name: 'Save food default' }).click();
  await expect(page.getByText(/my lentils remembered/)).toBeVisible();
  await save(page, 'Reference replacement');
  await expect(page.getByText('Reference replacement saved.', { exact: true })).toBeVisible();
  await page.reload(); await date(page, '2026-10-18'); await total(page, 116);
  const data = await stored(page), item = data.meals[0].items[0];
  expect(data.meals[0].date).toBe('2026-10-18');
  expect(item).toMatchObject({ grams: 100, calories: 116, protein: 9, carbs: 20.1, fat: 0.4, fiber: 7.9,
    referenceFoodId: 902, usdaFdcId: 999002, usdaMatchStatus: 'UsdaApiMatch' });
  expect(data.foods[0]).toMatchObject({ alias: 'my lentils', usePortion: true, item });
});

test('favorites, recent/repeat, editing, deletion, and each Undo update daily and weekly totals', async ({ page }) => {
  await seed(page, backup({ meals: [meal()] })); await stubApis(page); await open(page); await total(page, 165);
  await page.getByRole('button', { name: 'Favorite Training meal', exact: true }).click();
  await page.reload();
  await expect(savedCard(page, 'Training meal').getByRole('button', { name: 'Remove favorite Training meal', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Edit Training meal', exact: true }).click();
  await page.getByRole('button', { name: 'Edit details', exact: true }).click();
  await page.getByLabel('Portion for Fixture chicken').fill('200');
  await page.getByLabel('Portion for Fixture chicken').press('Tab');
  await clearToasts(page); await page.getByRole('button', { name: 'Save changes' }).click();
  await total(page, 330); await expect(page.locator('.week-stats')).toContainText('330');
  expect((await stored(page)).favorites[0].items[0].grams).toBe(100); // Snapshot, not a live link.
  await page.getByRole('button', { name: 'Undo', exact: true }).click(); await total(page, 165);
  await clearToasts(page); await page.getByRole('button', { name: 'Repeat Training meal', exact: true }).click();
  await save(page, 'Repeated meal'); await total(page, 330);
  await page.getByRole('button', { name: 'Undo', exact: true }).click(); await total(page, 165);
  await clearToasts(page); await page.getByRole('button', { name: 'Delete Training meal', exact: true }).click();
  await total(page, 0); await expect(page.locator('.week-stats')).toContainText('0/7');
  await page.getByRole('button', { name: 'Undo', exact: true }).click(); await total(page, 165);
  await date(page, '2026-10-19');
  await page.locator('.meal-library .library-pick').filter({ hasText: 'Training meal' }).click();
  await save(page, 'Favorite on yesterday');
  await date(page, '2026-10-20');
  await expect(page.locator('.week-stats')).toContainText('2/7');
  expect((await stored(page)).meals.map(m => [m.id, m.date])).toEqual([[1, '2026-10-20'], [11, '2026-10-19']]);
  await page.getByRole('button', { name: 'Recent meals', exact: true }).click();
  await page.locator('.meal-library .library-pick').filter({ hasText: 'Training meal' }).click();
  await expect(page.getByLabel('Meal name', { exact: true })).toHaveValue('Training meal');
  await page.getByRole('button', { name: 'Discard', exact: true }).click();
});

test('remembered aliases resolve unknowns but explicit grams override the usual portion', async ({ page }) => {
  await seed(page, backup({ foods: [{ id: 1, alias: 'my lentils', usePortion: true, item: lentilItem }] }));
  await page.route('**/api/ai/parse', async route => {
    const { prompt } = route.request().postDataJSON();
    return route.fulfill({ json: parsed([{ ...chicken, foodName: 'my lentils', sourceText: prompt,
      grams: 100, calories: 0, protein: 0, fat: 0, carbs: 0, fiber: 0, usdaMatchStatus: 'Unparsed' }]) });
  });
  await open(page); await review(page, 'my lentils');
  await expect(page.locator('.item-heading')).toContainText('150 g'); await save(page, 'Usual portion');
  await review(page, '200g my lentils'); await expect(page.locator('.item-heading')).toContainText('200 g');
  await save(page, 'Explicit portion'); await total(page, 406);
  expect((await stored(page)).meals.map(m => m.items[0].grams)).toEqual([150, 200]);
  await page.reload(); await expect(page.locator('.week-stats')).toContainText('406');
});

test('historical targets, missing days, rapid navigation, and keyboard goal dialog remain correct', async ({ page }) => {
  await seed(page, backup({ meals: [meal(1, '2026-10-18', 'Earlier'), meal(2)], targets: [] }));
  await open(page);
  const trigger = page.getByRole('button', { name: 'Adjust daily targets', exact: true });
  await trigger.click(); const dialog = page.getByRole('dialog', { name: 'Daily goals' });
  await expect(dialog.getByRole('button', { name: 'Close daily goals' })).toBeFocused();
  await page.keyboard.press('Shift+Tab'); await expect(dialog.getByRole('button', { name: 'Save goals' })).toBeFocused();
  await page.keyboard.press('Tab'); await expect(dialog.getByRole('button', { name: 'Close daily goals' })).toBeFocused();
  await page.keyboard.press('Escape'); await expect(dialog).not.toBeVisible(); await expect(trigger).toBeFocused();
  for (const calories of ['2100', '2200']) {
    await trigger.click(); await dialog.getByLabel('Calories (kcal)', { exact: true }).fill(calories);
    await dialog.getByRole('button', { name: 'Save goals', exact: true }).click();
    await expect(page.getByText('Your daily targets are updated.', { exact: true }).last()).toBeVisible();
  }
  await expect(page.locator('.ring-label')).toContainText('2,200');
  await expect(page.locator('.week-stats')).toContainText('2/7');
  await expect(page.getByRole('button', { name: 'Open diary for 2026-10-19' })).toContainText('No meals logged');
  await expect(page.getByRole('button', { name: 'Open diary for 2026-10-18' })).toContainText('2500 kcal');
  await expect(page.getByRole('button', { name: 'Open diary for 2026-10-20' })).toContainText('2200 kcal');
  // Two real clicks with minimal spacing exercise rapid UI navigation; controlled stale promises are also in npm test.
  await page.getByRole('button', { name: 'Previous day', exact: true }).click();
  await page.getByRole('button', { name: 'Previous day', exact: true }).click();
  await expect(page.getByLabel('Choose diary date')).toHaveValue('2026-10-18');
  await expect(page.locator('.ring-label')).toContainText('2,500');
  await expect(page.locator('.saved-meal').getByRole('heading')).toHaveText('Earlier');
  expect((await stored(page)).targets.map(t => t.effectiveDate)).toEqual(['2026-10-20', '2026-10-20']);
});

test('backup export/restore, invalid and oversized files preserve data and keyboard access', async ({ page }) => {
  const data = backup({ meals: [meal()], favorites: [{ id: 1, name: 'Snapshot', items: [chicken] }],
    foods: [{ id: 1, alias: 'my lentils', usePortion: true, item: lentilItem }],
    targets: [{ id: 1, effectiveDate: '2026-10-19', name: 'Fixture goals', targetCalories: 1900, targetProtein: 150, targetCarbs: 200, targetFat: 60, targetFiber: 35 }] });
  await seed(page, data); await open(page);
  const trigger = page.getByRole('button', { name: 'Open diary settings' });
  await trigger.click(); const dialog = page.getByRole('dialog', { name: 'Diary backup' });
  await expect(dialog.getByRole('button', { name: 'Close settings' })).toBeFocused();
  await page.keyboard.press('Shift+Tab'); await expect(dialog.getByLabel('Choose diary backup')).toBeFocused();
  await page.keyboard.press('Tab'); await expect(dialog.getByRole('button', { name: 'Close settings' })).toBeFocused();
  const download = page.waitForEvent('download');
  await dialog.getByRole('button', { name: 'Export your diary' }).click();
  const file = await download; const exported = JSON.parse(await readFile(await file.path(), 'utf8'));
  expect(exported).toEqual(await stored(page));
  page.once('dialog', d => d.accept());
  await dialog.getByLabel('Choose diary backup').setInputFiles({ name: 'invalid.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({ version: 1, meals: [{}], targets: [] })) });
  await expect(page.getByText('Invalid meal in diary backup.', { exact: true })).toBeVisible();
  expect(await stored(page)).toEqual(exported);
  await dialog.getByLabel('Choose diary backup').setInputFiles({ name: 'large.json', mimeType: 'application/json', buffer: Buffer.alloc(10 * 1024 * 1024 + 1) });
  await expect(page.getByText('Choose a diary backup smaller than 10 MB.', { exact: true })).toBeVisible();
  expect(await stored(page)).toEqual(exported);
  const replacement = backup({ meals: [meal(2, '2026-10-20', 'Restored meal')] });
  page.once('dialog', d => d.accept());
  await dialog.getByLabel('Choose diary backup').setInputFiles({ name: 'valid-v1.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(replacement)) });
  await expect(page.getByText('Diary restored in this browser.', { exact: true })).toBeVisible();
  await page.keyboard.press('Escape'); await expect(dialog).not.toBeVisible(); await expect(trigger).toBeFocused();
  await page.reload(); await expect(page.locator('.saved-meal').getByRole('heading')).toHaveText('Restored meal');
  expect((await stored(page)).meals).toEqual(replacement.meals);
  await trigger.click(); page.once('dialog', d => d.accept());
  await page.getByLabel('Choose diary backup').setInputFiles({ name: 'roundtrip.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(exported)) });
  await expect(page.getByText('Diary restored in this browser.', { exact: true })).toBeVisible();
  expect(await stored(page)).toEqual(exported); // Targets, favorites, defaults, source fields, and IDs round-trip.
});

test('quota errors never announce success or overwrite the saved diary', async ({ page }) => {
  await seed(page, backup({ meals: [meal()] }));
  await page.addInitScript(key => {
    const original = Storage.prototype.setItem;
    Storage.prototype.setItem = function(k, value) {
      if (k === key && sessionStorage.getItem('omnom-smoke-seeded')) throw new DOMException('Synthetic quota failure', 'QuotaExceededError');
      return original.call(this, k, value);
    };
  }, KEY);
  await stubApis(page); await open(page); const before = await stored(page);
  await review(page); await page.getByLabel('Meal name', { exact: true }).fill('Must not save');
  await page.getByRole('button', { name: 'Save meal', exact: true }).click();
  await expect(page.getByText(/Could not save in this browser/)).toBeVisible();
  await expect(page.getByText('Must not save saved.', { exact: true })).not.toBeVisible();
  expect(await stored(page)).toEqual(before);
  await expect(page.getByRole('button', { name: 'Save meal', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Discard', exact: true }).click();
  await page.getByRole('button', { name: 'Open diary settings' }).click();
  page.once('dialog', d => d.accept());
  await page.getByLabel('Choose diary backup').setInputFiles({ name: 'valid.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(backup())) });
  await expect(page.getByText('Diary restored in this browser.', { exact: true })).not.toBeVisible();
  expect(await stored(page)).toEqual(before);
});

test('corrupt storage is surfaced without clearing it, and blocked reads remain visible', async ({ page }) => {
  await page.addInitScript(key => localStorage.setItem(key, 'synthetic corrupt backup'), KEY);
  await open(page); await expect(page.getByText(/The saved diary could not be read/)).toBeVisible();
  expect(await page.evaluate(key => localStorage.getItem(key), KEY)).toBe('synthetic corrupt backup');
  await page.getByRole('button', { name: 'Open diary settings' }).click();
  const download = page.waitForEvent('download'); await page.getByRole('button', { name: 'Export your diary' }).click();
  expect(await readFile(await (await download).path(), 'utf8')).toBe('synthetic corrupt backup');
  await page.keyboard.press('Escape');
  await page.evaluate(key => {
    const get = Storage.prototype.getItem;
    Storage.prototype.getItem = function(k) { if (k === key) throw new Error('Synthetic storage blocked'); return get.call(this, k); };
  }, KEY);
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await expect(page.getByText('Synthetic storage blocked', { exact: false })).toBeVisible();
});

test('API failure/loading feedback, search errors, and responsive controls stay usable', async ({ page }, info) => {
  await page.route('**/api/ai/parse', route => route.fulfill({ status: 503, json: { message: 'Synthetic parse unavailable' } }));
  await open(page); await page.getByLabel('Describe your meal').fill('fixture chicken');
  await page.getByLabel('Describe your meal').press('Enter');
  await expect(page.getByRole('alert')).toHaveText('Synthetic parse unavailable');
  await expect(page.getByRole('button', { name: 'Review meal', exact: true })).toBeEnabled();
  await page.unroute('**/api/ai/parse');
  let finishParse;
  const parseGate = new Promise(resolve => { finishParse = resolve; });
  await page.route('**/api/ai/parse', async route => { await parseGate; await route.fulfill({ json: parsed() }); });
  await page.getByRole('button', { name: 'Review meal', exact: true }).click();
  await expect(page.getByText(/Putting your meal together/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Working on it…', exact: true })).toBeDisabled();
  finishParse(); await expect(page.getByLabel('Meal name', { exact: true })).toBeVisible();
  await page.route('**/api/foods/search?*', async route => {
    const q = new URL(route.request().url()).searchParams.get('q');
    return q === 'missing' ? route.fulfill({ status: 503, json: { message: 'Synthetic search unavailable' } })
      : route.fulfill({ json: [lentils] });
  });
  await page.getByRole('button', { name: 'Edit details', exact: true }).click();
  await expect(page.getByRole('button', { name: /Fixture lentils/ })).toBeVisible();
  await page.getByLabel('Replace with a reference food').fill('missing');
  await page.getByRole('button', { name: 'Search reference foods', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveText('Synthetic search unavailable');
  await expect(page.getByRole('button', { name: 'Search reference foods', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Discard', exact: true }).click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'No horizontal overflow').toBe(true);
  await page.screenshot({ path: `.smoke/controls-${info.project.name}.png`, fullPage: true });
});

test('real API corrections and beverage units preserve review assumptions, unresolved containers, and saved weights', async ({ page }, info) => {
  await open(page);
  await page.getByRole('button', { name: 'Exact', exact: true }).click();
  let response = page.waitForResponse(r => r.url().endsWith('/api/ai/parse'));
  const correctedSource = '200g cooked chicken breast, actually 150g, no butter';
  await review(page, correctedSource);
  const corrected = await (await response).json();
  expect(corrected.items).toHaveLength(1); expect(corrected.items[0].grams).toBe(150);
  await save(page, 'Corrected amount');
  await clearToasts(page);
  response = page.waitForResponse(r => r.url().endsWith('/api/ai/parse'));
  await review(page, '8 fl oz whole milk');
  const milk = await (await response).json();
  expect(milk.items[0]).toMatchObject({ quantity: 8, unit: 'fl oz', grams: 236.6 });
  await expect(page.getByRole('button', { name: /Volume estimate: assumed 1 g\/ml/ })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: `.smoke/om-005-units-${info.project.name}.png`, fullPage: true });
  await save(page, 'Volume estimate');
  await clearToasts(page);
  await review(page, '1 bottle whole milk');
  await expect(page.getByRole('button', { name: /Container size is unknown/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save meal', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Discard', exact: true }).click();
  await page.reload();
  const data = await stored(page);
  expect(data.meals).toHaveLength(2);
  expect(data.meals[0].items[0]).toMatchObject({ grams: 150, calories: 247.5 });
  expect(data.meals[1].items[0]).toMatchObject({ grams: 236.6, quantity: 8, unit: 'fl oz' });
  expect(data.meals[1].items[0].assumptions.some(a => a.includes('assumed 1 g/ml'))).toBe(true);
});
