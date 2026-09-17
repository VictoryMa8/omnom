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
  return load(source + '\nexport { editableItems, activeChips, removeItem, updateItemGrams, selectChipOption };', {
    '../utils/nutritionReview': load(read('../src/utils/nutritionReview.ts')),
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
