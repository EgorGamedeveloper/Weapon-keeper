'use strict';
// Weapon Keeper Story — приложение для Mac: окно к редактору сюжета, который правит
// <Unity-проект>/Assets/01_GAME/08_Story/story_graph.json прямо на диске.
//
// Редактор (story_editor.html) берётся из самого проекта (Tools/StoryEditor) — его обновления приходят с git pull,
// приложение переустанавливать не нужно. Встроенная копия — запасной вариант (нет файла в проекте или он требует
// более новую оболочку). Вся работа с диском — в storage.js; страница видит только узкий мост storyHost (preload.js).

const { app, BrowserWindow, dialog, ipcMain, Menu, shell } = require('electron');
const fs = require('fs');
const path = require('path');
const { ProjectStorage, isGameProject, writeAtomic } = require('./storage');

// Версия моста storyHost. Редактор пишет в себе минимально нужную (<!-- story-host-api: N -->).
const HOST_API = 1;

// Для автотестов: своя папка данных вместо ~/Library/Application Support.
if (process.env.WK_STORY_USER_DATA) app.setPath('userData', process.env.WK_STORY_USER_DATA);
const BUNDLED_EDITOR = path.join(__dirname, 'editor', 'story_editor.html');

let win = null;
let storage = null;
let dirty = false;
let allowClose = false;
let openedBackupDone = false;
let editorSource = 'bundled';

const settingsPath = () => path.join(app.getPath('userData'), 'settings.json');

function loadSettings() {
  try { return JSON.parse(fs.readFileSync(settingsPath(), 'utf8')); } catch { return {}; }
}

function saveSettings(settings) {
  writeAtomic(settingsPath(), JSON.stringify(settings, null, 2));
}

function openProject(projectPath) {
  if (storage) fs.unwatchFile(storage.graphPath);
  storage = projectPath && isGameProject(projectPath) ? new ProjectStorage(projectPath, app.getPath('userData')) : null;
  openedBackupDone = false;
  if (!storage) return;

  // Файл поменяли снаружи (git pull, импорт в Unity) — редактор решает, перечитать или спросить.
  fs.watchFile(storage.graphPath, { interval: 1000 }, (cur, prev) => {
    if (cur.mtimeMs !== prev.mtimeMs && win) win.webContents.send('story:fileChanged', { mtimeMs: cur.mtimeMs, exists: cur.mtimeMs > 0 });
  });
}

/** Нужная редактору версия моста (0 — не указана). */
function requiredApi(html) {
  const match = /story-host-api:\s*(\d+)/.exec(html.slice(0, 20000));
  return match ? Number(match[1]) : 0;
}

/** Страница окна: редактор из проекта (если подходит) или встроенный, в полном HTML-документе. */
function buildPage() {
  let source = BUNDLED_EDITOR;
  editorSource = 'bundled';
  if (storage && fs.existsSync(storage.editorPath)) {
    try {
      if (requiredApi(fs.readFileSync(storage.editorPath, 'utf8')) <= HOST_API) {
        source = storage.editorPath;
        editorSource = 'project';
      } else editorSource = 'bundled-newer-needed';
    } catch { /* встроенный */ }
  }

  const body = fs.readFileSync(source, 'utf8');
  const html = '<!doctype html><html lang="ru"><head><meta charset="utf-8">' +
    '<meta name="viewport" content="width=device-width,initial-scale=1">' +
    '<style>html,body{margin:0;height:100%}body{font:14px system-ui,-apple-system,sans-serif}[hidden]{display:none!important}</style>' +
    '</head><body>' + body + '</body></html>';
  const page = path.join(app.getPath('userData'), 'editor', 'index.html');
  writeAtomic(page, html);
  return page;
}

function createWindow() {
  win = new BrowserWindow({
    width: 1500,
    height: 950,
    minWidth: 900,
    minHeight: 600,
    title: 'Weapon Keeper — Сюжет',
    backgroundColor: '#14181c',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  win.loadFile(buildPage());

  // Ссылки — во внешний браузер, страница никуда не уходит.
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:/.test(url)) shell.openExternal(url);
    return { action: 'deny' };
  });
  win.webContents.on('will-navigate', (event, url) => {
    if (!url.startsWith('file:')) {
      event.preventDefault();
      if (/^https?:/.test(url)) shell.openExternal(url);
    }
  });

  // Закрытие: сначала редактор дописывает несохранённое.
  win.on('close', (event) => {
    if (allowClose || !dirty) return;
    event.preventDefault();
    if (closing) return;
    closing = true;
    flushRenderer().then(async (ok) => {
      closing = false;
      if (!win) return;
      if (!ok) {
        const { response } = await dialog.showMessageBox(win, {
          type: 'warning',
          buttons: ['Не закрывать', 'Закрыть'],
          defaultId: 0,
          cancelId: 0,
          message: 'Последние правки не удалось записать в проект.',
          detail: 'Черновик сохранён — при следующем запуске приложение предложит его восстановить. Закрыть всё равно?',
        });
        if (response !== 1) return;
      }
      allowClose = true;
      win.close();
      allowClose = false;
    });
  });
  win.on('closed', () => { win = null; });
}

// Попросить страницу дописать несохранённое. true — записано (или нечего писать), false — не вышло или нет ответа.
let flushResolve = null;
let closing = false;
function flushRenderer(timeoutMs = 5000) {
  return new Promise((resolve) => {
    if (!win || !dirty) { resolve(true); return; }
    if (flushResolve) flushResolve(false);
    flushResolve = resolve;
    win.webContents.send('story:flush');
    setTimeout(() => {
      if (flushResolve !== resolve) return;
      flushResolve = null;
      resolve(false);
    }, timeoutMs);
  });
}

async function reloadEditor() {
  if (!win) return;
  await flushRenderer();
  if (win) win.loadFile(buildPage());
}

async function chooseProject() {
  const result = await dialog.showOpenDialog(win, {
    title: 'Папка Unity-проекта Weapon Keeper',
    message: 'Выберите папку проекта (в ней есть папка Assets)',
    properties: ['openDirectory'],
  });
  if (result.canceled || !result.filePaths.length) return { canceled: true };
  const chosen = result.filePaths[0];
  if (!isGameProject(chosen)) return { error: 'В этой папке нет Assets/01_GAME — это не проект Weapon Keeper.' };

  // Несохранённое дописывается в СТАРЫЙ проект, и только потом переключаемся.
  if (!(await flushRenderer())) {
    const { response } = await dialog.showMessageBox(win, {
      type: 'warning', buttons: ['Остаться', 'Переключить'], defaultId: 0, cancelId: 0,
      message: 'Последние правки не удалось записать.',
      detail: 'Они остались в черновике текущего проекта. Переключиться на другой проект?',
    });
    if (response !== 1) return { canceled: true };
  }

  const settings = loadSettings();
  settings.projectPath = chosen;
  saveSettings(settings);
  openProject(chosen);
  dirty = false;
  setTimeout(() => { if (win) win.loadFile(buildPage()); }, 50);
  return { ok: true, path: chosen };
}

// ───────── Мост для страницы ─────────

function needStorage() {
  if (!storage) throw new Error('Проект не выбран');
  return storage;
}

function registerIpc() {
  ipcMain.handle('story:getInfo', () => ({
    apiVersion: HOST_API,
    shellVersion: app.getVersion(),
    editorSource,
    project: storage ? { path: storage.projectPath, name: path.basename(storage.projectPath), graphPath: storage.graphPath } : null,
  }));
  ipcMain.handle('story:chooseProject', () => chooseProject());
  ipcMain.handle('story:readGraph', () => {
    const result = needStorage().readGraph();
    // Копия при каждом открытии — точка возврата «как было до этого сеанса».
    if (!openedBackupDone && result.exists && result.text) { storage.backup(result.text, 'open'); openedBackupDone = true; }
    return result;
  });
  ipcMain.handle('story:writeGraph', (_e, text, expectedMtimeMs) => needStorage().writeGraph(String(text), Number(expectedMtimeMs) || 0));
  ipcMain.handle('story:readCatalog', () => needStorage().readCatalog());
  ipcMain.handle('story:writeCatalog', (_e, text) => { needStorage().writeCatalog(String(text)); return true; });
  ipcMain.handle('story:listBackups', () => needStorage().listBackups());
  ipcMain.handle('story:readBackup', (_e, id) => needStorage().readBackup(String(id)));
  ipcMain.handle('story:backupNow', (_e, text, reason) => needStorage().backup(String(text), String(reason || 'manual')));
  ipcMain.handle('story:readDraft', () => needStorage().readDraft());
  ipcMain.handle('story:writeDraft', (_e, text, baseMtimeMs) => { needStorage().writeDraft(String(text), Number(baseMtimeMs) || 0); return true; });
  ipcMain.handle('story:clearDraft', () => { if (storage) storage.clearDraft(); return true; });
  ipcMain.handle('story:showBackups', () => {
    fs.mkdirSync(needStorage().backupDir, { recursive: true });
    return shell.openPath(storage.backupDir);
  });
  ipcMain.handle('story:revealGraph', () => { shell.showItemInFolder(needStorage().graphPath); return true; });
  ipcMain.on('story:setDirty', (_e, value) => { dirty = !!value; });
  ipcMain.on('story:flushed', (_e, ok) => {
    const resolve = flushResolve;
    flushResolve = null;
    if (resolve) resolve(!!ok);
  });
}

// ───────── Меню ─────────

function send(command) {
  if (win) win.webContents.send('story:menu', command);
}

function buildMenu() {
  const isMac = process.platform === 'darwin';
  const template = [
    ...(isMac ? [{ role: 'appMenu', label: app.name }] : []),
    {
      label: 'Файл',
      submenu: [
        { label: 'Открыть папку проекта…', accelerator: 'CmdOrCtrl+O', click: () => chooseProject().then((r) => { if (r && r.error) dialog.showErrorBox('Не тот проект', r.error); }) },
        { label: 'Показать граф в Finder', click: () => storage && shell.showItemInFolder(storage.graphPath) },
        { type: 'separator' },
        { label: 'История версий…', accelerator: 'CmdOrCtrl+Shift+H', click: () => send('history') },
        { label: 'Папка резервных копий', click: () => { if (storage) { fs.mkdirSync(storage.backupDir, { recursive: true }); shell.openPath(storage.backupDir); } } },
        { type: 'separator' },
        { label: 'Экспорт JSON…', click: () => send('export') },
        ...(isMac ? [] : [{ type: 'separator' }, { role: 'quit', label: 'Выход' }]),
      ],
    },
    {
      label: 'Правка',
      submenu: [
        // Отмена/повтор решает страница: в поле ввода — текст, на холсте — граф.
        { label: 'Отменить', accelerator: 'CmdOrCtrl+Z', click: () => send('undo') },
        { label: 'Повторить', accelerator: 'Shift+CmdOrCtrl+Z', click: () => send('redo') },
        { type: 'separator' },
        { role: 'cut', label: 'Вырезать' },
        { role: 'copy', label: 'Копировать' },
        { role: 'paste', label: 'Вставить' },
        { role: 'selectAll', label: 'Выделить всё' },
      ],
    },
    {
      label: 'Вид',
      submenu: [
        { label: 'Перезагрузить редактор', accelerator: 'CmdOrCtrl+R', click: reloadEditor },
        { role: 'toggleDevTools', label: 'Инструменты разработчика' },
        { type: 'separator' },
        { role: 'togglefullscreen', label: 'Во весь экран' },
      ],
    },
    { role: 'windowMenu', label: 'Окно' },
    {
      label: 'Справка',
      submenu: [
        { label: 'Как работать с сюжетом', click: () => { if (storage) shell.openPath(path.join(storage.projectPath, 'Docs', 'Story', 'STORY_GRAPH.md')); } },
        { label: `Версия приложения ${app.getVersion()}`, enabled: false },
      ],
    },
  ];
  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

// ───────── Запуск ─────────

const single = app.requestSingleInstanceLock();
if (!single) app.quit();
else {
  app.on('second-instance', () => { if (win) { if (win.isMinimized()) win.restore(); win.focus(); } });

  app.whenReady().then(() => {
    registerIpc();
    buildMenu();
    openProject(loadSettings().projectPath);
    createWindow();
    app.on('activate', () => { if (!win) createWindow(); });
  });

  app.on('window-all-closed', () => app.quit());
}
