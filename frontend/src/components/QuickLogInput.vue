<script setup lang="ts">
import { ref, onUnmounted } from 'vue';
import { Mic, MicOff, ArrowUpRight, Loader2, Plus } from 'lucide-vue-next';
import { useToastStore } from '../stores/toastStore';
const props = defineProps<{ loading: boolean }>();
const emit = defineEmits<{ (e: 'submit', prompt: string): void }>();
const toast = useToastStore();
const inputPrompt = ref('');
const isListening = ref(false);
const shortcuts = [
  { label: 'Eggs', text: '2 large whole eggs' }, { label: 'Chicken', text: '150g cooked chicken breast' },
  { label: 'Greek yogurt', text: '200g nonfat Greek yogurt' }, { label: 'Rice', text: '150g cooked jasmine rice' },
];
let recognition: any = null;
onUnmounted(() => recognition?.abort());
const toggleVoice = () => {
  if (isListening.value) { recognition?.stop(); return; }
  const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
  if (!SpeechRecognition) { toast.info('Voice input is not available in this browser. You can type your meal below.'); return; }
  try {
    recognition = new SpeechRecognition(); recognition.lang = 'en-US';
    recognition.onstart = () => { isListening.value = true; };
    recognition.onend = () => { isListening.value = false; };
    recognition.onerror = () => { isListening.value = false; toast.info('Could not hear you. Check microphone access or type your meal.'); };
    recognition.onresult = (event: any) => {
      inputPrompt.value = [inputPrompt.value, event.results[0][0].transcript].filter(Boolean).join(' ');
    };
    recognition.start();
  } catch { isListening.value = false; toast.error('Could not start the microphone.'); }
};
const submit = () => {
  if (!inputPrompt.value.trim() || props.loading) return;
  recognition?.stop(); emit('submit', inputPrompt.value.trim());
};
const keydown = (event: KeyboardEvent) => {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); submit(); }
};
</script>
<template>
  <section class="composer card">
    <div class="section-heading"><div><p class="eyebrow">A moment for your meal</p><h2>What’s on your plate?</h2></div><span class="ai-badge">AI assisted</span></div>
    <p class="muted composer-intro">Just describe it. We’ll help with the numbers.</p>
    <label class="sr-only" for="meal-description">Describe your meal</label>
    <textarea id="meal-description" v-model="inputPrompt" :disabled="loading" maxlength="4000" rows="3" @keydown="keydown" placeholder="Two eggs on sourdough, a little butter, and a coffee…"></textarea>
    <div class="composer-actions"><button class="secondary-button" :disabled="loading" @click="toggleVoice"><component :is="isListening ? MicOff : Mic" class="icon" />{{ isListening ? 'Listening…' : 'Use your voice' }}</button>
      <button class="primary-button" :disabled="loading || !inputPrompt.trim()" @click="submit"><Loader2 v-if="loading" class="icon animate-spin" />{{ loading ? 'Working on it…' : 'Review meal' }}<ArrowUpRight v-if="!loading" class="icon" /></button></div>
    <div class="shortcuts"><span>Quick add</span><button v-for="shortcut in shortcuts" :key="shortcut.label" :disabled="loading" @click="inputPrompt = [inputPrompt.trim(), shortcut.text].filter(Boolean).join(', ')"><Plus class="small-icon" />{{ shortcut.label }}</button></div>
  </section>
</template>
