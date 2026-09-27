# Weapon Keeper

Игрока назначают управляющим оружейного магазина посреди зомби-апокалипсиса. Игровой цикл:
убрать разбросанные внутри обломки (кирпичи, доски, битые окна/двери, пятна крови), отремонтировать
повреждённую проводку и другие точки повреждений (свет, лифт и т.п.), расставить найденное оружие и
патроны по типизированным полкам, постепенно восстанавливая здание и открывая новые возможности
(терминал заказа поставок, продажа оружия дроном, экономика). Подробный, живой план итераций — в
`.claude/plans/` текущей рабочей сессии; этот файл не дублирует его, только фиксирует то, что не
меняется от итерации к итерации.

## Технологии

- Unity **6000.6.0f1**, рендер-пайплайн — **URP 17.6.0**.
- Стрельба — сторонний ассет **Easy Weapons** (`Assets/Easy Weapons/`, компоненты `Weapon`/`WeaponSystem`).
- Анимации/твины — **DOTween** (`Assets/Plugins/Demigiant/DOTween`), уже используется в `ShelfSlot`.
- New Input System подключён (`InputSystem_Actions.inputactions`), но игровой код сейчас читает ввод
  через старый `UnityEngine.Input` (`Input.GetKeyDown`, `Input.GetAxis` и т.д.) — придерживайтесь этого,
  не смешивайте с новым Input System без явного повода.

## Структура проекта

Весь собственный код и контент — под `Assets/01_GAME/`, с нумерованными подпапками:

- `01_Scripts/` — весь C#-код, дальше разложен по фиче (`Inventory/`, `Items/`, `Player/`, `Shelf/`, `UI/`, …).
  Новую фичу — в свою подпапку по этому же принципу.
- `02_Scenes/` — рабочая сцена **`TestScene.unity`**. Это активная сцена разработки, а не
  `Assets/Scenes/SampleScene.unity` (это неиспользуемая заготовка Unity по умолчанию — игнорируйте её).
- `03_Prefabs/` — префабы предметов/UI.
- `04_Data/` — ассеты ScriptableObject (`Item_*.asset`, `Shelf_*.asset` и т.п.).
- `05_Shaders/` — свои шейдеры (`ItemHologram.shader`: призраки, подсказки, полоса установки).
- `06_Materials/` — свои материалы (`M_Brick`, `M_BrickDark`, `M_Rebar`, `M_Concrete`).

Прочее в `Assets/` — сторонние ассеты, не трогать без причины: `Easy Weapons/` (стрельба),
`Standard Assets/` (легаси FPS-контроллер), `Ultimate 10 Plus Shaders/` (шейдер-пак, включая неиспользуемый
пока `Outline.shader`), `TextMesh Pro/`, `02_ART/Firearms/` (арт-пак оружия, исключён из git).

**Важно:** `.unity`, `.prefab`, `.asset`, `.mat` в этом проекте сериализованы **бинарно**, не как YAML-текст —
их нельзя читать/грепать как текст, только через редактор Unity (см. `.claudeignore`).

## Конвенции кода

- **Без C# namespace** и без `.asmdef` — весь код компилируется в `Assembly-CSharp`. Не вводите namespace
  без явного обсуждения с пользователем.
- Комментарии и XML-doc — **на русском**, идентификаторы (классы/методы/поля) — на английском.
- Данные — через `[CreateAssetMenu]` ScriptableObject (`ItemData`, `ShelfCategory`), ассеты живут в
  `04_Data/`. Для новых типов данных следуйте этому паттерну, а не MonoBehaviour-конфигам.
- События — обычные C# `event Action`/`event Action<T>`, подписка в `OnEnable`, отписка в `OnDisable`.
  UnityEvents и event-бас в проекте не используются.
- До итерации 1 в проекте не было ни одного интерфейса и ни одного синглтона. `IPlaceableSlot` (общий
  контракт для `ShelfSlot`/`RepairPoint`) и `BuildingRestorationTracker.Instance` — первые осознанные
  исключения, введены под конкретную проблему (растущая цепочка `if/else` при разборе луча игрока и
  необходимость читать % восстановления из несвязанных систем соответственно). Третье исключение —
  `IInteractable` (терминал, кнопки лифта, разбор коробки отменённого заказа): объекты, которые игрок
  использует кликом, не ставя в них предмет, — по той же причине, что и `IPlaceableSlot`. Звук —
  статический `Audio/SoundPlayer` (звук нужен из десятка несвязанных мест) и `UISoundFeedback.Instance`
  (к нему обращаются статические помощники окон). Не плодите новые интерфейсы/синглтоны по аналогии без
  такой же конкретной причины.
- `[Header]`/`[Tooltip]` — обязательны для публичных полей, инспектор должен оставаться читаемым без
  открытия кода.

## Ключевые системы (на момент начала итерации 1)

- **Подбор/удержание предмета**: `WorldItem` (состояние `InWorld/PickingUp/HeldVisible/CarriedHidden/PlacedOnShelf`),
  наведение — луч из центра вьюпорта в `PlayerItemInteraction`, подсветка при наведении — белая
  силуэтная обводка через сторонний ассет `Assets/AdvancedOutline/` (namespace `ITISKIRUHERE`,
  компонент `AdvancedOutline`, стенсильная mask+fill техника — обводит силуэт ВСЕХ дочерних
  рендереров единой линией, поэтому многосоставные модели вроде M16 (11 рендереров) не рисуют
  обводку по каждой детали отдельно). `WorldItem` требует этот компонент (`[RequireComponent]`),
  держит его выключенным по умолчанию и просто переключает `advancedOutline.enabled` в
  `SetHighlight`. Раньше подсветка шла через `Ultimate 10+ Shaders/Outline` (per-submesh хал-экструд,
  давал обводку по каждой детали M16 отдельно) — заменено по запросу на этот более качественный
  ассет; `Ultimate 10+ Shaders/Outline` в проекте остался, просто больше не используется для этого.
- **Инвентарь уборки** (слот `1`): `InventorySystem`, лимит `maxItemCount` (по умолчанию 5), группировка
  одинаковых предметов в `InventoryEntry`, визуальное отображение в руке — `EquippedItemHolder`.
- **Инвентарь экипировки** (слот `2`): `EquipmentInventory` + `PlayerInventoryModeController` (`Q` —
  экипировать/снять). Правило: экипировано может быть только одно оружие одновременно — экипировка
  нового автоматически возвращает предыдущее в инвентарь уборки.
- **Стрельба**: связка с Easy Weapons через `EquipmentWeaponBridge` — при экипировке `ItemData` с заданным
  `weaponPrefab` инстанцирует префаб Easy Weapons и регистрирует его в `WeaponSystem.weapons[]`.
- **Полки**: `Shelf`/`ShelfSlot`/`ShelfCategory`, типизированная совместимость, опциональная стопка
  (`isStackSlot`), анимации — секция `GameConfig.shelf`. **ЛКМ только ставит (и подбирает с пола), E
  снимает с полки** (`PlayerItemInteraction.takeKey`): так серия быстрых кликов по стопке никогда не
  забирает только что поставленный предмет. Наведение на предмет на полке → E снимает его; на голограмму
  над стопкой → E снимает верхний; ЛКМ по стопке с подходящим предметом в руке ставит наверх. Подпись
  у прицела — `ItemInfoUI.ShowActions` («ЛКМ — поставить, E — взять»). Триггер ячейки — это
  только «свободное место» (у одиночной ячейки включён, пока она пуста; у стопки стоит на следующем
  «этаже» над верхом и выключается, когда стопка полна), поэтому луч по стоящему предмету попадает в
  сам предмет. Стоящий предмет кинематический, но `Rigidbody.detectCollisions = true`
  (`WorldItem.EnablePlacedColliders`) — с `false` его коллайдеры не видит ни один Raycast. Опыт за
  установку — один раз на экземпляр (`WorldItem.PlacementRewarded`, сохраняется).
  С подходящим предметом в руке все свободные места в радиусе `placementHintRadius` (4 м) светятся
  приглушёнными пульсирующими голограммами, место под прицелом — яркой (`IPlaceableSlot.ShowGhost(item, GhostMode)`).
- **«Видение»** (`Player/PlacementVision`, клавиша V, будущая способность; `GameConfig.vision`: 15 с,
  затухание последние 5 с, перезарядка 0): с предметом в руке — голограммы ВСЕХ подходящих мест сцены и все
  однотипные предметы (`WorldItem.DrawOverlay`) сквозь стены (`GhostMode.Vision`, материал с `ZTest Always`,
  общая яркость `GhostPreviewUtility.SetVisionIntensity`). Места в радиусе подсказок остаются обычными
  подсказками без затухания. Повторное V во время действия игнорируется; HUD нет — есть события и `Remaining`. Призрак-превью при наведении с предметом в руке
  (`GhostPreviewUtility`, общий для `ShelfSlot`/`RepairPoint`) — ярко-жёлтая голограмма на своём шейдере
  `Assets/01_GAME/05_Shaders/ItemHologram.shader` (`Weapon Keeper/Item Hologram`, аддитивный: заливка +
  френель-рим, текстура предмета не участвует). Призрак — только визуал: из копии префаба снимаются
  `WorldItem`/`AdvancedOutline`/физика/коллайдеры (раньше призраки попадали в сейв и становились
  дубликатами). Предмет долетает из руки (`WorldItem.PlaceOnShelfAnimated`, DOTween) и принимает масштаб
  префаба — как призрак и как после загрузки; в полёте никакого эффекта. По приземлению по нему снизу
  вверх проходит светящаяся полоса (`Interaction/PlacementScanEffect`, тот же шейдер): она рисуется
  отдельным проходом через `Graphics.RenderMesh` по всем сабмешам ИСХОДНЫХ мешей и не трогает массивы
  материалов рендереров, поэтому с обводкой наведения не конфликтует. Шейдер в Always Included Shaders
  (ищется через `Shader.Find`). Все прежние варианты эффекта (дисолв, угасающая обводка, наложение
  голограммы Force Field) заменены по запросам пользователя.
- **`Assets/AdvancedOutline/Scripts/AdvancedOutline.cs` пропатчен** (помечено `ПАТЧ Weapon Keeper`): убран
  `[ExecuteAlways]`, `Awake` больше не трогает рендереры, `RefreshRenderers` не работает вне Play Mode.
  До патча `Awake` даже выключенного компонента клонировал меши и дописывал временные материалы, а в
  редакторе их никто не снимал — при любом сохранении сцены/префаба они писались как NULL (так трижды
  «слетали» меши у `M16.prefab`, последний раз — 8 деталей; восстановлено из
  `02_ART/Firearms/Prefabs/AR/AR2/AR2.prefab`). При обновлении ассета патч нужно перенести. Правьте поля
  префабов предметов по-прежнему через `LoadAssetAtPath` + запись полей + `SetDirty` + `SaveAssets`, без
  `LoadPrefabContents`. Подсветка в `PlayerItemInteraction` переключается только при смене цели (раньше —
  каждый кадр, и каждый кадр клонировались меши).
- **`Assets/Easy Weapons/Scripts/Weapon.cs` пропатчен** (`fireBlocked`, а также `ПАТЧ Weapon Keeper`): лучи
  выстрела игнорируют триггеры, `hittableLayers` исключает `Player` (9) и `IgnoreBullets` (24 — на нём
  лежат подбираемые предметы). Слой `IgnoreBullets` — это 24, не 9.
- **Ремонт кладкой**: `RepairPoint` с дочерними `RepairSlot` (место под один кирпич, `layer` — ряд)
  работает как кладка: принимают только пустые места нижнего незаполненного ряда (триггеры остальных
  выключены), предмет расходуется, а место включает свой «уложенный» визуал с той же полосой; сейв хранит
  `RepairPointSave.filledSlots`. Пример — `05_Gameplay/Interactables/BrickWall_Damaged` (стена из
  примитивов, 8 мест в 3 ряда, цель квеста `repairwall`), кирпичи — `05_Gameplay/Items/BrickPile`.
- **Оружие прячется только кодом** (`WeaponHolster.Holster/Unholster` из `EquipmentWeaponBridge` при смене
  режима) — ручной клавиши H нет по решению дизайна.
- **Крутые склоны** (круче `slopeLimit` CharacterController'а): игрок не стоит, а соскальзывает
  (`PlayerCharacterController.ApplySlide`, контакты из `OnControllerColliderHit`); кромки ступеней не
  считаются склоном — проверяется поверхность под центром.
- **Враги** (`01_Scripts/Enemies/`): `EnemySpawnPoint` → `EnemySpawner.Spawn` (снап к NavMesh,
  `Enemy.Initialize`) → `Enemy` блуждает цепочкой коротких шагов вокруг точки появления в пределах
  `EnemyData.wanderRadius` (плавный поворот, «шаркающая» скорость, паузы с осматриванием) → смерть через
  `EnemyHealth.ChangeHealth` (его зовёт Easy Weapons) → респавн. Где NPC может ходить, задаёт только
  NavMesh: компонент `NavMeshSurface` (пакет AI Navigation) на объекте, по которому ходят NPC, после
  изменения геометрии — перепечь (Bake на компоненте). В `TestScene` он на `04_Level/Environment/Floor`
  в режиме Volume по игровой области: пол там 5000×5000 м, без Volume бейк растянулся бы на 5 км.
  Отдельной зоны-коллайдера (`EnemyWanderZone`) больше нет. Гизмо точки спавна: зелёная сфера — под ней
  есть NavMesh, красная — нет.
- **Вход в игру**: сцена `02_Scenes/Bootstrap.unity` (индекс 0 в Build Settings) — `GameBootstrap`
  (DontDestroyOnLoad) с меню «Новая игра/Продолжить/Выход», грузит `TestScene`.
- **Сохранения**: один файл `Save/SaveGameData.cs` через `JsonUtility`, путь —
  `Application.persistentDataPath` (на Windows `%USERPROFILE%\AppData\LocalLow\GEGA Games\Gun Keeper\savegame.sav`) —
  этот путь настроен под Steam Auto-Cloud (Root `WinAppDataLocalLow`, Subdirectory `GEGA Games/Gun Keeper`,
  Pattern `*.sav`), поэтому `PlayerPrefs` для сейвов не используется. Хранится только то, что нельзя
  пересчитать из сцены (позиции предметов, содержимое полок, завершённые квесты, опыт/уровень) —
  счётчики трекеров и физическое состояние `WorldItem` восстанавливаются заново. Загрузка идёт в
  `SaveLoadService` (`[DefaultExecutionOrder(-1000)]`) без единого игрового события — иначе прогресс,
  квесты и опыт задваивались бы при каждой загрузке. Схема версионирована (`SaveGameData.CurrentVersion`,
  миграция в `SaveLoadService.MigrateToCurrent`); сейв проверяется по `ItemCatalog` ДО уничтожения
  предметов сцены. Сохранение выключено, если сцену запустили без `Bootstrap` (Play на `TestScene` в
  редакторе не перезаписывает настоящий сейв).
- **Звук**: `SoundCue` (ScriptableObject: варианты клипов без повтора подряд, разброс громкости/высоты,
  2D/3D, канал настроек, `minInterval`) играется только через `SoundPlayer.Play(cue, pos)` / `Play2D(cue)`
  (оба принимают null — незаданный звук молчит). `SoundPlayer` — пул из 24 голосов, громкость канала
  применяет `AudioChannelVolume.SetBaseVolume` на голосе. `AudioSource.PlayClipAtPoint` не использовать.
  Ассеты — `04_Data/Audio/Cues` и `Surfaces`, пересобираются меню Tools/Weapon Keeper/Audio/Create Sound
  Assets (`Editor/Audio/SoundAssetsBuilder`: таблица звуков + звуки предметов) из CC0-паков Kenney в
  `02_ART/Audio/Kenney` (вручную подкрученные громкости в ассетах меню затирает — правьте таблицу).
  Предметы: `ItemData.pickupSound/placeSound/impactSound` (удар — `WorldItem.OnCollisionEnter`, только
  InWorld, от скорости, первые 0.5 с после загрузки сцены молчат). Шаги — `Player/PlayerFootsteps` по
  пройденному пути, поверхность — `SurfaceTag` на объекте/родителе коллайдера (без тега — бетон).
  Интерфейс — `UI/UISoundFeedback` на префабе `PersistentServices`: наведение/клик по любому Selectable
  (луч EventSystem, разметка кнопок не нужна) и открытие/закрытие окон.
- **Режим работы с объектом** (`Player/PlayerToolActions`): ЛКМ по пятну (руки свободны или лом) или по
  доске с ломом — обзор и ходьба замирают через `GameplayInputBlocker.Acquire(owner, releaseCursor: false)`,
  мышь управляет инструментом; ПКМ — выход, прогресс остаётся на объекте (в сейв частичный прогресс не
  пишется). Курсор блокировщик освобождает только для владельцев-окон, `IsBlocked` = открыто окно — пауза
  и окна открываются поверх режима, а режим сам завершается (свободный курсор = выход из режима). Выключенный
  блокировщиком `CursorLockController` в `OnDisable` освобождает курсор — для режима работы блокировщик сразу
  захватывает его обратно, иначе режим закрывался в тот же кадр. `LastEndFrame` — чтобы ПКМ выхода не бросила
  предмет. Кольцо прогресса — `UI/HoldProgressUI`, подсказка — `ItemInfoUI.ShowHint`.
- **Пятна крови — декали** (`CleanableStain` + URP `DecalProjector`, в URP-рендерере включён Decal Renderer
  Feature): при старте текстура пятна копируется в собственную Texture2D, её альфа — маска; тряпка стирает
  штампами вдоль пути (`ScrubSegment`), при 90% остаток тает, `OnCleaned`. Объект стоит на поверхности,
  декаль проецирует по +Z. Круглые текстуры `06_Materials/Textures/T_BloodStain_0N` (материалы
  `M_StainDecal_0N`) сгенерированы процедурно. Готовые «painter»-ассеты редактора (UV Mask Painter) для
  этого не годятся — они рисуют маски только в Scene view.
- **Инструменты** (слот 2, листаются колесом): `ItemData.toolKind` (`Crowbar`/`Sledgehammer`, вместо
  старого флага `canBreakObjects`), `PlayerInventoryModeController.ActiveToolKind`. Модели из примитивов —
  `03_Prefabs/Tools/CrowbarModel` (пивот — кончик лапки, шест по +Z) и `SledgehammerModel` (пивот — хват);
  они же вложены в мировые префабы `Crowbar`/`Sledgehammer` и стоят под `HandPoint` (`ToolPresenter.GetVisual`).
  Лом снимает доску рычагом (`Breakable.canPry`, раскачивание мышью вверх-вниз, доска поднимается вокруг
  дальнего конца и падает целой); кувалда (`Player/SledgehammerSwing`) бьёт замахом: `Breakable.Hit`
  (`canSmash`, `hitPoints`), урон врагам через `EnemyHealth.ChangeHealth(−урон)`; с кувалдой в руках, как с
  оружием, подбора и установки нет.
- **Кладка по кирпичу** (`Repair/BrickWallSmash` рядом с `Breakable`; пример —
  `05_Gameplay/Interactables/BrickedDoorway/Bricks`): кувалда бьёт ближайший к точке удара кирпич — первый
  удар сдвигает его вглубь, второй выбивает (кирпич рядом с пробоиной — с одного удара); выбитый целый кирпич
  становится предметом `Brick` (его подбирают), половинка — пылью. После `knockoutsToCollapse` (3) выбитых
  следующий удар обрушивает кладку: остальное — в пыль и крошку (`FX_BrickCrumble`), `Breakable.Break`
  (у такой кладки `spawnOnBreak` пуст). Выбитые кирпичи хранятся в сейве (`SaveGameData.brickWalls`) —
  иначе после загрузки стена снова целая, а выбитые кирпичи лежат предметами (бесконечная «добыча»).
- **Ощущение**: подбор летит в руку по дуге, рука «ловит» (`EquippedItemHolder.PlayCatchDip`); установка на
  полку/в кладку — по дуге, «пружинка» и пыль (`Interaction/LandingFeedback`, `FX_DustPuff`). Подсказка у
  прицела появляется/гаснет за ~0.1 с (CanvasGroup в `ItemInfoUI`), тексты — ключи `hud.*` в `strings.csv`.
  «+N XP» у шкалы опыта — `UI/XPGainPopupUI` по `PlayerProgression.OnXPGained`.
- **Сюжет** (граф квестов/рации/триггеров/кат-сцен, редактор `Tools/StoryEditor/` — страница и приложение для Mac,
  импорт в Unity `Tools → Weapon Keeper → Story`, рантайм `01_Scripts/Story/`): устройство, формат `story_graph.json`,
  рецепты «как добавить триггер/ноду/событие» и автотесты `Tools/StoryEditor/tests/run.sh` — в
  `Docs/Story/STORY_EDITOR_ARCHITECTURE.md`. Прочитать перед любой правкой сюжетной системы.
