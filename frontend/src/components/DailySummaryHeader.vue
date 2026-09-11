<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { SlidersHorizontal, Flame, Activity, CheckCircle2 } from 'lucide-vue-next';
import QuickTargetModal from './QuickTargetModal.vue';

const diaryStore = useDiaryStore();
const showTargetModal = ref(false);

const target = computed(() => diaryStore.timeline?.target);
const consumedCal = computed(() => diaryStore.timeline?.consumedCalories ?? 0);
const remainingCal = computed(() => diaryStore.timeline?.remainingCalories ?? 0);
const targetCal = computed(() => target.value?.targetCalories ?? 2500);
const calPercent = computed(() => Math.min(Math.round((consumedCal.value / (targetCal.value || 1)) * 100), 100));

// Protein
const consumedP = computed(() => diaryStore.timeline?.consumedProtein ?? 0);
const targetP = computed(() => target.value?.targetProtein ?? 180);
const percentP = computed(() => Math.min(Math.round((consumedP.value / (targetP.value || 1)) * 100), 100));
const deltaP = computed(() => Math.round(targetP.value - consumedP.value));

// Carbs
const consumedC = computed(() => diaryStore.timeline?.consumedCarbs ?? 0);
const targetC = computed(() => target.value?.targetCarbs ?? 275);
const percentC = computed(() => Math.min(Math.round((consumedC.value / (targetC.value || 1)) * 100), 100));
const deltaC = computed(() => Math.round(targetC.value - consumedC.value));

// Fat
const consumedF = computed(() => diaryStore.timeline?.consumedFat ?? 0);
const targetF = computed(() => target.value?.targetFat ?? 75);
const percentF = computed(() => Math.min(Math.round((consumedF.value / (targetF.value || 1)) * 100), 100));
const deltaF = computed(() => Math.round(targetF.value - consumedF.value));

// Caloric Macro Split (% of total calories derived from P / C / F)
const calFromP = computed(() => consumedP.value * 4);
const calFromC = computed(() => consumedC.value * 4);
const calFromF = computed(() => consumedF.value * 9);
const totalMacroCal = computed(() => calFromP.value + calFromC.value + calFromF.value);

const splitP = computed(() => (totalMacroCal.value > 0 ? Math.round((calFromP.value / totalMacroCal.value) * 100) : 0));
const splitC = computed(() => (totalMacroCal.value > 0 ? Math.round((calFromC.value / totalMacroCal.value) * 100) : 0));
const splitF = computed(() => (totalMacroCal.value > 0 ? Math.max(0, 100 - splitP.value - splitC.value) : 0));

// Precision circular gauge
const radius = 42;
const circumference = 2 * Math.PI * radius;
const strokeDashoffset = computed(() => {
  const ratio = Math.min(consumedCal.value / (targetCal.value || 1), 1);
  return circumference - ratio * circumference;
});
</script>

<template>
  <div class="relative glass-card rounded-2xl p-5 sm:p-6 shadow-omnom overflow-hidden">
    <!-- Ambient top glow -->
    <div class="absolute -top-12 -right-12 w-48 h-48 bg-omnom-violet/15 rounded-full blur-3xl pointer-events-none"></div>

    <!-- Header: Target Name & Adjuster -->
    <div class="flex items-center justify-between gap-3 pb-4 border-b border-indigo-500/15 relative z-10">
      <div class="flex items-center gap-2.5">
        <div class="w-8 h-8 rounded-lg bg-omnom-violet/15 border border-omnom-violet/30 flex items-center justify-center text-purple-300">
          <Activity class="w-4 h-4" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <h2 class="text-base sm:text-lg font-bold font-display text-white tracking-tight">
              Daily Fuel
            </h2>
            <span class="text-[10px] font-bold font-mono uppercase tracking-wider px-2 py-0.5 rounded-md bg-omnom-violet/15 text-purple-200 border border-omnom-violet/30">
              {{ target?.name || 'Target' }}
            </span>
          </div>
        </div>
      </div>

      <button
        @click="showTargetModal = true"
        class="btn-bounce inline-flex items-center gap-1.5 text-xs font-mono font-bold px-3 py-1.5 rounded-lg bg-white/[0.04] hover:bg-white/[0.08] text-slate-200 border border-indigo-500/20 hover:border-omnom-violet/40 transition-all cursor-pointer shrink-0"
      >
        <SlidersHorizontal class="w-3.5 h-3.5 text-omnom-violet" />
        <span class="hidden sm:inline">Targets</span>
      </button>
    </div>

    <!-- Center Section: Precision Calorie Gauge + 3 Macro Cards -->
    <div class="py-5 grid grid-cols-1 lg:grid-cols-12 gap-5 items-center relative z-10">
      <!-- Calorie Gauge -->
      <div class="lg:col-span-4 flex items-center justify-center sm:justify-start gap-4 p-3 rounded-xl bg-black/30 border border-indigo-500/15">
        <div class="relative w-24 h-24 flex items-center justify-center shrink-0">
          <svg class="w-full h-full -rotate-90" viewBox="0 0 100 100">
            <defs>
              <linearGradient id="calorie-gauge-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                <stop offset="0%" stop-color="#818cf8" />
                <stop offset="100%" stop-color="#c084fc" />
              </linearGradient>
            </defs>
            <circle
              cx="50"
              cy="50"
              :r="radius"
              stroke="currentColor"
              stroke-width="7"
              class="text-white/[0.06]"
              fill="transparent"
            />
            <circle
              cx="50"
              cy="50"
              :r="radius"
              stroke="url(#calorie-gauge-grad)"
              stroke-width="7"
              stroke-linecap="round"
              class="transition-all duration-700 ease-out"
              :stroke-dasharray="circumference"
              :stroke-dashoffset="strokeDashoffset"
              fill="transparent"
            />
          </svg>

          <!-- Inside Ring Metrics -->
          <div class="absolute inset-0 flex flex-col items-center justify-center text-center">
            <Flame class="w-3.5 h-3.5 text-omnom-violet mb-0.5" />
            <span class="text-[9px] font-mono text-slate-400 uppercase tracking-wider font-bold">Remaining</span>
            <span class="text-sm font-mono font-black text-white leading-none mt-0.5">
              {{ remainingCal > 0 ? remainingCal : 0 }}
            </span>
            <span class="text-[8px] font-mono text-slate-500">kcal</span>
          </div>
        </div>

        <div>
          <div class="flex items-baseline gap-1 text-xl font-bold font-mono tracking-tight text-white">
            <span>{{ consumedCal }}</span>
            <span class="text-xs font-normal text-slate-400 font-mono">/ {{ targetCal }}</span>
          </div>
          <div class="text-[11px] font-mono text-slate-400 mt-1 flex items-center gap-1.5">
            <span class="text-purple-300 font-bold">{{ calPercent }}%</span> of ceiling
          </div>
          <div class="text-[10px] font-mono text-slate-500 mt-0.5">
            {{ consumedCal > targetCal ? `+${consumedCal - targetCal} kcal over` : `${remainingCal} kcal left` }}
          </div>
        </div>
      </div>

      <!-- 3 Precision Macro Cards -->
      <div class="lg:col-span-8 grid grid-cols-3 gap-2.5 sm:gap-3">
        <!-- Protein Card -->
        <div class="bg-black/25 border border-omnom-rose/25 hover:border-omnom-rose/50 rounded-xl p-3 relative overflow-hidden transition-all group">
          <div class="flex items-center justify-between mb-1.5">
            <span class="text-xs font-bold font-mono text-omnom-rose flex items-center gap-1">
              <span class="w-1.5 h-1.5 rounded-full bg-omnom-rose"></span> Protein
            </span>
            <span class="text-[10px] font-mono font-bold text-rose-300">{{ percentP }}%</span>
          </div>
          <div class="flex items-baseline gap-1">
            <span class="text-lg sm:text-xl font-mono font-black text-white tracking-tight">{{ Math.round(consumedP) }}</span>
            <span class="text-[10px] text-slate-400 font-mono">/{{ targetP }}g</span>
          </div>
          <div class="w-full h-1.5 bg-white/[0.06] rounded-full overflow-hidden mt-2">
            <div
              class="h-full bg-omnom-rose rounded-full transition-all duration-500"
              :style="{ width: `${percentP}%` }"
            ></div>
          </div>
          <div class="mt-2 text-[10px] font-mono font-bold flex items-center gap-1">
            <span v-if="deltaP <= 0" class="text-omnom-emerald flex items-center gap-0.5">
              <CheckCircle2 class="w-2.5 h-2.5" /> Met
            </span>
            <span v-else class="text-slate-400">
              -{{ deltaP }}g left
            </span>
          </div>
        </div>

        <!-- Carbs Card -->
        <div class="bg-black/25 border border-omnom-cyan/25 hover:border-omnom-cyan/50 rounded-xl p-3 relative overflow-hidden transition-all group">
          <div class="flex items-center justify-between mb-1.5">
            <span class="text-xs font-bold font-mono text-omnom-cyan flex items-center gap-1">
              <span class="w-1.5 h-1.5 rounded-full bg-omnom-cyan"></span> Carbs
            </span>
            <span class="text-[10px] font-mono font-bold text-sky-300">{{ percentC }}%</span>
          </div>
          <div class="flex items-baseline gap-1">
            <span class="text-lg sm:text-xl font-mono font-black text-white tracking-tight">{{ Math.round(consumedC) }}</span>
            <span class="text-[10px] text-slate-400 font-mono">/{{ targetC }}g</span>
          </div>
          <div class="w-full h-1.5 bg-white/[0.06] rounded-full overflow-hidden mt-2">
            <div
              class="h-full bg-omnom-cyan rounded-full transition-all duration-500"
              :style="{ width: `${percentC}%` }"
            ></div>
          </div>
          <div class="mt-2 text-[10px] font-mono font-bold flex items-center gap-1">
            <span v-if="deltaC <= 0" class="text-omnom-emerald flex items-center gap-0.5">
              <CheckCircle2 class="w-2.5 h-2.5" /> Met
            </span>
            <span v-else class="text-slate-400">
              -{{ deltaC }}g left
            </span>
          </div>
        </div>

        <!-- Fat Card -->
        <div class="bg-black/25 border border-omnom-amber/25 hover:border-omnom-amber/50 rounded-xl p-3 relative overflow-hidden transition-all group">
          <div class="flex items-center justify-between mb-1.5">
            <span class="text-xs font-bold font-mono text-omnom-amber flex items-center gap-1">
              <span class="w-1.5 h-1.5 rounded-full bg-omnom-amber"></span> Fat
            </span>
            <span class="text-[10px] font-mono font-bold text-amber-300">{{ percentF }}%</span>
          </div>
          <div class="flex items-baseline gap-1">
            <span class="text-lg sm:text-xl font-mono font-black text-white tracking-tight">{{ Math.round(consumedF) }}</span>
            <span class="text-[10px] text-slate-400 font-mono">/{{ targetF }}g</span>
          </div>
          <div class="w-full h-1.5 bg-white/[0.06] rounded-full overflow-hidden mt-2">
            <div
              class="h-full bg-omnom-amber rounded-full transition-all duration-500"
              :style="{ width: `${percentF}%` }"
            ></div>
          </div>
          <div class="mt-2 text-[10px] font-mono font-bold flex items-center gap-1">
            <span v-if="deltaF <= 0" class="text-omnom-emerald flex items-center gap-0.5">
              <CheckCircle2 class="w-2.5 h-2.5" /> Met
            </span>
            <span v-else class="text-slate-400">
              -{{ deltaF }}g left
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- Caloric Macro Split Bar -->
    <div class="pt-3 border-t border-indigo-500/15 relative z-10">
      <div class="flex items-center justify-between text-[10px] font-mono text-slate-400 mb-1.5">
        <span class="uppercase tracking-wider font-bold">Caloric Split</span>
        <div class="flex items-center gap-3">
          <span class="text-rose-400 font-bold">{{ splitP }}% P</span>
          <span class="text-sky-400 font-bold">{{ splitC }}% C</span>
          <span class="text-amber-400 font-bold">{{ splitF }}% F</span>
        </div>
      </div>

      <!-- Segmented Bar -->
      <div class="w-full h-1.5 rounded-full overflow-hidden flex bg-white/[0.04]">
        <div
          class="h-full bg-omnom-rose transition-all duration-500"
          :style="{ width: `${splitP}%` }"
        ></div>
        <div
          class="h-full bg-omnom-cyan transition-all duration-500"
          :style="{ width: `${splitC}%` }"
        ></div>
        <div
          class="h-full bg-omnom-amber transition-all duration-500"
          :style="{ width: `${splitF}%` }"
        ></div>
      </div>
    </div>

    <!-- Quick Target Modal -->
    <QuickTargetModal
      :show="showTargetModal"
      @close="showTargetModal = false"
    />
  </div>
</template>
