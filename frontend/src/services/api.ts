import axios from 'axios';
import { localDiary } from './localDiary';

export interface MealItem {
  sourceText?: string;
  referenceFoodId?: number;
  id?: number;
  foodName: string;
  quantity: number;
  unit: string;
  grams: number;
  calories: number;
  protein: number;
  carbs: number;
  fat: number;
  fiber: number;
  usdaFdcId?: number;
  selectedClarification?: string;
  usdaMatchStatus?: string;
  assumptions?: string[];
}

export interface MealEntry {
  id: number;
  date: string;
  time?: string;
  name: string;
  rawDescription?: string;
  notes?: string;
  totalCalories: number;
  totalProtein: number;
  totalCarbs: number;
  totalFat: number;
  totalFiber: number;
  runningCalories: number;
  runningProtein: number;
  runningCarbs: number;
  runningFat: number;
  items: MealItem[];
}

export interface DailyTarget {
  id: number;
  effectiveDate: string;
  name: string;
  targetCalories: number;
  targetProtein: number;
  targetCarbs: number;
  targetFat: number;
  targetFiber?: number;
}

export interface DayTimeline {
  date: string;
  target: DailyTarget;
  consumedCalories: number;
  consumedProtein: number;
  consumedCarbs: number;
  consumedFat: number;
  consumedFiber: number;
  remainingCalories: number;
  remainingProtein: number;
  remainingCarbs: number;
  remainingFat: number;
  meals: MealEntry[];
}

export interface ClarificationOption {
  id: string;
  label: string;
  replacementCalories?: number;
  replacementProtein?: number;
  replacementCarbs?: number;
  replacementFat?: number;
  replacementFiber?: number;
  replacementFdcId?: number;
  replacementReferenceFoodId?: number;
  replacementMatchStatus?: string;
  replacementGrams?: number;
  replacementFoodName?: string;
  isAdditiveItem?: boolean;
  additiveItemName?: string;
  additiveGrams?: number;
  additiveCalories?: number;
  additiveProtein?: number;
  additiveCarbs?: number;
  additiveFat?: number;
  additiveFiber?: number;
  additiveFdcId?: number;
  additiveReferenceFoodId?: number;
  additiveMatchStatus?: string;
}

export interface FoodReference {
  id?: number; fdcId?: number; name: string; category: string;
  caloriesPer100g: number; proteinPer100g: number; carbsPer100g: number; fatPer100g: number; fiberPer100g: number;
  defaultServingGrams: number; defaultServingUnit: string; isStaple: boolean;
}

export interface FavoriteMeal {
  id: number; sourceMealId?: number; name: string; rawDescription?: string; items: MealItem[];
}

export interface RememberedFood {
  id: number; alias: string; item: MealItem; usePortion: boolean;
}

export interface AiClarificationChip {
  id: string;
  label: string;
  targetItemIndex?: number;
  selectedOptionId: string;
  options: ClarificationOption[];
}

export interface AiParsedMealResult {
  suggestedMealName: string;
  items: MealItem[];
  clarificationChips: AiClarificationChip[];
  aiSummary?: string;
}

export interface SettingsData {
  openRouterApiKeyMasked: string;
  openRouterModel: string;
  usdaApiKeyMasked: string;
  hasPasscodeConfigured: boolean;
  passcodeManaged?: boolean;
  availableFreeModels: string[];
}

const api = axios.create({
  baseURL: '/api',
  timeout: 55000,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('omnom_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const DiaryApi = localDiary;

export const FoodsApi = {
  search: (q: string, signal?: AbortSignal) => api.get<FoodReference[]>('/foods/search', { params: { q, limit: 20 }, signal }).then(r => {
    if (!Array.isArray(r.data)) throw new Error('Could not load reference foods. Reload this page and try again.');
    return r.data;
  }),
};

export const AiApi = {
  parseMeal: (prompt: string, mealTypeHint?: string, signal?: AbortSignal, mode: 'ai' | 'exact' = 'ai') =>
    api.post<AiParsedMealResult>('/ai/parse', { prompt, mealTypeHint, mode }, { signal }).then((r) => {
      if (!r.data || !Array.isArray(r.data.items)) throw new Error('Browser verification is required. Reload this page and try again.');
      return r.data;
    }),
};

export const TargetsApi = localDiary;

export const SettingsApi = {
  getSettings: () => api.get<SettingsData>('/settings').then((r) => r.data),
  updateSettings: (data: {
    openRouterApiKey?: string;
    openRouterModel?: string;
    usdaApiKey?: string;
    newPasscode?: string;
  }) => api.post('/settings', data).then((r) => r.data),
};

export const AuthApi = {
  getStatus: () => api.get<{ passcodeRequired: boolean; authenticated: boolean }>('/auth/status').then((r) => r.data),
  verify: (passcode: string) =>
    api.post<{ success: boolean; token: string; message: string }>('/auth/verify', { passcode }).then((r) => r.data),
};

export default api;
