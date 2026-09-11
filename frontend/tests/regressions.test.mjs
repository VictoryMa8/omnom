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
  return load(source + '\nexport { editableItems, activeChips, removeItem, updateItemGrams, selectChipOption };', {}, {
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

for (const staleFails of [false, true]) {
  test(`date navigation ignores stale ${staleFails ? 'errors' : 'responses'}`, async () => {
    setActivePinia(createPinia());
    const pending = [];
    const { useDiaryStore } = load(read('../src/stores/diaryStore.ts'), {
      '../services/api': { DiaryApi: { getTimeline: () => new Promise((resolve, reject) => pending.push({ resolve, reject })) } },
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
