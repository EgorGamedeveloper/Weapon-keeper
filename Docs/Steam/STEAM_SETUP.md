# Steam: настройка в партнёрке и проверка

Документ для ручного ввода в Steamworks (partner.steamgames.com) всего, что игра ожидает на стороне Steam:
статистика, достижения, Rich Presence, Steam Cloud — и как проверить интеграцию до и после получения своего App ID.

Код: `Assets/01_GAME/01_Scripts/Platform/Steam/` (SteamManager, SteamAchievementsBackend, SteamRichPresence),
`Assets/01_GAME/01_Scripts/Achievements/` (StatsService, StatsEventAdapter), данные — `Assets/01_GAME/04_Data/Achievements/`
(создаёт меню **Tools → Weapon Keeper → Achievements → Create Achievement Assets**).

## 1. Как устроено в игре

- **SteamManager** (префаб `03_Prefabs/Systems/PersistentServices`, живёт весь процесс): `SteamAPI.RestartAppIfNecessary`
  (только в билде и только при `SteamConfig.restartIfNecessary`) → `SteamAPI.InitEx` → `SteamAPI.RunCallbacks()` каждый кадр →
  `SteamAPI.Shutdown()` в `OnDestroy`. Если Steam не запущен — игра работает, достижения пишутся локально.
- **Steam-код компилируется только с символом `STEAMWORKS_NET`** (его сам добавляет пакет Steamworks.NET при импорте) и только
  на Windows/Mac/Linux — все обращения к Steamworks под `#if !DISABLESTEAMWORKS`.
- **StatsService** выбирает хранилище: Steam (если `SteamManager.Initialized`) или файл `achievements.sav`
  (`persistentDataPath`). Статистика — через `SetStat`, достижения — `SetAchievement` + `StoreStats`.
  `RequestCurrentStats` не вызывается: клиент Steam загружает статистику до запуска игры (в текущем Steamworks.NET метода уже нет).
- **StoreStats — редко**: сразу при получении достижения (иначе Steam не покажет уведомление), при выходе в меню и из игры,
  на паузе (не чаще раза в 30 с) и по таймеру раз в 5 минут, если что-то изменилось.
- **Прогресс** достижений хранится в статистике (`SetStat`); `IndicateAchievementProgress` только показывает всплывающее
  уведомление на шагах 25/50/75 % (у каждого достижения свои шаги — поле `progressSteps`).
- Тосты «Достижение получено» игра рисует сама только для локального хранилища — Steam показывает свои уведомления в оверлее.
- **Оверлей** (Shift+Tab) ставит игру на паузу (`GameOverlayActivated_t` → меню паузы).
- **Язык при первом запуске** — язык игры в Steam (`SteamApps.GetCurrentGameLanguage()`), иначе язык системы, иначе английский.

## 2. Статистика (App Admin → Stats & Achievements → Stats)

Для каждой строки — **New Stat**: API Name, Type = INT, Set By = Client, Increment Only, Min/Max, Max Change, Default = 0,
Display Name. Max Change — защита от накрутки (больше за одну отправку статистика не вырастет); значения с запасом.

| API Name | Type | Increment Only | Min | Max | Max Change | Default | Display Name (EN) | Название (RU) |
|---|---|---|---|---|---|---|---|---|
| `enemies_killed` | INT | да | 0 | — | 500 | 0 | Zombies killed | Убито зомби |
| `items_shelved` | INT | да | 0 | — | 500 | 0 | Items shelved | Расставлено товаров |
| `items_picked_up` | INT | да | 0 | — | 1000 | 0 | Items picked up | Подобрано предметов |
| `walls_repaired` | INT | да | 0 | — | 50 | 0 | Walls repaired | Заделано стен |
| `stains_cleaned` | INT | да | 0 | — | 200 | 0 | Stains cleaned | Отмыто пятен |
| `objects_broken` | INT | да | 0 | — | 200 | 0 | Objects broken | Разобрано объектов |
| `quests_completed` | INT | да | 0 | — | 50 | 0 | Tasks completed | Выполнено заданий |
| `items_delivered` | INT | да | 0 | — | 200 | 0 | Items delivered | Доставлено предметов |
| `money_earned` | INT | да | 0 | — | 1000000 | 0 | Money earned | Заработано денег |
| `jumps` | INT | да | 0 | — | 5000 | 0 | Jumps | Прыжков |
| `distance_walked_m` | INT | да | 0 | — | 100000 | 0 | Meters walked | Пройдено метров |
| `play_time_min` | INT | да | 0 | — | 600 | 0 | Minutes played | Минут в игре |
| `max_level` | INT | да | 0 | 1000 | 100 | 0 | Highest level | Максимальный уровень |
| `repairs_completed` | INT | да | 0 | — | 50 | 0 | Repairs completed | Выполнено ремонтов |
| `debris_disposed` | INT | да | 0 | — | 500 | 0 | Debris disposed | Выброшено обломков |

## 3. Достижения (App Admin → Stats & Achievements → Achievements)

Для каждой строки — **New Achievement**: API Name, Set By = Client, Hidden, название и описание (EN — основной язык;
RU — после добавления русского в поддерживаемые языки поля перевода появятся у каждого достижения), иконки.
Для достижений с прогрессом в разделе **Progress** выберите статистику и Min/Max из таблицы — тогда Steam сам
покажет полосу прогресса в библиотеке.

| # | API Name | Name (EN) | Description (EN) | Название (RU) | Описание (RU) | Hidden | Progress Stat | Min | Max |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `ACH_FIRST_KILL` | First Blood | Kill your first zombie. | Первая кровь | Убейте первого зомби. | нет | — | — | — |
| 2 | `ACH_KILL_10` | Pest Control | Kill 10 zombies. | Санитар | Убейте 10 зомби. | нет | `enemies_killed` | 0 | 10 |
| 3 | `ACH_KILL_100` | Undead Nightmare | Kill 100 zombies. | Гроза мертвецов | Убейте 100 зомби. | нет | `enemies_killed` | 0 | 100 |
| 4 | `ACH_FIRST_SHELF` | Grand Opening | Put your first item on a shelf. | Открытие витрины | Поставьте первый товар на полку. | нет | — | — | — |
| 5 | `ACH_SHELF_25` | Merchandiser | Shelve 25 items. | Мерчендайзер | Расставьте по полкам 25 предметов. | нет | `items_shelved` | 0 | 25 |
| 6 | `ACH_SHELF_100` | Model Store | Shelve 100 items. | Образцовый магазин | Расставьте по полкам 100 предметов. | нет | `items_shelved` | 0 | 100 |
| 7 | `ACH_FIRST_WALL` | Bricklayer | Patch up a hole in the wall. | Каменщик | Заделайте пролом в стене. | нет | — | — | — |
| 8 | `ACH_ALL_STAINS` | Spotless | Clean every blood stain in the shop. | Ни пятнышка | Отмойте все пятна крови в магазине. | нет | — | — | — |
| 9 | `ACH_BREAK_5` | Demolition Crew | Pry apart 5 objects with the crowbar. | Ломать — не строить | Разберите ломом 5 объектов. | нет | `objects_broken` | 0 | 5 |
| 10 | `ACH_DEBRIS_10` | Spring Cleaning | Throw 10 pieces of debris in the trash. | Генеральная уборка | Выбросите в мусор 10 обломков. | нет | `debris_disposed` | 0 | 10 |
| 11 | `ACH_FIRST_QUEST` | First Assignment | Complete your first task. | Первое поручение | Выполните первое задание. | нет | — | — | — |
| 12 | `ACH_QUESTS_5` | Store Manager | Complete 5 tasks. | Управляющий | Выполните 5 заданий. | нет | `quests_completed` | 0 | 5 |
| 13 | `ACH_FIRST_DELIVERY` | Courier | Deliver an item for a task. | Курьер | Доставьте предмет по заданию. | нет | — | — | — |
| 14 | `ACH_LEVEL_5` | Seasoned Keeper | Reach level 5. | Опытный кладовщик | Достигните 5-го уровня. | нет | `max_level` | 0 | 5 |
| 15 | `ACH_LEVEL_10` | Armory Veteran | Reach level 10. | Ветеран оружейной | Достигните 10-го уровня. | нет | `max_level` | 0 | 10 |
| 16 | `ACH_MONEY_1000` | First Grand | Earn $1000. | Первая тысяча | Заработайте 1000 $. | нет | `money_earned` | 0 | 1000 |
| 17 | `ACH_MONEY_10000` | Tycoon | Earn $10,000. | Капиталист | Заработайте 10 000 $. | нет | `money_earned` | 0 | 10000 |
| 18 | `ACH_PLAY_1H` | Full Shift | Play for 1 hour. | Смена отработана | Проведите в игре 1 час. | нет | `play_time_min` | 0 | 60 |
| 19 | `ACH_JUMPS_100` | Grasshopper | Jump 100 times. | Кузнечик | Подпрыгните 100 раз. | да | `jumps` | 0 | 100 |
| 20 | `ACH_WALK_5KM` | Patrol Duty | Walk 5 km. | Обход территории | Пройдите 5 км. | нет | `distance_walked_m` | 0 | 5000 |

`ACH_ALL_STAINS` выдаёт код (все пятна крови в сцене отмыты) — прогресса у него нет. Hidden-достижение до получения
показывается в Steam как скрытое.

### Иконки

- На каждое достижение — **две** иконки: полученное и неполученное (обычно серая версия той же картинки).
- **256 × 256 px, JPG**; без прозрачности; одна стилистика на весь набор.
- В игре иконки задаются в ассетах `AchievementData` (`icon`, `lockedIcon`) — для окна достижений и локальных тостов;
  в Steam их загружают отдельно в партнёрке.

### Публикация

Изменения статистики, достижений, Rich Presence и Cloud не видны игрокам (и игре), пока не опубликованы:
**Publish → Prepare for Publishing → Publish to Steam** (подтверждение вводом слова STEAMWORKS).

## 4. Rich Presence (Edit Steamworks Settings → Community → Rich Presence)

Загрузите файлы токенов `Docs/Steam/rich_presence_russian.vdf` и `Docs/Steam/rich_presence_english.vdf` (по файлу на язык),
затем опубликуйте. Игра ставит `steam_display` = `#Status_MainMenu` (меню) или `#Status_Restoring` (в игре) и подстановку
`progress` = «45%» (процент восстановления здания, обновление раз в 30 с). Проверка — страница Rich Presence Tester
в разделе разработчика Steam Community (`steamcommunity.com/dev/testrichpresence`).

## 5. Steam Cloud (App Admin → Cloud)

Auto-Cloud уже настроен под пути игры: Root = `WinAppDataLocalLow`, Subdirectory = `GEGA Games/Gun Keeper`, Pattern = `*.sav`.

| Файл | Попадает в облако | Почему |
|---|---|---|
| `savegame.sav` | да | сейв игры (`SaveFileService`) |
| `achievements.sav` | да | локальные достижения для билдов без Steam — безвредно |
| `settings.cfg` | **нет** | разрешение, качество и звук зависят от конкретного ПК — расширение намеренно не `.sav` |

## 6. Проверка с тестовым App ID 480 (Spacewar)

Пока своего App ID нет, в `04_Data/Platform/SteamConfig.asset` стоит **480**, а в корне проекта лежит `steam_appid.txt` с `480`
(только для разработки: в билд Unity его не копирует, а `SteamBuildGuard` удалит, если он туда попал).

- Запущен клиент Steam → Play в редакторе: в консоли `[Steam] Steam инициализирован: App ID 480`.
- Shift+Tab — оверлей открывается, игра встаёт на паузу.
- У Spacewar **свои** API-имена статистики и достижений, поэтому наши `SetStat`/`SetAchievement` Steam отклонит — в консоли по
  одному предупреждению на имя. Проверка на 480 — только что вызовы идут и приходят колбэки `UserStatsStored_t`
  (в консоли `[Steam] Статистика и достижения отправлены`). Полноценно — после своего App ID.
- Без клиента Steam (или с выключенным `SteamConfig.enableInEditor`) — локальное хранилище: тосты, окно достижений,
  файл `achievements.sav`; отладочная панель — **F9** (только редактор и Development Build).

### Консоль Steam (steam://open/console)

- `achievement_clear <AppID> <ACH_NAME>` — снять одно достижение (например, `achievement_clear 480 ACH_FIRST_KILL`);
- `reset_all_stats <AppID>` — сбросить всю статистику и достижения приложения.

## 7. Как сменить 480 на свой App ID

1. Завести в Steamworks статистику и достижения по таблицам выше, загрузить иконки и файлы Rich Presence, **опубликовать**.
2. В `04_Data/Platform/SteamConfig.asset` — поле `appId` = ваш App ID; `restartIfNecessary` = включить (с 480 сборка с этим
   флагом намеренно падает — перезапуск ушёл бы в Spacewar).
3. В корне проекта `steam_appid.txt` — ваш App ID (одно число, без перевода строки и BOM). В билд его не класть.
4. Настроить Steam Cloud (раздел 5) и опубликовать.
5. Проверить на билде, запущенном из Steam: достижение → уведомление Steam; `reset_all_stats <AppID>` для повторной проверки.

## 8. Источники

- [Достижения и статистика](https://partner.steamgames.com/doc/features/achievements)
- [ISteamUserStats](https://partner.steamgames.com/doc/api/ISteamUserStats)
- [Enhanced Rich Presence](https://partner.steamgames.com/doc/features/enhancedrichpresence)
- [Языки и коды языков API](https://partner.steamgames.com/doc/store/localization/languages)
- [Steam Cloud](https://partner.steamgames.com/doc/features/cloud)
- [Steamworks.NET](https://steamworks.github.io)
