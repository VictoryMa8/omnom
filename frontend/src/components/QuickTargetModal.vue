<script setup lang="ts">
import { ref, watch, toRef } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import { useModal } from '../utils/useModal';
import { X } from 'lucide-vue-next';
const props = defineProps<{ show: boolean }>();
const emit = defineEmits<{ (e: 'close'): void }>();
const diary = useDiaryStore(), toast = useToastStore();
const dialog = ref<HTMLElement | null>(null);
const saving = ref(false);
const close = () => { if (!saving.value) emit('close'); };
useModal(toRef(props, 'show'), dialog, close);
const form = ref({ name: 'Maintenance', targetCalories: 2500, targetProtein: 180, targetCarbs: 275, targetFat: 75 });
const date = ref('');
watch(() => props.show, value => {
  if (value) {
    date.value = diary.selectedDate;
    const target = diary.timeline?.target;
    if (target) form.value = { name: target.name, targetCalories: target.targetCalories, targetProtein: target.targetProtein, targetCarbs: target.targetCarbs, targetFat: target.targetFat };
  }
});
const presets = [
  { name: 'Lean bulk', targetCalories: 2800, targetProtein: 200, targetCarbs: 340, targetFat: 70 },
  { name: 'Cut', targetCalories: 2000, targetProtein: 190, targetCarbs: 180, targetFat: 55 },
  { name: 'Maintenance', targetCalories: 2500, targetProtein: 180, targetCarbs: 275, targetFat: 75 },
  { name: 'Refeed', targetCalories: 3100, targetProtein: 175, targetCarbs: 450, targetFat: 60 },
];
const fields = [
  { key: 'targetCalories', label: 'Calories (kcal)' }, { key: 'targetProtein', label: 'Protein (g)' },
  { key: 'targetCarbs', label: 'Carbs (g)' }, { key: 'targetFat', label: 'Fat (g)' },
] as const;
const save = async () => {
  if (saving.value) return;
  if (fields.some(f => !Number.isFinite(form.value[f.key]) || form.value[f.key] < 0)) { toast.error('Enter a valid, non-negative number for each target.'); return; }
  saving.value = true;
  try { await diary.updateTarget({ ...form.value, effectiveDate: date.value }); toast.success('Your daily targets are updated.'); emit('close'); }
  catch (error: any) { toast.error(error.message || 'Could not save targets.'); }
  finally { saving.value = false; }
};
</script>
<template>
  <div v-if="show" class="modal-backdrop" @click.self="close">
    <section ref="dialog" class="modal" role="dialog" aria-modal="true" aria-labelledby="target-title">
      <div class="section-heading"><h2 id="target-title">Your daily goals</h2><button class="icon-button" aria-label="Close daily goals" :disabled="saving" @click="close"><X class="icon" /></button></div>
      <p class="muted">Choose a starting point, then adjust it to suit you. Applies from {{ date }}.</p>
      <fieldset :disabled="saving" class="review-fieldset">
        <div class="target-grid"><button v-for="preset in presets" :key="preset.name" class="preset-button" :class="{ selected: form.name === preset.name }" @click="form = { ...preset }">{{ preset.name }}<small>{{ preset.targetCalories.toLocaleString() }} kcal</small></button></div>
        <label class="block mt-5">Goal name<input v-model="form.name" aria-label="Goal name" /></label>
        <div class="target-grid"><label v-for="field in fields" :key="field.key">{{ field.label }}<input v-model.number="form[field.key]" type="number" min="0" step="any" :aria-label="field.label" /></label></div>
        <button class="text-button mt-2" @click="form.targetCalories = Math.round(form.targetProtein * 4 + form.targetCarbs * 4 + form.targetFat * 9)">Calculate calories from macros</button>
        <div class="button-row"><button class="text-button" @click="close">Cancel</button><button class="primary-button" @click="save">{{ saving ? 'Saving…' : 'Save goals' }}</button></div>
      </fieldset>
    </section>
  </div>
</template>
