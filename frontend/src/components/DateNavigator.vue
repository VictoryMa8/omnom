<script setup lang="ts">
import { computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { ChevronLeft, ChevronRight, CalendarDays } from 'lucide-vue-next';
const diary = useDiaryStore();
const today = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`; };
const isToday = computed(() => diary.selectedDate === today());
const label = computed(() => isToday.value ? 'Today' : new Date(diary.selectedDate + 'T12:00:00').toLocaleDateString('en-US', { month:'short', day:'numeric' }));
</script>
<template>
  <div class="date-control">
    <button class="icon-button" aria-label="Previous day" @click="diary.shiftDate(-1)"><ChevronLeft class="icon" /></button>
    <label class="date-picker"><CalendarDays class="icon" /><span>{{ label }}</span><input aria-label="Choose diary date" type="date" :value="diary.selectedDate" @change="($event.target as HTMLInputElement).value && diary.fetchTimeline(($event.target as HTMLInputElement).value)" /></label>
    <button class="icon-button" aria-label="Next day" @click="diary.shiftDate(1)"><ChevronRight class="icon" /></button>
    <button v-if="!isToday" class="text-button" @click="diary.setDateToToday">Today</button>
  </div>
</template>
