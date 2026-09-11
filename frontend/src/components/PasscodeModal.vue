<script setup lang="ts">
import { ref } from 'vue';
import { useAuthStore } from '../stores/authStore';
import { ArrowRight, AlertCircle, Lock } from 'lucide-vue-next';

const authStore = useAuthStore();
const passcode = ref('');
const error = ref<string | null>(null);
const loading = ref(false);

const handleUnlock = async () => {
  if (!passcode.value.trim()) return;
  loading.value = true;
  error.value = null;

  try {
    const success = await authStore.loginWithPasscode(passcode.value);
    if (!success) {
      error.value = 'Incorrect passcode. Please try again.';
    }
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Verification failed';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/90 backdrop-blur-md font-sans">
    <div class="glass-card border border-white/[0.12] rounded-xl w-full max-w-sm p-6 shadow-2xl text-center">
      <div class="w-12 h-12 rounded-xl bg-omnom-violet/10 border border-omnom-violet/20 flex items-center justify-center mx-auto mb-3 text-omnom-violet shadow-nom-violet">
        <Lock class="w-6 h-6 stroke-[2.5]" />
      </div>

      <h3 class="text-lg font-black uppercase tracking-tight text-white mb-1">Omnom Locked</h3>
      <p class="text-xs text-slate-400 mb-5">Enter passcode to unlock</p>

      <form @submit.prevent="handleUnlock" class="space-y-3">
        <div>
          <input
            v-model="passcode"
            type="password"
            autofocus
            placeholder="••••"
            class="w-full text-center bg-black/40 border border-white/[0.1] focus:border-omnom-violet rounded-lg py-2.5 px-4 text-white text-xl tracking-widest focus:outline-none font-mono font-bold"
          />
        </div>

        <div v-if="error" class="flex items-center justify-center gap-1.5 text-xs text-omnom-rose font-mono font-bold">
          <AlertCircle class="w-3.5 h-3.5" /> {{ error }}
        </div>

        <button
          type="submit"
          :disabled="!passcode.trim() || loading"
          class="btn-bounce w-full py-2.5 rounded-lg bg-gradient-to-r from-omnom-indigo to-omnom-violet hover:brightness-110 disabled:opacity-40 text-white font-bold font-mono text-xs uppercase tracking-wider transition-all flex items-center justify-center gap-2 shadow-nom-violet cursor-pointer"
        >
          <span>{{ loading ? 'Verifying...' : 'Unlock Diary' }}</span>
          <ArrowRight class="w-3.5 h-3.5 stroke-[2.5]" />
        </button>
      </form>
    </div>
  </div>
</template>
