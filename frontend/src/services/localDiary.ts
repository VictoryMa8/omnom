import type { DailyTarget, DayTimeline, MealEntry, MealItem, FavoriteMeal, RememberedFood } from './api';

const KEY = 'omnom_diary_v1';
interface DiaryData { version: 1; meals: MealEntry[]; targets: DailyTarget[]; nextMealId?: number; favorites?: FavoriteMeal[]; foods?: RememberedFood[] }
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
const validFood = (item: MealItem) => item && typeof item.foodName === 'string' && item.foodName.trim()
  && typeof item.unit === 'string' && fields.every(field => validNumber(item[field])) && item.grams > 0 && item.quantity > 0
  && item.usdaMatchStatus !== 'Unparsed' && (item.assumptions == null || Array.isArray(item.assumptions) && item.assumptions.every(a => typeof a === 'string'));
const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));
const aliasKey = (alias: string) => alias.trim().replace(/\s+/g, ' ').toLocaleLowerCase();

function validate(data: DiaryData): DiaryData {
  if (data?.version !== 1 || !Array.isArray(data.meals) || !Array.isArray(data.targets)) throw new Error('Invalid diary backup.');
  if (data.nextMealId != null && (!Number.isSafeInteger(data.nextMealId) || data.nextMealId < 1)) throw new Error('Invalid diary sequence.');
  for (const meal of data.meals) {
    if (!meal || !validDate(meal.date) || !Number.isSafeInteger(meal.id) || typeof meal.name !== 'string'
      || !Array.isArray(meal.items) || meal.items.some((item: MealItem) => !item || typeof item.foodName !== 'string'
        || typeof item.unit !== 'string' || fields.some(field => !validNumber(item[field]))
        || (item.assumptions != null && (!Array.isArray(item.assumptions) || item.assumptions.some(a => typeof a !== 'string'))))) throw new Error('Invalid meal in diary backup.');
  }
  for (const target of data.targets) {
    if (!target || !validDate(target.effectiveDate) || !Number.isSafeInteger(target.id) || typeof target.name !== 'string'
      || ![target.targetCalories, target.targetProtein, target.targetCarbs, target.targetFat].every(validNumber)
      || (target.targetFiber != null && !validNumber(target.targetFiber))) throw new Error('Invalid target in diary backup.');
  }
  if (new Set(data.meals.map(m => m.id)).size !== data.meals.length
    || new Set(data.targets.map(t => t.id)).size !== data.targets.length) throw new Error('Duplicate entries in diary backup.');
  if (data.favorites != null && (!Array.isArray(data.favorites) || data.favorites.some(f => !f || !Number.isSafeInteger(f.id) || f.id < 1
    || typeof f.name !== 'string' || !f.name.trim() || !Array.isArray(f.items) || !f.items.length || !f.items.every(validFood))
    || new Set(data.favorites.map(f => f.id)).size !== data.favorites.length)) throw new Error('Invalid favorites in diary backup.');
  if (data.foods != null && (!Array.isArray(data.foods) || data.foods.some(f => !f || !Number.isSafeInteger(f.id) || f.id < 1
    || typeof f.alias !== 'string' || !f.alias.trim() || f.alias.length > 120 || typeof f.usePortion !== 'boolean' || !validFood(f.item))
    || new Set(data.foods.map(f => f.id)).size !== data.foods.length
    || new Set(data.foods.map(f => aliasKey(f.alias))).size !== data.foods.length)) throw new Error('Invalid remembered foods in diary backup.');
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
  function timelineFor(data: DiaryData, date: string): DayTimeline {
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
  }
  return {
    async getTimeline(date: string): Promise<DayTimeline> { return timelineFor(read(), date); },
    async getWeek(endDate: string): Promise<DayTimeline[]> {
      if (!validDate(endDate)) throw new Error('Choose a valid date.');
      const data = read();
      const end = new Date(endDate + 'T12:00:00');
      return Array.from({ length: 7 }, (_, index) => {
        const date = new Date(end); date.setDate(date.getDate() - 6 + index);
        const key = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
        return timelineFor(data, key);
      });
    },
    async getRecentMeals(limit = 8): Promise<MealEntry[]> {
      return read().meals.sort((a, b) => b.date.localeCompare(a.date) || b.id - a.id).slice(0, limit).map(m => ({ ...m, ...totals(m.items) }));
    },
    async getLibrary() { const data = read(); return { favorites: data.favorites ?? [], foods: data.foods ?? [] }; },
    async saveFavorite(meal: Pick<MealEntry, 'name' | 'rawDescription' | 'items'> & { id?: number }) {
      const data = read(); data.favorites ??= [];
      const existing = meal.id == null ? undefined : data.favorites.find(f => f.sourceMealId === meal.id);
      if (existing) return existing;
      const favorite = clone({ id: Math.max(0, ...data.favorites.map(f => f.id)) + 1,
        sourceMealId: meal.id, name: meal.name, rawDescription: meal.rawDescription, items: meal.items });
      data.favorites.push(favorite); save(data); return favorite;
    },
    async deleteFavorite(id: number) {
      const data = read(); data.favorites = (data.favorites ?? []).filter(f => f.id !== id); save(data);
    },
    async rememberFood(alias: string, item: MealItem, usePortion: boolean) {
      const data = read(); data.foods ??= [];
      const existing = data.foods.find(f => aliasKey(f.alias) === aliasKey(alias));
      const food = clone({ id: existing?.id ?? Math.max(0, ...data.foods.map(f => f.id)) + 1, alias: alias.trim(), item, usePortion });
      data.foods = data.foods.filter(f => f.id !== food.id); data.foods.push(food); save(data); return food;
    },
    async forgetFood(id: number) {
      const data = read(); data.foods = (data.foods ?? []).filter(f => f.id !== id); save(data);
    },
    async createMeal(meal: NewMeal) {
      const data = read();
      const id = Math.max(data.nextMealId || 1, Math.max(0, ...data.meals.map(m => m.id)) + 1);
      data.nextMealId = id + 1;
      const entry = { ...meal, id, ...totals(meal.items), runningCalories: 0, runningProtein: 0, runningCarbs: 0, runningFat: 0 };
      data.meals.push(entry);
      const snapshot = JSON.stringify(entry);
      save(data);
      return { id, undo: async () => {
        const current = read();
        const index = current.meals.findIndex(m => m.id === id);
        if (index < 0 || JSON.stringify(current.meals[index]) !== snapshot) throw new Error('This meal has changed. Undo is no longer available.');
        current.meals.splice(index, 1);
        save(current);
      } };
    },
    async deleteMeal(id: number) {
      const data = read();
      const meal = data.meals.find(m => m.id === id);
      if (!meal) throw new Error('Meal not found.');
      data.nextMealId = Math.max(data.nextMealId || 1, Math.max(0, ...data.meals.map(m => m.id)) + 1);
      data.meals = data.meals.filter(m => m.id !== id);
      save(data);
      return { undo: async () => {
        const current = read();
        if (current.meals.some(m => m.id === id)) throw new Error('This meal ID is already in use. Undo is no longer available.');
        current.meals.push(meal);
        current.nextMealId = Math.max(current.nextMealId || 1, id + 1);
        save(current);
      } };
    },
    async updateMeal(id: number, meal: Omit<NewMeal, 'date'>) {
      const data = read(); const index = data.meals.findIndex(m => m.id === id);
      if (index < 0) throw new Error('Meal not found.');
      const before = clone(data.meals[index]);
      data.meals[index] = { ...data.meals[index], ...meal, ...totals(meal.items) };
      const snapshot = JSON.stringify(data.meals[index]); save(data);
      return { undo: async () => {
        const current = read(); const position = current.meals.findIndex(m => m.id === id);
        if (position < 0 || JSON.stringify(current.meals[position]) !== snapshot) throw new Error('This meal has changed. Undo is no longer available.');
        current.meals[position] = before; save(current);
      } };
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
