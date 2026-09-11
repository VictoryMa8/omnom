import { useAuthStore } from './authStore';
import { defineStore } from 'pinia';
import { ref } from 'vue';
import { SettingsApi, type SettingsData } from '../services/api';

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<SettingsData | null>(null);
  const loading = ref(false);

  const fetchSettings = async () => {
    loading.value = true;
    try {
      settings.value = await SettingsApi.getSettings();
    } finally {
      loading.value = false;
    }
  };

  const saveSettings = async (data: {
    openRouterApiKey?: string;
    openRouterModel?: string;
    usdaApiKey?: string;
    newPasscode?: string;
  }) => {
    loading.value = true;
    try {
      await SettingsApi.updateSettings(data);
      if (data.newPasscode != null) {
        const auth = useAuthStore();
        await auth.loginWithPasscode(data.newPasscode);
        await auth.checkStatus();
      }
      await fetchSettings();
    } finally {
      loading.value = false;
    }
  };

  return {
    settings,
    loading,
    fetchSettings,
    saveSettings,
  };
});
