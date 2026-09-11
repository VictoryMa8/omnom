<script setup lang="ts">
import { ref, watch } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import { X, Check, SlidersHorizontal, Loader2 } from 'lucide-vue-next';

const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const diaryStore = useDiaryStore();
const toastStore = useToastStore();

const targetName = ref('');
const targetCalories = ref(2500);
const targetProtein = ref(180);
const targetCarbs = ref(275);
const targetFat = ref(75);
const isSaving = ref(false);

watch(
  () => diaryStore.timeline?.target,
  (t) => {
    if (t) {
      targetName.value = t.name;
      targetCalories.value = t.targetCalories;
      targetProtein.value = t.targetProtein;
      targetCarbs.value = t.targetCarbs;
      targetFat.value = t.targetFat;
    }
  },
  { immediate: true }
);

const applyPreset = (name: string, cal: number, p: number, c: number, f: number) => {
  targetName.value = name;
  targetCalories.value = cal;
  targetProtein.value = p;
  targetCarbs.value = c;
  targetFat.value = f;
};

const autoCalculateCalories = () => {
  targetCalories.value = Math.round(targetProtein.value * 4 + targetCarbs.value * 4 + targetFat.value * 9);
};

const save = async () => {
  isSaving.value = true;
  try {
    await diaryStore.updateTarget({
      name: targetName.value || 'Custom Target',
      targetCalories: Number(targetCalories.value),
      targetProtein: Number(targetProtein.value),
      targetCarbs: Number(targetCarbs.value),
      targetFat: Number(targetFat.value),
    });
    toastStore.success(`Targets updated: ${targetName.value} (${targetCalories.value} kcal)`);
    emit('close');
  } catch (err: any) {
    toastStore.error(err?.message || 'Failed to update daily targets.');
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md">
    <div class="glass-card border border-white/[0.12] rounded-xl w-full max-w-md p-5 sm:p-6 shadow-2xl relative animate-in fade-in zoom-in-95 duration-150">
      <button
        @click="$emit('close')"
        class="btn-bounce absolute top-4 right-4 p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
      >
        <X class="w-4 h-4" />
      </button>

      <div class="flex items-center gap-2.5 mb-4">
        <div class="w-8 h-8 rounded-lg bg-omnom-violet/10 border border-omnom-violet/20 flex items-center justify-center text-omnom-violet">
          <SlidersHorizontal class="w-4 h-4" />
        </div>
        <div>
          <h3 class="text-base font-black uppercase tracking-tight text-white">Daily Macro Targets</h3>
          <p class="text-xs text-slate-400">Daily calorie and macronutrient targets</p>
        </div>
      </div>

      <!-- Presets -->
      <div class="mb-4">
        <label class="text-[10px] font-mono font-bold text-omnom-violet uppercase tracking-wider mb-2 block">Presets</label>
        <div class="grid grid-cols-2 gap-2">
          <button
            type="button"
            @click="applyPreset('Lean Bulk (2,800 kcal)', 2800, 200, 340, 70)"
            class="btn-bounce p-2 text-left rounded-lg bg-white/[0.02] hover:bg-white/[0.06] border border-white/[0.06] hover:border-omnom-indigo/50 transition-all cursor-pointer font-mono"
          >
            <div class="text-xs font-black text-omnom-indigo">
              Lean Bulk
            </div>
            <div class="text-[10px] text-slate-400 mt-0.5">2,800 kcal • 200g P</div>
          </button>

          <button
            type="button"
            @click="applyPreset('Cutting (2,000 kcal)', 2000, 190, 180, 55)"
            class="btn-bounce p-2 text-left rounded-lg bg-white/[0.02] hover:bg-white/[0.06] border border-white/[0.06] hover:border-omnom-rose/50 transition-all cursor-pointer font-mono"
          >
            <div class="text-xs font-black text-omnom-rose">
              Cutting
            </div>
            <div class="text-[10px] text-slate-400 mt-0.5">2,000 kcal • 190g P</div>
          </button>

          <button
            type="button"
            @click="applyPreset('Maintenance (2,500 kcal)', 2500, 180, 275, 75)"
            class="btn-bounce p-2 text-left rounded-lg bg-white/[0.02] hover:bg-white/[0.06] border border-white/[0.06] hover:border-omnom-cyan/50 transition-all cursor-pointer font-mono"
          >
            <div class="text-xs font-black text-omnom-cyan">
              Maintenance
            </div>
            <div class="text-[10px] text-slate-400 mt-0.5">2,500 kcal • 180g P</div>
          </button>

          <button
            type="button"
            @click="applyPreset('Carb Refeed (3,100 kcal)', 3100, 175, 450, 60)"
            class="btn-bounce p-2 text-left rounded-lg bg-white/[0.02] hover:bg-white/[0.06] border border-white/[0.06] hover:border-omnom-amber/50 transition-all cursor-pointer font-mono"
          >
            <div class="text-xs font-black text-omnom-amber">
              Carb Refeed
            </div>
            <div class="text-[10px] text-slate-400 mt-0.5">3,100 kcal • 450g C</div>
          </button>
        </div>
      </div>

      <!-- Custom Grams Form -->
      <div class="space-y-3 mb-5">
        <div>
          <label class="text-[11px] font-mono font-bold text-slate-400 block mb-1">Target Name</label>
          <input
            v-model="targetName"
            type="text"
            class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-violet font-medium"
            placeholder="e.g. Training Day or Rest Day"
          />
        </div>

        <div class="grid grid-cols-2 gap-2.5">
          <div>
            <label class="text-[11px] font-mono font-bold text-slate-400 block mb-1">Calories (kcal)</label>
            <input
              v-model.number="targetCalories"
              type="number"
              class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-violet font-mono font-bold"
            />
          </div>

          <div>
            <label class="text-[11px] font-mono font-bold text-omnom-rose block mb-1">Protein (g)</label>
            <input
              v-model.number="targetProtein"
              @input="autoCalculateCalories"
              type="number"
              class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-rose font-mono font-bold"
            />
          </div>

          <div>
            <label class="text-[11px] font-mono font-bold text-omnom-cyan block mb-1">Carbs (g)</label>
            <input
              v-model.number="targetCarbs"
              @input="autoCalculateCalories"
              type="number"
              class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-cyan font-mono font-bold"
            />
          </div>

          <div>
            <label class="text-[11px] font-mono font-bold text-omnom-amber block mb-1">Fat (g)</label>
            <input
              v-model.number="targetFat"
              @input="autoCalculateCalories"
              type="number"
              class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-amber font-mono font-bold"
            />
          </div>
        </div>
      </div>

      <div class="flex items-center justify-end gap-2 pt-2 border-t border-white/[0.06]">
        <button
          type="button"
          @click="$emit('close')"
          class="btn-bounce px-3.5 py-1.5 rounded-lg text-xs font-mono font-bold text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
        >
          Cancel
        </button>
        <button
          type="button"
          @click="save"
          :disabled="isSaving"
          class="btn-bounce px-4 py-2 rounded-lg text-xs font-mono font-bold bg-gradient-to-r from-omnom-indigo to-omnom-violet hover:brightness-110 disabled:opacity-50 text-white transition-all shadow-nom-violet flex items-center gap-1.5 cursor-pointer"
        >
          <Loader2 v-if="isSaving" class="w-3.5 h-3.5 animate-spin" />
          <Check v-else class="w-3.5 h-3.5 stroke-[3]" />
          <span>{{ isSaving ? 'Saving...' : 'Save Targets' }}</span>
        </button>
      </div>
    </div>
  </div>
</template>
