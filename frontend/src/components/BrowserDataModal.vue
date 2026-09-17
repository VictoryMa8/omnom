<script setup lang="ts">
import { ref, toRef } from 'vue';
import { X, Download, Upload } from 'lucide-vue-next';
import { localDiary } from '../services/localDiary';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
import { useModal } from '../utils/useModal';
const props = defineProps<{ show: boolean }>();
const emit = defineEmits<{ (e: 'close'): void }>();
const diary = useDiaryStore(), toast = useToastStore();
const busy = ref(false);
const dialog = ref<HTMLElement | null>(null);
const close = () => { if (!busy.value) emit('close'); };
useModal(toRef(props, 'show'), dialog, close);
function exportDiary() {
  try {
    const url = URL.createObjectURL(new Blob([localDiary.exportBackup()], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url; link.download = `omnom-ai-diary-${diary.selectedDate}.json`; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    toast.success('Diary backup downloaded.');
  } catch (error: any) { toast.error(error.message || 'Could not export the diary.'); }
}
async function importDiary(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  try {
    if (file.size > 10 * 1024 * 1024) throw new Error('Choose a diary backup smaller than 10 MB.');
    if (!confirm('Replace this browser’s diary with this backup? Export your current diary first if you want to keep it.')) return;
    busy.value = true;
    localDiary.importBackup(await file.text());
    toast.clearActions();
    await diary.fetchTimeline();
    toast.success('Diary restored in this browser.');
  } catch (error: any) { toast.error(error.message || 'Invalid diary backup.'); }
  finally { busy.value = false; input.value = ''; }
}
</script>
<template>
  <div v-if="show" class="modal-backdrop" @click.self="close">
    <section ref="dialog" role="dialog" aria-modal="true" aria-labelledby="diary-settings-title" class="modal">
      <div class="section-heading"><h2 id="diary-settings-title">Your diary, with you.</h2><button aria-label="Close settings" class="icon-button" :disabled="busy" @click="close"><X class="icon" /></button></div>
      <p class="muted">omnom AI saves meals and goals in this browser. Export a backup to keep a copy or bring your diary to another device.</p>
      <button class="primary-button w-full" @click="exportDiary"><Download class="icon" />Export your diary</button>
      <label class="backup-import"><span class="flex items-center gap-2"><Upload class="icon" />Restore a backup</span><input aria-label="Choose diary backup" type="file" accept="application/json,.json" :disabled="busy" @change="importDiary" /></label>
      <p class="muted small">Clearing browser data removes this diary. People using the same browser profile share it. Meal descriptions are sent to our server and AI provider to help estimate nutrition.</p>
    </section>
  </div>
</template>
