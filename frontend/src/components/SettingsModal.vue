<script setup lang="ts">
import { ref, watch } from 'vue';
import { useSettingsStore } from '../stores/settingsStore';
import { X, ShieldCheck, Lock, Check, ExternalLink, Settings as SettingsIcon } from 'lucide-vue-next';

const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const settingsStore = useSettingsStore();

const usdaApiKey = ref('');
const newPasscode = ref('');
const savedSuccess = ref(false);

watch(
  () => props.show,
  async (isOpen) => {
    if (isOpen) {
      await settingsStore.fetchSettings();
      usdaApiKey.value = '';
      newPasscode.value = '';
      savedSuccess.value = false;
    }
  }
);

const save = async () => {
  await settingsStore.saveSettings({
    usdaApiKey: usdaApiKey.value || undefined,
    newPasscode: settingsStore.settings?.passcodeManaged ? undefined : newPasscode.value || undefined,
  });

  savedSuccess.value = true;
  setTimeout(() => {
    savedSuccess.value = false;
    emit('close');
  }, 800);
};
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md">
    <div class="glass-card border border-white/[0.12] rounded-xl w-full max-w-lg p-5 sm:p-6 shadow-2xl relative animate-in fade-in zoom-in-95 duration-150 max-h-[90vh] overflow-y-auto">
      <button
        @click="$emit('close')"
        class="btn-bounce absolute top-4 right-4 p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
      >
        <X class="w-4 h-4" />
      </button>

      <div class="flex items-center gap-2.5 mb-4">
        <div class="w-8 h-8 rounded-lg bg-omnom-violet/10 border border-omnom-violet/20 flex items-center justify-center text-omnom-violet">
          <SettingsIcon class="w-4 h-4" />
        </div>
        <div>
          <h3 class="text-base font-black uppercase tracking-tight text-white font-sans">Settings & Integrations</h3>
          <p class="text-xs text-slate-400 font-sans">API credentials and access security</p>
        </div>
      </div>

      <div class="space-y-3.5 mb-5 font-sans">
        <!-- USDA API Key -->
        <div>
          <div class="flex items-center justify-between mb-1">
            <label class="text-xs font-mono font-bold text-slate-300 flex items-center gap-1.5">
              <ShieldCheck class="w-3.5 h-3.5 text-omnom-cyan" /> USDA FoodData Central Key (Optional)
            </label>
            <a
              href="https://fdc.nal.usda.gov/api-key-signup.html"
              target="_blank"
              class="text-[11px] font-mono text-omnom-cyan hover:underline flex items-center gap-1 font-bold"
            >
              Get Key <ExternalLink class="w-3 h-3" />
            </a>
          </div>
          <input
            v-model="usdaApiKey"
            type="password"
            :placeholder="settingsStore.settings?.usdaApiKeyMasked || 'DEMO_KEY'"
            class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-violet font-mono"
          />
          <p class="text-[10px] text-slate-400 mt-1 font-sans">
            Local staple database is queried first. USDA API is queried for unlisted foods.
          </p>
        </div>

        <!-- Master Passcode PIN -->
        <div v-if="!settingsStore.settings?.passcodeManaged">
          <label class="text-xs font-mono font-bold text-slate-300 flex items-center gap-1.5 mb-1">
            <Lock class="w-3.5 h-3.5 text-omnom-rose" /> Master Passcode / PIN
          </label>
          <input
            v-model="newPasscode"
            type="password"
            placeholder="Set a PIN to lock your diary"
            class="w-full bg-black/40 border border-white/[0.1] rounded-lg px-3 py-1.5 text-xs sm:text-sm text-white focus:outline-none focus:border-omnom-violet font-mono"
          />
          <p class="text-[10px] text-slate-400 mt-1 font-sans">
            {{ settingsStore.settings?.hasPasscodeConfigured ? 'PIN protection is active.' : 'No PIN set (open access).' }}
          </p>
        </div>
      </div>

      <div class="flex items-center justify-end gap-2 pt-3 border-t border-white/[0.06]">
        <button
          type="button"
          @click="$emit('close')"
          class="btn-bounce px-3.5 py-1.5 rounded-lg text-xs font-mono font-bold text-slate-400 hover:text-white hover:bg-white/[0.06] transition-colors cursor-pointer"
        >
          Cancel
        </button>
        <button
          type="button"
          @click="save"
          class="btn-bounce px-4 py-2 rounded-lg text-xs font-mono font-bold bg-gradient-to-r from-omnom-indigo to-omnom-violet hover:brightness-110 text-white transition-all shadow-nom-violet flex items-center gap-1.5 cursor-pointer"
        >
          <Check class="w-3.5 h-3.5 stroke-[3]" /> {{ savedSuccess ? 'Saved' : 'Save Settings' }}
        </button>
      </div>
    </div>
  </div>
</template>
