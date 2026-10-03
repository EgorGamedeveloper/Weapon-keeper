# Редактор сюжета Weapon Keeper — устройство, возможности, связь с Unity

Документ для разработчика или ИИ-агента, которому предстоит менять систему сюжета: редактор, приложение для Mac или игровую часть в Unity.
- Пользовательская инструкция — [`STORY_GRAPH.md`](STORY_GRAPH.md).
- Здесь — как всё устроено, почему именно так и что трогать, чтобы добавить новое и ничего не сломать.

Состояние описано на момент PR «кат-сцены, линии, рамки, оформление, поиск» (сентябрь 2026). Если код разошёлся с документом, прав код. Поправьте документ в том же PR.

---

## 1. Что это и из чего состоит

Сюжет игры — **граф**: ноды (квест, разговор по рации, триггер, кат-сцена…) соединены «нитками». Нода включается, когда завершилась нода, чья нитка в неё входит.

- Граф рисуют в **редакторе**.
- Сохраняется он в **`story_graph.json`**.
- Unity **импортирует** его: создаёт ассеты квестов и расставляет объекты в сцене.
- В игре **`StoryDirector`** ведёт игрока по графу.

```
┌──────────────── редактор (один HTML-файл) ────────────────┐
│ Tools/StoryEditor/story_editor.html                        │
│  • страница claude.ai (хранилище db)                       │
│  • приложение для Mac (Electron, файлы проекта)            │
│  • любой браузер (localStorage) — запасной режим           │
└───────────────┬────────────────────────────────────────────┘
                │ пишет / читает
                ▼
  Assets/01_GAME/08_Story/story_graph.json   ◄── scene_catalog.json (Unity → редактор: списки целей)
                │
                │ Tools → Weapon Keeper → Story → Import Story Graph
                ▼
  Unity (редактор): QuestData-ассеты, StoryQuestCatalog, strings.csv, объекты в TestScene
                │
                ▼
  Unity (игра): StoryDirector → квесты, RadioCallUI, StoryCutscenePlayer, триггеры, события → сейв
```

### Карта файлов

| Путь | Что это |
|---|---|
| `Tools/StoryEditor/story_editor.html` | **Весь редактор**: разметка, стили и JS в одном файле, без сборки и зависимостей (только шрифты Google). ~3300 строк. |
| `Tools/StoryEditor/app/` | Приложение для Mac (Electron): `main.js` (окно, меню, диск), `preload.js` (мост `storyHost`), `storage.js` (атомарная запись, копии, черновик), `test/storage.test.js`, `package.json` (сборка .dmg), `README.md`. |
| `Tools/StoryEditor/tests/` | Автотесты редактора и приложения (`run.sh`, `web.test.js`, `app.e2e.js`, `wrap.py`, `mockdb.html`). |
| `.github/workflows/story-editor-mac.yml` | Сборка `.dmg` на GitHub Actions (macos-latest) и релиз `story-editor-v<версия>`. |
| `Assets/01_GAME/08_Story/` | Данные: `story_graph.json`, `scene_catalog.json`, `Audio/` (заглушки звуков рации), `Cutscenes/` (Timeline-ассеты). Создаётся инструментами, в git может не быть до первого сохранения. |
| `Assets/01_GAME/01_Scripts/Story/` | Игровая часть: `StoryGraphData`, `StoryDirector`, `StoryScene`, `StoryTrigger` + `StoryTriggerKinds`, `RadioCallUI`, `StoryCutscene`, `StoryCutscenePlayer`, `StoryObject`, `StoryQuestCatalog`. |
| `Assets/01_GAME/01_Scripts/Editor/Story/` | Меню Unity: `StoryEditorTools` (импорт, каталог, миграция квестов, Create Cutscene), `StoryUIBuilder` (префабы рации и кат-сцен, модель рации, звуки). |
| `Assets/01_GAME/04_Data/Quests/Story/`, `04_Data/Story/StoryQuestCatalog.asset` | Ассеты квестов графа (создаёт импорт). |
| `Assets/01_GAME/03_Prefabs/UI/Story/` | `RadioCallUI.prefab`, `StoryCutscenePlayer.prefab` (строит `StoryUIBuilder`). |
| `Docs/Story/STORY_GRAPH.md` | Инструкция для пользователя. |

**Важно для агента в облаке.** `.unity`, `.prefab`, `.asset`, `.mat` в проекте **бинарные**. Их нельзя читать и править как текст, Unity Editor в облаке нет.

Всё, что связано со сценой и ассетами, делается кодом редакторных инструментов (`StoryEditorTools`, `StoryUIBuilder`). Пользователь запускает их из меню у себя.

---

## 2. Формат `story_graph.json`

Единственный контракт между редактором и Unity. Unity читает его через `JsonUtility`, отсюда два ограничения:
- **словарей нет** — локализованные тексты хранятся массивами `{lang, text}`;
- **незнакомые поля JsonUtility молча пропускает**. Благодаря этому редактор может хранить в файле свои данные (линии, рамки, картинки), и Unity на них не спотыкается.

```jsonc
{
  "version": 1,                         // FILE_VERSION в редакторе; см. «Миграции»
  "languages": ["ru", "en"],            // порядок = приоритет; первый — основной (обязателен для названий)
  "nodes": [ { /* нода, см. ниже */ } ],
  "links": [ { "from": "n_start", "fromPort": "out", "to": "q_x", "toPort": "in" } ],

  // только для редактора (Unity не читает):
  "storylines": [ { "id": "main", "title": "Основная линия" } ],
  "groups": [ { "id": "g_ab12cd", "storyline": "main", "title": "Пролог", "x": 0, "y": 0, "w": 520, "h": 320, "color": "--t-radio" } ],
  "decor":  [ { "id": "d_…", "storyline": "main", "kind": "shape|icon|image", "shape": "rect|round|ellipse|arrow|line|text",
                "icon": "radio", "image": "img_…", "text": "…", "x": 0, "y": 0, "w": 200, "h": 60,
                "color": "--ink", "opacity": 1, "rot": 0, "fill": true } ],
  "annotations": [ { "id": "a_…", "from": { "kind": "node|decor", "id": "…" }, "to": "<id ноды>" } ],  // пунктирные связи
  "images": { "img_…": "data:image/webp;base64,…" }
}
```

### Нода — общие поля
`id` (уникальный, генерируется: `q_xxxxxx` для квестов, `n_xxxxxx` для остальных), `type`, `x`, `y` (позиция на холсте), `storyline` (линия; редактор).

### Поля по типам (`type`)

| type | Поля | В Unity (`StoryDirector.Activate`) |
|---|---|---|
| `start` | — | завершается сразу; корень графа |
| `quest` | `questId` (латиница/цифры/_; **не менять после релиза — по нему сейв находит квест**), `title`/`description` (`[{lang,text}]`), `questType` (`CleanStains`/`RepairPoints`/`ShelveItems`/`DeliverItem`), `targetCount`, `target` (PersistentId точки/пятна/полки), `category` (имя ассета ShelfCategory), `item` (itemId), `zone` (zoneId), `xp`, `money` | запускает `QuestData` через `QuestManager`; завершается по `OnQuestCompleted` |
| `radio` | `speaker` (`[{lang,text}]`), `portrait` (id из `RadioCallUI.portraits`), `lines: [{seconds, text:[{lang,text}]}]` | очередь `RadioCallUI`; завершается, когда разговор закончен |
| `trigger` | `trigger` (вид, см. §6.4), цели `target`/`category`/`item`/`zone`/`orderId`/`lootBoxId`/`enemyType`/`questId`/`skillId`, `count`, `value` | `StoryTrigger.Create(kind)`; завершается при срабатывании |
| `wait` | `seconds` | корутина, игровое время |
| `and` | — | включается, когда завершены **все** входящие |
| `action` | `action` (`giveMoney`/`giveXp`/`enableObject`/`disableObject`/`spawnEnemies`), `value`, `target` (storyId объекта; у `spawnEnemies` — PersistentId точки спавна). Для `spawnEnemies`: `where` (`point`/`player`), `spawns: [{enemyType, count}]` (пустой тип — тип точки), `aggro`, `radius` | выполняется и завершается сразу |
| `cutscene` | `target` (storyId `StoryCutscene`), `skippable` (bool, по умолчанию true), `hideHud` (bool, true) | очередь `StoryCutscenePlayer`; завершается по окончании/пропуску |
| `note` | `text` | в игре не участвует |

В редакторе внутри нод тексты — объекты `{ru: "...", en: "..."}`. Конвертация на границе: `toFileFormat`/`fromFileFormat` (ключи `LOC_KEYS = title, description, speaker` + `lines[].text`).

### Правила графа (одинаковые в редакторе-проверке и в Unity)
- Нода включается, когда завершилась **любая** нода с ниткой в неё; `and` — когда **все**.
- Корни: `start` и `trigger` без входящих ниток (слушает с начала игры).
- `note` никогда не включается.
- Нитки только внутри одной линии (редактор). Линии в игре идут параллельно; зависимость между линиями — триггер `questCompleted`.

### Совместимость и миграции
- **Незнакомые поля верхнего уровня** редактор сохраняет как есть (`state.graph.extra`, `KNOWN_TOP`). Незнакомые поля нод тоже сохраняются: нода — обычный объект, копируется целиком.
- **`FILE_VERSION`** (сейчас 1) и **`MIGRATIONS`** в редакторе. Меняете формат несовместимо:
  1. поднимите `FILE_VERSION`;
  2. допишите шаг `MIGRATIONS[старая] = (data) => { …; data.version = новая; return data; }`;
  3. старые шаги не трогайте.

  Добавочные поля (как `storylines`, `groups`) версию **не** поднимают.
- Файл новее редактора открывается **только для просмотра** (`migrateGraph` бросает `err.newer`).
- Unity: `StoryGraphData.Parse` читает любую версию. Новые поля в C# добавляйте с безопасными значениями по умолчанию (как `skippable = true`).

---

## 3. Редактор (`story_editor.html`)

### 3.1 Принципы
- **Один файл, без сборки и библиотек** — vanilla JS + SVG. Файл работает:
  - как страница claude.ai: артефакт https://claude.ai/artifact/P8aAP6hr6d9pWyf9W1pciN;
  - внутри Electron;
  - в любом браузере.

  Не добавляйте внешние скрипты: страница claude.ai разрешает скрипты только с cdnjs/jsdelivr, а приложение должно работать офлайн.
- Весь JS — внутри одного IIFE, `'use strict'`. Файл разбит на разделы с заголовками `// ───── Название ─────`, по ним удобно искать.
- Комментарии и тексты интерфейса — по-русски, идентификаторы — по-английски (правило проекта).
- Маркер `<!-- story-host-api: 1 -->` в начале файла — минимальная версия моста приложения для Mac (см. §4.3).

### 3.2 Разделы файла (по порядку)

| Раздел | Что внутри |
|---|---|
| `<style>` | Токены цветов на `:root` (светлая тема), тёмная — `@media (prefers-color-scheme: dark)` и `:root[data-theme="dark"]`. Цвета типов нод: `--t-start`, `--t-quest`, `--t-radio`, `--t-trigger`, `--t-wait`, `--t-and`, `--t-action`, `--t-note`, `--t-cutscene`. Раскладка: `.work` — CSS-grid `canvas | vsplit | insp` / `hsplit` / `pal`. |
| Разметка | Шапка (`.bar`: статус, поиск, кнопки), палитра `#palette` (вкладки `#palTabs`, `#palItems`), холст `#canvas` → `#world` (`#board`, `svg#wires`, `#nodes`), вкладки линий `#lineTabs`, окна `.sheet` (`sheetIssues`, `sheetColors`, `sheetLangs`, `sheetCatalog`, `sheetImport`, `sheetExport`, `sheetWelcome`, `sheetDraft`, `sheetConflict`, `sheetBroken`, `sheetHistory`), инспектор `#inspector`, разделители `#splitV`/`#splitH`. |
| Справочники | `TYPES` (типы нод: подпись, цвет, есть ли вход/выход, описание), `QUEST_TYPES`, `TARGETS` (вид цели → список каталога), `QUEST_FIELDS`, `TRIGGERS`, `ACTIONS`, `CATALOG_LISTS`, `FILE_VERSION`, `LOCAL_KEY`. |
| Состояние | `state` (см. 3.3), хелперы `$`, `el`, `uid`, `langs`, `locText`, `normalizeGraph`, `setGraph`, `inLine`, `lineTitle`, `newNode` (поля по умолчанию каждого типа). |
| История (Ctrl+Z) | `snapshot`/`restore` (JSON всего графа, кроме `images`), `beginEdit(coalesce)`, `undo`/`redo`, `undoOnce(source)` (защита от двойной команды меню в Electron), `changed(structural)`. |
| Отрисовка | `applyView` (transform `#world`), `nodeSummary` (тело ноды на холсте по типу), `buildNodeEl`/`placeNodeEl`, `renderNodes`/`renderNodesLight`, `portPos`/`wirePath`/`renderWires`, `renderAll`, `renderBanner`. |
| Инспектор | `catalogList`/`catalogName` (у `quests` подмешаны квесты самого графа), генераторы полей `fieldWrap`, `textInput`, `selectInput`, `catalogInput` (input + datalist из каталога), `locInput` (языки, переводы свёрнуты), `checkField`; `renderInspector` и `renderQuest`/`renderRadio`/`renderTrigger`/`renderAction`, ветка `cutscene`. |
| Проверка графа | `validate()` → `state.issues [{sev: err/warn/info, node, text}]`; достижимость от корней, обязательные поля по справочникам, каталог (`checkCatalog`), переводы. `renderIssues`, `issueWhere` (с линией). |
| Холст: вставка в нитку, пунктир | `canSplice`/`wireAt` (`isPointInStroke` по `path.hit[data-link]`)/`spliceIntoLink`, подсветка `splitHover` (класс `split`); пунктирные связи `annotations`: `appendAnnoPorts` (точки `.aport` у заметок и оформления, занятые + одна свободная), `annoSource` (с учётом поворота), `renderAnnos` (в `renderWires`, класс `anno`, выделение `state.sel.anno`), `addAnnotation`. Точки `.aport` лежат под слоем ниток — `pointerdown` ищет их через `elementsFromPoint`. |
| Рамки и оформление | `SWATCHES`, `SHAPES`, `ICONS` (свои SVG 24×24), `iconSvg`, `shapeSvg`, `renderBoard`, `buildGroupEl`/`buildDecorEl`, `selectItem`, `boardPointerDown`/`boardPointerMove` (перенос, размер, рамка тащит содержимое), `addGroup`/`addDecor`, инспекторы рамки/оформления, картинки (`readImageFile` → 512 px WebP, `importImages`, drop файлов на холст, `removeUnusedImages`). |
| Поиск | `nodeSearchTexts`, `searchAll`, `renderSearch`, `applySearchHighlight` (`.hit`/`.dim`), `goToResult` (переключает линию), клавиши в `#searchInput`. |
| Сюжетные линии | `renderTabs`, `switchLine` (запоминает `state.views`), `addLine`, `renameLine`, `deleteLine` (только пустую), `toast`. |
| Мышь, касания, клавиатура | `pointerdown/move/up` на холсте: средняя кнопка → всегда панорама, не левая → ничего; `.aport` → пунктирная связь, пунктир → выделение, порт → нитка, полоса прокрутки `.n-body` → ничего, нода → перенос (свободную можно бросить на нитку), `.bi` → рамка/оформление (`.rz` — размер, `.rt` — поворот), иначе панорама; колесо над прокручиваемым `.n-body` листает ноду; колесо — масштаб; щипок; `fitAll`, `focusNode`, `selectNode`, `connect`, `deleteSelection`, `duplicateSelection`, `addNode(type, at, splitIdx)`; перетаскивание из палитры (`palPointerDown(e, payload)` → призрак → drop, на нитку — в разрыв); двойной щелчок по карточке — `addAtCenter(payload)`; глобальные клавиши (Del, Ctrl+D/Z/Y/F, Esc). |
| Панели | `closeSheets`/`openSheet` (окна с `data-sticky` закрываются только своими кнопками), языки, каталог (`applyCatalogText`), импорт/экспорт JSON. |
| Оформление: тема и цвета | `prefs` (`wk-story-prefs-v1` + db `graph/appearance`), `COLOR_VARS` (меню «Цвета»), `applyPrefs`. |
| Формат файла | `toFileFormat`, `MIGRATIONS`, `migrateGraph`, `KNOWN_TOP`, `fromFileFormat`. |
| Хранение | `caps`, `saved`, `scheduleSync` (дебаунс), `sync` (ветки `db`/`file`/`local`), `syncDb`, `saveCatalog`, `loadFromDb`, `watchDb`. |
| Приложение для Mac | объект `file`, `syncFile`, `flushFile`, `initFile`, черновик (`writeDraft`, `offerDraft`), конфликт (`onFileChanged`, `openConflict`), битый файл (`enterBroken`), история версий (`renderBackupList`, `restoreBackup`), `onMenu`. |
| Пример | `exampleGraph()` — показывается, пока граф пуст; первая правка делает его «своим» (`adoptExample`). |
| Размеры панелей | `layout` (`wk-story-layout-v1` в localStorage), `makeSplitter`. |
| Инспектор «Спаун врагов» | `ensureSpawnFields`, `renderSpawnEnemies` (где, точка, разброс, «сразу нападают», строки тип × сколько), `spawnSummary` (тело ноды). |
| Запуск | `buildPalette` (вкладки «Ноды/Формы/Иконки/Картинки»), `init()` — выбор режима хранения и загрузка. |

### 3.3 Модель данных в памяти
```js
state = {
  graph: { languages, nodes: Map<id, node>, links: [], storylines: [], groups: [], decor: [], annotations: [], images: {}, extra },
  line,        // id открытой линии
  views,       // {lineId: {x,y,k}}
  catalog,     // {repairPoints:[{id,name}], …, skills, cutscenes} — из scene_catalog.json
  view,        // {x, y, k} — сдвиг и масштаб холста
  sel,         // {node, link, group, decor, anno} — выделено одно
  isExample, issues, storage,  // storage: 'db' | 'local' | 'file' | 'none'
}
```
- Граф всегда меняйте через **`setGraph(g)`**: она нормализует линии, рамки, оформление и чинит `state.line`.
- Ноды хранятся в `Map`; линия ноды — `node.storyline`. На холсте рисуются только элементы текущей линии (`inLine`).

### 3.4 Как делается любая правка (обязательный шаблон)
```js
beginEdit(false);           // снимок ДО правки (для Ctrl+Z); true — «набор текста», серия правок = один шаг
/* меняем state.graph */
changed(true);              // true — перерисовать всё; false — только тела нод, нитки и рамки
```
`changed` делает три вещи:
1. перерисовывает;
2. запускает `validate()`;
3. вызывает `scheduleSync()` — сохранение с задержкой 0,7 с, в приложении 1 с.

Правка без `beginEdit` не отменяется, правка без `changed` не сохраняется.

### 3.5 Режимы хранения (`init()` выбирает)

| Режим | Когда | Как хранит |
|---|---|---|
| `file` | есть `window.storyHost` (приложение для Mac) | файл проекта через мост; черновик, копии, конфликты — §4 |
| `db` | страница claude.ai и `window.claude.use('db')` доступен | документы: `nodes/<id>` (по ноде), `graph/links`, `graph/meta` (языки, линии), `graph/groups`, `graph/decor`, `graph/annotations`, `images/<id>`, `graph/catalog`, `graph/appearance`; сравнение с `saved` — пишется только изменённое |
| `local` | всё остальное | `localStorage['wk-story-graph-v1']` (+ `-catalog`); баннер «только в этом браузере» |

Артефакт на claude.ai — отдельная опубликованная копия HTML. Сам с git он **не обновляется**: чтобы страница получила новую версию, её нужно переопубликовать тем же файлом. Приложение для Mac берёт редактор из проекта и обновляется через `git pull`.

### 3.6 Каталог сцены
`scene_catalog.json` делает Unity (**Export Scene Catalog**). Формат: `{repairPoints:[{id,name}], breakables, stains, shelves, categories, zones, items, enemySpawns, enemyTypes, orders, lootBoxes, storyObjects, quests, speakers, skills, cutscenes}`.

- В редакторе он даёт выпадающие списки целей (`catalogInput` + `<datalist>`) и предупреждения «нет в каталоге».
- Приложение для Mac читает каталог из проекта само, а страница — через кнопку «Каталог».
- Новый список нужно добавить в трёх местах:
  1. `SceneCatalog` + `ExportSceneCatalog` (C#);
  2. `CATALOG_LISTS`;
  3. при необходимости `TARGETS` в редакторе.

---

## 4. Приложение для Mac (`Tools/StoryEditor/app`)

### 4.1 Как устроено
- **Electron 44**, `contextIsolation`, `sandbox`, без `nodeIntegration`. Страница видит только мост `window.storyHost` (`preload.js`).
- **Какой редактор открывается** (`main.js → buildPage()`):
  - если в выбранном проекте есть `Tools/StoryEditor/story_editor.html` и его `story-host-api` ≤ `HOST_API` — берётся он. Поэтому правки редактора доходят до пользователя через `git pull` + **Вид → Перезагрузить**;
  - иначе открывается встроенная копия `app/editor/story_editor.html` (кладётся при сборке `scripts/copy-editor.js`), и баннер просит обновить приложение.
- **Папка проекта** хранится в `userData/settings.json`. Проект распознаётся по наличию `Assets/01_GAME`.
- **Копии и черновик** лежат в `~/Library/Application Support/Weapon Keeper Story/`:
  - `backups/<sha1 пути проекта>/`;
  - `drafts/<ключ>.json`.

  Переменная `WK_STORY_USER_DATA` задаёт другую папку для тестов.

### 4.2 Мост `storyHost` (preload.js → ipc `story:*` → storage.js)
| Метод | Что делает |
|---|---|
| `apiVersion`, `getInfo()` | версия моста; `{apiVersion, shellVersion, editorSource, project:{path,name,graphPath}}` |
| `chooseProject()` | диалог выбора папки; перезагружает окно |
| `readGraph()` | `{exists, text, mtimeMs, error?}`; первое чтение за сеанс делает копию `open` |
| `writeGraph(text, expectedMtimeMs)` | атомарная запись; если файл изменён снаружи — `{conflict:true}` и **ничего не пишет**; раз в 5 мин копия `auto` |
| `readCatalog()` / `writeCatalog(text)` | `scene_catalog.json` |
| `listBackups()` / `readBackup(id)` / `backupNow(text, reason)` | история версий (последние 100 + по одной за день × 30 дней) |
| `readDraft()` / `writeDraft(text, baseMtime)` / `clearDraft()` | черновик несохранённого |
| `showBackups()`, `revealGraph()` | открыть в Finder |
| `setDirty(bool)` | есть ли несохранённое (закрытие окна ждёт записи) |
| `onFileChanged(cb)`, `onMenu(cb)`, `onFlushRequest(cb)` | события: файл изменён снаружи; команда меню (`undo`, `redo`, `history`, `export`); «допиши всё перед закрытием» |

### 4.3 Защита данных — инварианты (не ломать)
1. **Запись атомарная** (`writeAtomic`): tmp → fsync → rename.
2. **Файл, изменённый снаружи, не затирается.** `writeGraph` сравнивает mtime; при расхождении открывается окно «какую версию оставить», а вторая версия уходит в копию.
3. **Битый файл не перезаписывается** (`file.blocked = 'corrupt'`). Файл новее редактора — только просмотр (`'newer'`).
4. **Черновик** пишется через 250 мс после правки. После сбоя приложение предлагает восстановить черновик, отброшенный вариант уходит в копию.
5. **Закрытие окна** ждёт `flushFile()`. Если записать не удалось, приложение предупреждает и не закрывается молча.
6. **Любое восстановление** сначала сохраняет текущее состояние в копию.

### 4.4 Версии и выпуск
- **Мост.** Методы моста только **добавляются**. Нужен новый метод — поднимите одновременно:
  - `HOST_API` в `main.js`;
  - `apiVersion` в `preload.js`;
  - маркер `story-host-api` в редакторе.

  Затем выпустите новую сборку. Старое приложение с новым редактором откроет встроенную копию, а не сломается.
- **Сборка `.dmg`** — `.github/workflows/story-editor-mac.yml`:
  - запускается при изменениях в `Tools/StoryEditor/app/**` (PR — только сборка; `main` — ещё и релиз `story-editor-v<version>`);
  - перед правкой оболочки поднимите `version` в `app/package.json`.
- **Подпись.** Подпись ad-hoc (`mac.identity "-"`), платной подписи Apple нет. На первом запуске: «Системные настройки → Конфиденциальность → Всё равно открыть».
- **Когда пересборка не нужна.** Правки только в `story_editor.html` пересборки не требуют.

---

## 5. Связь с Unity — инструменты редактора Unity

Меню **Tools → Weapon Keeper → Story** (`StoryEditorTools.cs`):

| Пункт | Что делает |
|---|---|
| **Import Story Graph** | 1) `ValidateGraph` (ошибки — стоп, список в консоли); 2) `ImportQuests`: для каждой ноды `quest` создаёт/обновляет `QuestData` в `04_Data/Quests/Story/Quest_<questId>.asset` (существующий ассет с тем же `questId` обновляется на месте; `prerequisiteQuests` пусты, `requiredLevel = 1` — порядок задаёт граф); 3) `StoryQuestCatalog.asset` = список квестов графа; 4) `WriteQuestTexts`: названия/описания в `strings.csv` (ключи `quest.<questId>.title/desc`, раздел «Сюжет: квесты», новый язык — новый столбец); 5) собирает префабы, если их нет (`RadioCallUI`, `StoryCutscenePlayer`); 6) открывает `TestScene`, `SetupScene`: объект `Story` (`StoryDirector` с `graphJson`, `questCatalog`, `radio`, `cutscenes`, `itemCatalog`), экземпляры префабов, их ссылки (`WireRadio`, `WireCutscenes`: что выключать на время разговора/кат-сцены, какие канвасы HUD гасить), `SaveLoadService.storyDirector`; сохраняет сцену. Повторный импорт безопасен. |
| **Export Scene Catalog** | `scene_catalog.json` по открытой сцене + ассетам. Объектам-целям без `PersistentId` предлагает его добавить (сцену надо сохранить). |
| **Export Existing Quests To Graph** | квесты `QuestManager` → стартовый граф (связи из `prerequisiteQuests`). |
| **Create Cutscene** | объект `Cutscene_<id>`: `StoryCutscene` + `PlayableDirector` (Wrap Hold) + `CutsceneCamera` + новый Timeline в `08_Story/Cutscenes/`. |
| **Rebuild Radio UI** | пересобрать префаб рации. |
| **Open Story Editor (web)** | открыть страницу claude.ai. |

`StoryUIBuilder.cs` строит префабы из кода через `UIBuilderKit` (из `Editor/Settings`):
- рация: плашка вызова, строка субтитров, звуки-заглушки WAV, модель рации из примитивов;
- кат-сцена: чёрные полосы и подсказка пропуска с полоской.

---

## 6. Игровая часть (Unity runtime)

### 6.1 `StoryGraphData`
Классы для `JsonUtility`:
- `StoryGraphData`, `StoryNodeData` — плоский класс со **всеми** полями всех типов;
- `StoryLinkData`, `StoryLocText`, `StoryRadioLine`;
- константы типов;
- `StoryText.Pick/Get/Languages` — выбор текста на текущем языке с откатом на английский.

Новое поле ноды нужно добавить в `StoryNodeData` со значением по умолчанию.

### 6.2 `StoryDirector` — алгоритм
- `[DefaultExecutionOrder(-1100)]`, раньше `SaveLoadService` (-1000).
  - `Awake`: разбирает граф (`nodes`, `outgoing`, `incoming`) и регистрирует квесты графа в `QuestManager.RegisterStoryQuests`, чтобы сейв находил их по id.
- **Старт.** Через кадр после `Start` (`RunNextFrame`):
  1. собирает `StoryScene` (ссылки на системы сцены);
  2. повторно применяет `enable/disableObject` из уже пройденных нод;
  3. включает все ноды, готовые по `IsReady`.
- **Включение и завершение.**
  - `Activate(node)` — `switch` по типу, см. таблицу в §2.
  - `Complete(node)` отмечает ноду в `done`/`doneOrder`, снимает её триггер и ожидание, поднимает `OnNodeCompleted` и включает готовые следующие ноды.
- **Сейв.**
  - `CaptureDoneNodes()` → `SaveGameData.storyDoneNodes`: только завершённые ноды, в порядке завершения.
  - `RestoreDoneNodes()` зовёт `SaveLoadService.ApplyPhaseA` без событий.
  - Всё остальное выводится заново: квест продолжится (его прогресс восстановил `QuestManager`), триггер перепроверит сцену, недослушанная рация или кат-сцена придёт снова, награды «Событий» не повторятся.
- **Ошибки в данных** (нет объекта или сервиса в сцене) дают предупреждение `[Story]` один раз (`StoryScene.Warn`). Нода проходит, чтобы сюжет не застрял. Исключение — нет квеста: тогда `LogError`, и сюжет стоит на этой ноде.
- **Отладка.** Галочка `logFlow` у объекта `Story` пишет в консоль включения и завершения нод.

### 6.3 `StoryScene`
Доступ к системам сцены:
- `Quests`, `Wallet`, `Progression`, `TidyUp`, `Equipment`, `Shipping`, `Supply`, `Items`, `Skills` — берутся из полей `StoryDirector` или ищутся `FindAnyObjectByType`.

Поиск объектов по id (среди неактивных тоже):
- `FindByPersistentId<T>`;
- `FindStoryObject`;
- `FindCutscene`;
- `FindItem`.

### 6.4 Триггеры
`StoryTrigger` — абстрактный класс. Однократное срабатывание, отписка и счётчик `Count()` уже реализованы в нём. Наследнику остаётся:
- `Subscribe()` — подписаться на событие игры. `false` означает, что цели нет в сцене;
- `Unsubscribe()`;
- `IsSatisfied()` — условие уже выполнено. Нужно для состояний, чтобы триггер работал и после загрузки сейва.

`Matches(wanted, actual)`: пустое `wanted` значит «любой».

| id | Подпись в редакторе | Событие игры | Поля |
|---|---|---|---|
| `repairPointRepaired` | Починена точка ремонта | `RepairPoint` | target |
| `breakableBroken` | Сломан объект | `Breakable` | target |
| `stainCleaned` | Отмыто пятно | `CleanableStain` | target |
| `shelfItemsPlaced` | Поставлено на полки | полки категории | category, count |
| `itemDelivered` | Доставлено в зону | `DeliveryZone` | zone?, item?, count |
| `itemPickedUp` | Подобран предмет | инвентари | item |
| `levelReached` | Достигнут уровень | `PlayerProgression` | value |
| `restorationPercent` | Восстановление здания ≥ % | `BuildingRestorationTracker` | value |
| `moneyReached` | На счету денег ≥ | `PlayerWallet.OnBalanceChanged` | value |
| `enemyKilled` | Убит враг | `EnemySpawnPoint.OnEnemyKilled` (и дополнительные враги точки из `SpawnExtra`); без точки — ещё `StoryDirector.OnWaveEnemyKilled` (сюжетные волны вокруг игрока) | target(spawn)?, enemyType?, count |
| `orderPacked` / `orderShipped` | Собран / отправлен заказ | `ShippingService` | orderId? |
| `crateArrived` / `crateOpened` | Приехал / открыт ящик | `SupplyService.OnCrateArrived/OnCrateOpened` | lootBoxId? |
| `crateOrdered` | Куплен ящик поставки | `SupplyService.OnCrateOrdered` | lootBoxId?, count |
| `itemReceived` | Выпал предмет из ящика | `SupplyService.OnItemReceived` (по штуке) | item, count |
| `itemPurchased` | Куплен предмет/инструмент в терминале | `SupplyService.OnItemPurchased` ← `NotifyItemPurchased(item)` (**магазин инструментов ещё не сделан** — ему нужно вызвать этот метод) | item?, count |
| `skillUnlocked` | Открыт навык | `PlayerSkills.OnSkillsChanged` + `IsOwned` | skillId |
| `questCompleted` | Выполнен квест (любой линии или вне графа) | `QuestManager.OnQuestCompleted` | questId |
| `nightStarted` / `eveningStarted` | Наступила ночь / вечер | `GameClock.OnNightStarted/OnEveningStarted` (+ `IsNight/IsEvening` — сразу) | — |
| `morningStarted` | Наступило утро | `GameClock.OnDayStarted` | count |
| `dayReached` | Наступил день № | `GameClock.OnDayStarted` + `Day ≥ value` | value |
| `hourReached` | На часах наступил час | `GameClock.OnHourChanged == value` | value (0–23) |
| `satietyBelow` / `satietyAbove` | Сытость ≤ / ≥ | `PlayerConsumption.Satiety`, проверка на `OnHourChanged`/`OnTimeSkipped`/`OnConsumed` | value (0–100) |
| `fatigueAbove` | Усталость ≥ | `PlayerStamina.Fatigue`, проверка на `OnHourChanged`/`OnTiredChanged`/`OnExhaustedChanged` | value (0–100) |
| `playerWinded` | Игрок выдохся | `PlayerStamina.OnWindedChanged(true)` | count |
| `playerSlept` | Игрок поспал | `SleepService.OnWokeUp` | count |
| `itemConsumed` | Съедено / выпито | `PlayerConsumption.OnConsumed` | item?, count |

События времени и выживания (`StoryDirector.ExecuteAction`): `skipToHour` (`GameClock.SkipTo(NextOccurrence(h))`), `skipHours`, `changeSatiety` (`PlayerConsumption.AddSatiety`, ±), `changeFatigue` (`AddFatigue`/`RelieveFatigue`, ±), `refillStamina` (`RefillBar`), `applyEffect` (`PlayerStatusEffects.Apply(FindById(target), часы)`; эффекты — список `effects` каталога). В редакторе у действий флаги `signed` (± без проверки > 0), `zeroOk`, `help`; `fields.target` — ключ `TARGETS`.

Счётчики `count` считаются с момента, когда триггер начал слушать, и после загрузки сейва начинаются заново.

### 6.4.1 Событие «Спаун врагов» (`StoryDirector.SpawnEnemies`)
- `where = "point"`: `EnemySpawnPoint.SpawnExtra(data, count, radius, aggro)` — враги вокруг точки, без респавна; их смерть поднимает `OnEnemyKilled` точки.
- `where = "player"`: `EnemyWaveSpawner.SpawnWave(data, count, list)` (первый в сцене) — кольцом вокруг игрока с постоянным агром; director подписывается на их `OnDied` → `OnWaveEnemyKilled`.
- Тип по `enemyId`: `StoryDirector.enemyTypes` (заполняет импорт: все `EnemyData`), иначе типы точек спавна сцены.
- Враги в сейв не пишутся; событие не повторяется после загрузки (оно в `done`).

### 6.5 Рация — `RadioCallUI`
Состояния `Idle` → `Ringing` → `Talking`, очередь разговоров.
- **Когда можно ответить** (`CanTalk`): нет паузы, окна закрыты (`GameplayInputBlocker`), курсор захвачен, не идёт кат-сцена.
- **На время разговора** (`HidePlayer`/`RestorePlayer`):
  - выключаются компоненты из `disableDuringCall`;
  - оружие уходит в кобуру;
  - предмет из рук прячется;
  - HUD гаснет;
  - поднимается модель рации.
- `[DefaultExecutionOrder(-300)]`: E обрабатывается раньше `PlayerItemInteraction` (-200).
- Строки интерфейса берутся из `strings.csv`: `radio.incoming`, `radio.answer`, `radio.skip`.

### 6.6 Кат-сцены — `StoryCutscene` + `StoryCutscenePlayer`
- **`StoryCutscene`** стоит на объекте в сцене. Поля: `storyId`, `director` (Timeline), `cutsceneCamera`, `durationIfNoTimeline`, `showDuringCutscene[]`.
  - Методы: `Begin()`, `Tick(dt)` (true — показ закончился), `End(skipped)`.
  - При пропуске Timeline перематывается в конец.
- **`StoryCutscenePlayer`** — один на сцену. Держит очередь; пока идёт разговор по рации, показ ждёт.
  - На время показа выключает `disableDuringCutscene`: движение, взаимодействие, инструменты.
  - Прячет оружие и предмет из рук, гасит HUD, если это задано в ноде, показывает полосы `letterbox`.
  - Пропуск — удерживать E `skipHoldSeconds` секунд. Строка `cutscene.skip`.
- **Время** игровое: на паузе показ стоит.

### 6.7 Прочее
- **`StoryObject`** (`storyId`, `target`) — то, что включают и выключают события.
- **`StoryQuestCatalog`** — список `QuestData` графа.
- **Точечные правки существующих систем** ради сюжета:
  - `QuestManager`: `RegisterStoryQuests`, `IsCompleted`, `IsStarted`, повторный старт квеста запрещён; квесты графа не запускаются автоматикой условий;
  - `SaveGameData.storyDoneNodes`;
  - `SaveLoadService.storyDirector`;
  - события `ShippingService.OnShipmentCompleted`, `SupplyService.OnCrateArrived/Opened/Ordered/OnItemReceived/OnItemPurchased`, `EnemySpawnPoint.OnEnemyKilled`;
  - `EnemySpawnPoint.SpawnExtra`, перегрузка `EnemyWaveSpawner.SpawnWave(data, count, list)`, `StoryScene.Director`.

---

## 7. Рецепты: как добавить…

Каждый рецепт — полный список мест. Пропустите одно, и что-то сломается: редактор, импорт или игра.

### Новый вид триггера
1. **C#:** наследник `StoryTrigger` в `Story/StoryTriggerKinds.cs`. Нужно событие в системе игры — добавьте `event Action<…>` точечно; события не должны подниматься при загрузке сейва.
2. **C#:** строка в `StoryTrigger.Registry`. Новое поле цели — поле в `StoryNodeData`.
3. **Редактор:** строка в `TRIGGERS` (`fields`: какие поля спрашивать, `req`: обязательные). Если поле новое:
   - `renderTrigger` — поле ввода;
   - `triggerDetail` — подпись на ноде;
   - `validate` → `checkCatalog`;
   - `fieldLabel`;
   - значение по умолчанию в `newNode('trigger')`.
4. **Каталог**, если цель новая: список в `SceneCatalog`/`ExportSceneCatalog` и в `CATALOG_LISTS`.
5. **Документация:** таблица §6.4 здесь и список в `STORY_GRAPH.md`.
6. **Тесты:** проверка в `tests/web.test.js` — вид есть в списке, обязательное поле проверяется.

### Новое событие (нода «Событие»)
1. Строка в `ACTIONS` (редактор). Поля в `renderAction` / `validate`, если нужны.
2. `case` в `StoryDirector.ExecuteAction`. Если действие должно восстанавливаться после загрузки, как включение объектов, — добавьте его в `IsObjectAction`.
3. Имя действия в списке `actions` в `StoryEditorTools.ValidateGraph`.

### Новый тип ноды
1. **Редактор:**
   - `TYPES` (цвет `--t-<type>`: добавить в оба блока тёмной темы и в `:root`, строка в `COLOR_VARS`);
   - `newNode` — поля по умолчанию;
   - `nodeSummary` — тело на холсте;
   - ветка в `renderInspector`;
   - `validate`;
   - `nodeSearchTexts` — если есть свои тексты;
   - `LOC_KEYS` — если есть локализуемые поля.
2. **Unity:**
   - константа и поля в `StoryNodeData`;
   - `case` в `StoryDirector.Activate` — нода обязана когда-нибудь вызвать `Complete(node)`;
   - `known` и проверки в `ValidateGraph`.
3. **Сейв** ничего менять не нужно: хранятся только завершённые ноды.

### Новый язык
В редакторе — кнопка «Языки». В Unity новый столбец в `strings.csv` появится при импорте. Код менять не нужно.

### Новое поле в формате файла
- **Добавочное поле:**
  - редактор: в нормализацию (`normalizeGraph` или `fromFileFormat`), в `toFileFormat`, в `snapshot`/`restore`, если оно участвует в Ctrl+Z;
  - db: документ в `syncDb`/`loadFromDb`;
  - `KNOWN_TOP` — для верхнего уровня.
- **Несовместимое изменение:** поднимите `FILE_VERSION` и допишите `MIGRATIONS` (§2).

### Новая иконка или форма оформления
- Иконка — строка в `ICONS`: `[подпись, внутренность <svg> 24×24, контур currentColor]`.
- Форма — `SHAPES` и ветка в `shapeSvg`.

### Изменение приложения для Mac
- Поднимите `version` в `app/package.json`.
- Если меняется мост — порядок из §4.4.
- `npm test` в `app/` и `tests/run.sh app`.

---

## 8. Проверка изменений

| Что | Как | Покрытие |
|---|---|---|
| Редактор | `Tools/StoryEditor/tests/run.sh` (нужен `playwright`; свой Chromium — `PW_CHROMIUM=/путь/chrome`; в облачной среде: `NODE_PATH=/opt/node22/lib/node_modules`) | ~116 проверок (в т.ч. вставка в нитку, двойной щелчок, средняя кнопка, прокрутка нод, поворот, пунктирные связи, спаун врагов): ноды, нитки, перетаскивание, Ctrl+Z, экспорт/импорт, каталог, тема/цвета, db-синхронизация, кат-сцена, триггеры, линии, рамки, формы/иконки, картинка файлом, поиск, панели, телефонная ширина |
| Приложение | `tests/run.sh app` (Linux: сам вызовет `xvfb-run`; нужно `npm ci` в `app/`) | ~42 проверки: выбор проекта, запись, закрытие, «сбой» (kill) и черновик, изменение снаружи, конфликт, история, битый файл, файл новее, незнакомые поля, редактор новее оболочки, Cmd+F |
| Хранение | `cd Tools/StoryEditor/app && npm test` | 10 тестов `storage.js` на настоящем диске |
| C# | В облаке Unity нет. Прежние сессии собирали скрипты стендом `csc` с заглушками Unity/URP/Timeline — в репозитории его нет. Минимум: внимательное чтение + пользователь открывает проект в Unity. | — |
| Unity-поведение | Только у пользователя: чек-лист в PR. | — |

Тесты печатают `OK`/`FAIL` на каждую проверку. Перед пушем должно быть 0 `FAIL`.

Меняли разметку — проверьте, что тесты по-прежнему находят элементы. Они ищут по `id`, классам (`.pal-item`, `.node`, `.bi.group`, `.line-tab`…) и подписям.

---

## 9. Подводные камни и договорённости

- **`questId` и `id` нод после выхода игры не менять.** Сейв ссылается на них: `storyDoneNodes` хранит id нод, квесты ищутся по `questId`.
- **Бинарные ассеты Unity** не трогать из облака. Сцену и префабы меняет только код инструментов, а запускает его пользователь.
- **Картинки хранятся в `story_graph.json`** (dataURL, ≤512 px). Файл — это `TextAsset` у `StoryDirector` и попадает в билд. Если картинок станет много, стоит в импорте писать облегчённую копию для игры без `images`/`decor`.
- **Порядок выполнения** важен:
  - `StoryDirector` (-1100) раньше `SaveLoadService` (-1000);
  - `RadioCallUI` (-300) раньше `PlayerItemInteraction` (-200).
- **Ctrl+Z в приложении.** Команда меню и нажатие клавиши могут прийти обе; двойник отсекает `undoGuard`. Щелчок по холсту снимает фокус с полей инспектора, иначе Ctrl+Z отменял бы текст в поле.
- **Окна `data-sticky`** (выбор проекта, конфликт, битый файл, черновик) закрываются только своими кнопками: Esc их не трогает.
- **Конвенции проекта** — см. `CLAUDE.md`:
  - без namespace и asmdef;
  - комментарии и XML-doc по-русски;
  - `[Header]`/`[Tooltip]` на публичных полях;
  - старый `UnityEngine.Input`;
  - C#-события с подпиской в `OnEnable` и отпиской в `OnDisable`;
  - новые интерфейсы и синглтоны — только с обоснованием.
- **Страница claude.ai** не синхронизирована с git (§3.5). Основной инструмент пользователя — приложение для Mac.

## 10. Идеи, которые обсуждались, но не сделаны
- **Экономика и баланс** — отдельная вкладка «Экономика» с таблицами (предметы, ящики, заказы, навыки), расчётом ценности ящиков и маржи заказов, симуляцией. Выгрузка `economy.json` из Unity и загрузка обратно в ассеты по id. Отложено: пользователь ещё не определился с моделью; `ItemData` пока без цены.
- **Магазин инструментов** в терминале — должен вызывать `SupplyService.NotifyItemPurchased(item)`.
- **Прогон сюжета в редакторе** — отказались: граф нелинейный.
- **Экспорт текстов для внешнего переводчика** в CSV — не нужен, пока переводы ведутся в самом графе.
