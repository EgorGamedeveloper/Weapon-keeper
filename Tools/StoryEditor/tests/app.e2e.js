// Сквозная проверка приложения для Mac: настоящий Electron + настоящий диск (временный «проект» в /tmp).
// Нужны: npm ci в Tools/StoryEditor/app (там Electron), playwright; на Linux без экрана — xvfb-run. Запуск: tests/run.sh app
const { _electron: electron } = require('playwright');
const fs = require('fs'); const os = require('os'); const path = require('path');
const APP = path.join(__dirname, '..', 'app');
const EDITOR = path.join(__dirname, '..', 'story_editor.html');
const ok = (c, m) => { console.log((c ? 'OK   ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };

// Перетащить ноду из палитры на холст (dx, dy — точка на холсте).
async function dragPal(page, label, dx = 420, dy = 240) {
  const it = await page.locator(`.pal-item:has-text("${label}")`).boundingBox();
  const cv = await page.locator('#canvas').boundingBox();
  await page.mouse.move(it.x + it.width / 2, it.y + it.height / 2); await page.mouse.down();
  await page.mouse.move(cv.x + dx, cv.y + dy, { steps: 8 }); await page.mouse.up();
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const root = fs.mkdtempSync(path.join(os.tmpdir(), 'wk-e2e-'));
const project = path.join(root, 'Weapon-keeper');
fs.mkdirSync(path.join(project, 'Assets/01_GAME'), { recursive: true });
fs.mkdirSync(path.join(project, 'Tools/StoryEditor'), { recursive: true });
fs.copyFileSync(EDITOR, path.join(project, 'Tools/StoryEditor/story_editor.html'));
const graphPath = path.join(project, 'Assets/01_GAME/08_Story/story_graph.json');
const userData = path.join(root, 'userdata');
const readGraph = () => JSON.parse(fs.readFileSync(graphPath, 'utf8'));
const backupsDir = () => { const b = path.join(userData, 'backups'); const d = fs.existsSync(b) ? fs.readdirSync(b) : []; return d.length ? path.join(b, d[0]) : null; };
const backups = () => { const d = backupsDir(); return d ? fs.readdirSync(d).filter((f) => f.endsWith('.json')) : []; };

async function launch(withProject = true) {
  fs.mkdirSync(userData, { recursive: true });
  fs.writeFileSync(path.join(userData, 'settings.json'), JSON.stringify(withProject ? { projectPath: project } : {}));
  const app = await electron.launch({ executablePath: APP + '/node_modules/electron/dist/electron', args: [APP, '--no-sandbox'], env: { ...process.env, WK_STORY_USER_DATA: userData } });
  const page = await app.firstWindow();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.waitForSelector('#status');
  await page.waitForFunction(() => document.getElementById('status').textContent !== 'Загрузка…');
  await sleep(300);
  return { app, page, errors };
}
async function addQuest(page, title, dx, dy) {
  await dragPal(page, 'Квест', dx, dy);
  await page.locator('.inspector .loc input').first().fill(title);
}
async function closeApp(app) {
  const exited = new Promise((r) => app.process().once('exit', r));
  await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].close());
  await Promise.race([exited, sleep(8000)]);
}

(async () => {
  // 1. Проект не выбран — окно выбора
  let { app, page, errors } = await launch(false);
  ok(await page.locator('#sheetWelcome').isVisible(), 'без проекта: окно выбора папки');
  await page.keyboard.press('Escape');
  ok(await page.locator('#sheetWelcome').isVisible(), 'Esc не закрывает окно выбора');
  ok(await page.evaluate(() => typeof window.require === 'undefined' && typeof window.process === 'undefined'), 'у страницы нет Node');
  await app.close();

  // 2. Проект без графа — пример; первая правка создаёт файл
  ({ app, page, errors } = await launch());
  ok(await page.locator('.node').count() === 9, 'новый проект: пример');
  ok((await page.locator('#btnExport').textContent()) === 'История', 'кнопка «История» вместо скачивания');
  ok((await page.locator('#brandSub').textContent()).includes('Weapon-keeper'), 'в шапке имя проекта');
  ok(!fs.existsSync(graphPath), 'пример не пишется до правки');
  await addQuest(page, 'Первый');
  await sleep(1800);
  ok(fs.existsSync(graphPath) && readGraph().nodes.length === 10, 'правка записана в проект: 10 нод');
  ok((await page.locator('#status').textContent()) === 'Сохранено в проект', 'статус «Сохранено в проект»');
  ok(!fs.existsSync(graphPath + '.tmp'), 'временного файла нет');
  await page.locator('#canvas').click({ position: { x: 600, y: 600 } });
  await page.keyboard.press('Meta+f').catch(() => {});
  if (await page.evaluate(() => document.activeElement.id) !== 'searchInput') await page.keyboard.press('Control+f');
  ok(await page.evaluate(() => document.activeElement.id) === 'searchInput', 'Cmd/Ctrl+F в приложении открывает поиск');
  await page.keyboard.press('Escape');

  // 3. Закрытие сразу после правки — дописывает
  await addQuest(page, 'Перед закрытием');
  await closeApp(app);
  ok(readGraph().nodes.length === 11, 'закрытие окна дописало правку (11 нод)');
  ok(!fs.existsSync(path.join(userData, 'drafts')) || fs.readdirSync(path.join(userData, 'drafts')).length === 0, 'черновик после записи убран');

  // 4. «Сбой»: правка, затем kill до автосохранения — черновик предложен и восстанавливается
  ({ app, page, errors } = await launch());
  ok(await page.locator('.node').count() === 11, 'повторный запуск: граф из проекта (11)');
  ok(backups().length >= 1, 'копия при открытии сделана');
  await page.evaluate(() => { /* растянуть автосохранение, чтобы «упасть» раньше него */ });
  await addQuest(page, 'Потеряется?');
  await sleep(500); // черновик пишется через 250 мс, файл — через 1000
  app.process().kill('SIGKILL');
  await sleep(500);
  const onDisk = readGraph().nodes.length;
  ({ app, page, errors } = await launch());
  ok(await page.locator('#sheetDraft').isVisible(), 'после сбоя предложен черновик' + (onDisk === 12 ? ' (файл уже успел записаться)' : ''));
  await page.click('#draftRestore');
  await sleep(1800);
  ok(readGraph().nodes.length === 12 && readGraph().nodes.some((n) => (n.title || []).some((t) => t.text === 'Потеряется?')), 'черновик восстановлен и записан');

  // 5. Файл поменяли снаружи, своих правок нет — перечитан
  const g = readGraph(); g.nodes = g.nodes.slice(0, 5); g.links = g.links.filter((l) => g.nodes.some((n) => n.id === l.from) && g.nodes.some((n) => n.id === l.to));
  fs.writeFileSync(graphPath, JSON.stringify(g));
  await sleep(2500);
  ok(await page.locator('.node').count() === 5, 'изменение снаружи подхвачено (5 нод)');
  ok(!(await page.locator('#sheetConflict').isVisible()), 'без своих правок — без вопросов');

  // 6. Снаружи поменяли, а у нас правка — конфликт; «Мои правки» оставляет нашу, чужая — в истории
  await addQuest(page, 'Моя правка');
  const g2 = readGraph(); g2.nodes.push({ id: 'n_ext', type: 'note', x: 0, y: 0, text: 'снаружи' }); fs.writeFileSync(graphPath, JSON.stringify(g2));
  await sleep(2500);
  ok(await page.locator('#sheetConflict').isVisible(), 'конфликт: окно выбора версии');
  ok(readGraph().nodes.some((n) => n.id === 'n_ext'), 'чужая версия не затёрта молча');
  await page.click('#conflictMine');
  await sleep(1800);
  ok(!readGraph().nodes.some((n) => n.id === 'n_ext') && readGraph().nodes.length === 6, 'оставлены мои правки');
  ok(backups().some((f) => f.includes('conflict-disk')), 'версия с диска ушла в копию');

  // 7. История версий: восстановить копию «при открытии» (11/12 нод), потом отменить Ctrl+Z
  await page.click('#btnExport');
  await sleep(300);
  const rows = await page.locator('#historyList li').count();
  ok(rows >= 2, 'в истории есть копии: ' + rows);
  const openRow = page.locator('#historyList li', { hasText: 'при открытии' }).first();
  await openRow.locator('button').click();
  await sleep(1800);
  const restored = readGraph().nodes.length;
  ok(restored >= 11, 'восстановлено из истории: ' + restored + ' нод');
  ok(backups().some((f) => f.includes('before-restore')), 'перед восстановлением сделана копия');
  await page.locator('.canvas').click({ position: { x: 5, y: 700 } });
  await page.keyboard.press('Control+z');
  await sleep(1800);
  ok(readGraph().nodes.length === 6, 'Ctrl+Z отменил восстановление');
  ok(errors.length === 0, 'ошибок JS нет: ' + errors.join(' | '));
  await app.close();

  // 8. Битый файл — не перезаписывается, восстановление из копии
  fs.writeFileSync(graphPath, '{ "version": 1, "nodes": [ обрыв');
  ({ app, page, errors } = await launch());
  ok(await page.locator('#sheetBroken').isVisible(), 'битый файл: окно восстановления');
  await addQuest(page, 'Не должна записаться', 60, 500); // не на окно восстановления
  ok(await page.locator('.node').count() === 1, 'нода добавлена рядом с окном восстановления');
  await sleep(1800);
  ok(fs.readFileSync(graphPath, 'utf8').includes('обрыв'), 'битый файл не перезаписан правкой');
  await page.locator('#brokenList li button:not([disabled])').first().click();
  await sleep(1800);
  ok(readGraph().nodes.length >= 6, 'после восстановления файл снова целый');
  ok(backups().some((f) => fs.readFileSync(path.join(backupsDir(), f), 'utf8').includes('обрыв')), 'битый текст сохранён в копию');
  ok(!(await page.locator('#sheetBroken').isVisible()), 'окно восстановления закрылось');
  await app.close();

  // 9. Файл новее редактора — только просмотр, ничего не пишется
  const g3 = readGraph(); g3.version = 99; g3.futureField = { keep: true }; fs.writeFileSync(graphPath, JSON.stringify(g3));
  const before = fs.readFileSync(graphPath, 'utf8');
  ({ app, page, errors } = await launch());
  ok((await page.locator('#banner').textContent()).includes('более новой версией'), 'новый формат: баннер «только просмотр»');
  ok(await page.locator('.node').count() === g3.nodes.length, 'граф нового формата показан');
  await addQuest(page, 'x');
  await sleep(1800);
  ok(fs.readFileSync(graphPath, 'utf8') === before, 'файл нового формата не тронут');
  // Закрытие с незаписанной правкой — приложение предупреждает и не закрывается само.
  await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].close());
  await sleep(1500);
  ok(app.process().exitCode === null, 'незаписанная правка: окно не закрылось молча');
  app.process().kill('SIGKILL'); await sleep(500);
  fs.rmSync(path.join(userData, 'drafts'), { recursive: true, force: true });

  // 10. Неизвестные поля верхнего уровня переживают сохранение
  g3.version = 1; fs.writeFileSync(graphPath, JSON.stringify(g3));
  ({ app, page, errors } = await launch());
  await addQuest(page, 'y');
  await sleep(1800);
  ok(readGraph().futureField && readGraph().futureField.keep === true, 'неизвестное поле сохранено');
  ok(errors.length === 0, 'ошибок JS нет: ' + errors.join(' | '));
    await closeApp(app);

  // 11. Редактор в проекте требует мост новее — открывается встроенная копия с подсказкой
  const projEditor = path.join(project, 'Tools/StoryEditor/story_editor.html');
  fs.writeFileSync(projEditor, fs.readFileSync(projEditor, 'utf8').replace('story-host-api: 1', 'story-host-api: 2'));
  ({ app, page, errors } = await launch());
  ok((await page.locator('#banner').textContent()).includes('более новую версию приложения'), 'редактор требует новую оболочку: встроенная копия + подсказка');
  ok(await page.locator('.node').count() > 0, 'граф при этом открыт');
  await app.close();
  fs.rmSync(root, { recursive: true, force: true });
})().catch((e) => { console.error(e); process.exitCode = 1; process.exit(1); });
