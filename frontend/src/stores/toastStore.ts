import { defineStore } from 'pinia';
import { ref } from 'vue';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
}

export const useToastStore = defineStore('toast', () => {
  const toasts = ref<ToastMessage[]>([]);

  const show = (message: string, type: 'success' | 'error' | 'info' = 'success', timeout = 3500) => {
    const id = Math.random().toString(36).substring(2, 9);
    toasts.value.push({ id, type, message });

    setTimeout(() => {
      remove(id);
    }, timeout);
  };

  const remove = (id: string) => {
    toasts.value = toasts.value.filter((t) => t.id !== id);
  };

  return {
    toasts,
    show,
    remove,
    success: (msg: string) => show(msg, 'success'),
    error: (msg: string) => show(msg, 'error', 5000),
    info: (msg: string) => show(msg, 'info'),
  };
});
