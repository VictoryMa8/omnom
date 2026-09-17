import { defineStore } from 'pinia';
import { ref } from 'vue';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
  action?: () => Promise<void>;
  pending?: boolean;
}
export const useToastStore = defineStore('toast', () => {
  const toasts = ref<ToastMessage[]>([]);
  const timers = new Map<string, ReturnType<typeof setTimeout>>();
  const remove = (id: string) => {
    clearTimeout(timers.get(id)); timers.delete(id);
    toasts.value = toasts.value.filter(t => t.id !== id);
  };
  const pause = (id: string) => { clearTimeout(timers.get(id)); };
  const resume = (id: string) => {
    pause(id);
    if (!toasts.value.some(t => t.id === id && !t.pending)) return;
    timers.set(id, setTimeout(() => remove(id), 12000));
  };
  const show = (message: string, type: ToastMessage['type'] = 'success', timeout = 3500, action?: () => Promise<void>) => {
    const id = crypto.randomUUID();
    toasts.value.push({ id, type, message, action });
    timers.set(id, setTimeout(() => remove(id), timeout));
  };
  const runAction = async (id: string) => {
    const toast = toasts.value.find(t => t.id === id);
    if (!toast?.action || toast.pending) return;
    toast.pending = true; pause(id);
    try {
      await toast.action();
      remove(id); show('Change undone.');
    } catch (error: any) {
      toast.pending = false; resume(id);
      show(error.message || 'Could not undo. Please try again.', 'error', 5000);
    }
  };
  const clearActions = () => {
    for (const toast of [...toasts.value]) if (toast.action) remove(toast.id);
  };
  return { toasts, show, remove, pause, resume, runAction, clearActions,
    success: (message: string, action?: () => Promise<void>) => show(message, 'success', action ? 12000 : 3500, action),
    error: (message: string) => show(message, 'error', 5000),
    info: (message: string) => show(message, 'info'),
  };
});
