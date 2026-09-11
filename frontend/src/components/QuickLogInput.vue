<script setup lang="ts">
import { ref } from 'vue';
import { Mic, MicOff, CornerDownLeft, Loader2, Plus, X } from 'lucide-vue-next';

const props = defineProps<{
  loading: boolean;
}>();

const emit = defineEmits<{
  (e: 'submit', prompt: string): void;
}>();

const inputPrompt = ref('');
const isListening = ref(false);

// Lifter staple shortcuts (1-tap quick add)
const stapleShortcuts = [
  { label: '+ 30g Whey', text: '30g whey protein isolate', tag: '24g P' },
  { label: '+ 4 Whole Eggs', text: '4 large whole eggs', tag: '24g P' },
  { label: '+ 200g Chicken', text: '200g cooked chicken breast', tag: '62g P' },
  { label: '+ 150g Rice', text: '150g cooked jasmine rice', tag: '42g C' },
  { label: '+ 200g Greek Yogurt', text: '200g nonfat Greek yogurt', tag: '20g P' },
  { label: '+ 1 Banana', text: '1 medium banana', tag: '27g C' },
];

const appendStaple = (text: string) => {
  if (!inputPrompt.value.trim()) {
    inputPrompt.value = text;
  } else {
    inputPrompt.value = `${inputPrompt.value.trim()}, ${text}`;
  }
};

const clearInput = () => {
  inputPrompt.value = '';
};

// Web Speech API
let recognition: any = null;

const toggleSpeechRecognition = () => {
  const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
  if (!SpeechRecognition) {
    alert('Speech recognition is not supported in this browser.');
    return;
  }

  if (isListening.value) {
    if (recognition) recognition.stop();
    isListening.value = false;
    return;
  }

  try {
    recognition = new SpeechRecognition();
    recognition.continuous = false;
    recognition.interimResults = false;
    recognition.lang = 'en-US';

    recognition.onstart = () => {
      isListening.value = true;
    };

    recognition.onresult = (event: any) => {
      const transcript = event.results[0][0].transcript;
      inputPrompt.value = inputPrompt.value ? `${inputPrompt.value} ${transcript}` : transcript;
      isListening.value = false;
    };

    recognition.onerror = () => {
      isListening.value = false;
    };

    recognition.onend = () => {
      isListening.value = false;
    };

    recognition.start();
  } catch (err) {
    console.error('Speech recognition error:', err);
    isListening.value = false;
  }
};

const handleSubmit = () => {
  const text = inputPrompt.value.trim();
  if (!text || props.loading) return;
  emit('submit', text);
};

const onKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    handleSubmit();
  }
};
</script>

<template>
  <div class="mb-6 space-y-2.5">
    <!-- Quick Staples Tray -->
    <div class="flex items-center gap-1.5 overflow-x-auto py-1 px-0.5 no-scrollbar">
      <button
        v-for="staple in stapleShortcuts"
        :key="staple.label"
        type="button"
        @click="appendStaple(staple.text)"
        class="btn-bounce shrink-0 inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-black/30 hover:bg-white/[0.06] border border-indigo-500/15 hover:border-omnom-violet/50 text-slate-300 hover:text-white transition-all text-xs cursor-pointer group font-mono"
      >
        <Plus class="w-3 h-3 text-omnom-violet group-hover:text-purple-300" />
        <span class="font-medium text-[11px] font-sans">{{ staple.label }}</span>
        <span class="text-[9px] text-slate-500 font-bold group-hover:text-purple-300">
          {{ staple.tag }}
        </span>
      </button>
    </div>

    <!-- Main Command-Dock Input Card -->
    <div
      class="relative glass-card border border-indigo-500/20 focus-within:border-omnom-violet/80 rounded-xl p-3.5 sm:p-4 shadow-omnom transition-all duration-200 focus-within:ring-2 focus-within:ring-omnom-violet/25 group"
    >
      <div class="relative">
        <textarea
          v-model="inputPrompt"
          @keydown="onKeydown"
          rows="2"
          placeholder="8oz flank steak, 200g jasmine rice, 1 tbsp olive oil..."
          class="w-full bg-transparent text-white placeholder-slate-500 text-sm md:text-base resize-none focus:outline-none py-1 font-sans leading-relaxed pr-6"
        ></textarea>

        <button
          v-if="inputPrompt"
          @click="clearInput"
          type="button"
          class="absolute right-0 top-1 text-slate-500 hover:text-slate-300 p-1 rounded-md cursor-pointer"
          title="Clear"
        >
          <X class="w-3.5 h-3.5" />
        </button>
      </div>

      <!-- Bottom Control Toolbar -->
      <div class="flex items-center justify-between pt-2.5 border-t border-indigo-500/15 mt-1">
        <!-- Voice Dictation -->
        <button
          type="button"
          @click="toggleSpeechRecognition"
          class="btn-bounce px-2.5 py-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.06] border border-transparent hover:border-indigo-500/20 transition-all flex items-center gap-1.5 text-xs font-mono font-medium cursor-pointer"
          :class="{ 'text-omnom-rose animate-pulse bg-omnom-rose/10 border-omnom-rose/30': isListening }"
          title="Voice Dictate"
        >
          <component :is="isListening ? MicOff : Mic" class="w-3.5 h-3.5 text-omnom-violet" />
          <span>{{ isListening ? 'Listening...' : 'Voice' }}</span>
        </button>

        <div class="flex items-center gap-2">
          <span class="keycap hidden sm:inline-flex">↵</span>

          <!-- Analyze / Parse Button -->
          <button
            type="button"
            @click="handleSubmit"
            :disabled="!inputPrompt.trim() || loading"
            class="btn-bounce px-4 py-1.5 rounded-lg bg-gradient-to-r from-omnom-indigo to-omnom-violet hover:from-indigo-500 hover:to-purple-500 disabled:opacity-40 disabled:cursor-not-allowed text-white font-bold text-xs flex items-center gap-1.5 shadow-nom-violet cursor-pointer transition-all font-mono"
          >
            <Loader2 v-if="loading" class="w-3.5 h-3.5 animate-spin" />
            <span>{{ loading ? 'Parsing...' : 'Log Fuel' }}</span>
            <CornerDownLeft v-if="!loading" class="w-3 h-3 stroke-[2.5]" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
