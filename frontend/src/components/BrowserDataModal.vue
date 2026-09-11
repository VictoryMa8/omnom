<script setup lang="ts">
import { ref } from 'vue';
import { X, Download, Upload } from 'lucide-vue-next';
import { localDiary } from '../services/localDiary';
import { useDiaryStore } from '../stores/diaryStore';
import { useToastStore } from '../stores/toastStore';
defineProps<{ show: boolean }>();
const emit = defineEmits<{ (e: 'close'): void }>();
const diary = useDiaryStore();
const toast = useToastStore();
const busy = ref(false);
function exportDiary() {
  try {
    const url = URL.createObjectURL(new Blob([localDiary.exportBackup()], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url; link.download = `omnom-diary-${diary.selectedDate}.json`; link.click();
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
    await diary.fetchTimeline();
    toast.success('Diary restored in this browser.');
  } catch (error: any) { toast.error(error.message || 'Invalid diary backup.'); }
  finally { busy.value = false; input.value = ''; }
}
</script>
<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm">
    <section role="dialog" aria-modal="true" aria-labelledby="diary-settings-title" class="glass-card relative rounded-xl p-6 w-full max-w-md space-y-4">
      <button aria-label="Close settings" class="absolute right-3 top-3 p-2" @click="emit('close')"><X class="w-4 h-4" /></button>
      <h2 id="diary-settings-title" class="text-lg font-bold">Your diary</h2>
      <p class="text-sm text-slate-300">Meals and targets stay in this browser on this device. Other visitors have their own diary. People sharing the same browser profile share its diary.</p>
      <p class="text-sm text-slate-400">Clearing browser data removes your diary. Export a backup to keep a copy or move it to another device. Meal descriptions are sent to the server and AI provider when you parse them.</p>
      <button class="w-full rounded-lg bg-omnom-violet p-3 flex justify-center gap-2" @click="exportDiary"><Download class="w-4 h-4" /> Export backup</button>
      <label class="block rounded-lg border border-white/20 p-3 text-sm">
        <span class="flex gap-2 mb-2"><Upload class="w-4 h-4" /> Restore a backup</span>
        <input type="file" accept="application/json,.json" :disabled="busy" @change="importDiary" class="w-full text-xs" />
      </label>
    </section>
  </div>
</template>
