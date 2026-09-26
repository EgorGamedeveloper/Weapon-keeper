// Редактор в браузере (headless Chromium): режим «браузер» (local.html) и режим «db» страницы claude.ai (db.html с
// поддельным db из mockdb.html). Запуск: tests/run.sh (соберёт обёртки и запустит). Печатает OK/FAIL по проверкам.
const { chromium } = require('playwright');
const S = process.argv[2]; // папка с local.html и db.html (их собирает wrap.py)
const ok = (c, m) => { console.log((c ? 'OK   ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };

// Перетащить ноду из палитры на холст (dx, dy — точка на холсте).
async function dragPal(page, label, dx = 420, dy = 240) {
  const it = await page.locator(`.pal-item:has-text("${label}")`).boundingBox();
  const cv = await page.locator('#canvas').boundingBox();
  await page.mouse.move(it.x + it.width / 2, it.y + it.height / 2); await page.mouse.down();
  await page.mouse.move(cv.x + dx, cv.y + dy, { steps: 8 }); await page.mouse.up();
}

(async () => {
  const browser = await chromium.launch(process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {});
  const page = await browser.newPage({ viewport: { width: 1400, height: 860 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error' && !/ERR_CERT|fonts\.g/.test(m.text())) errors.push(m.text()); });

  // ── Без хранилища: пример, правки, localStorage ──
  await page.goto('file://' + S + '/local.html');
  await page.waitForTimeout(400);
  ok(await page.locator('.node').count() === 9, 'пример: 9 нод');
  ok(await page.locator('.wires path:not(.hit)').count() === 8, 'пример: 8 ниток');
  ok(await page.locator('#banner').isVisible(), 'баннер примера виден');
  const issues = await page.locator('#issueCount').textContent();
  ok(issues === '1', 'проверка: 1 ошибка (триггер без двери), есть: ' + issues);

  // Добавить квест из палитры и заполнить
  await page.click('.pal-item:has-text("Квест")');
  ok(await page.locator('.node').count() === 9, 'щелчок по палитре ноду не добавляет');
  await dragPal(page, 'Квест');
  ok(await page.locator('.node').count() === 10, 'добавлен квест');
  const qid = page.locator('.inspector input.mono').first();
  await qid.fill('Repair Wall!');
  ok(await qid.inputValue() === 'repair_wall_', 'id квеста очищается до латиницы: ' + await qid.inputValue());
  await page.locator('.inspector .loc input').first().fill('Заделать пролом');
  ok((await page.locator('.node.sel .n-body .t').textContent()) === 'Заделать пролом', 'название видно на ноде');
  ok(!(await page.locator('#banner').textContent()).includes('пример'), 'после правки пример стал своим');

  // Соединить «Старт» → новый квест
  const newId = await page.locator('.node.sel').getAttribute('data-id');
  const out = await page.locator('.node[data-id="n_start"] .port.out').boundingBox();
  const inp = await page.locator(`.node[data-id="${newId}"] .port.in`).boundingBox();
  await page.mouse.move(out.x + out.width / 2, out.y + out.height / 2);
  await page.mouse.down();
  await page.mouse.move(inp.x + inp.width / 2 + 40, inp.y + inp.height / 2 + 20, { steps: 8 });
  await page.mouse.up();
  ok(await page.locator('.wires path:not(.hit)').count() === 9, 'нитка протянута');

  // Перетаскивание ноды
  const head = await page.locator(`.node[data-id="${newId}"] .n-head`).boundingBox();
  await page.mouse.move(head.x + 20, head.y + 8); await page.mouse.down();
  await page.mouse.move(head.x + 140, head.y + 90, { steps: 6 }); await page.mouse.up();
  const moved = await page.locator(`.node[data-id="${newId}"]`).boundingBox();
  ok(Math.abs(moved.x - head.x - 120) < 4, 'нода перетаскивается');

  // Удалить и отменить
  await page.locator('.canvas').click({ position: { x: 5, y: 700 } });
  await page.click(`.node[data-id="${newId}"] .n-head`);
  await page.keyboard.press('Delete');
  ok(await page.locator('.node').count() === 9, 'Delete удалил ноду');
  await page.keyboard.press('Control+z');
  ok(await page.locator('.node').count() === 10 && await page.locator('.wires path:not(.hit)').count() === 9, 'Ctrl+Z вернул ноду и нитку');

  // Экспорт (без downloads — панель с текстом)
  await page.click('#btnExport');
  const json = JSON.parse(await page.locator('#exportText').inputValue());
  ok(json.version === 1 && json.nodes.length === 10 && json.links.length === 9, 'экспорт: 10 нод, 9 ниток');
  const q = json.nodes.find((n) => n.id === newId);
  ok(Array.isArray(q.title) && q.title[0].lang === 'ru' && q.title[0].text === 'Заделать пролом', 'тексты в файле — массивы {lang,text}');
  const radio = json.nodes.find((n) => n.type === 'radio');
  ok(Array.isArray(radio.lines[0].text) && radio.lines[0].seconds === 4, 'реплики рации в формате файла');

  // Перезагрузка — граф из localStorage
  await page.waitForTimeout(900);
  await page.reload();
  await page.waitForTimeout(400);
  ok(await page.locator('.node').count() === 10, 'после перезагрузки граф из браузера');

  // Импорт: только старт + радио
  await page.click('#btnImport');
  await page.fill('#importText', JSON.stringify({ version: 1, languages: ['ru', 'en', 'de'], nodes: [json.nodes[0], radio], links: [{ from: json.nodes[0].id, to: radio.id }] }));
  await page.click('#importApplyText');
  ok(await page.locator('.node').count() === 2, 'импорт заменил граф');
  ok(await page.locator('.line-tab').count() === 1 && await page.locator('.node').count() === 2, 'файл без линий открылся целиком в одной линии');
  await page.click('#sheetImport [data-close]');
  await page.click(`.node[data-id="${radio.id}"] .n-head`);
  ok(await page.locator('.inspector .loc .lang:text("de")').count() > 0, 'третий язык из файла появился в полях');
  ok(!(await page.locator('.inspector .loc .lang:text("en")').first().isVisible()), 'переводы по умолчанию свёрнуты');
  ok(await page.locator('.inspector .loc .lang:text("ru")').first().isVisible(), 'первый язык виден');
  const tg = page.locator('.inspector .loc-toggle').first();
  ok((await tg.textContent()).includes('EN') || (await tg.textContent()).toLowerCase().includes('en'), 'кнопка переводов показывает языки: ' + await tg.textContent());
  await tg.click();
  ok(await page.locator('.inspector .loc .lang:text("en")').first().isVisible() && await page.locator('.inspector .loc .lang:text("de")').first().isVisible(), 'кнопка раскрыла переводы');
  await page.locator('.inspector .loc input:visible').nth(1).fill('Dispatcher');
  ok((await tg.textContent()).includes('✓'), 'заполненный перевод отмечен галочкой');
  // Перетаскивание из палитры на холст
  const nBefore = await page.locator(".node").count();
  const pal = await page.locator('.pal-item:has-text("Заметка")').boundingBox();
  const cv = await page.locator('#canvas').boundingBox();
  ok(pal.y > cv.y + cv.height - 2, 'палитра под холстом');
  await page.mouse.move(pal.x + 20, pal.y + 10); await page.mouse.down();
  await page.mouse.move(cv.x + 300, cv.y + 200, { steps: 10 });
  ok(await page.locator('.pal-ghost').count() === 1, 'при перетаскивании виден призрак ноды');
  await page.mouse.up();
  ok(await page.locator('.node').count() === nBefore + 1 && await page.locator('.pal-ghost').count() === 0, 'нода перетащена на холст');
  const nb = await page.locator('.node.sel').boundingBox();
  ok(Math.abs(nb.x - (cv.x + 300)) < 80 && Math.abs(nb.y - (cv.y + 200)) < 40, 'нода встала там, где отпустили');
  // Отпустили не на холсте — ноды нет
  await page.mouse.move(pal.x + 20, pal.y + 10); await page.mouse.down();
  await page.mouse.move(pal.x + 200, pal.y + 5, { steps: 6 }); await page.mouse.up();
  ok(await page.locator('.node').count() === nBefore + 1, 'отпущено мимо холста — нода не добавлена');
  // Нода «Кат-сцена» и новые триггеры
  await dragPal(page, 'Кат-сцена', 500, 300);
  ok((await page.locator('.node.sel .n-head').textContent()).includes('Кат-сцена'), 'нода «Кат-сцена» добавлена');
  await page.locator('.inspector .check-field input').first().uncheck();
  await page.click('#btnExport');
  const cj = JSON.parse(await page.locator('#exportText').inputValue());
  const cs = cj.nodes.find((n) => n.type === 'cutscene');
  ok(cs && cs.skippable === false && cs.hideHud === true && cs.target === '', 'поля кат-сцены в JSON: ' + JSON.stringify(cs && { s: cs.skippable, h: cs.hideHud }));
  await page.click('#sheetExport [data-close]');
  await page.click('#btnIssues');
  ok((await page.locator('#issueList').textContent()).includes('Не выбрана кат-сцена'), 'проверка: пустая кат-сцена — ошибка');
  await page.click('#sheetIssues [data-close]');
  await dragPal(page, 'Триггер', 520, 420);
  const opts = await page.locator('.inspector select').first().locator('option').allTextContents();
  ok(['Куплен ящик поставки', 'Выпал предмет из ящика', 'Куплен предмет/инструмент в терминале', 'Открыт навык'].every((o) => opts.includes(o)), 'новые виды триггеров в списке');
  await page.locator('.inspector select').first().selectOption('skillUnlocked');
  ok(await page.locator('.inspector .lbl:text("Навык *"), .inspector label:text("Навык *")').count() > 0, 'у «Открыт навык» поле «Навык» обязательное');
  // Сюжетные линии
  const nodesMain = await page.locator('.node').count();
  ok(await page.locator('.line-tab').count() === 1, 'одна линия по умолчанию');
  await page.click('.line-add');
  await page.keyboard.type('Ветка Б'); await page.keyboard.press('Enter');
  ok(await page.locator('.line-tab').count() === 2 && (await page.locator('.line-tab.on').textContent()).includes('Ветка Б'), 'новая линия создана и переименована');
  ok(await page.locator('.node').count() === 0, 'новая линия пустая');
  await dragPal(page, 'Квест', 400, 250);
  await page.locator('.inspector .loc input').first().fill('Квест ветки Б');
  await page.locator('.inspector input.mono').first().fill('branch_b');
  ok(await page.locator('.node').count() === 1, 'нода попала в новую линию');
  await page.locator('.line-tab').first().click();
  ok(await page.locator('.node').count() === nodesMain, 'первая линия показывает свои ноды: ' + nodesMain);
  await page.click('#btnExport');
  const lj = JSON.parse(await page.locator('#exportText').inputValue());
  const qb = lj.nodes.find((n) => n.questId === 'branch_b');
  ok(lj.storylines.length === 2 && qb && qb.storyline === lj.storylines[1].id && lj.nodes.filter((n) => n.storyline === lj.storylines[0].id).length === nodesMain, 'линии и принадлежность нод в JSON');
  await page.click('#sheetExport [data-close]');
  await page.locator('.line-tab').first().click();
  await dragPal(page, 'Триггер', 600, 450);
  await page.locator('.inspector select').first().selectOption('questCompleted');
  const qopts = await page.locator('.inspector datalist option').evaluateAll((os) => os.map((o) => o.value));
  ok(qopts.includes('branch_b'), '«Выполнен квест» предлагает квест другой линии');
  await page.keyboard.press('Delete');
  await page.locator('.line-tab').nth(1).click();
  ok((await page.locator('.line-tab.on .x').count()) === 1, 'у открытой линии есть крестик');
  await page.locator('.line-tab.on .x').click();
  ok(await page.locator('.line-tab').count() === 2 && await page.locator('.toast').isVisible(), 'непустую линию удалить нельзя — подсказка');
  await page.click('#btnIssues');
  const bIssue = page.locator('#issueList li', { hasText: 'Ветка Б' }).first();
  ok(await bIssue.count() === 1, 'в «Проверке» видно, из какой линии проблема');
  await page.locator('.line-tab').first().click();
  await bIssue.locator('button').click();
  ok((await page.locator('.line-tab.on').textContent()).includes('Ветка Б'), 'переход к проблеме переключает линию');
  await page.click('#sheetIssues [data-close]');
  await page.keyboard.press('Delete');
  await page.locator('.line-tab.on .x').click();
  ok(await page.locator('.line-tab').count() === 1, 'пустая линия удалена');
  await page.keyboard.press('Control+z');
  ok(await page.locator('.line-tab').count() === 2, 'Ctrl+Z вернул линию');
  await page.keyboard.press('Control+z');
  await page.locator('.line-tab').first().click();
  // Рамки и оформление
  await page.click('#palTabs button[data-tab="shapes"]');
  const cvb = await page.locator('#canvas').boundingBox();
  // рамка вокруг «Старта»
  const st = await page.locator('.node[data-type="start"]').boundingBox();
  const gdrop = { x: st.x + 20 - cvb.x, y: st.y - 10 - cvb.y };
  await dragPal(page, 'Рамка', gdrop.x, gdrop.y);
  ok(await page.locator('.bi.group').count() === 1 && (await page.locator('.inspector .type-chip').textContent()).includes('Рамка'), 'рамка создана и выбрана');
  await page.locator('.inspector input[type="text"]').first().fill('Пролог');
  ok((await page.locator('.bi.group .g-head').textContent()).includes('Пролог'), 'подпись рамки видна на холсте');
  const gh = await page.locator('.bi.group .g-head').boundingBox();
  const st0 = await page.locator('.node[data-type="start"]').boundingBox();
  const gb0 = await page.locator('.bi.group').boundingBox();
  const outsideId = await page.evaluate((g) => [...document.querySelectorAll('.node')].map((d) => [d.dataset.id, d.getBoundingClientRect()])
    .find(([, r]) => { const cx = r.x + r.width / 2, cy = r.y + r.height / 2; return cx < g.x || cx > g.x + g.width || cy < g.y || cy > g.y + g.height; })?.[0], gb0);
  const others0 = await page.locator(`.node[data-id="${outsideId}"]`).boundingBox();
  await page.mouse.move(gh.x + 30, gh.y + 10); await page.mouse.down();
  await page.mouse.move(gh.x + 130, gh.y + 70, { steps: 6 }); await page.mouse.up();
  const st1 = await page.locator('.node[data-type="start"]').boundingBox();
  const others1 = await page.locator(`.node[data-id="${outsideId}"]`).boundingBox();
  ok(Math.abs(st1.x - st0.x - 100) < 3 && Math.abs(st1.y - st0.y - 60) < 3, 'нода внутри рамки поехала вместе с ней');
  ok(Math.abs(others1.x - others0.x) < 1, 'нода снаружи осталась на месте');
  const rz = await page.locator('.bi.group .rz').boundingBox();
  const gw0 = (await page.locator('.bi.group').boundingBox()).width;
  await page.mouse.move(rz.x + 5, rz.y + 5); await page.mouse.down();
  await page.mouse.move(rz.x + 85, rz.y + 45, { steps: 5 }); await page.mouse.up();
  ok(Math.abs((await page.locator('.bi.group').boundingBox()).width - gw0 - 80) < 3, 'рамка меняет размер за уголок');
  await page.keyboard.press('Control+z'); await page.keyboard.press('Control+z');
  const st2 = await page.locator('.node[data-type="start"]').boundingBox();
  ok(Math.abs(st2.x - st0.x) < 3, 'Ctrl+Z вернул рамку и ноду ' + [st0.x, st1.x, st2.x]);
  // формы и иконки
  await dragPal(page, 'Надпись', 700, 120);
  await page.locator('.inspector textarea').first().fill('Глава 1');
  ok((await page.locator('.bi.decor .txt').textContent()) === 'Глава 1', 'надпись на холсте');
  await page.click('#palTabs button[data-tab="icons"]');
  const ib = await page.locator('.pal-item.icon-item').nth(0).boundingBox();
  await page.mouse.move(ib.x + ib.width / 2, ib.y + ib.height / 2); await page.mouse.down();
  await page.mouse.move(cvb.x + 760, cvb.y + 220, { steps: 8 }); await page.mouse.up();
  ok(await page.locator('.bi.decor svg').count() >= 1, 'иконка на холсте');
  await page.locator('.inspector .swatches button').nth(2).click();
  ok((await page.locator('.bi.decor.sel').getAttribute('style')).includes('--t-trigger'), 'цвет иконки меняется');
  await page.keyboard.press('Control+d');
  const decorCount = await page.locator('.bi.decor').count();
  await page.keyboard.press('Delete');
  ok(await page.locator('.bi.decor').count() === decorCount - 1, 'копия и удаление оформления');
  // картинка с компьютера — бросить файл на холст
  await page.evaluate(async ([x, y]) => {
    const c = document.createElement('canvas'); c.width = 900; c.height = 600;
    const g = c.getContext('2d'); g.fillStyle = '#c33'; g.fillRect(0, 0, 900, 600); g.fillStyle = '#fff'; g.fillRect(100, 100, 300, 200);
    const blob = await new Promise((r) => c.toBlob(r, 'image/png'));
    const dt = new DataTransfer(); dt.items.add(new File([blob], 'map.png', { type: 'image/png' }));
    const cv = document.getElementById('canvas');
    cv.dispatchEvent(new DragEvent('dragover', { dataTransfer: dt, bubbles: true, cancelable: true, clientX: x, clientY: y }));
    cv.dispatchEvent(new DragEvent('drop', { dataTransfer: dt, bubbles: true, cancelable: true, clientX: x, clientY: y }));
  }, [cvb.x + 500, cvb.y + 500]);
  await page.waitForTimeout(600);
  ok(await page.locator('.bi.decor img').count() === 1, 'брошенная картинка появилась на холсте');
  await page.click('#btnExport');
  const ej = JSON.parse(await page.locator('#exportText').inputValue());
  const imgIds = Object.keys(ej.images || {});
  const imgData = imgIds.length ? ej.images[imgIds[0]] : '';
  ok(imgIds.length === 1 && /^data:image\/(webp|png)/.test(imgData), 'картинка в библиотеке графа');
  const dims = await page.evaluate((src) => new Promise((r) => { const i = new Image(); i.onload = () => r([i.naturalWidth, i.naturalHeight]); i.src = src; }), imgData);
  ok(dims[0] === 512 && dims[1] === 341, 'картинка уменьшена до 512 px: ' + dims);
  ok(ej.groups.length === 1 && ej.groups[0].title === 'Пролог' && ej.decor.length >= 2, 'рамки и оформление в JSON');
  await page.click('#sheetExport [data-close]');
  await page.click('#palTabs button[data-tab="images"]');
  ok(await page.locator('.pal-item.img-item').count() === 1, 'картинка во вкладке «Картинки»');
  await page.click('#palTabs button[data-tab="nodes"]');
  // Поиск
  await page.locator('.line-tab').first().click();
  await page.locator('#canvas').click({ position: { x: 900, y: 700 } });
  await page.keyboard.press('Control+f');
  ok(await page.evaluate(() => document.activeElement.id) === 'searchInput', 'Ctrl+F открывает поиск');
  await page.keyboard.type('ветки б');
  ok(await page.locator('#searchResults li').count() >= 1 && (await page.locator('#searchResults li').first().textContent()).includes('Ветка Б'), 'поиск находит квест другой линии');
  ok(await page.locator('.node.dim').count() > 0, 'несовпавшие ноды приглушены');
  await page.keyboard.press('Enter');
  ok((await page.locator('.line-tab.on').textContent()).includes('Ветка Б') && (await page.locator('.node.sel').textContent()).includes('Квест ветки Б'), 'Enter переключил линию и выделил ноду');
  await page.locator('#searchInput').fill('пролог');
  ok((await page.locator('#searchResults li').first().textContent()).includes('Рамка'), 'поиск находит рамку по названию');
  await page.locator('#searchInput').press('Escape');
  ok(await page.locator('.node.dim').count() === 0 && await page.locator('#searchResults').isHidden(), 'Esc очищает поиск и подсветку');
  await page.locator('.line-tab').first().click();
  // Границы панелей тянутся и запоминаются
  const insp0 = (await page.locator('#inspector').boundingBox()).width;
  const sv = await page.locator('#splitV').boundingBox();
  await page.mouse.move(sv.x + 3, sv.y + 200); await page.mouse.down();
  await page.mouse.move(sv.x - 97, sv.y + 200, { steps: 5 }); await page.mouse.up();
  const insp1 = (await page.locator('#inspector').boundingBox()).width;
  ok(Math.abs(insp1 - insp0 - 100) < 3, `панель свойств шире на 100 px: ${insp0} → ${insp1}`);
  const pal0 = (await page.locator('#palette').boundingBox()).height;
  const sh = await page.locator('#splitH').boundingBox();
  await page.mouse.move(sh.x + 200, sh.y + 3); await page.mouse.down();
  await page.mouse.move(sh.x + 200, sh.y - 117, { steps: 5 }); await page.mouse.up();
  const pal1 = (await page.locator('#palette').boundingBox()).height;
  ok(Math.abs(pal1 - pal0 - 120) < 3, `полоса нод выше на 120 px: ${pal0} → ${pal1}`);
  ok(await page.locator('.palette.tall').count() === 1 && await page.locator('.pal-item small').first().isVisible(), 'высокая полоса показывает описания нод');
  await page.mouse.move(sv.x - 97, sv.y + 200); await page.mouse.down();
  await page.mouse.move(sv.x - 5000, sv.y + 200, { steps: 3 }); await page.mouse.up();
  const cvw = (await page.locator('#canvas').boundingBox()).width;
  ok(cvw > 300, 'панель свойств не съедает весь холст: холст ' + cvw);
  await page.locator('#splitV').dblclick();
  ok(Math.abs((await page.locator('#inspector').boundingBox()).width - 320) < 2, 'двойной щелчок вернул ширину 320');
  await page.reload(); await page.waitForTimeout(400);
  ok(Math.abs((await page.locator('#palette').boundingBox()).height - pal1) < 3, 'высота полосы сохранилась после перезагрузки');
  ok(await page.locator('.bi.group').count() === 1 && await page.locator('.bi.decor img').count() === 1, 'рамка и картинка пережили перезагрузку');
  await page.locator('#splitH').dblclick();

  // Каталог
  await page.click('#btnCatalog');
  await page.click('#catalogPasteToggle');
  await page.fill('#catalogText', JSON.stringify({ breakables: [{ id: 'abc123', name: 'Дверь склада' }], categories: [{ id: 'Category_Trash', name: 'Мусор' }] }));
  await page.click('#catalogApplyText');
  ok((await page.locator('#catalogSummary').textContent()).includes('Разрушаемые: 1'), 'каталог загружен');

  // Тема и цвета
  const before = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
  await page.click('#btnTheme');
  const after = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
  ok(after && after !== before, 'кнопка темы переключает: ' + before + ' → ' + after);
  const bgDark = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  await page.click('#btnTheme');
  const bgLight = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  ok(bgDark !== bgLight, 'фон меняется между темами: ' + bgDark + ' / ' + bgLight);
  await page.click('#btnColors');
  const colorInputs = page.locator('#colorList input[type=color]');
  ok(await colorInputs.count() === 13, 'в меню цветов 13 полей');
  await colorInputs.nth(4).evaluate((i) => { i.value = '#ff0000'; i.dispatchEvent(new Event('input')); });
  const radioHead = await page.locator('.node[data-type="radio"] .n-head').first().evaluate((h) => getComputedStyle(h).backgroundColor);
  ok(radioHead === 'rgb(255, 0, 0)', 'цвет ноды «Рация» поменялся: ' + radioHead);
  await page.reload(); await page.waitForTimeout(400);
  const head2 = await page.locator('.node[data-type="radio"] .n-head').first().evaluate((h) => getComputedStyle(h).backgroundColor);
  ok(head2 === 'rgb(255, 0, 0)', 'цвет сохранился после перезагрузки');
  await page.click('#btnTheme');
  await page.screenshot({ path: S + '/shot-dark.png' });
  ok(errors.length === 0, 'ошибок JS нет' + (errors.length ? ': ' + errors.join(' | ') : ''));
  await page.screenshot({ path: S + '/shot-local.png' });

  // ── С поддельным db ──
  const p2 = await browser.newPage({ viewport: { width: 1400, height: 860 } });
  const e2 = [];
  p2.on('pageerror', (e) => e2.push(e.message));
  await p2.goto('file://' + S + '/db.html');
  await p2.waitForTimeout(500);
  ok(await p2.locator('.node').count() === 9, 'db пустой: показан пример');
  ok(await p2.evaluate(() => Object.keys(window.__store).length) === 0, 'пример не пишется в db до правки');
  await dragPal(p2, 'Ожидание');
  await p2.waitForTimeout(1200);
  const keys = await p2.evaluate(() => Object.keys(window.__store));
  ok(keys.filter((k) => k.startsWith('nodes/')).length === 10 && keys.includes('graph/links') && keys.includes('graph/meta'), 'после правки в db: 10 нод, links, meta');
  ok((await p2.locator('#status').textContent()) === 'Сохранено', 'статус «Сохранено»');
  // удалить ноду → документ удалён
  await p2.keyboard.press('Delete');
  await p2.waitForTimeout(1200);
  ok(await p2.evaluate(() => Object.keys(window.__store).filter((k) => k.startsWith('nodes/')).length) === 9, 'удаление ноды удаляет документ');
  const writes = await p2.evaluate(() => window.__writes);
  await p2.reload();
  await p2.waitForTimeout(600);
  ok(await p2.locator('.node').count() === 9 && !(await p2.locator('#banner').isVisible()), 'перезагрузка: граф из db, не пример');
  await p2.waitForTimeout(1000);
  ok(await p2.evaluate(() => window.__writes || 0) === 0, 'загрузка ничего не перезаписывает (было записей до перезагрузки: ' + writes + ')');
  ok(e2.length === 0, 'db: ошибок JS нет' + (e2.length ? ': ' + e2.join(' | ') : ''));

  // Узкий экран
  const p3 = await browser.newPage({ viewport: { width: 400, height: 800 } });
  await p3.goto('file://' + S + '/local.html');
  await p3.waitForTimeout(400);
  const sw = await p3.evaluate(() => document.documentElement.scrollWidth);
  ok(sw <= 400, 'телефон: нет горизонтальной прокрутки (' + sw + ')');
  await p3.screenshot({ path: S + '/shot-phone.png' });
  await browser.close();
})();
