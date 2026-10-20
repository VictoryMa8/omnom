<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { FoodsApi, type FoodReference } from '../services/api';
import { Search, Loader2 } from 'lucide-vue-next';
const props = defineProps<{ initialQuery: string; index: number }>();
const emit = defineEmits<{ (e: 'select', food: FoodReference): void }>();
const query = ref(props.initialQuery.slice(0, 120));
const foods = ref<FoodReference[]>([]), loading = ref(false), error = ref('');
let controller: AbortController | null = null;
const search = async () => {
  controller?.abort(); const request = new AbortController(); controller = request;
  loading.value = true; error.value = '';
  try { const results = await FoodsApi.search(query.value.trim(), request.signal); if (controller === request) foods.value = results; }
  catch (err: any) { if (controller === request && err.code !== 'ERR_CANCELED') error.value = err.response?.data?.message || err.message || 'Could not search foods.'; }
  finally { if (controller === request) loading.value = false; }
};
onMounted(search); onUnmounted(() => controller?.abort());
</script>
<template>
  <div class="food-search">
    <form @submit.prevent="search"><label :for="'food-search-' + index">Replace with a reference food</label>
      <div class="food-search-input"><input :id="'food-search-' + index" v-model="query" maxlength="120" placeholder="Chicken thigh, brown rice…" /><button type="submit" class="secondary-button" :disabled="loading"><Loader2 v-if="loading" class="icon animate-spin" /><Search v-else class="icon" /><span class="sr-only">Search reference foods</span></button></div>
    </form>
    <p v-if="error" class="small review-note" role="alert">{{ error }}</p>
    <p v-else-if="loading" class="muted small" role="status">Searching reference foods…</p>
    <div v-else class="food-results">
      <button v-for="food in foods" :key="food.id ?? food.fdcId ?? food.name" type="button" class="food-result" @click="emit('select', food)"><span>{{ food.name }}<small>{{ food.isStaple ? 'Reference food' : 'USDA match' }} · {{ food.proteinPer100g }}g protein / 100g</small></span><strong>{{ food.caloriesPer100g }} <small>kcal / 100g</small></strong></button>
      <p v-if="!foods.length" class="muted small">No reference found. Enter the label values below and remember this food for next time.</p>
    </div>
    <p class="muted small">Selecting a food replaces all nutrition values at the current weight.</p>
  </div>
</template>
