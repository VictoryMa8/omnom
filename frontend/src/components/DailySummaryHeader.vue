<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { ArrowUpRight, SlidersHorizontal } from 'lucide-vue-next';
import QuickTargetModal from './QuickTargetModal.vue';
const diary = useDiaryStore();
const showTargetModal = ref(false);
const target = computed(() => diary.timeline?.target);
const calories = computed(() => diary.timeline?.consumedCalories ?? 0);
const goal = computed(() => target.value?.targetCalories ?? 2500);
const remaining = computed(() => goal.value - calories.value);
const circumference = 2 * Math.PI * 82;
const offset = computed(() => circumference * (1 - Math.min(1, calories.value / (goal.value || 1))));
const macros = computed(() => [
  { name: 'Protein', amount: diary.timeline?.consumedProtein ?? 0, goal: target.value?.targetProtein ?? 180, color: '#a66348' },
  { name: 'Carbs', amount: diary.timeline?.consumedCarbs ?? 0, goal: target.value?.targetCarbs ?? 275, color: '#77947d' },
  { name: 'Fat', amount: diary.timeline?.consumedFat ?? 0, goal: target.value?.targetFat ?? 75, color: '#b99854' },
]);
</script>
<template>
  <section class="summary-card" :aria-busy="diary.loading">
    <div class="section-heading"><h2>Daily overview</h2><button class="icon-button" aria-label="Adjust daily targets" @click="showTargetModal = true"><SlidersHorizontal class="icon" /></button></div>
    <span class="pill">{{ target?.name || 'Maintenance' }}</span>
    <div class="calorie-ring">
      <svg viewBox="0 0 200 200" aria-hidden="true"><circle cx="100" cy="100" r="82" fill="none" stroke="#dfe6d9" stroke-width="9" /><circle cx="100" cy="100" r="82" fill="none" stroke="#315344" stroke-width="9" stroke-linecap="round" :stroke-dasharray="circumference" :stroke-dashoffset="offset" transform="rotate(-90 100 100)" /></svg>
      <div class="ring-label"><span class="eyebrow">Calories {{ remaining < 0 ? 'over' : 'left' }}</span><strong>{{ Math.round(Math.abs(remaining)).toLocaleString() }}</strong><span class="muted">of {{ goal.toLocaleString() }} kcal</span></div>
    </div>
    <div class="calorie-caption"><strong>{{ Math.round(calories).toLocaleString() }}</strong> kcal logged <ArrowUpRight class="icon" /></div>
    <div class="macro-list">
      <div v-for="macro in macros" :key="macro.name" class="macro-row">
        <div><span><i :style="{ background: macro.color }"></i>{{ macro.name }}</span><span><strong>{{ Math.round(macro.amount) }}</strong><small> / {{ macro.goal }} g</small></span></div>
        <div class="progress-track" role="progressbar" :aria-label="macro.name" :aria-valuenow="Math.round(macro.amount)" :aria-valuemax="Math.max(macro.goal, Math.round(macro.amount))" aria-valuemin="0"><div :style="{ width: Math.min(100, macro.amount / (macro.goal || 1) * 100) + '%', background: macro.color }"></div></div>
      </div>
    </div>
    <button class="summary-target" @click="showTargetModal = true">Edit goals <ArrowUpRight class="icon" /></button>
  </section>
  <QuickTargetModal :show="showTargetModal" @close="showTargetModal = false" />
</template>
