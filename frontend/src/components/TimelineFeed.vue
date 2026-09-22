<script setup lang="ts">
import { computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import MealCard from './MealCard.vue';
import { Utensils } from 'lucide-vue-next';
const diary = useDiaryStore();
const toast = useToastStore();
const meals = computed(() => diary.timeline?.meals || []);
const handleDelete = async (id: number) => {
  try { const undo = await diary.deleteMeal(id); toast.success('Meal removed.', undo); }
  catch (error: any) { toast.error(error.message || 'Could not remove meal.'); }
};
</script>
<template>
  <section class="meal-feed" :aria-busy="diary.loading">
    <div class="section-heading"><h2>Your meals</h2><span class="muted small">{{ meals.length }} {{ meals.length === 1 ? 'meal' : 'meals' }} logged</span></div>
    <div v-if="diary.loading && !diary.timeline" class="empty-state" role="status">Loading your diary…</div>
    <div v-else-if="!meals.length && !diary.error" class="empty-state"><div class="empty-icon"><Utensils class="icon" /></div><h3>No meals yet</h3></div>
    <div v-else class="meal-list"><MealCard v-for="meal in meals" :key="meal.id" :meal="meal" @delete="handleDelete" /></div>
  </section>
</template>
