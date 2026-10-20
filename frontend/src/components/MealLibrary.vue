<script setup lang="ts">
import { ref } from 'vue';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import type { FavoriteMeal, MealEntry, RememberedFood } from '../services/api';
import { Copy, X, Star } from 'lucide-vue-next';
defineProps<{ busy?: boolean }>();
const emit = defineEmits<{ (e: 'repeat', meal: FavoriteMeal | MealEntry): void; (e: 'food', food: RememberedFood): void }>();
const diary = useDiaryStore(), toast = useToastStore();
const tab = ref<'favorites' | 'recent' | 'foods'>('favorites');
const removing = ref(false);
const remove = async (id: number, food = false) => {
  removing.value = true;
  try { if (food) await diary.forgetFood(id); else await diary.deleteFavorite(id); toast.success(food ? 'Food default forgotten.' : 'Favorite removed.'); }
  catch (error: any) { toast.error(error.message || 'Could not update your library.'); }
  finally { removing.value = false; }
};
const calories = (meal: FavoriteMeal | MealEntry) => Math.round(meal.items.reduce((sum, item) => sum + item.calories, 0));
</script>
<template>
  <section class="card meal-library">
    <div class="section-heading"><h2>Your shortcuts</h2><Star class="icon" /></div>
    <div class="library-tabs" role="group" aria-label="Shortcut category"><button v-for="choice in (['favorites', 'recent', 'foods'] as const)" :key="choice" :aria-pressed="tab === choice" :class="{ selected: tab === choice }" @click="tab = choice">{{ choice === 'foods' ? 'My foods' : choice === 'recent' ? 'Recent meals' : 'Favorites' }}</button></div>
    <div v-if="tab === 'favorites'" class="library-list"><div v-for="meal in diary.favorites" :key="meal.id" class="library-row"><button class="library-pick" :disabled="busy || removing || diary.loading" @click="emit('repeat', meal)"><span>{{ meal.name }}<small>{{ calories(meal) }} kcal · {{ meal.items.length }} foods</small></span><Copy class="icon" /></button><button class="icon-button" :disabled="removing || diary.loading" :aria-label="'Remove favorite ' + meal.name" @click="remove(meal.id)"><X class="icon" /></button></div><p v-if="!diary.favorites.length" class="muted small">Star a saved meal to keep it here. Repeating opens a review for the selected date.</p></div>
    <div v-if="tab === 'recent'" class="library-list"><div v-for="meal in diary.recentMeals" :key="meal.id" class="library-row"><button class="library-pick" :disabled="busy || removing || diary.loading" @click="emit('repeat', meal)"><span>{{ meal.name }}<small>{{ new Date(meal.date + 'T12:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) }} · {{ calories(meal) }} kcal</small></span><Copy class="icon" /></button></div><p v-if="!diary.recentMeals.length" class="muted small">Your recent meals will appear here after saving.</p></div>
    <div v-if="tab === 'foods'" class="library-list"><div v-for="food in diary.rememberedFoods" :key="food.id" class="library-row"><button class="library-pick" :disabled="busy || removing || diary.loading" @click="emit('food', food)"><span>{{ food.alias }}<small>{{ food.item.grams }}g · {{ Math.round(food.item.calories) }} kcal{{ food.usePortion ? ' · usual portion' : ' · nutrition only' }}</small></span><Copy class="icon" /></button><button class="icon-button" :disabled="removing || diary.loading" :aria-label="'Forget ' + food.alias" @click="remove(food.id, true)"><X class="icon" /></button></div><p v-if="!diary.rememberedFoods.length" class="muted small">Open a food’s details in review to remember a checked portion, brand, and nutrition values.</p><p v-else class="muted small">An exact name match uses your checked nutrition. Amounts you type take priority over the usual portion.</p></div>
  </section>
</template>
