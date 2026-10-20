<script setup lang="ts">
import { computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import MealCard from './MealCard.vue';
import { Utensils } from 'lucide-vue-next';
import type { MealEntry } from '../services/api';
defineProps<{ busy?: boolean }>();
defineEmits<{ (e: 'edit', meal: MealEntry): void; (e: 'repeat', meal: MealEntry): void }>();
const diary = useDiaryStore();
const toast = useToastStore();
const meals = computed(() => diary.timeline?.meals || []);
const handleDelete = async (id: number) => {
  try { const undo = await diary.deleteMeal(id); toast.success('Meal removed.', undo); }
  catch (error: any) { toast.error(error.message || 'Could not remove meal.'); }
};
const handleFavorite = async (meal: MealEntry) => {
  try {
    const favorite = diary.favorites.find(f => f.sourceMealId === meal.id);
    if (favorite) { await diary.deleteFavorite(favorite.id); toast.success('Favorite removed.'); }
    else { await diary.saveFavorite(meal); toast.success('Meal saved to favorites.'); }
  } catch (error: any) { toast.error(error.message || 'Could not update favorites.'); }
};
</script>
<template>
  <section class="meal-feed" :aria-busy="diary.loading">
    <div class="section-heading"><h2>Your meals</h2><span class="muted small">{{ meals.length }} {{ meals.length === 1 ? 'meal' : 'meals' }} logged</span></div>
    <div v-if="diary.loading && !diary.timeline" class="empty-state" role="status">Loading your diary…</div>
    <div v-else-if="!meals.length && !diary.error" class="empty-state"><div class="empty-icon"><Utensils class="icon" /></div><h3>No meals yet</h3></div>
    <div v-else class="meal-list"><MealCard v-for="meal in meals" :key="meal.id" :meal="meal" :busy="busy || diary.loading" :favorite="diary.favorites.some(f => f.sourceMealId === meal.id)" @delete="handleDelete" @edit="$emit('edit', $event)" @repeat="$emit('repeat', $event)" @favorite="handleFavorite" /></div>
  </section>
</template>
