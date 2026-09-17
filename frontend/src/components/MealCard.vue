<script setup lang="ts">
import { ref } from 'vue';
import type { MealEntry } from '../services/api';
import { Clock3, ChevronDown, Trash2, Utensils } from 'lucide-vue-next';
defineProps<{ meal: MealEntry }>();
defineEmits<{ (e: 'delete', id: number): void }>();
const expanded = ref(false);
const formatTime = (value?: string) => {
  if (!value || !/^([01]\d|2[0-3]):[0-5]\d$/.test(value)) return '';
  const [h, m] = value.split(':').map(Number);
  return `${h % 12 || 12}:${String(m).padStart(2,'0')} ${h >= 12 ? 'PM' : 'AM'}`;
};
</script>
<template>
  <article class="card saved-meal">
    <div class="saved-meal-heading"><div class="meal-symbol"><Utensils class="icon" /></div><div class="meal-title"><h3>{{ meal.name }}</h3><p class="muted small">{{ meal.items.length }} {{ meal.items.length === 1 ? 'food' : 'foods' }}<span v-if="formatTime(meal.time)" class="meal-time"><Clock3 class="small-icon" />{{ formatTime(meal.time) }}</span></p></div>
      <div class="meal-energy"><strong>{{ Math.round(meal.totalCalories) }}</strong><span class="muted small">kcal</span></div>
      <button class="icon-button" :aria-label="(expanded ? 'Collapse ' : 'Expand ') + meal.name" :aria-expanded="expanded" @click="expanded = !expanded"><ChevronDown class="icon" :class="{ 'rotate-180': expanded }" /></button></div>
    <p v-if="meal.rawDescription" class="meal-description muted">{{ meal.rawDescription }}</p>
    <div class="saved-meal-footer"><div class="macro-tags"><span>{{ meal.totalProtein }}g <small>protein</small></span><span>{{ meal.totalCarbs }}g <small>carbs</small></span><span>{{ meal.totalFat }}g <small>fat</small></span></div><button class="icon-button delete-button" :aria-label="'Delete ' + meal.name" @click="$emit('delete', meal.id)"><Trash2 class="icon" /></button></div>
    <div v-if="expanded" class="meal-breakdown"><div v-for="(item, index) in meal.items" :key="index" class="breakdown-row"><div><strong>{{ item.foodName }}</strong><p class="muted small">{{ item.grams }}g · {{ item.protein }}g protein · {{ item.carbs }}g carbs · {{ item.fat }}g fat</p><p v-if="item.assumptions?.length" class="muted small">{{ item.assumptions.join(' · ') }}</p></div><span>{{ item.calories }} kcal</span></div><p class="muted small">Day so far: {{ meal.runningCalories }} kcal · {{ meal.runningProtein }}g protein</p></div>
  </article>
</template>
