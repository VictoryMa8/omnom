<script setup lang="ts">
import { computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { TrendingUp } from 'lucide-vue-next';
const diary = useDiaryStore();
const logged = computed(() => diary.week.filter(day => day.meals.length));
const average = computed(() => logged.value.length ? Math.round(logged.value.reduce((sum, day) => sum + day.consumedCalories, 0) / logged.value.length) : 0);
const proteinDays = computed(() => logged.value.filter(day => day.target.targetProtein > 0 && day.consumedProtein >= day.target.targetProtein).length);
const ratio = (consumed: number, target: number) => target > 0 ? Math.min(100, consumed / target * 100) : 0;
</script>
<template>
  <section class="card weekly-progress" :aria-busy="diary.loading">
    <div class="section-heading"><h2>Past 7 days</h2><TrendingUp class="icon" /></div>
    <p class="muted small" v-if="diary.week.length">Ending {{ new Date(diary.selectedDate + 'T12:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) }}</p>
    <div class="week-stats"><div><strong>{{ logged.length }}/7</strong><small>days logged</small></div><div><strong>{{ average.toLocaleString() }}</strong><small>avg. kcal</small></div><div><strong>{{ proteinDays }}</strong><small>protein goals met</small></div></div>
    <div class="week-days"><button v-for="day in diary.week" :key="day.date" class="week-day" :class="{ 'selected-day': day.date === diary.selectedDate }" :aria-label="'Open diary for ' + day.date" @click="diary.fetchTimeline(day.date)"><span>{{ new Date(day.date + 'T12:00:00').toLocaleDateString('en-US', { weekday: 'short' }) }}<small>{{ new Date(day.date + 'T12:00:00').getDate() }}</small></span><div><div class="week-track"><i :style="{ width: ratio(day.consumedCalories, day.target.targetCalories) + '%' }"></i></div><small>{{ day.meals.length ? `${Math.round(day.consumedCalories)} / ${day.target.targetCalories} kcal · ${Math.round(day.consumedProtein)} / ${day.target.targetProtein}g protein` : 'No meals logged' }}</small></div></button></div>
    <p class="muted small">Averages include logged days only. Goals use the targets in effect on each day.</p>
  </section>
</template>
