<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useDiaryStore } from './stores/diaryStore';
import { useToastStore } from './stores/toastStore';
import { AiApi, type AiParsedMealResult, type MealItem } from './services/api';
import DailySummaryHeader from './components/DailySummaryHeader.vue';
import QuickLogInput from './components/QuickLogInput.vue';
import MealConfirmCard from './components/MealConfirmCard.vue';
import TimelineFeed from './components/TimelineFeed.vue';
import DateNavigator from './components/DateNavigator.vue';
import BrowserDataModal from './components/BrowserDataModal.vue';
import { Settings2, AlertCircle, CheckCircle2, Info, X, ArrowUpRight, Loader2 } from 'lucide-vue-next';

const diaryStore = useDiaryStore();
const toastStore = useToastStore();
const showSettingsModal = ref(false);
const isParsing = ref(false);
const isSavingMeal = ref(false);
const parseError = ref<string | null>(null);
const activeParsedResult = ref<AiParsedMealResult | null>(null);
const currentRawPrompt = ref('');
const entryDate = ref('');
const pageDate = computed(() => new Date(diaryStore.selectedDate + 'T12:00:00').toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' }));
onMounted(() => { void diaryStore.fetchTimeline(); });
let parseController: AbortController | null = null;
const cancelParsing = () => { parseController?.abort(); };
onUnmounted(cancelParsing);

const handlePromptSubmit = async (prompt: string) => {
  if (isParsing.value || isSavingMeal.value) return;
  parseController = new AbortController();
  isParsing.value = true; parseError.value = null; activeParsedResult.value = null;
  currentRawPrompt.value = prompt;
  entryDate.value = diaryStore.selectedDate;
  try {
    activeParsedResult.value = await AiApi.parseMeal(prompt, undefined, parseController.signal);
  } catch (err: any) {
    if (err.code === 'ERR_CANCELED') { toastStore.info('Meal review canceled.'); return; }
    parseError.value = err.response?.data?.message || (err.response?.status === 403
      ? 'Browser verification is required. Reload this page and try again.'
      : err.code === 'ECONNABORTED' ? 'This is taking a little longer. Try a shorter description.' : err.message || 'Could not read this meal. Please try again.');
  } finally { isParsing.value = false; parseController = null; }
};
const handleConfirmMeal = async (data: { name: string; time?: string; items: MealItem[]; rawDescription: string }) => {
  isSavingMeal.value = true;
  try {
    const undo = await diaryStore.addMeal({ ...data, date: entryDate.value });
    toastStore.success(`${data.name} saved.`, undo);
    activeParsedResult.value = null; currentRawPrompt.value = '';
  } catch (err: any) { toastStore.error(err.response?.data?.message || err.message || 'Could not save meal.'); }
  finally { isSavingMeal.value = false; }
};
</script>

<template>
  <div class="toast-stack" aria-live="polite" aria-relevant="additions">
    <div v-for="toast in toastStore.toasts" :key="toast.id" class="toast" :class="'toast-' + toast.type"
      @mouseenter="toastStore.pause(toast.id)" @mouseleave="toastStore.resume(toast.id)"
      @focusin="toastStore.pause(toast.id)" @focusout="toastStore.resume(toast.id)">
      <CheckCircle2 v-if="toast.type === 'success'" class="icon" />
      <AlertCircle v-else-if="toast.type === 'error'" class="icon" />
      <Info v-else class="icon" />
      <span>{{ toast.message }}</span>
      <button v-if="toast.action" class="text-button" :disabled="toast.pending" @click="toastStore.runAction(toast.id)">{{ toast.pending ? 'Undoing…' : 'Undo' }}</button>
      <button class="icon-button" aria-label="Dismiss notification" :disabled="toast.pending" @click="toastStore.remove(toast.id)"><X class="icon" /></button>
    </div>
  </div>
  <header class="site-header">
    <div class="nav-inner">
      <a href="#" class="brand" aria-label="omnom AI home"><span class="brand-mark" aria-hidden="true">o</span>omnom <span class="ai-tag">AI</span></a>
      <span class="nav-caption">A little more in balance.</span>
      <button class="icon-button settings-button" @click="showSettingsModal = true" aria-label="Open diary settings"><Settings2 class="icon" /></button>
    </div>
  </header>
  <main class="app-main">
    <div class="page-heading">
      <div><p class="eyebrow">{{ pageDate }}</p><h1>Your daily balance<span>.</span></h1><p class="muted">Eat well. Move more. Make it yours.</p></div>
      <DateNavigator />
    </div>
    <div class="dashboard-layout">
      <aside class="summary-column"><DailySummaryHeader />
        <div class="quiet-note"><span class="tiny-dot"></span><div><strong>Your diary, your space.</strong><p>Saved on this device. Keep a copy in <button class="inline-link" @click="showSettingsModal = true">settings <ArrowUpRight class="inline-icon" /></button>.</p></div></div>
      </aside>
      <div class="diary-column">
        <QuickLogInput :loading="isParsing || isSavingMeal" @submit="handlePromptSubmit" />
        <div v-if="isParsing" class="status-note" role="status"><Loader2 class="icon animate-spin" /><span>Putting your meal together… up to 35 seconds.</span><button class="text-button" @click="cancelParsing">Cancel</button></div>
        <div v-if="parseError" class="review-note" role="alert"><AlertCircle class="icon" /><span>{{ parseError }}</span></div>
        <div v-if="activeParsedResult?.aiSummary" class="review-note" role="status">{{ activeParsedResult.aiSummary }}</div>
        <MealConfirmCard v-if="activeParsedResult" :parsed-result="activeParsedResult" :raw-prompt="currentRawPrompt" :is-saving="isSavingMeal" :entry-date="entryDate"
          @confirm="handleConfirmMeal" @discard="activeParsedResult = null" />
        <p v-if="diaryStore.error" class="review-note" role="alert">{{ diaryStore.error }} <button class="text-button" @click="diaryStore.fetchTimeline()">Try again</button></p>
        <TimelineFeed />
      </div>
    </div>
    <footer class="site-footer"><span>omnom AI</span><span>A little awareness goes a long way.</span></footer>
  </main>
  <BrowserDataModal :show="showSettingsModal" @close="showSettingsModal = false" />
</template>
