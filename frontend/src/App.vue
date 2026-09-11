<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useDiaryStore } from './stores/diaryStore';
import { useToastStore } from './stores/toastStore';
import { AiApi, type AiParsedMealResult, type MealItem } from './services/api';
import DailySummaryHeader from './components/DailySummaryHeader.vue';
import QuickLogInput from './components/QuickLogInput.vue';
import MealConfirmCard from './components/MealConfirmCard.vue';
import TimelineFeed from './components/TimelineFeed.vue';
import DateNavigator from './components/DateNavigator.vue';
import BrowserDataModal from './components/BrowserDataModal.vue';
import { Settings, AlertCircle, CheckCircle2, Info, Dumbbell } from 'lucide-vue-next';

const diaryStore = useDiaryStore();
const toastStore = useToastStore();

const showSettingsModal = ref(false);
const isParsing = ref(false);
const isSavingMeal = ref(false);
const parseError = ref<string | null>(null);

// Active preview state for the Quick-Confirm card
const activeParsedResult = ref<AiParsedMealResult | null>(null);
const currentRawPrompt = ref<string>('');

onMounted(() => { void diaryStore.fetchTimeline(); });
let parseController: AbortController | null = null;
const cancelParsing = () => { parseController?.abort(); };
onUnmounted(cancelParsing);

const handlePromptSubmit = async (prompt: string) => {
  if (isParsing.value || isSavingMeal.value) return;
  parseController = new AbortController();
  isParsing.value = true;
  parseError.value = null;
  activeParsedResult.value = null;
  currentRawPrompt.value = prompt;

  try {
    const result = await AiApi.parseMeal(prompt, undefined, parseController.signal);
    activeParsedResult.value = result;
  } catch (err: any) {
    if (err.code === 'ERR_CANCELED') { toastStore.info('Parsing canceled.'); return; }
    parseError.value =
      err.response?.data?.message ||
      (err.response?.status === 403 ? 'Browser verification is required. Reload this page and try again.' : null) ||
      (err.code === 'ECONNABORTED' ? 'Parsing timed out. Please try again or use a shorter description.' : err.message || 'Could not parse this meal. Please try again.');
    toastStore.error('Could not parse meal. Please try again.');
  } finally {
    isParsing.value = false;
    parseController = null;
  }
};

const handleConfirmMeal = async (data: {
  name: string;
  time?: string;
  items: MealItem[];
  rawDescription: string;
}) => {
  isSavingMeal.value = true;
  try {
    await diaryStore.addMeal(data);
    const totalCal = Math.round(data.items.reduce((s, i) => s + (i.calories || 0), 0));
    const totalP = Math.round(data.items.reduce((s, i) => s + (i.protein || 0), 0));
    toastStore.success(`Meal logged: ${data.name} (+${totalCal} kcal, +${totalP}g protein)`);
    // Clear the confirm card
    activeParsedResult.value = null;
    currentRawPrompt.value = '';
  } catch (err: any) {
    console.error('Failed to save meal:', err);
    toastStore.error(err.response?.data?.message || err.message || 'Failed to save meal to diary');
  } finally {
    isSavingMeal.value = false;
  }
};

const handleDiscardMeal = () => {
  activeParsedResult.value = null;
  currentRawPrompt.value = '';
};
</script>

<template>
  <!-- Global Floating Toasts -->
  <div class="fixed top-4 left-1/2 -translate-x-1/2 z-50 flex flex-col gap-2 pointer-events-none max-w-md w-full px-4 font-mono">
    <div
      v-for="toast in toastStore.toasts"
      :key="toast.id"
      class="pointer-events-auto flex items-center justify-between gap-3 px-3.5 py-2.5 rounded-lg shadow-2xl border text-xs font-bold animate-in fade-in slide-in-from-top-2 duration-150 backdrop-blur-xl"
      :class="{
        'bg-[#0d0f22]/95 border-omnom-violet/40 text-violet-200 shadow-nom-violet': toast.type === 'success',
        'bg-[#0d0f22]/95 border-omnom-rose/40 text-rose-200': toast.type === 'error',
        'bg-[#0d0f22]/95 border-indigo-500/30 text-indigo-200': toast.type === 'info',
      }"
    >
      <div class="flex items-center gap-2">
        <CheckCircle2 v-if="toast.type === 'success'" class="w-4 h-4 text-omnom-violet shrink-0" />
        <AlertCircle v-else-if="toast.type === 'error'" class="w-4 h-4 text-omnom-rose shrink-0" />
        <Info v-else class="w-4 h-4 text-omnom-cyan shrink-0" />
        <span>{{ toast.message }}</span>
      </div>
      <button
        @click="toastStore.remove(toast.id)"
        class="text-slate-500 hover:text-white p-1 rounded transition-colors cursor-pointer"
      >
        ✕
      </button>
    </div>
  </div>

  <div class="min-h-screen bg-omnom-dark text-omnom-cream flex flex-col selection:bg-omnom-violet selection:text-white pb-16 md:pb-8 font-sans">
    <!-- Top Navigation Bar -->
    <header class="sticky top-0 z-30 bg-[#070811]/80 backdrop-blur-xl border-b border-indigo-500/15 px-4 py-2.5">
      <div class="max-w-3xl mx-auto flex items-center justify-between gap-3">
        <!-- Athletic Omnom Brand Logo -->
        <div class="flex items-center gap-2.5 group cursor-default">
          <div class="w-8 h-8 rounded-lg bg-gradient-to-br from-omnom-indigo to-omnom-violet flex items-center justify-center text-white shadow-nom-violet select-none transition-transform group-hover:scale-105">
            <Dumbbell class="w-4 h-4 stroke-[2.5]" />
          </div>
          <div class="flex items-center gap-2 leading-none">
            <span class="font-black text-lg tracking-tight text-white font-display">omnom</span>
            <span class="w-1.5 h-1.5 rounded-full bg-omnom-violet shadow-nom-violet animate-pulse"></span>
          </div>
        </div>

        <!-- Date Navigator in Center/Right -->
        <div class="flex-1 max-w-xs mx-2">
          <DateNavigator />
        </div>

        <!-- Header Actions -->
        <div class="flex items-center gap-1.5">
          <button
            @click="showSettingsModal = true"
            class="btn-bounce p-2 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.06] border border-transparent hover:border-indigo-500/30 transition-all cursor-pointer"
            title="Settings"
          >
            <Settings class="w-4 h-4" />
          </button>

        </div>
      </div>
    </header>

    <!-- Main Content Area -->
    <main class="flex-1 max-w-3xl w-full mx-auto px-4 py-6 space-y-6">
      <!-- 1. Daily Telemetry Cockpit -->
      <DailySummaryHeader />

      <!-- 2. Command-Dock Fuel Input -->
      <QuickLogInput
        :loading="isParsing || isSavingMeal"
        @submit="handlePromptSubmit"
      />

      <div v-if="isParsing" class="flex items-center justify-between text-sm text-slate-300">
        <span>Analyzing your meal… This may take up to 35 seconds.</span>
        <button class="underline p-2" @click="cancelParsing">Cancel</button>
      </div>
      <p class="text-xs text-slate-400">Your diary is saved in this browser. Use Settings to export a backup before switching devices.</p>
      <p v-if="diaryStore.error" role="alert" class="text-sm text-rose-300">{{ diaryStore.error }}</p>
      <p v-if="activeParsedResult?.aiSummary" role="status" class="text-sm text-amber-200">{{ activeParsedResult.aiSummary }}</p>

      <!-- Parse Error Notification -->
      <div
        v-if="parseError"
        class="flex items-start gap-3 p-3.5 rounded-xl bg-omnom-rose/10 border border-omnom-rose/30 text-rose-200 text-xs animate-in fade-in duration-200 font-mono"
      >
        <AlertCircle class="w-4 h-4 text-omnom-rose shrink-0 mt-0.5" />
        <div class="flex-1">
          <div class="font-bold mb-0.5 uppercase tracking-wider">Parsing Error</div>
          <div class="font-sans text-xs text-rose-300">{{ parseError }}</div>
        </div>

      </div>

      <!-- 3. Interactive Quick-Confirm Card -->
      <MealConfirmCard
        v-if="activeParsedResult"
        :parsed-result="activeParsedResult"
        :raw-prompt="currentRawPrompt"
        :is-saving="isSavingMeal"
        @confirm="handleConfirmMeal"
        @discard="handleDiscardMeal"
      />

      <!-- 4. Chronological Daily Timeline Feed -->
      <TimelineFeed />
    </main>

    <!-- Settings Modal -->
    <BrowserDataModal
      :show="showSettingsModal"
      @close="showSettingsModal = false"
    />
  </div>
</template>
