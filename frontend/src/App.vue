<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue';
import { version } from '../package.json';
import { useDiaryStore } from './stores/diaryStore';
import { useToastStore } from './stores/toastStore';
import { AiApi, type AiParsedMealResult, type MealItem, type MealEntry, type FavoriteMeal, type RememberedFood } from './services/api';
import { applyFoodDefaults } from './utils/foodDefaults';
import DailySummaryHeader from './components/DailySummaryHeader.vue';
import QuickLogInput from './components/QuickLogInput.vue';
import MealConfirmCard from './components/MealConfirmCard.vue';
import TimelineFeed from './components/TimelineFeed.vue';
import DateNavigator from './components/DateNavigator.vue';
import BrowserDataModal from './components/BrowserDataModal.vue';
import MealLibrary from './components/MealLibrary.vue';
import WeeklyProgress from './components/WeeklyProgress.vue';
import { Settings2, AlertCircle, CheckCircle2, Info, X, ArrowUpRight, Loader2 } from 'lucide-vue-next';

const diaryStore = useDiaryStore();
const releaseVersion = `beta ${version.replace(/-beta$/, '')}`;
const toastStore = useToastStore();
const showSettingsModal = ref(false);
const isParsing = ref(false);
const isSavingMeal = ref(false);
const parseError = ref<string | null>(null);
const activeParsedResult = ref<AiParsedMealResult | null>(null);
const parseMode = ref<'ai' | 'exact'>('ai');
const currentRawPrompt = ref('');
const entryDate = ref('');
const editingMealId = ref<number | null>(null);
const initialTime = ref(''), reviewSession = ref(0), remembering = ref(false);
const reviewBusy = computed(() => isParsing.value || isSavingMeal.value || remembering.value || activeParsedResult.value != null);
const pageDate = computed(() => new Date(diaryStore.selectedDate + 'T12:00:00').toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' }));
onMounted(() => { void diaryStore.fetchTimeline(); });
let parseController: AbortController | null = null;
const cancelParsing = () => { parseController?.abort(); };
onUnmounted(cancelParsing);

const handlePromptSubmit = async (prompt: string, mode: 'ai' | 'exact' = 'ai') => {
  if (reviewBusy.value) return;
  parseController = new AbortController();
  parseMode.value = mode;
  isParsing.value = true; parseError.value = null; activeParsedResult.value = null;
  currentRawPrompt.value = prompt;
  entryDate.value = diaryStore.selectedDate;
  editingMealId.value = null; initialTime.value = ''; reviewSession.value++;
  try {
    const result = await AiApi.parseMeal(prompt, undefined, parseController.signal, mode);
    activeParsedResult.value = applyFoodDefaults(result, diaryStore.rememberedFoods);
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
    const editing = editingMealId.value != null;
    const undo = editing ? await diaryStore.editMeal(editingMealId.value!, data) : await diaryStore.addMeal({ ...data, date: entryDate.value });
    toastStore.success(`${data.name} ${editing ? 'updated' : 'saved'}.`, undo);
    activeParsedResult.value = null; currentRawPrompt.value = '';
    editingMealId.value = null;
  } catch (err: any) { toastStore.error(err.response?.data?.message || err.message || 'Could not save meal.'); }
  finally { isSavingMeal.value = false; }
};
const openMeal = async (meal: FavoriteMeal | MealEntry, editing = false) => {
  if (reviewBusy.value) return;
  editingMealId.value = editing ? meal.id : null;
  entryDate.value = editing && 'date' in meal ? meal.date : diaryStore.selectedDate;
  initialTime.value = editing && 'time' in meal ? meal.time || '' : '';
  currentRawPrompt.value = meal.rawDescription || meal.name;
  parseError.value = null; reviewSession.value++;
  activeParsedResult.value = { suggestedMealName: meal.name, items: JSON.parse(JSON.stringify(meal.items)), clarificationChips: [] };
  await nextTick(); document.querySelector('.meal-review')?.scrollIntoView({ block: 'start', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
};
const openFood = (food: RememberedFood) => openMeal({ id: food.id, name: 'Meal', items: [food.item], rawDescription: food.alias });
const rememberFood = async (data: { alias: string; item: MealItem; usePortion: boolean }) => {
  remembering.value = true;
  try { await diaryStore.rememberFood(data.alias, data.item, data.usePortion); toastStore.success(`${data.alias} remembered. You can remove it in My foods.`); }
  catch (error: any) { toastStore.error(error.message || 'Could not remember this food.'); }
  finally { remembering.value = false; }
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
      <a href="#" class="brand" aria-label="omnom AI home"><img class="brand-mark" src="/favicon.svg" alt="" />omnom <span class="ai-tag">AI</span></a>
      <button class="icon-button settings-button" @click="showSettingsModal = true" aria-label="Open diary settings"><Settings2 class="icon" /></button>
    </div>
  </header>
  <main class="app-main">
    <div class="page-heading">
      <h1>{{ pageDate }}</h1>
      <DateNavigator />
    </div>
    <div class="dashboard-layout">
      <aside class="summary-column"><DailySummaryHeader />
        <div class="quiet-note"><span class="tiny-dot"></span><p>Saved on this device. <button class="inline-link" @click="showSettingsModal = true">Back up in settings <ArrowUpRight class="inline-icon" /></button></p></div>
        <WeeklyProgress />
      </aside>
      <div class="diary-column">
        <QuickLogInput :loading="isParsing || isSavingMeal || remembering" :disabled="activeParsedResult != null" @submit="handlePromptSubmit" />
        <div v-if="isParsing" class="status-note" role="status"><Loader2 class="icon animate-spin" /><span>{{ parseMode === 'exact' ? 'Reading your meal…' : 'Putting your meal together… up to 50 seconds.' }}</span><button class="text-button" @click="cancelParsing">Cancel</button></div>
        <div v-if="parseError" class="review-note" role="alert"><AlertCircle class="icon" /><span>{{ parseError }}</span></div>
        <div v-if="activeParsedResult?.aiSummary" class="review-note" role="status">{{ activeParsedResult.aiSummary }}</div>
        <MealConfirmCard v-if="activeParsedResult" :key="reviewSession" :parsed-result="activeParsedResult" :raw-prompt="currentRawPrompt" :is-saving="isSavingMeal || remembering" :entry-date="entryDate" :initial-time="initialTime" :is-editing="editingMealId != null"
          @confirm="handleConfirmMeal" @remember="rememberFood" @discard="activeParsedResult = null; editingMealId = null" />
        <p v-if="diaryStore.error" class="review-note" role="alert">{{ diaryStore.error }} <button class="text-button" @click="diaryStore.fetchTimeline()">Try again</button></p>
        <MealLibrary :busy="reviewBusy" @repeat="openMeal($event)" @food="openFood" />
        <TimelineFeed :busy="reviewBusy" @edit="openMeal($event, true)" @repeat="openMeal($event)" />
      </div>
    </div>
    <footer class="site-footer"><span>omnom AI</span><span>{{ releaseVersion }}</span></footer>
  </main>
  <BrowserDataModal :show="showSettingsModal" @close="showSettingsModal = false" />
</template>
