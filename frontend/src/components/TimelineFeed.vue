<script setup lang="ts">
import { computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import MealCard from './MealCard.vue';
import { Clock, Utensils } from 'lucide-vue-next';

const diaryStore = useDiaryStore();
const toastStore = useToastStore();

const meals = computed(() => diaryStore.timeline?.meals || []);

const handleDelete = async (id: number) => {
  if (confirm('Delete this meal from your diary?')) {
    try {
      await diaryStore.deleteMeal(id);
      toastStore.success('Meal deleted');
    } catch (err: any) {
      toastStore.error(err?.message || 'Failed to delete meal');
    }
  }
};
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between px-0.5">
      <h3 class="text-xs font-mono font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
        <Clock class="w-3.5 h-3.5 text-omnom-violet" />
        <span>Meal Log</span>
      </h3>
      <span class="text-[11px] text-slate-400 font-mono font-bold bg-white/[0.04] px-2 py-0.5 rounded border border-white/[0.06]">
        {{ meals.length }} {{ meals.length === 1 ? 'ENTRY' : 'ENTRIES' }}
      </span>
    </div>

    <!-- Empty State -->
    <div
      v-if="meals.length === 0"
      class="glass-card border border-dashed border-white/[0.08] rounded-xl p-8 text-center"
    >
      <div class="w-10 h-10 rounded-xl bg-omnom-violet/10 border border-omnom-violet/20 flex items-center justify-center mx-auto mb-3 text-omnom-violet">
        <Utensils class="w-4 h-4" />
      </div>
      <h4 class="text-xs font-mono font-bold text-slate-300 uppercase tracking-wider">No entries logged today</h4>
      <p class="text-xs text-slate-500 mt-1">Log a meal above using text or voice.</p>
    </div>

    <!-- Timeline List -->
    <div v-else class="pt-1">
      <MealCard
        v-for="meal in meals"
        :key="meal.id"
        :meal="meal"
        @delete="handleDelete"
      />
    </div>
  </div>
</template>
