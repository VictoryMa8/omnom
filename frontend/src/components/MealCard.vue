<script setup lang="ts">
import { ref } from 'vue';
import type { MealEntry } from '../services/api';
import { Clock, ChevronDown, ChevronUp, Trash2, Flame } from 'lucide-vue-next';

const props = defineProps<{
  meal: MealEntry;
}>();

const emit = defineEmits<{
  (e: 'delete', id: number): void;
}>();

const isExpanded = ref(false);

const formatTime = (timeStr?: string) => {
  if (!timeStr) return '';
  const parts = timeStr.split(':').map(Number);
  if (parts.length < 2 || isNaN(parts[0]) || isNaN(parts[1])) return '';
  const [h, m] = parts;
  const period = h >= 12 ? 'PM' : 'AM';
  const hour12 = h % 12 || 12;
  return `${hour12}:${String(m).padStart(2, '0')} ${period}`;
};
</script>

<template>
  <div class="relative pl-5 pb-5 border-l border-white/[0.08] last:border-l-0 last:pb-0 group">
    <!-- Timeline Dot with Violet Indicator -->
    <div class="absolute -left-[11px] top-3 w-5 h-5 rounded-full bg-omnom-dark border border-omnom-violet/70 flex items-center justify-center text-xs shadow-nom-violet group-hover:scale-110 transition-transform">
      <div class="w-1.5 h-1.5 rounded-full bg-omnom-violet"></div>
    </div>

    <div class="glass-card hover:border-omnom-indigo/30 rounded-xl p-4 transition-all">
      <!-- Card Header -->
      <div class="flex flex-wrap items-center justify-between gap-2">
        <div class="flex items-center gap-3">
          <div>
            <div class="flex items-center gap-2">
              <h4 class="text-sm font-black text-white tracking-tight uppercase">{{ meal.name }}</h4>
              <span v-if="meal.time && formatTime(meal.time)" class="text-[11px] text-slate-400 font-mono flex items-center gap-1 font-bold bg-white/[0.04] px-1.5 py-0.5 rounded border border-white/[0.06]">
                <Clock class="w-3 h-3 text-omnom-violet" /> {{ formatTime(meal.time) }}
              </span>
            </div>
            <div v-if="meal.rawDescription" class="text-[11px] text-slate-400 italic truncate max-w-xs sm:max-w-md mt-0.5">
              "{{ meal.rawDescription }}"
            </div>
          </div>
        </div>

        <!-- Meal Macros -->
        <div class="flex items-center gap-3">
          <div class="text-right font-mono">
            <div class="text-sm font-black text-white flex items-center gap-1 justify-end">
              <Flame class="w-3.5 h-3.5 text-omnom-violet" /> {{ meal.totalCalories }} kcal
            </div>
            <div class="text-[11px] font-mono font-bold space-x-1.5">
              <span class="text-omnom-rose">{{ meal.totalProtein }}g P</span>
              <span class="text-slate-600">•</span>
              <span class="text-omnom-cyan">{{ meal.totalCarbs }}g C</span>
              <span class="text-slate-600">•</span>
              <span class="text-omnom-amber">{{ meal.totalFat }}g F</span>
            </div>
          </div>

          <button
            @click="isExpanded = !isExpanded"
            class="btn-bounce p-1.5 rounded-lg hover:bg-white/[0.06] text-slate-400 hover:text-white transition-colors cursor-pointer"
            :title="isExpanded ? 'Collapse' : 'Expand'"
          >
            <component :is="isExpanded ? ChevronUp : ChevronDown" class="w-4 h-4" />
          </button>
        </div>
      </div>

      <!-- Running Daily Cumulative Total After This Meal -->
      <div class="mt-2.5 pt-2 border-t border-white/[0.06] flex items-center justify-between text-[10px] sm:text-[11px] text-slate-400 font-mono">
        <div class="flex items-center gap-1.5 flex-wrap">
          <span class="text-slate-500 font-bold uppercase tracking-wider text-[9px]">Day Cumulative:</span>
          <span class="text-white font-black">{{ meal.runningCalories }} kcal</span>
          <span class="text-slate-600">|</span>
          <span class="text-omnom-rose font-bold">{{ meal.runningProtein }}g P</span>
          <span class="text-slate-600">|</span>
          <span class="text-omnom-cyan font-bold">{{ meal.runningCarbs }}g C</span>
          <span class="text-slate-600">|</span>
          <span class="text-omnom-amber font-bold">{{ meal.runningFat }}g F</span>
        </div>

        <button
          @click="$emit('delete', meal.id)"
          class="btn-bounce p-1 rounded-md text-slate-500 hover:text-omnom-rose hover:bg-white/[0.04] transition-colors cursor-pointer shrink-0 ml-2"
          title="Delete meal"
        >
          <Trash2 class="w-3.5 h-3.5" />
        </button>
      </div>

      <!-- Expanded Items Breakdown -->
      <div v-if="isExpanded" class="mt-3 pt-2.5 border-t border-white/[0.06] space-y-1.5 animate-in fade-in duration-200">
        <div
          v-for="(item, idx) in meal.items"
          :key="item.id"
          class="flex flex-wrap items-center justify-between gap-2 py-1.5 px-2.5 rounded-lg bg-black/40 border border-white/[0.04] text-xs font-mono"
        >
          <div class="flex items-center gap-2">
            <span class="text-[10px] text-slate-500 font-bold">#{{ idx + 1 }}</span>
            <span class="text-white font-medium text-xs font-sans">{{ item.foodName }}</span>
            <span class="text-[10px] text-omnom-indigo font-bold bg-omnom-indigo/10 px-1.5 py-0.5 rounded border border-omnom-indigo/20">
              {{ item.grams }}g
            </span>
          </div>
          <div class="flex items-center gap-2 font-mono text-[11px]">
            <span class="font-bold text-white">
              {{ item.calories }} kcal
            </span>
            <span class="text-slate-600">•</span>
            <span class="text-omnom-rose font-bold">{{ item.protein }}g P</span>
            <span class="text-slate-600">•</span>
            <span class="text-omnom-cyan font-bold">{{ item.carbs }}g C</span>
            <span class="text-slate-600">•</span>
            <span class="text-omnom-amber font-bold">{{ item.fat }}g F</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
