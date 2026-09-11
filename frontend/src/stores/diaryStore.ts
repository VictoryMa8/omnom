import { defineStore } from 'pinia';
import { ref } from 'vue';
import { DiaryApi, TargetsApi, type DayTimeline, type MealItem } from '../services/api';

export const useDiaryStore = defineStore('diary', () => {
  const getTodayString = () => {
    const d = new Date();
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  };

  const selectedDate = ref<string>(getTodayString());
  const timeline = ref<DayTimeline | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  let latestRequest = 0;

  const fetchTimeline = async (date = selectedDate.value) => {
    const requestId = ++latestRequest;
    if (date !== selectedDate.value) timeline.value = null;
    loading.value = true;
    error.value = null;
    try {
      selectedDate.value = date;
      const data = await DiaryApi.getTimeline(date);
      if (requestId === latestRequest) timeline.value = data;
    } catch (err: any) {
      if (requestId === latestRequest) {
        timeline.value = null;
        error.value = err.message || 'Failed to load timeline';
      }
    } finally {
      if (requestId === latestRequest) loading.value = false;
    }
  };

  const shiftDate = (days: number) => {
    const [y, m, d] = selectedDate.value.split('-').map(Number);
    const dateObj = new Date(y, m - 1, d);
    dateObj.setDate(dateObj.getDate() + days);

    const nextYear = dateObj.getFullYear();
    const nextMonth = String(dateObj.getMonth() + 1).padStart(2, '0');
    const nextDay = String(dateObj.getDate()).padStart(2, '0');
    fetchTimeline(`${nextYear}-${nextMonth}-${nextDay}`);
  };

  const setDateToToday = () => {
    fetchTimeline(getTodayString());
  };

  const addMeal = async (mealData: {
    name: string;
    rawDescription?: string;
    items: MealItem[];
    time?: string;
  }) => {
    loading.value = true;
    try {
      await DiaryApi.createMeal({
        date: selectedDate.value,
        name: mealData.name,
        rawDescription: mealData.rawDescription,
        items: mealData.items,
        time: mealData.time,
      });
      await fetchTimeline(selectedDate.value);
    } finally {
      loading.value = false;
    }
  };

  const deleteMeal = async (mealId: number) => {
    loading.value = true;
    try {
      await DiaryApi.deleteMeal(mealId);
      await fetchTimeline(selectedDate.value);
    } finally {
      loading.value = false;
    }
  };

  const updateTarget = async (targetData: {
    name: string;
    targetCalories: number;
    targetProtein: number;
    targetCarbs: number;
    targetFat: number;
    targetFiber?: number;
    effectiveDate?: string;
  }) => {
    await TargetsApi.updateTarget({
      ...targetData,
      effectiveDate: targetData.effectiveDate || selectedDate.value,
    });
    await fetchTimeline(selectedDate.value);
  };

  return {
    selectedDate,
    timeline,
    loading,
    error,
    fetchTimeline,
    shiftDate,
    setDateToToday,
    addMeal,
    deleteMeal,
    updateTarget,
  };
});
