'use strict';
// Мост между страницей редактора и диском. Странице доступны только эти функции — никакого Node и произвольных путей:
// всё, что можно прочитать или записать, — граф, каталог, копии и черновик выбранного проекта.
const { contextBridge, ipcRenderer } = require('electron');

const invoke = (channel, ...args) => ipcRenderer.invoke('story:' + channel, ...args);

contextBridge.exposeInMainWorld('storyHost', {
  apiVersion: 1,
  getInfo: () => invoke('getInfo'),
  chooseProject: () => invoke('chooseProject'),
  readGraph: () => invoke('readGraph'),
  writeGraph: (text, expectedMtimeMs) => invoke('writeGraph', text, expectedMtimeMs),
  readCatalog: () => invoke('readCatalog'),
  writeCatalog: (text) => invoke('writeCatalog', text),
  listBackups: () => invoke('listBackups'),
  readBackup: (id) => invoke('readBackup', id),
  backupNow: (text, reason) => invoke('backupNow', text, reason),
  readDraft: () => invoke('readDraft'),
  writeDraft: (text, baseMtimeMs) => invoke('writeDraft', text, baseMtimeMs),
  clearDraft: () => invoke('clearDraft'),
  showBackups: () => invoke('showBackups'),
  revealGraph: () => invoke('revealGraph'),
  setDirty: (value) => ipcRenderer.send('story:setDirty', !!value),
  onFileChanged: (callback) => ipcRenderer.on('story:fileChanged', (_event, info) => callback(info)),
  onMenu: (callback) => ipcRenderer.on('story:menu', (_event, command) => callback(command)),
  // Приложение закрывается: страница дописывает несохранённое и отвечает, получилось ли.
  onFlushRequest: (callback) => ipcRenderer.on('story:flush', async () => {
    let ok = false;
    try { ok = await callback(); } catch { ok = false; }
    ipcRenderer.send('story:flushed', !!ok);
  }),
});
