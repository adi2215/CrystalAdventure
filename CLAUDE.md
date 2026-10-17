# Crystal Adventure — заметки для Claude

## Что за игра

2D-изометрическая головоломка про программирование (ориентация экрана — авто-поворот, в проекте есть Adaptive Performance и Device Simulator, т.е. целится в мобильные). В Player Settings продукт пока называется `BetaCube`. Игрок собирает из карточек программу (вперёд / влево / вправо / назад, функции F1/F2, циклы While, условия по цвету клетки), жмёт Play, и герой-куб выполняет её на поле, собирая кристаллы. Звёзды (1–3) зависят от числа карточек относительно оптимума, подсказок и того, не стоял ли герой на краю.

- Главы по 15 уровней: **Forest / Winter / Sky** (карта глав — `UI/LevelMapMenu.cs`).
- Персонаж-помощник в диалогах и туториале: в `TutorialDirector` её зовут **Эла**, в коде нового ядра — **Luma**. Имя надо уточнить у автора и привести к одному.
- Враги и боссы (ядро CrystalCore): `LevelSt.enemies` (+ `heroHp`, `superAttacks`), карточки `Attack` / `Shield` / `SuperAttack` (новые значения `ItemType` — только в конец enum). Атака бьёт туда, куда герой последний раз шагнул или упёрся (поворотов нет). Показ — `World/EnemyLayer` (враги, красные клетки удара, сердечки, полоска здоровья босса, призыв, заморозка), рисунки — `World/EnemyArt`, иконки карточек — `UI/CardIcons`, кнопки новых карточек `SpawnScript` создаёт клоном. На уровнях с врагами победу объявляет ядро (`ActiveBlockManager.victoryByCore` → `ForceVictory`).
- Механики тайлов: грибы (прыжок на 2 клетки), кристаллы, цветные клетки для условий; в главе Winter — лёд (скольжение до обычной клетки или края) и рассыпающиеся плиты (пройти можно один раз). `SuperBlocks.Ice` / `Crumbling`; вид и осыпание — `World/SpecialTiles` (картинки рисуются кодом по силуэту куба, на уровнях главы Winter — снег на блоках).

## Техника

- **Unity 2022.3.5f1**, URP 14, новый Input System, 2D Tilemap (изометрия), TextMeshPro, Timeline.
- Твины: **DOTween** (`Assets/Plugins/Demigiant`) и **LeanTween** (`Assets/LeanTween`). Это сторонний код, не трогать.
- Сцены: в сборку включена только `GameScenes/CollectorLevel` (индекс 0). `Menu`, `FirstLevel`, `SecondLevel`, `SceneFu`, `SceneFu 1`, `SceneWh`, `BetaEnd`, `UpdateLevel`, `2Level` числятся в Build Settings, но выключены. Автор подтвердил: используется только `CollectorLevel`, прототипы не нужны и на новое ядро не переносятся. `Levelloader` всегда грузит сцену 0. Остальные сцены в `Assets/Scenes` — старые/экспериментальные (`BadApple` + `FunVideo/VideoToTilemap.cs` — пасхалка).
- **Все уровни живут в одной сцене** `CollectorLevel`. Номер текущего уровня — в `Data.NextLevel`, сами уровни — ассеты `LevelSt` (`Assets/Scenes/GameScenes/Level*.asset`), загружает их `MapManager` / `TilemapData`.

## Архитектура: две системы

### 1. Старая система — она сейчас работает в игре

Все игровые сцены используют её. Основные скрипты (лежат в корне `Assets/Scripts`):

| Скрипт | Роль |
|---|---|
| `MovingManager` | Слоты карточек (`Commands`: Main / FunctionOne / FunctionTwo), Play/Reset, сборка очереди команд |
| `CharacterCode` | Движение героя-куба по очереди команд, анимации, победа |
| `InventoryItems` | Карточка в слоте: удаление, число повторов While, покраска (условие) |
| `Item`, `ItemType`, `SlotType` | ScriptableObject карточки и перечисления |
| `LevelSt`, `TileInfo`, `SolutionCard` | Данные уровня (тайлы, старт, супер-тайлы, эталонное решение, туториал) |
| `MapManager`, `TileMapCode/TilemapData`, `WayTiles`, `PathFinder` | Построение поля и путь |
| `LevelSolver` | Перебор минимального решения по правилам старой механики (для звёзд, без зависимостей от Unity) |
| `FinishingLevel` | Окно победы со звёздами |
| `SaveManager` | Сохранение звёзд по уровням |
| `Levelloader` | Переход между уровнями (защита от двойного клика) |
| `Tutorial/TutorialDirector` | Пошаговый туториал (затемнение, «дырка», рука, печатающийся текст) |
| `UI/HintSystem`, `UI/HintGhost` | Подсказки |
| `UI/*`, `World/*` | «Сок»: эффекты кнопок, пиксельные эффекты, звуки, атмосфера острова, свет, декор |
| `Editor/LevelEditorWindow` | Окно «Crystal Adventure → Редактор уровней» (в т.ч. «Решить все уровни») |

### 2. Новое ядро CrystalCore — написано, но ещё НЕ подключено к сценам

`Assets/Scripts/CrystalCore/` — переписанная логика с детерминированной симуляцией:

- `Core/` — отдельная сборка `CrystalAdventure.Core.asmdef` с `noEngineReferences: true`. **Никакого `UnityEngine` здесь использовать нельзя.**
  - `Grid.cs` (Cell, Dir, RelDir), `World.cs` (состояние, правила, каналы рычагов/кнопок), `Entities.cs` (герой, враги, боссы, кристаллы; Intent — намерения на ход), `Tiles.cs` (поведение тайлов: лёд, гриб, рычаги, ворота, ветер, телепорты, шипы, облака…).
  - `Program/` — программа игрока как дерево блоков (AST): `Blocks.cs`, `Conditions.cs`, `ProgramRunner.cs` (интерпретатор), `ProgramText.cs` (текстовая запись программы).
  - `Turn/TurnResolver.cs` — одновременное разрешение хода героя и врагов.
  - `Enemies/` — `EnemyBrains.cs`, `Bosses.cs` (Wood Mech Guardian, Frost General).
  - `Simulation.cs` — при Play весь прогон считается сразу, затем проигрываются события.
- `Unity/` — мост к Unity: `CrystalGameController` (точка входа уровня), `Bridge/ItemProgramAdapter` (карточки старого UI → программа ядра), `Playback/TurnPlayer`, `View/*`, `Level/LevelDefinition`.
- `Editor/LevelValidator.cs`.

`CrystalGameController` пока не стоит ни в одной сцене. Переход со старой системы на новую — открытая задача; до решения автора старую систему не удалять и не ломать.

## Тесты ядра

`CrystalCoreTests/` (вне `Assets`, Unity его не видит): `Tests.cs` — тесты ядра, `LegacyTests.cs` — старые правила и сверка с настоящими `Level*.asset` (эталонные решения + ~7000 случайных программ: ядро против `LegacyReplay`), `Stubs/` — заглушки Unity и старых классов игры (держать сигнатуры как в `Assets/Scripts`). `WinterTests.cs` — лёд и плиты: ядро, эталон `LegacyReplay` и `LevelSolver` сверяются друг с другом (≈12 000 программ на случайных зимних картах + уровни Winter). `EnemyTests.cs` — уровни с врагами: решения и минимальность оптимумов (перебор программ через ядро), мост (упор во врага ≠ край, смерть, победа). Всего 99 тестов. Файл `.csproj` в репозиторий не попадает (`*.csproj` в `.gitignore`). Чтобы запустить, нужен .NET SDK 8 и консольный проект (`net8.0`, `LangVersion 9.0`, `EnableDefaultCompileItems=false`), который включает `CrystalCoreTests/**/*.cs`, **весь** `Assets/Scripts/CrystalCore/**/*.cs` (тесты используют и `Unity/`, и `Editor/` через заглушки) и `Assets/Scripts/LevelSolver.cs`. Любые изменения в `CrystalCore` сопровождать тестом здесь.

**Проверка компиляции runtime-кода игры без Unity** (делать после правок в `Assets/Scripts`): справочные сборки движка — NuGet `UnityEngine.Modules` 2021.3.33 (`lib/netstandard2.0`), uGUI и TextMeshPro — исходники из `github.com/needle-mirror/com.unity.ugui` (тег `1.0.0/Unity-2021.1.17f1`) и `com.unity.textmeshpro` (тег `3.0.9`), собранные отдельными проектами (`GenerateAssemblyInfo=false`). Проект игры: `Assets/Scripts/**/*.cs` без папок `Editor`, без `PlayerInputActions.cs` и `RadialBlurEffect.cs` (Input System / URP — заглушки), + `Assets/LeanTween/**` + `DOTween.dll`. Editor-скрипты проверяются только в тестовом проекте на заглушках.

## Перенос на новое ядро — состояние

- **Решения автора:** гриб — старое поведение («встал на гриб → следующий шаг на 2 клетки, куда велит карточка»; `LevelRules.ClassicMushroom`, по умолчанию true). Сцены-прототипы не переносим.
- **Фазы 1–2 сделаны:** ядро повторяет старые правила (цвет клетки → `ActionBlock.Guard` и пропуск без хода мира, край → `Bumped.Info = "edge"` и `SimulationResult.EdgeHits`, кристалл по приходу → `PickupOnArrival`, While со скобкой и повторами каждой карточки → `ItemProgramAdapter.FromMovingManager`, звёзды по карточкам без «def»). Импорт старых уровней — `Unity/Level/LegacyLevelImport`. Меню **Crystal Adventure → Ядро → Проверить совпадение со старой системой** и **→ Создать LevelDefinition из LevelSt** (`Editor/LegacyParityCheck`, эталон старых правил — `Editor/LegacyReplay`).
- **Известное отличие:** в программах длиннее 300 шагов старый `MovingManager` молча выкидывал часть повторов цикла, ядро останавливается на 300-м ходу (`TurnLimit`).
- **Фаза 3 проверена автором, ядро включено насовсем:** `MovingManager.useCrystalCore` (по умолчанию true; false — запасной старый путь через `WayTiles`). Включена — `StartPlay` берёт план у `Unity/Bridge/CoreRunBridge` (ядро считает прогон по `TilemapData.CurrentLevel` и старту `CharacterCode.StartTile`), а `CharacterCode.RunPlan` проигрывает его прежними анимациями. Кристаллы, победа, подсветка карточек, звёзды, туториал, подсказки — старый код. Ход i ядра = шаг i очереди `BuildQueue`, поэтому подсветка по номеру шага верна. Если ядро не смогло посчитать — откат на старый путь с предупреждением. `CrystalGameController` (полностью новый визуал: `EntityView`, враги) пока не используется — это следующий этап.
- **Уровни:** в игре 15 уровней. `LevelFive`, `LevelSeven`, `LevelEleven` были черновиками вне списка, `LevelEleven` — точная копия `LevelTen`. Кнопка **Crystal Adventure → Уровни → Заполнить черновики и добавить в игру** (`Editor/DraftLevels`) заменяет `LevelEleven` уровнем «Змейка» (грибы + цветные клетки, оптимум 7, без цветов — 8) и дописывает все три в конец списка `TilemapData` (№16–18; вставка в середину сдвинула бы сохранённые звёзды — `SaveManager` хранит их по номеру уровня).

## Стиль кода

- Комментарии и `/// <summary>` — на русском, подробные, с объяснением «почему». Блок «Исправлено: …» в summary описывает исправленный баг — так принято в проекте.
- Классы ядра в пространстве имён `CrystalAdventure.Core` / `CrystalAdventure.UnityLayer`; старые скрипты — без namespace.
- Многие UI-объекты создаются кодом сами (например, `LevelMapMenu`, туториал), чтобы ничего не настраивать в сцене. При возможности держаться этого подхода.
- Пиксельный стиль: анимации сдвигают на целые пиксели.

## Правила работы

- Отвечай автору на **русском**.
- Работа идёт в облаке **без Unity Editor**: запустить игру, сцену или Play Mode нельзя. Автор тестирует сам после pull. Поэтому внимательно проверяй код на ошибки компиляции, существование классов, полей и методов, на которые ссылаешься, версию API Unity 2022.3.
- **Не редактируй `.unity`, `.prefab`, `.asset`, `.meta` без разрешения.** Если для задачи нужно изменить сцену или префаб, опиши, что сделать руками в Unity.
- Для новых скриптов **не создавай `.meta`** — Unity сгенерирует их сама.
- Не трогай `Library`, `Temp`, `Logs`, `Build`, `UserSettings`, а также сторонний код (`Assets/Plugins`, `Assets/LeanTween`, `Assets/TextMesh Pro`).
- Не используй `UnityEngine` в `CrystalCore/Core`.
- Не делай больших рефакторингов и не удаляй старую систему без согласования.
- После каждой задачи кратко напиши: что изменено, в каких файлах и что проверить в Unity.

## Что стоит обсудить с автором

- **Порядок дальнейшей работы (решение автора):** 1) скорость ×1/×2/×4 — сделано (`UI/SpeedControl`, создаётся кодом над кнопкой «?», ускоряет через `Time.timeScale` только на время прогона; UI проекта идёт по unscaledTime); 2) новые клетки — лёд и плиты сделаны, уровни Winter 1–4 создаёт кнопка **Crystal Adventure → Уровни → Создать уровни Winter** (`Editor/WinterLevels`, №19–22); 3) враги и 4) два босса — сделаны: кнопка **Crystal Adventure → Уровни → Создать уровни с врагами** (`Editor/EnemyLevels`) создаёт карточки Attack/Shield/SuperAttack и уровни №23–27 («Чучело», «Патрульный», «Ледяные пушки», Wood Mech Guardian, Frost General). Босс Sky — позже. Босс леса стоит в главе Winter (глава Forest уже полна) — обсудить порядок глав.
- Уровни 16–18 (`LevelFive`, `LevelSeven` — лёгкие) открывают главу Winter; снег на блоки теперь кладётся автоматически, но по сложности они проще Forest 15. Запасной старый путь (`useCrystalCore = false`) лёд понимает (`WayTiles.ApplyIce`), а плиты — только визуально.
- Единое имя помощницы (Эла / Luma).
- Сцены-прототипы не нужны (подтверждено) — можно ли удалить их файлы из проекта. `Menu` тоже выключена в сборке — так и задумано?
- Проект тестов без `.csproj` в репозитории — добавить, например, `CrystalCoreTests/CrystalCoreTests.csproj` с исключением в `.gitignore`.
