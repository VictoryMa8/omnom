<script setup lang="ts">
import { ref, computed } from 'vue';
import type { AiParsedMealResult, MealItem, ClarificationOption, AiClarificationChip } from '../services/api';
import { nutritionWarnings } from '../utils/nutritionReview';
import { Check, Trash2, Plus, Minus, Loader2, Pencil, AlertCircle, ChevronDown } from 'lucide-vue-next';
const props = defineProps<{ parsedResult: AiParsedMealResult; rawPrompt: string; isSaving?: boolean; entryDate?: string }>();
const emit = defineEmits<{
  (e: 'confirm', data: { name: string; time?: string; items: MealItem[]; rawDescription: string }): void;
  (e: 'discard'): void;
}>();
const mealName = ref(props.parsedResult.suggestedMealName || 'Meal');
const mealTime = ref('');
const editableItems = ref<MealItem[]>(JSON.parse(JSON.stringify(props.parsedResult.items)));
const activeChips = ref<AiClarificationChip[]>(JSON.parse(JSON.stringify(props.parsedResult.clarificationChips || [])));
const openIndex = ref<number | null>(null);
const changedChips = ref(new Set<string>());
const renamedItems = ref(new Set<MealItem>());
const nutrientFields = ['calories', 'protein', 'carbs', 'fat', 'fiber'] as const;
const totalCal = computed(() => Math.round(editableItems.value.reduce((s, i) => s + (i.calories || 0), 0)));
const warnings = computed(() => editableItems.value.map(nutritionWarnings));
const warningCount = computed(() => warnings.value.filter(w => w.length).length);
const valid = computed(() => editableItems.value.length > 0 && editableItems.value.every(i => i.foodName.trim() && i.grams > 0
  && [i.quantity, i.grams, ...nutrientFields.map(f => i[f])].every(n => Number.isFinite(n) && n >= 0)));
const itemChips = (idx: number) => activeChips.value.filter(c => c.targetItemIndex === idx);
const assumptions = (item: MealItem) => item.assumptions ?? (item.usdaMatchStatus === 'Estimated' ? ['Estimated nutrition'] : []);
const selectChipOption = (chip: AiClarificationChip, option: ClarificationOption) => {
  chip.selectedOptionId = option.id; changedChips.value.add(chip.id);
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
  if (chip.id === 'chip-cooking-oil') {
    editableItems.value = editableItems.value.filter(i => !i.selectedClarification?.startsWith('Added Oil:'));
    if (option.isAdditiveItem && option.additiveItemName) editableItems.value.push({
      foodName: option.additiveItemName, quantity: 1, unit: 'tbsp', grams: option.additiveGrams ?? 14,
      calories: option.additiveCalories ?? 124, protein: option.additiveProtein ?? 0, carbs: option.additiveCarbs ?? 0,
      fat: option.additiveFat ?? 14, fiber: 0, selectedClarification: `Added Oil: ${option.label}`, usdaMatchStatus: 'VerifiedStaple',
    });
  }
};
const updateItemGrams = (item: MealItem, grams: number) => {
  if (!Number.isFinite(grams) || grams <= 0) return;
  const ratio = grams / (item.grams || 100);
  item.grams = grams; item.quantity *= ratio;
  for (const field of nutrientFields) item[field] = Math.round(item[field] * ratio * 10) / 10;
  item.assumptions = assumptions(item).filter(a => a !== 'Estimated portion');
};
const changeGrams = (item: MealItem, event: Event) => {
  const input = event.target as HTMLInputElement;
  updateItemGrams(item, Number(input.value));
  input.value = String(item.grams);
};
const removeItem = (index: number) => {
  editableItems.value.splice(index, 1);
  activeChips.value = activeChips.value.filter(c => c.targetItemIndex !== index);
  for (const chip of activeChips.value) if (chip.targetItemIndex != null && chip.targetItemIndex > index) chip.targetItemIndex--;
  openIndex.value = null;
};
const addItem = () => {
  editableItems.value.push({ foodName: 'New food', quantity: 1, unit: 'serving', grams: 100, calories: 0,
    protein: 0, carbs: 0, fat: 0, fiber: 0, usdaMatchStatus: 'Estimated', assumptions: ['Enter nutrition from the label'] });
  openIndex.value = editableItems.value.length - 1;
};
const renameItem = (item: MealItem) => {
  renamedItems.value.add(item);
  item.usdaMatchStatus = 'Estimated';
  item.usdaFdcId = undefined;
  item.assumptions = assumptions(item).filter(a => a !== 'Cheddar assumed');
};
const submitConfirm = () => {
  if (!valid.value || props.isSaving) return;
  emit('confirm', { name: mealName.value.trim() || 'Meal', time: mealTime.value || undefined, items: editableItems.value, rawDescription: props.rawPrompt });
};
</script>
<template>
  <section class="card meal-review">
    <div class="section-heading"><h2>Review</h2></div>
    <p class="muted review-description">“{{ rawPrompt }}”</p>
    <fieldset :disabled="isSaving" class="review-fieldset">
      <div class="meal-meta"><label>Meal name<input v-model="mealName" aria-label="Meal name" /></label><label>Time <span class="muted">(optional)</span><input v-model="mealTime" type="time" aria-label="Optional meal time" /></label></div>
      <p v-if="entryDate" class="muted small">Saving to {{ new Date(entryDate + 'T12:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) }}</p>
      <div class="review-items">
        <article v-for="(item, idx) in editableItems" :key="idx" class="review-item">
          <div class="item-heading"><div><h3>{{ item.foodName }}</h3><p class="muted small">{{ item.grams }} g · {{ Math.round(item.calories) }} kcal <span class="source-label">{{ item.usdaMatchStatus === 'VerifiedStaple' ? 'Reference food' : item.usdaMatchStatus === 'UsdaApiMatch' ? 'USDA match' : item.usdaMatchStatus === 'Unparsed' ? 'Could not read' : 'Estimate / edited' }}</span></p></div>
            <button class="icon-button" :aria-label="'Remove ' + item.foodName" @click="removeItem(idx)"><Trash2 class="icon" /></button></div>
          <div class="assumptions">
            <button v-for="assumption in assumptions(item)" :key="assumption" class="assumption-pill" @click="openIndex = idx"><Pencil class="small-icon" />{{ assumption }}</button>
            <button v-for="chip in itemChips(idx)" :key="chip.id" class="assumption-pill" @click="openIndex = idx">{{ changedChips.has(chip.id) ? 'Selected: ' : 'Assumed: ' }}{{ chip.options.find(o => o.id === chip.selectedOptionId)?.label.replace(' (Default)', '') }}<ChevronDown class="small-icon" /></button>
          </div>
          <div v-if="warnings[idx]?.length || renamedItems.has(item)" class="item-warning" role="status"><AlertCircle class="icon" /><div><p v-if="renamedItems.has(item)">Name changed. Nutrition stays the same until you edit the values below.</p><p v-for="warning in warnings[idx]" :key="warning">{{ warning }}</p><button class="inline-link" @click="openIndex = idx">Review values</button></div></div>
          <div class="item-bottom"><span class="muted small">{{ item.protein }}g protein · {{ item.carbs }}g carbs · {{ item.fat }}g fat</span><button class="text-button" :aria-expanded="openIndex === idx" @click="openIndex = openIndex === idx ? null : idx">{{ openIndex === idx ? 'Done' : 'Edit details' }}<Pencil class="small-icon" /></button></div>
          <div v-if="openIndex === idx" class="item-editor">
            <label>Food name<input v-model="item.foodName" @change="renameItem(item)" :aria-label="'Food name ' + (idx + 1)" /></label>
            <div class="portion-controls"><label>Portion (g)<input type="number" min="0.1" step="any" :value="item.grams" @change="changeGrams(item, $event)" :aria-label="'Portion for ' + item.foodName" /></label><button class="secondary-button" aria-label="Reduce portion by 10 grams" @click="updateItemGrams(item, Math.max(1, item.grams - 10))"><Minus class="small-icon" />10g</button><button class="secondary-button" aria-label="Increase portion by 10 grams" @click="updateItemGrams(item, item.grams + 10)"><Plus class="small-icon" />10g</button></div>
            <div v-for="chip in itemChips(idx)" :key="chip.id" class="clarification"><span class="small">{{ chip.label }}</span><div class="option-row"><button v-for="option in chip.options" :key="option.id" class="option-button" :class="{ selected: chip.selectedOptionId === option.id }" :aria-pressed="chip.selectedOptionId === option.id" @click="selectChipOption(chip, option)">{{ option.label }}</button></div></div>
            <p class="muted small">Nutrition for this entire portion. Changing the weight scales these values.</p>
            <div class="nutrient-inputs"><label v-for="field in nutrientFields" :key="field">{{ field === 'calories' ? 'Calories' : field }} {{ field === 'calories' ? '(kcal)' : '(g)' }}<input v-model.number="item[field]" type="number" min="0" step="any" :aria-label="field + ' for ' + item.foodName" @change="item.usdaMatchStatus = 'Estimated'; item.usdaFdcId = undefined" /></label></div>
          </div>
        </article>
      </div>
      <div v-for="chip in activeChips.filter(c => c.targetItemIndex == null)" :key="chip.id" class="clarification meal-clarification"><p class="small">{{ chip.label }} <span class="muted">· {{ changedChips.has(chip.id) ? 'your choice' : 'assumed unless you change it' }}</span></p><div class="option-row"><button v-for="option in chip.options" :key="option.id" class="option-button" :class="{ selected: chip.selectedOptionId === option.id }" :aria-pressed="chip.selectedOptionId === option.id" @click="selectChipOption(chip, option)">{{ option.label }}</button></div></div>
      <button class="text-button add-food" @click="addItem"><Plus class="icon" />Add a food manually</button>
      <p v-if="warningCount" class="review-note">{{ warningCount }} {{ warningCount === 1 ? 'food has' : 'foods have' }} values worth a second look. You can still save this meal.</p>
      <p v-if="!valid" class="small muted">Add a food with a positive weight and valid nutrition values to save.</p>
      <div class="review-footer"><div><span class="eyebrow">Meal total</span><strong>{{ totalCal.toLocaleString() }} <small>kcal</small></strong></div><div class="button-row"><button class="text-button" @click="emit('discard')">Discard</button><button class="primary-button" :disabled="!valid || isSaving" @click="submitConfirm"><Loader2 v-if="isSaving" class="icon animate-spin" /><Check v-else class="icon" />{{ isSaving ? 'Saving…' : 'Save meal' }}</button></div></div>
    </fieldset>
  </section>
</template>
