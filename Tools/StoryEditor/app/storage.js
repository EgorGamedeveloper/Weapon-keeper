'use strict';
// Хранение графа сюжета на диске — без Electron, чтобы проверяться обычными тестами Node (test/storage.test.js).
//
// Главное правило: данные пользователя не теряются.
// - Граф живёт в Unity-проекте: <проект>/Assets/01_GAME/08_Story/story_graph.json. Приложение — только окно к нему.
// - Запись атомарная: сначала .tmp рядом, потом rename — сбой посреди записи оставит старый файл целым.
// - Перед перезаписью, если с прошлой копии прошло больше BACKUP_INTERVAL_MS, прежнее содержимое уходит в резервную
//   копию; копия делается и при каждом открытии проекта, и перед восстановлением/заменой графа.
// - Черновик (draft) пишется на каждую правку отдельно от графа — после сбоя приложения его предлагают восстановить.
// - Если файл на диске изменился с момента чтения (git pull, импорт), запись не выполняется, а возвращается конфликт.

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const GRAPH_RELATIVE = path.join('Assets', '01_GAME', '08_Story', 'story_graph.json');
const CATALOG_RELATIVE = path.join('Assets', '01_GAME', '08_Story', 'scene_catalog.json');
const EDITOR_RELATIVE = path.join('Tools', 'StoryEditor', 'story_editor.html');

const BACKUP_INTERVAL_MS = 5 * 60 * 1000;
const KEEP_RECENT = 100;
const KEEP_DAILY_DAYS = 30;

const sha1 = (text) => crypto.createHash('sha1').update(text).digest('hex');

/** Папка похожа на Unity-проект этой игры (есть Assets/01_GAME). */
function isGameProject(projectPath) {
  try {
    return fs.statSync(path.join(projectPath, 'Assets', '01_GAME')).isDirectory();
  } catch {
    return false;
  }
}

/** Атомарная запись текста: .tmp рядом → fsync → rename поверх. */
function writeAtomic(filePath, text) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const tmp = filePath + '.tmp';
  const fd = fs.openSync(tmp, 'w');
  try {
    fs.writeSync(fd, text, null, 'utf8');
    fs.fsyncSync(fd);
  } finally {
    fs.closeSync(fd);
  }
  fs.renameSync(tmp, filePath);
}

function readIfExists(filePath) {
  try {
    const stat = fs.statSync(filePath);
    return { exists: true, text: fs.readFileSync(filePath, 'utf8'), mtimeMs: stat.mtimeMs };
  } catch (err) {
    if (err.code === 'ENOENT') return { exists: false, text: '', mtimeMs: 0 };
    return { exists: true, text: '', mtimeMs: 0, error: err.message };
  }
}

/** Сколько нод в тексте графа (для списка резервных копий); битый текст — null. */
function countNodes(text) {
  try {
    const data = JSON.parse(text.replace(/^﻿/, ''));
    return Array.isArray(data.nodes) ? data.nodes.length : null;
  } catch {
    return null;
  }
}

/**
 * Хранилище одного проекта. dataDir — личная папка приложения (Application Support): там резервные копии и черновик,
 * по подпапке на проект (хеш пути), чтобы копии разных проектов не смешивались.
 */
class ProjectStorage {
  constructor(projectPath, dataDir, now = () => Date.now()) {
    this.projectPath = projectPath;
    this.now = now;
    this.graphPath = path.join(projectPath, GRAPH_RELATIVE);
    this.catalogPath = path.join(projectPath, CATALOG_RELATIVE);
    this.editorPath = path.join(projectPath, EDITOR_RELATIVE);
    const key = sha1(path.resolve(projectPath)).slice(0, 12);
    this.backupDir = path.join(dataDir, 'backups', key);
    this.draftPath = path.join(dataDir, 'drafts', key + '.json');
    this.lastBackupAt = 0;
  }

  readGraph() {
    return readIfExists(this.graphPath);
  }

  /**
   * Записать граф. expectedMtimeMs — mtime файла, который редактор прочитал или записал последним (0 — файла не было).
   * Файл изменился снаружи — { conflict: true }, ничего не записано. Иначе { ok: true, mtimeMs }.
   */
  writeGraph(text, expectedMtimeMs) {
    const current = readIfExists(this.graphPath);
    if (current.error) throw new Error('Не удалось прочитать файл графа: ' + current.error);
    if (current.exists && Math.abs(current.mtimeMs - (expectedMtimeMs || 0)) > 1) {
      return { conflict: true, mtimeMs: current.mtimeMs };
    }
    if (current.exists && current.text === text) return { ok: true, mtimeMs: current.mtimeMs, unchanged: true };

    if (current.exists && current.text && this.now() - this.lastBackupAt >= BACKUP_INTERVAL_MS) {
      this.backup(current.text, 'auto');
    }
    writeAtomic(this.graphPath, text);
    return { ok: true, mtimeMs: fs.statSync(this.graphPath).mtimeMs };
  }

  readCatalog() {
    return readIfExists(this.catalogPath);
  }

  writeCatalog(text) {
    writeAtomic(this.catalogPath, text);
  }

  // ───────── Резервные копии ─────────

  /** Сохранить копию текста (одинаковый с последней копией текст не дублируется). Возвращает id копии или null. */
  backup(text, reason) {
    if (!text) return null;
    fs.mkdirSync(this.backupDir, { recursive: true });
    const latest = this.listBackups()[0];
    if (latest && latest.hash === sha1(text)) {
      this.lastBackupAt = this.now();
      return latest.id;
    }

    const stamp = new Date(this.now()).toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
    const safeReason = String(reason || 'manual').replace(/[^a-z0-9-]/gi, '');
    let id = `${stamp}-${safeReason}`;
    for (let n = 2; fs.existsSync(path.join(this.backupDir, id + '.json')); n++) id = `${stamp}-${safeReason}-${n}`;
    writeAtomic(path.join(this.backupDir, id + '.json'), text);
    fs.writeFileSync(path.join(this.backupDir, id + '.meta'), JSON.stringify({ time: this.now(), reason: safeReason, hash: sha1(text), nodes: countNodes(text) }));
    this.lastBackupAt = this.now();
    this.prune();
    return id;
  }

  /** Копии, новые первыми: { id, time, reason, nodes, size, hash }. */
  listBackups() {
    let names;
    try {
      names = fs.readdirSync(this.backupDir);
    } catch {
      return [];
    }
    const list = [];
    for (const name of names) {
      if (!name.endsWith('.json')) continue;
      const id = name.slice(0, -5);
      let meta = {};
      try {
        meta = JSON.parse(fs.readFileSync(path.join(this.backupDir, id + '.meta'), 'utf8'));
      } catch {
        // Копия без метаданных (обрезанная запись) всё равно показывается — лучше лишняя, чем потерянная.
      }
      let size = 0;
      try { size = fs.statSync(path.join(this.backupDir, name)).size; } catch { /* нет */ }
      list.push({ id, time: meta.time || 0, reason: meta.reason || '', nodes: meta.nodes ?? null, size, hash: meta.hash || '' });
    }
    return list.sort((a, b) => b.time - a.time || (a.id < b.id ? 1 : -1));
  }

  readBackup(id) {
    if (!/^[A-Za-z0-9-]+$/.test(id)) throw new Error('Неверный id копии');
    return fs.readFileSync(path.join(this.backupDir, id + '.json'), 'utf8');
  }

  /** Чистка: последние KEEP_RECENT копий + по одной (самой поздней) за каждый из KEEP_DAILY_DAYS дней. */
  prune() {
    const list = this.listBackups();
    if (list.length <= KEEP_RECENT) return;
    const keep = new Set(list.slice(0, KEEP_RECENT).map((b) => b.id));
    const dayMs = 24 * 60 * 60 * 1000;
    const oldest = this.now() - KEEP_DAILY_DAYS * dayMs;
    const days = new Set();
    for (const b of list.slice(KEEP_RECENT)) {
      if (b.time < oldest) continue;
      const day = Math.floor(b.time / dayMs);
      if (days.has(day)) continue;
      days.add(day);
      keep.add(b.id);
    }
    for (const b of list) {
      if (keep.has(b.id)) continue;
      for (const ext of ['.json', '.meta']) {
        try { fs.unlinkSync(path.join(this.backupDir, b.id + ext)); } catch { /* уже нет */ }
      }
    }
  }

  // ───────── Черновик на случай сбоя ─────────

  writeDraft(text, baseMtimeMs) {
    writeAtomic(this.draftPath, JSON.stringify({ savedAt: this.now(), baseMtimeMs: baseMtimeMs || 0, text }));
  }

  readDraft() {
    try {
      const draft = JSON.parse(fs.readFileSync(this.draftPath, 'utf8'));
      return typeof draft.text === 'string' ? draft : null;
    } catch {
      return null;
    }
  }

  clearDraft() {
    try { fs.unlinkSync(this.draftPath); } catch { /* нет черновика */ }
  }
}

module.exports = { ProjectStorage, isGameProject, writeAtomic, countNodes, GRAPH_RELATIVE, CATALOG_RELATIVE, EDITOR_RELATIVE, BACKUP_INTERVAL_MS, KEEP_RECENT };
