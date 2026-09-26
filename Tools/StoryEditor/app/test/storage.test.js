'use strict';
// Тесты хранения: node --test (npm test). Каждый тест — во временной папке, настоящий диск.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { ProjectStorage, isGameProject, writeAtomic, KEEP_RECENT, BACKUP_INTERVAL_MS, GRAPH_RELATIVE } = require('../storage');

function makeProject() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'wk-story-'));
  const project = path.join(root, 'Weapon-keeper');
  fs.mkdirSync(path.join(project, 'Assets', '01_GAME'), { recursive: true });
  const data = path.join(root, 'appdata');
  return { root, project, data };
}

const graph = (n) => JSON.stringify({ version: 1, languages: ['ru', 'en'], nodes: Array.from({ length: n }, (_, i) => ({ id: 'n' + i, type: 'note' })), links: [] });

test('папка проекта распознаётся по Assets/01_GAME', () => {
  const { root, project } = makeProject();
  assert.equal(isGameProject(project), true);
  assert.equal(isGameProject(root), false);
});

test('первая запись создаёт 08_Story и файл графа', () => {
  const { project, data } = makeProject();
  const s = new ProjectStorage(project, data);
  assert.equal(s.readGraph().exists, false);
  const res = s.writeGraph(graph(2), 0);
  assert.equal(res.ok, true);
  const read = s.readGraph();
  assert.equal(read.exists, true);
  assert.equal(read.text, graph(2));
  assert.equal(read.mtimeMs, res.mtimeMs);
  assert.ok(fs.existsSync(path.join(project, GRAPH_RELATIVE)));
  assert.ok(!fs.existsSync(path.join(project, GRAPH_RELATIVE) + '.tmp'), 'временный файл не остаётся');
});

test('файл изменили снаружи — запись не выполняется, конфликт', () => {
  const { project, data } = makeProject();
  const s = new ProjectStorage(project, data);
  const first = s.writeGraph(graph(1), 0);
  // «git pull» переписал файл
  const file = path.join(project, GRAPH_RELATIVE);
  fs.writeFileSync(file, graph(5));
  fs.utimesSync(file, new Date(), new Date(Date.now() + 5000));
  const res = s.writeGraph(graph(2), first.mtimeMs);
  assert.equal(res.conflict, true);
  assert.equal(s.readGraph().text, graph(5), 'чужая версия не затёрта');
});

test('перед перезаписью прежняя версия уходит в резервную копию (не чаще раза в 5 минут)', () => {
  const { project, data } = makeProject();
  let clock = 1_000_000;
  const s = new ProjectStorage(project, data, () => clock);
  let res = s.writeGraph(graph(1), 0);
  clock += BACKUP_INTERVAL_MS + 1;
  res = s.writeGraph(graph(2), res.mtimeMs);
  let backups = s.listBackups();
  assert.equal(backups.length, 1);
  assert.equal(s.readBackup(backups[0].id), graph(1));
  assert.equal(backups[0].nodes, 1);

  clock += 1000; // меньше 5 минут — новой копии нет
  res = s.writeGraph(graph(3), res.mtimeMs);
  assert.equal(s.listBackups().length, 1);

  clock += BACKUP_INTERVAL_MS + 1;
  s.writeGraph(graph(4), res.mtimeMs);
  backups = s.listBackups();
  assert.equal(backups.length, 2);
  assert.equal(s.readBackup(backups[0].id), graph(3), 'новые копии первыми');
});

test('одинаковый текст не дублирует копию', () => {
  const { project, data } = makeProject();
  let clock = 1_000_000;
  const s = new ProjectStorage(project, data, () => clock);
  const a = s.backup(graph(1), 'open');
  clock += 10;
  const b = s.backup(graph(1), 'open');
  assert.equal(a, b);
  assert.equal(s.listBackups().length, 1);
});

test('чистка: последние 100 + по одной за день за 30 дней', () => {
  const { project, data } = makeProject();
  const day = 24 * 60 * 60 * 1000;
  let clock = Date.UTC(2026, 0, 1);
  const s = new ProjectStorage(project, data, () => clock);
  // 10 дней по 20 копий в день
  for (let d = 0; d < 10; d++) for (let i = 0; i < 20; i++) { clock = Date.UTC(2026, 0, 1) + d * day + i * 60000; s.backup(graph(d * 100 + i), 'auto'); }
  const list = s.listBackups();
  // 100 последних (5 последних дней целиком) + по одной за 5 более ранних дней
  assert.equal(list.length, KEEP_RECENT + 5);
});

test('черновик: запись, чтение, очистка', () => {
  const { project, data } = makeProject();
  const s = new ProjectStorage(project, data, () => 42);
  assert.equal(s.readDraft(), null);
  s.writeDraft(graph(3), 7);
  const draft = s.readDraft();
  assert.equal(draft.text, graph(3));
  assert.equal(draft.baseMtimeMs, 7);
  assert.equal(draft.savedAt, 42);
  s.clearDraft();
  assert.equal(s.readDraft(), null);
});

test('копии разных проектов не смешиваются', () => {
  const a = makeProject();
  const b = makeProject();
  const sa = new ProjectStorage(a.project, a.data);
  const sb = new ProjectStorage(b.project, a.data); // одна папка приложения
  sa.backup(graph(1), 'open');
  assert.equal(sb.listBackups().length, 0);
});

test('id копии проверяется (нельзя выйти из папки)', () => {
  const { project, data } = makeProject();
  const s = new ProjectStorage(project, data);
  assert.throws(() => s.readBackup('../../etc/passwd'));
});

test('writeAtomic при сбое записи оставляет старый файл целым', () => {
  const { root } = makeProject();
  const file = path.join(root, 'x.json');
  writeAtomic(file, 'old');
  const originalOpen = fs.writeSync;
  fs.writeSync = () => { throw new Error('диск отвалился'); };
  try {
    assert.throws(() => writeAtomic(file, 'new'));
  } finally {
    fs.writeSync = originalOpen;
  }
  assert.equal(fs.readFileSync(file, 'utf8'), 'old');
});
