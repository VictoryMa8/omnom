import type { DailyTarget, DayTimeline, MealEntry, MealItem } from './api';

const KEY = 'omnom_diary_v1';
interface DiaryData { version: 1; meals: MealEntry[]; targets: DailyTarget[] }
type NewMeal = Pick<MealEntry, 'date' | 'name' | 'time' | 'rawDescription' | 'notes' | 'items'>;
type NewTarget = Omit<DailyTarget, 'id' | 'effectiveDate'> & { effectiveDate?: string };
const today = () => {
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
};
const round = (value: number) => Math.round(value * 10) / 10;
const defaultTarget: DailyTarget = { id: 0, effectiveDate: '0001-01-01', name: 'Maintenance',
  targetCalories: 2500, targetProtein: 180, targetCarbs: 275, targetFat: 75, targetFiber: 35 };
const fields = ['calories', 'protein', 'carbs', 'fat', 'fiber', 'grams', 'quantity'] as const;
const validNumber = (value: unknown) => typeof value === 'number' && Number.isFinite(value) && value >= 0;
const validDate = (value: unknown) => typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)
  && !Number.isNaN(Date.parse(value)) && new Date(value).toISOString().startsWith(value);

function validate(data: DiaryData): DiaryData {
  if (data?.version !== 1 || !Array.isArray(data.meals) || !Array.isArray(data.targets)) throw new Error('Invalid diary backup.');
  for (const meal of data.meals) {
    if (!meal || !validDate(meal.date) || !Number.isSafeInteger(meal.id) || typeof meal.name !== 'string'
      || !Array.isArray(meal.items) || meal.items.some((item: MealItem) => !item || typeof item.foodName !== 'string'
        || typeof item.unit !== 'string' || fields.some(field => !validNumber(item[field])))) throw new Error('Invalid meal in diary backup.');
  }
  for (const target of data.targets) {
    if (!target || !validDate(target.effectiveDate) || !Number.isSafeInteger(target.id) || typeof target.name !== 'string'
      || ![target.targetCalories, target.targetProtein, target.targetCarbs, target.targetFat].every(validNumber)
      || (target.targetFiber != null && !validNumber(target.targetFiber))) throw new Error('Invalid target in diary backup.');
  }
  if (new Set(data.meals.map(m => m.id)).size !== data.meals.length
    || new Set(data.targets.map(t => t.id)).size !== data.targets.length) throw new Error('Duplicate entries in diary backup.');
  return data;
}

function totals(items: MealItem[]) {
  const sum = (field: 'calories' | 'protein' | 'carbs' | 'fat' | 'fiber') => round(items.reduce((n, item) => n + item[field], 0));
  return { totalCalories: sum('calories'), totalProtein: sum('protein'), totalCarbs: sum('carbs'), totalFat: sum('fat'), totalFiber: sum('fiber') };
}

export function createLocalDiary(storage: Pick<Storage, 'getItem' | 'setItem'>) {
  function read(): DiaryData {
    const raw = storage.getItem(KEY);
    if (!raw) return { version: 1, meals: [], targets: [] };
    try { return validate(JSON.parse(raw)); }
    catch { throw new Error('The saved diary could not be read. Export a backup before restoring another file.'); }
  }
  function save(data: DiaryData) {
    validate(data);
    try { storage.setItem(KEY, JSON.stringify(data)); }
    catch { throw new Error('Could not save in this browser. Storage may be full or disabled. Export a backup.'); }
  }
  function targetFor(data: DiaryData, date: string): DailyTarget {
    return data.targets.filter(t => t.effectiveDate <= date)
      .sort((a, b) => b.effectiveDate.localeCompare(a.effectiveDate) || b.id - a.id)[0] ?? { ...defaultTarget };
  }
  return {
    async getTimeline(date: string): Promise<DayTimeline> {
      const data = read();
      const target = targetFor(data, date);
      let cal = 0, protein = 0, carbs = 0, fat = 0, fiber = 0;
      const meals = data.meals.filter(m => m.date === date).sort((a, b) => a.id - b.id).map(meal => {
        const total = totals(meal.items);
        cal = round(cal + total.totalCalories); protein = round(protein + total.totalProtein);
        carbs = round(carbs + total.totalCarbs); fat = round(fat + total.totalFat); fiber = round(fiber + total.totalFiber);
        return { ...meal, ...total, runningCalories: cal, runningProtein: protein, runningCarbs: carbs, runningFat: fat };
      });
      return { date, target, meals, consumedCalories: cal, consumedProtein: protein, consumedCarbs: carbs,
        consumedFat: fat, consumedFiber: fiber, remainingCalories: round(target.targetCalories - cal),
        remainingProtein: round(target.targetProtein - protein), remainingCarbs: round(target.targetCarbs - carbs),
        remainingFat: round(target.targetFat - fat) };
    },
    async createMeal(meal: NewMeal) {
      const data = read();
      const id = Math.max(0, ...data.meals.map(m => m.id)) + 1;
      data.meals.push({ ...meal, id, ...totals(meal.items), runningCalories: 0, runningProtein: 0, runningCarbs: 0, runningFat: 0 });
      save(data);
      return { id };
    },
    async deleteMeal(id: number) {
      const data = read(); data.meals = data.meals.filter(m => m.id !== id); save(data);
    },
    async updateMeal(id: number, meal: Omit<NewMeal, 'date'>) {
      const data = read(); const index = data.meals.findIndex(m => m.id === id);
      if (index < 0) throw new Error('Meal not found.');
      data.meals[index] = { ...data.meals[index], ...meal, ...totals(meal.items) }; save(data);
    },
    async updateTarget(target: NewTarget) {
      const data = read();
      const saved = { ...target, id: Math.max(0, ...data.targets.map(t => t.id)) + 1, effectiveDate: target.effectiveDate || today() };
      data.targets.push(saved); save(data); return saved;
    },
    async getCurrentTarget() { return targetFor(read(), today()); },
    exportBackup() { return storage.getItem(KEY) || JSON.stringify(read()); },
    importBackup(raw: string) { save(validate(JSON.parse(raw))); },
  };
}

// Resolve storage only when needed so startup still renders when storage is blocked.
export const localDiary = createLocalDiary({
  getItem: key => window.localStorage.getItem(key),
  setItem: (key, value) => window.localStorage.setItem(key, value),
});
