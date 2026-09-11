<script setup lang="ts">
import { ref, computed } from 'vue';
import type { AiParsedMealResult, MealItem, ClarificationOption, AiClarificationChip } from '../services/api';
import { Check, Trash2, Clock, Plus, Minus, Star, Flame, Loader2, Sparkles } from 'lucide-vue-next';

const props = defineProps<{
  parsedResult: AiParsedMealResult;
  rawPrompt: string;
  isSaving?: boolean;
}>();

const emit = defineEmits<{
  (e: 'confirm', data: { name: string; time?: string; items: MealItem[]; rawDescription: string }): void;
  (e: 'discard'): void;
}>();

const mealName = ref(props.parsedResult.suggestedMealName || 'Meal');
const mealTime = ref('');
const showTimeInput = ref(false);

// Local editable copy of items
const editableItems = ref<MealItem[]>(JSON.parse(JSON.stringify(props.parsedResult.items)));

// State of selected chips
const activeChips = ref<AiClarificationChip[]>(JSON.parse(JSON.stringify(props.parsedResult.clarificationChips)));

const totalCal = computed(() => Math.round(editableItems.value.reduce((s, i) => s + (i.calories || 0), 0)));
const totalP = computed(() => Math.round(editableItems.value.reduce((s, i) => s + (i.protein || 0), 0)));
const totalC = computed(() => Math.round(editableItems.value.reduce((s, i) => s + (i.carbs || 0), 0)));
const totalF = computed(() => Math.round(editableItems.value.reduce((s, i) => s + (i.fat || 0), 0)));

// Handle clicking a clarification chip option
const selectChipOption = (chip: AiClarificationChip, option: ClarificationOption) => {
  chip.selectedOptionId = option.id;

  // Modifies existing item (e.g. Raw vs Cooked or Beef 93/7 vs 80/20)
  if (chip.targetItemIndex != null && editableItems.value[chip.targetItemIndex]) {
    const item = editableItems.value[chip.targetItemIndex];
    const ratio = item.grams / (option.replacementGrams || item.grams || 100);
    if (option.replacementFoodName) item.foodName = option.replacementFoodName;
    if (option.replacementCalories != null) item.calories = Math.round(option.replacementCalories * ratio * 10) / 10;
    if (option.replacementProtein != null) item.protein = Math.round(option.replacementProtein * ratio * 10) / 10;
    if (option.replacementCarbs != null) item.carbs = Math.round(option.replacementCarbs * ratio * 10) / 10;
    if (option.replacementFat != null) item.fat = Math.round(option.replacementFat * ratio * 10) / 10;
    item.selectedClarification = option.label;
  }

  // Additive cooking fat (e.g. 1 tbsp Olive Oil or Butter)
  if (chip.id === 'chip-cooking-oil') {
    editableItems.value = editableItems.value.filter((i) => !i.selectedClarification?.startsWith('Added Oil:'));

    if (option.isAdditiveItem && option.additiveItemName) {
      editableItems.value.push({
        foodName: option.additiveItemName,
        quantity: 1,
        unit: 'tbsp',
        grams: option.additiveGrams || 14,
        calories: option.additiveCalories || 124,
        protein: option.additiveProtein || 0,
        carbs: option.additiveCarbs || 0,
        fat: option.additiveFat || 14,
        fiber: 0,
        selectedClarification: `Added Oil: ${option.label}`,
        usdaMatchStatus: 'VerifiedStaple',
      });
    }
  }
};

const adjustGrams = (item: MealItem, delta: number) => {
  const current = item.grams || 100;
  const next = Math.max(10, current + delta);
  updateItemGrams(item, next);
};

const updateItemGrams = (item: MealItem, newGrams: number) => {
  if (!Number.isFinite(newGrams) || newGrams <= 0) return;
  const ratio = newGrams / (item.grams || 100);
  item.grams = newGrams;
  item.quantity *= ratio;
  item.fiber = Math.round(item.fiber * ratio * 10) / 10;
  item.calories = Math.round(item.calories * ratio);
  item.protein = Math.round(item.protein * ratio * 10) / 10;
  item.carbs = Math.round(item.carbs * ratio * 10) / 10;
  item.fat = Math.round(item.fat * ratio * 10) / 10;
};

const removeItem = (index: number) => {
  editableItems.value.splice(index, 1);
  activeChips.value = activeChips.value.filter(chip => chip.targetItemIndex !== index);
  for (const chip of activeChips.value) {
    if (chip.targetItemIndex != null && chip.targetItemIndex > index) chip.targetItemIndex--;
  }
};

const addItem = () => {
  editableItems.value.push({
    foodName: 'Food Item',
    quantity: 1,
    unit: 'serving',
    grams: 100,
    calories: 150,
    protein: 10,
    carbs: 15,
    fat: 4,
    fiber: 0,
    usdaMatchStatus: 'Estimated',
  });
};

const submitConfirm = () => {
  emit('confirm', {
    name: mealName.value || 'Meal',
    time: mealTime.value ? mealTime.value : undefined,
    items: editableItems.value,
    rawDescription: props.rawPrompt,
  });
};
</script>

<template>
  <div class="relative glass-card border border-omnom-violet/40 rounded-xl p-5 sm:p-6 shadow-omnom mb-6 animate-in fade-in slide-in-from-top-3 duration-200">
    <!-- Header: Title, Optional Time, Original Description -->
    <div class="flex flex-col gap-3 pb-4 border-b border-indigo-500/15 sm:flex-row sm:items-start sm:justify-between">
      <div class="flex items-start gap-3 min-w-0 flex-1">
        <div class="w-9 h-9 rounded-lg bg-omnom-violet/15 border border-omnom-violet/30 flex items-center justify-center text-purple-300 font-bold shrink-0">
          <Sparkles class="w-4 h-4" />
        </div>
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2">
            <span class="text-[10px] font-mono font-bold uppercase tracking-wider text-purple-300">Review Entry</span>
          </div>
          <input
            v-model="mealName"
            type="text"
            class="w-full min-w-0 bg-transparent font-bold text-lg text-white focus:outline-none hover:border-b border-indigo-500/30 focus:border-omnom-violet transition-colors font-display"
            placeholder="Meal Title"
          />
          <p class="text-[11px] text-slate-400 italic mt-0.5 font-sans line-clamp-2 break-words">"{{ rawPrompt }}"</p>
        </div>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <div class="flex items-center gap-2 px-2.5 py-1 rounded-lg bg-black/30 border border-indigo-500/20 text-xs font-mono text-slate-300">
          <Clock class="w-3.5 h-3.5 text-omnom-violet shrink-0" />
          <template v-if="mealTime || showTimeInput">
            <input
              v-model="mealTime"
              type="time"
              class="bg-transparent text-white focus:outline-none font-mono text-xs cursor-pointer w-[7.5rem]"
              title="Optional meal time"
            />
            <button
              type="button"
              @click="mealTime = ''; showTimeInput = false"
              class="text-[10px] text-slate-500 hover:text-slate-300 cursor-pointer"
              title="Clear time"
            >
              ✕
            </button>
          </template>
          <button
            v-else
            type="button"
            @click="showTimeInput = true"
            class="text-[10px] text-slate-500 italic hover:text-slate-300 cursor-pointer"
          >
            Optional time
          </button>
        </div>
      </div>
    </div>

    <!-- Clarification Chips -->
    <div v-if="activeChips && activeChips.length > 0" class="py-3 border-b border-indigo-500/15 space-y-2">
      <div class="text-[10px] font-mono font-bold uppercase tracking-wider text-purple-300">
        Clarifications
      </div>

      <div class="flex flex-wrap gap-2">
        <div
          v-for="chip in activeChips"
          :key="chip.id"
          class="flex flex-wrap items-center gap-1.5 max-w-full bg-black/25 border border-indigo-500/15 rounded-lg p-1 text-xs"
        >
          <span class="text-slate-400 font-mono text-[11px] font-bold px-1.5 shrink-0">{{ chip.label }}:</span>
          <div class="flex flex-wrap items-center gap-1 min-w-0">
            <button
              v-for="opt in chip.options"
              :key="opt.id"
              type="button"
              @click="selectChipOption(chip, opt)"
              class="btn-bounce px-2.5 py-0.5 rounded-md font-mono text-[11px] font-bold transition-all cursor-pointer whitespace-nowrap"
              :class="chip.selectedOptionId === opt.id
                ? 'bg-gradient-to-r from-omnom-indigo to-omnom-violet text-white shadow-nom-violet'
                : 'bg-white/[0.04] hover:bg-white/[0.08] text-slate-300 hover:text-white'"
            >
              {{ opt.label }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Ingredients List -->
    <div class="py-4 space-y-2">
      <div
        v-for="(item, idx) in editableItems"
        :key="idx"
        class="flex flex-col gap-2.5 p-3 rounded-lg bg-black/25 border border-indigo-500/15 hover:border-indigo-500/30 transition-all"
      >
        <!-- Name + Match Badge -->
        <div class="flex items-center gap-2 min-w-0">
          <span class="text-xs font-mono font-bold px-1.5 py-0.5 rounded bg-white/[0.06] text-slate-400 shrink-0">
            #{{ idx + 1 }}
          </span>
          <input
            v-model="item.foodName"
            type="text"
            class="bg-transparent text-sm font-bold text-white focus:outline-none hover:border-b border-indigo-500/30 focus:border-omnom-violet min-w-0 flex-1 w-full font-display"
          />
          <span
            v-if="item.usdaMatchStatus === 'VerifiedStaple'"
            class="inline-flex items-center gap-1 text-[9px] font-mono font-bold px-1.5 py-0.5 rounded bg-omnom-emerald/15 text-emerald-300 border border-omnom-emerald/30 shrink-0"
            title="Verified USDA Nutrition"
          >
            <Star class="w-2.5 h-2.5 fill-current" /> USDA
          </span>
          <button
            type="button"
            @click="removeItem(idx)"
            class="btn-bounce p-1.5 rounded-lg text-slate-500 hover:text-omnom-rose hover:bg-white/[0.04] transition-colors cursor-pointer shrink-0"
            title="Remove"
          >
            <Trash2 class="w-3.5 h-3.5" />
          </button>
        </div>

        <!-- Grams Stepper, Calories, and Macros -->
        <div class="flex items-center gap-2 flex-wrap font-mono">
          <!-- Grams Stepper -->
          <div class="flex items-center bg-black/40 rounded-lg border border-indigo-500/20 px-1 py-0.5">
            <button
              type="button"
              @click="adjustGrams(item, -25)"
              class="btn-bounce px-1.5 py-1 hover:bg-white/[0.08] text-slate-500 hover:text-white transition-colors rounded text-[10px] cursor-pointer"
              title="-25g"
            >
              -25
            </button>
            <button
              type="button"
              @click="adjustGrams(item, -10)"
              class="btn-bounce p-1 hover:bg-white/[0.08] text-slate-400 hover:text-white transition-colors rounded cursor-pointer"
              title="-10g"
            >
              <Minus class="w-3 h-3" />
            </button>
            <div class="px-1 py-0.5 flex items-baseline">
              <input
                type="number"
                :value="item.grams"
                @change="updateItemGrams(item, Number(($event.target as HTMLInputElement).value))"
                class="w-14 bg-transparent text-right font-bold text-xs sm:text-sm text-white focus:outline-none font-mono"
              />
              <span class="text-slate-500 font-bold text-[10px] ml-0.5">g</span>
            </div>
            <button
              type="button"
              @click="adjustGrams(item, 10)"
              class="btn-bounce p-1 hover:bg-white/[0.08] text-slate-400 hover:text-white transition-colors rounded cursor-pointer"
              title="+10g"
            >
              <Plus class="w-3 h-3" />
            </button>
            <button
              type="button"
              @click="adjustGrams(item, 25)"
              class="btn-bounce px-1.5 py-1 hover:bg-white/[0.08] text-slate-500 hover:text-white transition-colors rounded text-[10px] cursor-pointer"
              title="+25g"
            >
              +25
            </button>
          </div>

          <!-- Calories Display -->
          <div class="bg-black/40 border border-indigo-500/20 rounded-lg px-2.5 py-1 flex items-baseline gap-1 shrink-0">
            <span class="text-sm font-bold text-white leading-none">
              {{ item.calories }}
            </span>
            <span class="text-[9px] font-bold text-purple-300 uppercase">kcal</span>
          </div>

          <!-- Macro Badges (P / C / F) -->
          <div class="flex items-center gap-1 text-[11px] shrink-0">
            <span class="text-rose-400 font-bold px-1.5 py-0.5 rounded bg-omnom-rose/10 border border-omnom-rose/20" title="Protein">
              {{ item.protein }}g P
            </span>
            <span class="text-sky-400 font-bold px-1.5 py-0.5 rounded bg-omnom-cyan/10 border border-omnom-cyan/20" title="Carbs">
              {{ item.carbs }}g C
            </span>
            <span class="text-amber-400 font-bold px-1.5 py-0.5 rounded bg-omnom-amber/10 border border-omnom-amber/20" title="Fat">
              {{ item.fat }}g F
            </span>
          </div>
        </div>
      </div>

      <button
        @click="addItem"
        class="btn-bounce text-xs font-mono font-bold text-slate-400 hover:text-purple-300 flex items-center gap-1 px-2.5 py-1 rounded-lg hover:bg-white/[0.04] transition-colors cursor-pointer"
      >
        <Plus class="w-3 h-3" /> Add item
      </button>
    </div>

    <!-- Footer: Meal Totals & Commit Action -->
    <div class="pt-4 border-t border-indigo-500/15 flex flex-wrap items-center justify-between gap-3 font-mono">
      <div class="flex items-center gap-2">
        <span class="text-[10px] uppercase tracking-wider text-slate-400 font-bold">Total:</span>
        <span class="text-lg font-bold text-white flex items-center gap-1">
          <Flame class="w-4 h-4 text-omnom-violet" /> {{ totalCal }} kcal
        </span>
        <span class="text-slate-600 font-bold">•</span>
        <span class="text-rose-400 font-bold text-xs">{{ totalP }}g P</span>
        <span class="text-slate-600 font-bold">•</span>
        <span class="text-sky-400 font-bold text-xs">{{ totalC }}g C</span>
        <span class="text-slate-600 font-bold">•</span>
        <span class="text-amber-400 font-bold text-xs">{{ totalF }}g F</span>
      </div>

      <div class="flex items-center gap-2">
        <button
          type="button"
          @click="$emit('discard')"
          class="btn-bounce px-3.5 py-1.5 rounded-lg text-xs font-mono font-bold text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
        >
          Discard
        </button>
        <button
          type="button"
          @click="submitConfirm"
          :disabled="isSaving || editableItems.length === 0"
          class="btn-bounce px-5 py-2 rounded-lg text-xs font-mono font-bold bg-gradient-to-r from-omnom-indigo to-omnom-violet hover:from-indigo-500 hover:to-purple-500 disabled:opacity-50 text-white transition-all shadow-nom-violet flex items-center gap-1.5 cursor-pointer"
        >
          <Loader2 v-if="isSaving" class="w-3.5 h-3.5 animate-spin" />
          <Check v-else class="w-3.5 h-3.5 stroke-[3]" />
          <span>{{ isSaving ? 'Saving...' : 'Save Meal' }}</span>
        </button>
      </div>
    </div>
  </div>
</template>
