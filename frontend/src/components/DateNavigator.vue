<script setup lang="ts">
import { computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { ChevronLeft, ChevronRight, Calendar } from 'lucide-vue-next';

const diaryStore = useDiaryStore();

const formattedDate = computed(() => {
  if (!diaryStore.selectedDate) return '';
  const [y, m, d] = diaryStore.selectedDate.split('-').map(Number);
  const date = new Date(y, m - 1, d);
  
  const today = new Date();
  const isToday =
    date.getDate() === today.getDate() &&
    date.getMonth() === today.getMonth() &&
    date.getFullYear() === today.getFullYear();

  if (isToday) return 'Today';

  const yesterday = new Date();
  yesterday.setDate(today.getDate() - 1);
  if (
    date.getDate() === yesterday.getDate() &&
    date.getMonth() === yesterday.getMonth() &&
    date.getFullYear() === yesterday.getFullYear()
  ) return 'Yesterday';

  return date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  });
});

const isTodaySelected = computed(() => formattedDate.value === 'Today');

const onDateChange = (e: Event) => {
  const target = e.target as HTMLInputElement;
  if (target.value) {
    diaryStore.fetchTimeline(target.value);
  }
};
</script>

<template>
  <div class="flex items-center justify-between glass-card rounded-xl px-2.5 py-1.5 shadow-omnom">
    <button
      @click="diaryStore.shiftDate(-1)"
      class="btn-bounce p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
      title="Previous"
    >
      <ChevronLeft class="w-4 h-4" />
    </button>

    <div class="relative flex items-center gap-2 px-2 py-1 rounded-lg group cursor-pointer hover:bg-white/[0.04] transition-colors">
      <Calendar class="w-3.5 h-3.5 text-omnom-violet group-hover:text-purple-300 transition-colors" />
      <span class="text-xs sm:text-sm font-bold font-mono tracking-tight text-white select-none">
        {{ formattedDate }}
      </span>

      <!-- Hidden native date picker overlay -->
      <input
        type="date"
        :value="diaryStore.selectedDate"
        @change="onDateChange"
        class="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
      />
    </div>

    <div class="flex items-center gap-1">
      <button
        v-if="!isTodaySelected"
        @click="diaryStore.setDateToToday"
        class="btn-bounce text-[10px] font-mono font-bold uppercase tracking-wider px-2 py-0.5 bg-omnom-violet/15 text-purple-200 hover:bg-omnom-violet/25 border border-omnom-violet/30 rounded-md transition-colors cursor-pointer"
      >
        Today
      </button>
      <button
        @click="diaryStore.shiftDate(1)"
        class="btn-bounce p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
        title="Next"
      >
        <ChevronRight class="w-4 h-4" />
      </button>
    </div>
  </div>
</template>
