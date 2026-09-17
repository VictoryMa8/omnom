import { nextTick, onUnmounted, watch, type Ref } from 'vue';

export function useModal(show: Ref<boolean>, element: Ref<HTMLElement | null>, close: () => void) {
  let previous: HTMLElement | null = null;
  const focusable = () => [...(element.value?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), [href], [tabindex="0"]') ?? [])];
  const keydown = (event: KeyboardEvent) => {
    if (!show.value) return;
    if (event.key === 'Escape') { event.preventDefault(); close(); }
    if (event.key !== 'Tab') return;
    const items = focusable();
    const first = items[0], last = items[items.length - 1];
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
  };
  watch(show, async value => {
    if (value) {
      previous = document.activeElement as HTMLElement;
      document.addEventListener('keydown', keydown);
      await nextTick(); focusable()[0]?.focus();
    } else {
      document.removeEventListener('keydown', keydown); previous?.focus();
    }
  });
  onUnmounted(() => document.removeEventListener('keydown', keydown));
}
