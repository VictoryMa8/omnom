import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { test } from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
import { createPinia, setActivePinia } from 'pinia';

const require = createRequire(import.meta.url);
function load(source, mocks = {}, globals = {}) {
  const code = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
  } }).outputText;
  const module = { exports: {} };
  new Function('require', 'module', 'exports', ...Object.keys(globals), code)(
    name => mocks[name] ?? require(name), module, module.exports, ...Object.values(globals));
  return module.exports;
}
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

function editor() {
  const source = read('../src/components/MealConfirmCard.vue').match(/<script setup lang="ts">([\s\S]*?)<\/script>/)[1];
  const item = { foodName: 'Beef', grams: 100, quantity: 1, unit: 'serving', calories: 200,
    protein: 20, carbs: 4, fat: 10, fiber: 2 };
  return load(source + '\nexport { editableItems, activeChips, removeItem, updateItemGrams, selectChipOption, selectReference, renameItem, markEdited, valid };', {
    '../utils/nutritionReview': load(read('../src/utils/nutritionReview.ts')),
    '../utils/foodDefaults': load(read('../src/utils/foodDefaults.ts')),
    './FoodSearch.vue': {},
    '../stores/toastStore': { useToastStore: () => ({ success() {} }) },
  }, {
    defineProps: () => ({ parsedResult: { items: [{ ...item, foodName: 'Rice' }, item],
      clarificationChips: [{ id: 'beef', targetItemIndex: 1, options: [] }] } }),
    defineEmits: () => () => {},
  });
}

test('deleting an ingredient keeps clarification attached to its original food', () => {
  const e = editor();
  e.removeItem(0);
  e.selectChipOption(e.activeChips.value[0], { id: 'lean', replacementCalories: 150, replacementGrams: 100 });
  assert.equal(e.editableItems.value[0].calories, 150);
  e.removeItem(0);
  assert.equal(e.activeChips.value.length, 0);
});

test('portion changes scale fiber, quantity, and subsequent clarification nutrition', () => {
  const e = editor();
  const item = e.editableItems.value[1];
  e.updateItemGrams(item, 200);
  assert.equal(item.fiber, 4);
  assert.equal(item.quantity, 2);
  e.selectChipOption(e.activeChips.value[0], { id: 'lean', replacementCalories: 150, replacementGrams: 100 });
  assert.equal(item.calories, 300);
  e.updateItemGrams(item, NaN);
  assert.equal(item.grams, 200);
});

test('unmatched food requires nutrition review before saving', () => {
  const e = editor();
  assert.equal(e.valid.value, true);
  e.editableItems.value[0].usdaMatchStatus = 'Unparsed';
  assert.equal(e.valid.value, false);
  e.removeItem(0);
  assert.equal(e.valid.value, true);
});

for (const staleFails of [false, true]) {
  test(`date navigation ignores stale ${staleFails ? 'errors' : 'responses'}`, async () => {
    setActivePinia(createPinia());
    const pending = [];
    const { useDiaryStore } = load(read('../src/stores/diaryStore.ts'), {
      '../services/api': { DiaryApi: { getTimeline: () => new Promise((resolve, reject) => pending.push({ resolve, reject })),
        getWeek: async () => [], getRecentMeals: async () => [], getLibrary: async () => ({ favorites: [], foods: [] }) } },
    });
    const store = useDiaryStore();
    const first = store.fetchTimeline('2026-09-07');
    const second = store.fetchTimeline('2026-09-08');
    if (staleFails) pending[0].reject(new Error('Old request failed'));
    else pending[0].resolve({ date: '2026-09-07' });
    await first;
    assert.equal(store.loading, true);
    assert.equal(store.timeline, null);
    assert.equal(store.error, null);
    pending[1].resolve({ date: '2026-09-08' });
    await second;
    assert.equal(store.timeline.date, store.selectedDate);
    assert.equal(store.loading, false);
  });
}

function memoryStorage() {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
}
const meal = { date: '2026-09-10', name: 'Dinner', items: [{ foodName: 'Rice', quantity: 1, unit: 'serving',
  grams: 100, calories: 130, protein: 3, carbs: 28, fat: 1, fiber: 2 }] };

test('each browser diary is isolated and survives reload with accurate totals', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const storage = memoryStorage();
  const first = createLocalDiary(storage), other = createLocalDiary(memoryStorage());
  const { id } = await first.createMeal(meal);
  const reloaded = createLocalDiary(storage);
  assert.equal((await reloaded.getTimeline(meal.date)).consumedCalories, 130);
  assert.equal((await other.getTimeline(meal.date)).meals.length, 0);
  await reloaded.deleteMeal(id);
  assert.equal((await first.getTimeline(meal.date)).consumedCalories, 0);
});

test('targets take effect only from their date, including repeated same-day edits', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  const target = { name: 'Custom', effectiveDate: '2026-09-10', targetCalories: 2000, targetProtein: 150, targetCarbs: 200, targetFat: 65 };
  await diary.updateTarget(target);
  await diary.updateTarget({ ...target, targetCalories: 2200 });
  assert.equal((await diary.getTimeline('2026-09-09')).target.targetCalories, 2500);
  assert.equal((await diary.getTimeline('2026-09-10')).target.targetCalories, 2200);
});

test('backups restore a diary and invalid imports do not overwrite data', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  await diary.createMeal(meal);
  const backup = diary.exportBackup();
  const other = createLocalDiary(memoryStorage());
  other.importBackup(backup);
  assert.equal((await other.getTimeline(meal.date)).consumedFiber, 2);
  assert.throws(() => other.importBackup('{"version":1,"meals":[{}],"targets":[]}'));
  assert.equal(other.exportBackup(), backup);
});

test('public app keeps diaries in the browser and does not ask for a PIN', () => {
  const app = read('../src/App.vue');
  const api = read('../src/services/api.ts');
  assert.match(app, /BrowserDataModal/);
  assert.doesNotMatch(app, /PasscodeModal/);
  assert.match(api, /export const DiaryApi = localDiary/);
});

test('storage failures reject saving and corrupt storage is never silently cleared', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary({ getItem: () => null, setItem: () => { throw new Error('quota'); } });
  await assert.rejects(diary.createMeal(meal), /Could not save/);
  let writes = 0;
  const broken = createLocalDiary({ getItem: () => 'broken', setItem: () => writes++ });
  await assert.rejects(broken.createMeal(meal), /could not be read/);
  assert.equal(writes, 0);
});

test('undo deletion preserves original date, position, and later meals', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  const first = await diary.createMeal(meal);
  const second = await diary.createMeal({ ...meal, name: 'Second' });
  const deletion = await diary.deleteMeal(first.id);
  const third = await diary.createMeal({ ...meal, name: 'Third' });
  await diary.createMeal({ ...meal, date: '2026-09-11', name: 'Tomorrow' });
  await deletion.undo();
  const timeline = await diary.getTimeline(meal.date);
  assert.deepEqual(timeline.meals.map(m => m.id), [first.id, second.id, third.id]);
  assert.equal(timeline.consumedCalories, 390);
  assert.equal(timeline.meals[2].runningCalories, 390);
  assert.equal((await diary.getTimeline('2026-09-11')).meals[0].name, 'Tomorrow');
  await assert.rejects(deletion.undo(), /already in use/);
});

test('undo save removes only that entry and IDs are not reused after deleting', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  const first = await diary.createMeal(meal);
  const deleted = await diary.deleteMeal(first.id);
  const newer = await diary.createMeal({ ...meal, name: 'Newer' });
  assert.ok(newer.id > first.id);
  await assert.rejects(first.undo(), /changed/);
  await deleted.undo();
  await newer.undo();
  assert.deepEqual((await diary.getTimeline(meal.date)).meals.map(m => m.id), [first.id]);
});

test('undo refuses to delete a meal that has since been edited', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  const change = await diary.createMeal(meal);
  await diary.updateMeal(change.id, { ...meal, name: 'Edited' });
  await assert.rejects(change.undo(), /changed/);
  assert.equal((await diary.getTimeline(meal.date)).meals[0].name, 'Edited');
});

test('nutrition review flags implausible entries and accepts normal rounding, fiber, and water', () => {
  const { nutritionWarnings } = load(read('../src/utils/nutritionReview.ts'));
  assert.deepEqual(nutritionWarnings(meal.items[0]), []);
  assert.ok(nutritionWarnings({ ...meal.items[0], grams: 2000 }).some(w => w.includes('large portion')));
  assert.ok(nutritionWarnings({ ...meal.items[0], grams: 300, calories: 0 }).some(w => w.includes('few calories')));
  assert.ok(nutritionWarnings({ ...meal.items[0], calories: 500 }).some(w => w.includes('Calories and macros')));
  assert.ok(nutritionWarnings({ ...meal.items[0], grams: 5 }).some(w => w.includes('too high')));
  assert.ok(nutritionWarnings({ ...meal.items[0], grams: NaN }).length);
  assert.deepEqual(nutritionWarnings({ ...meal.items[0], foodName: 'Water', grams: 500, calories: 0, protein: 0, carbs: 0, fat: 0, fiber: 0 }), []);
  assert.deepEqual(nutritionWarnings({ ...meal.items[0], calories: 200, protein: 20, carbs: 50, fiber: 40, fat: 0 }), []);
});

test('undo toast executes only once, reports failures, and pauses its expiry', async () => {
  setActivePinia(createPinia());
  const timers = new Map(); let next = 0;
  const { useToastStore } = load(read('../src/stores/toastStore.ts'), {}, {
    setTimeout: callback => { timers.set(++next, callback); return next; },
    clearTimeout: id => timers.delete(id),
  });
  const toast = useToastStore();
  let resolve, calls = 0;
  toast.success('Saved', () => { calls++; return new Promise(r => { resolve = r; }); });
  const id = toast.toasts[0].id;
  toast.pause(id); assert.equal(timers.size, 0);
  toast.resume(id); assert.equal(timers.size, 1);
  const first = toast.runAction(id);
  await toast.runAction(id);
  assert.equal(calls, 1);
  resolve(); await first;
  assert.equal(toast.toasts[0].message, 'Change undone.');
  toast.success('Removed', async () => { throw new Error('Storage unavailable'); });
  const failing = toast.toasts.find(t => t.action);
  await toast.runAction(failing.id);
  assert.equal(failing.pending, false);
  assert.ok(toast.toasts.some(t => t.message === 'Storage unavailable'));
  toast.clearActions();
  assert.ok(toast.toasts.every(t => !t.action));
});

test('reference replacement updates every nutrient and removes stale clarification and source IDs', () => {
  const e = editor();
  e.editableItems.value[1].usdaFdcId = 999;
  e.selectReference(1, { id: 12, name: 'Chicken thigh', isStaple: true, defaultServingGrams: 120,
    caloriesPer100g: 200, proteinPer100g: 25, carbsPer100g: 2, fatPer100g: 10, fiberPer100g: 1 });
  const item = e.editableItems.value[1];
  assert.deepEqual([item.calories, item.protein, item.carbs, item.fat, item.fiber], [200, 25, 2, 10, 1]);
  assert.equal(item.grams, 100); assert.equal(item.quantity, 100); assert.equal(item.unit, 'g');
  assert.equal(item.referenceFoodId, 12); assert.equal(item.usdaFdcId, undefined);
  assert.equal(e.activeChips.value.length, 0);
});

test('renaming alone cannot save unrelated nutrition and manual correction clears old chips', () => {
  const e = editor(); const item = e.editableItems.value[1];
  item.foodName = 'My protein bar'; e.renameItem(item);
  assert.equal(e.valid.value, false); assert.equal(e.activeChips.value.length, 0);
  item.calories = 150; e.markEdited(item);
  assert.equal(e.valid.value, true); assert.equal(item.usdaMatchStatus, 'Estimated');
});

test('reference-backed chip selection updates fiber and provenance after weight changes', () => {
  const e = editor(); const item = e.editableItems.value[1];
  e.updateItemGrams(item, 200);
  e.selectChipOption(e.activeChips.value[0], { id: 'raw', replacementGrams: 100, replacementCalories: 120,
    replacementProtein: 22.5, replacementFat: 2.6, replacementCarbs: 0, replacementFiber: 0,
    replacementFoodName: 'Chicken breast raw', replacementReferenceFoodId: 4, replacementFdcId: 555,
    replacementMatchStatus: 'UsdaApiMatch' });
  assert.equal(item.calories, 240); assert.equal(item.fiber, 0);
  assert.equal(item.referenceFoodId, 4); assert.equal(item.usdaFdcId, 555);
});

test('favorites and food defaults survive reload, backup, and deletion of the original meal', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const storage = memoryStorage(), diary = createLocalDiary(storage);
  const saved = await diary.createMeal(meal);
  const fullMeal = (await diary.getTimeline(meal.date)).meals[0];
  const favorite = await diary.saveFavorite(fullMeal);
  await diary.saveFavorite(fullMeal);
  await diary.rememberFood('My Rice', fullMeal.items[0], true);
  await diary.deleteMeal(saved.id);
  const reloaded = createLocalDiary(storage);
  assert.equal((await reloaded.getLibrary()).favorites.length, 1);
  assert.equal((await reloaded.getLibrary()).favorites[0].items[0].grams, 100);
  const restored = createLocalDiary(memoryStorage()); restored.importBackup(reloaded.exportBackup());
  assert.deepEqual(await restored.getLibrary(), await reloaded.getLibrary());
  await restored.deleteFavorite(favorite.id); await restored.forgetFood((await restored.getLibrary()).foods[0].id);
  assert.deepEqual(await restored.getLibrary(), { favorites: [], foods: [] });
  assert.equal((await reloaded.getLibrary()).favorites.length, 1);
});

test('remembered aliases update deliberately and invalid backup defaults never overwrite good data', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  await diary.rememberFood('My Rice', meal.items[0], true);
  await diary.rememberFood(' my   rice ', { ...meal.items[0], grams: 150 }, false);
  const library = await diary.getLibrary();
  assert.equal(library.foods.length, 1); assert.equal(library.foods[0].item.grams, 150);
  const good = diary.exportBackup(), invalid = JSON.parse(good); invalid.foods[0].item.grams = 0;
  assert.throws(() => diary.importBackup(JSON.stringify(invalid)), /Invalid remembered/);
  assert.equal(diary.exportBackup(), good);
});

const savedWhey = { id: 1, alias: 'My whey', usePortion: true, item: { foodName: 'Vanilla whey — checked label',
  quantity: 1, unit: 'scoop', grams: 32, calories: 120, protein: 24, carbs: 3, fat: 1.5, fiber: 0, usdaMatchStatus: 'Estimated' } };
const parsedWhey = source => ({ suggestedMealName: 'Meal', items: [{ ...savedWhey.item, foodName: 'Unknown whey', sourceText: source,
  quantity: 1, unit: 'serving', grams: 100, calories: 0, protein: 0, carbs: 0, fat: 0, usdaMatchStatus: 'Unparsed' }],
  clarificationChips: [{ id: 'old', targetItemIndex: 0 }], aiSummary: 'Could not read: My whey' });

test('remembered nutrition resolves unknown aliases while explicit weights override usual portions', () => {
  const { applyFoodDefaults } = load(read('../src/utils/foodDefaults.ts'));
  const bare = applyFoodDefaults(parsedWhey('My whey'), [savedWhey]);
  assert.equal(bare.items[0].grams, 32); assert.equal(bare.items[0].protein, 24);
  assert.equal(bare.clarificationChips.length, 0); assert.equal(bare.aiSummary, undefined);
  const explicit = applyFoodDefaults(parsedWhey('64g My whey'), [savedWhey]);
  assert.equal(explicit.items[0].grams, 64); assert.equal(explicit.items[0].protein, 48);
  assert.equal(explicit.items[0].quantity, 64); assert.equal(explicit.items[0].unit, 'g');
  const unrelated = applyFoodDefaults(parsedWhey('Another whey'), [savedWhey]);
  assert.equal(unrelated.items[0].usdaMatchStatus, 'Unparsed');
});

test('remembered servings scale counts and nutrition-only defaults retain portion uncertainty', () => {
  const { applyFoodDefaults } = load(read('../src/utils/foodDefaults.ts'));
  const result = parsedWhey('2 scoops My whey'); result.items[0].quantity = 2; result.items[0].unit = 'scoops';
  const counted = applyFoodDefaults(result, [savedWhey]);
  assert.equal(counted.items[0].grams, 64); assert.equal(counted.items[0].calories, 240);
  const nutritionOnly = applyFoodDefaults(parsedWhey('My whey'), [{ ...savedWhey, usePortion: false }]);
  assert.equal(nutritionOnly.items[0].grams, 100);
  assert.ok(nutritionOnly.items[0].assumptions.includes('Estimated portion'));
  const branded = applyFoodDefaults(parsedWhey('My whey 2%'), [{ ...savedWhey, alias: 'My whey 2%' }]);
  assert.equal(branded.items[0].grams, 32); // Product names containing digits are not portions.
});

test('editing and undo preserve the original date and position without reverting later meals', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage()); const first = await diary.createMeal(meal);
  const edit = await diary.updateMeal(first.id, { name: 'Updated dinner', items: [{ ...meal.items[0], calories: 200 }] });
  const second = await diary.createMeal({ ...meal, name: 'Later' });
  let timeline = await diary.getTimeline(meal.date);
  assert.equal(timeline.consumedCalories, 330); assert.equal(timeline.meals[0].date, meal.date);
  await edit.undo(); timeline = await diary.getTimeline(meal.date);
  assert.deepEqual(timeline.meals.map(m => m.id), [first.id, second.id]);
  assert.equal(timeline.consumedCalories, 260); assert.equal(timeline.meals[0].name, 'Dinner');
  const stale = await diary.updateMeal(first.id, { name: 'One', items: meal.items });
  await diary.updateMeal(first.id, { name: 'Two', items: meal.items });
  await assert.rejects(stale.undo(), /changed/);
});

test('weekly progress distinguishes empty days and uses each day’s effective targets across DST', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  await diary.createMeal({ ...meal, date: '2026-03-07' });
  await diary.createMeal({ ...meal, date: '2026-03-09' });
  await diary.updateTarget({ name: 'Changed', effectiveDate: '2026-03-08', targetCalories: 2100, targetProtein: 160, targetCarbs: 240, targetFat: 65 });
  const days = await diary.getWeek('2026-03-10');
  assert.deepEqual(days.map(d => d.date), ['2026-03-04','2026-03-05','2026-03-06','2026-03-07','2026-03-08','2026-03-09','2026-03-10']);
  assert.equal(days[3].target.targetCalories, 2500); assert.equal(days[4].target.targetCalories, 2100);
  assert.equal(days[4].meals.length, 0); assert.equal(days[5].consumedCalories, 130);
  await assert.rejects(diary.getWeek('2026-02-30'), /valid date/);
});

test('older backups remain readable with an empty library', async () => {
  const { createLocalDiary } = load(read('../src/services/localDiary.ts'));
  const diary = createLocalDiary(memoryStorage());
  diary.importBackup('{"version":1,"meals":[],"targets":[]}');
  assert.deepEqual(await diary.getLibrary(), { favorites: [], foods: [] });
});
