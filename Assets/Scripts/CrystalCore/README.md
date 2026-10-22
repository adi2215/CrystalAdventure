# Crystal Adventure — новое ядро (CrystalCore)

## Идея в одной фразе
Игра больше не «ходит по кадрам». По кнопке **Старт** ядро мгновенно считает весь прогон
программы (герой + все враги, ход за ходом), а потом Unity просто проигрывает записанные события
с нужной скоростью. Логика не зависит от анимаций → меньше багов, скорость x1/x2/x4/«мгновенно»
работает бесплатно, а любой уровень можно проверить автоматически.

```
Карточки UI ──► ItemProgramAdapter ──► ProgramDef (дерево блоков)
                                             │
LevelDefinition ──► LevelSetup ──► Simulation.Run ──► SimulationResult (ходы → события)
                                                              │
                                    TurnPlayer + EntityView + TileView + IntentOverlay
```

## Папки
| Папка | Что внутри | Зависит от Unity? |
|---|---|---|
| `Core/` | Правила игры: сетка, тайлы, враги, боссы, интерпретатор, ход | **Нет** (asmdef `noEngineReferences`) |
| `Unity/` | Уровни (ScriptableObject), проигрывание, визуал, мост к старому UI | Да |
| `Editor/` | Меню *Crystal Adventure → Validate All Levels* | Да (только редактор) |
| `../../../CrystalCoreTests/` | 78 автотестов: ядро + сверка со старыми уровнями (`dotnet run`) | Нет |

## Как устроен ход (одновременно)
1. **Prepare** — повороты, щит, Interact / Spell / Break Ice / Change Wind, призывы босса
2. **Move** — все шаги сразу. Двое в одну клетку или обмен местами → оба упираются. «Паровозик» разрешён
3. **Effects** — лёд (скольжение), гриб (+2 клетки), телепорт, рассыпающиеся плиты
4. **Combat** — сначала бьёт герой (убитый враг уже не ответит), потом враги — по заранее показанным клеткам
5. **Tiles** — шипы, ветер, таймеры облаков
6. **Resolve** — падения, подбор кристаллов
7. **Plan** — враги решают следующий ход → игрок видит красные клетки и стрелки ещё до запуска

## Старые правила (уровни из LevelSt)
Ядро повторяет механику, которая сейчас в игре (`MovingManager` + `CharacterCode` + `WayTiles`):
- **Гриб:** стоишь на грибе → следующий шаг на 2 клетки, туда, куда велит карточка (`LevelRules.ClassicMushroom`, по умолчанию). `false` — новый вариант «отскок сразу при входе».
- **Цветные клетки:** покрашенная карточка → `ActionBlock.Guard` (`Condition.OnColor`). Не тот цвет — карточка пропускается, ход мира не тратится (`TurnRecord.Skipped`, событие `Skipped`). В тексте: `R@1`.
- **Край:** шаг в пустоту — `Bumped` с `Info = "edge"`, счётчик `SimulationResult.EdgeHits`.
- **Кристалл** берётся, когда герой пришёл на клетку (`LevelRules.PickupOnArrival`).
- **While** со скобкой и своим числом повторов у каждой карточки — `ItemProgramAdapter.FromMovingManager`.
- **Звёзды** по карточкам: панель функции — не карточка (`ProgramDef.CountFunctionDefs = false`).

Проверка без Play Mode: **Crystal Adventure → Ядро → Проверить совпадение со старой системой**
(эталонное решение каждого уровня через ядро и через `LegacyReplay` — эталон старых правил).
**→ Создать LevelDefinition из LevelSt** — ассеты в `Assets/Scenes/GameScenes/CoreLevels` (старт, правила, оптимум, решение).

## Ядро в игре со старым кубом (Фаза 3)
Галочка **Use Crystal Core** на объекте `MovingManager` в `CollectorLevel`: куда идти, решает ядро
(`Bridge/CoreRunBridge` → `CharacterCode.RunPlan`), всё остальное — прежний код. Сцену настраивать не нужно.

## Подключение в сцене полностью нового визуала (≈20 минут)
1. Unity сам скомпилирует папку. Старые скрипты не тронуты.
2. **Create → Crystal Adventure → Entity Prefabs**: укажи префабы героя / кристалла / врагов.
   На каждый префаб добавь компонент `EntityView` (SpriteRenderer подхватится сам).
3. **Crystal Adventure → Ядро → Создать LevelDefinition из LevelSt** (или вручную **Create → Crystal Adventure → Level**): `legacyLevel` = старый `LevelSt`,
   `heroCell` = клетка старта, враги в `entities` (Archetype: `SmallBallScout`, `ForestDrone`,
   `TrainingDummy`, `FrostTurret`, `HeavyIceGuard`, `WoodMechGuardian`…), звёзды и `referenceSolution`.
4. На сцене создай объект `CrystalGame` и добавь: `CrystalGameController`, `TurnPlayer`, `GridMapper`
   (tilemap = твой Tilemap), `IntentOverlay` (красный ромб + стрелка), `TileView` (+ `BiomeTileset`).
5. Кнопка **Play** → `CrystalGameController.Play`, **Rewind** → `ResetLevel`,
   кнопки скорости → `SetSpeed(1/2/4)`, «перемотать» → `SkipToEnd`.
6. Выключи `CharacterCode` (его LateUpdate двигал куб) — героя теперь ставит контроллер.
7. `onWin(stars)` → включить окно `finishLevel`. Звёзды уже посчитаны по `optimalBlocks/goodBlocks` уровня.
8. `onCardHighlight(slot, index)` → подсветить текущую карточку.

## Новые карточки
Добавь в `enum ItemType` значения с такими же именами, как в `ActionType`:
`Attack, SuperAttack, Shield, Interact, BreakIce, Spell, ChangeWind, Wait` — адаптер подхватит их сам.

## Текстовые программы (тесты, эталонные решения, подсказки Ela)
```
def A { F F A }            // функция
repeat 3 { call A R }      // повторить
while !crystals { if enemy { A } else { F } }
```
Действия: `F B L R TL TR A SA S I BI SP W WAIT`.
Условия: `path enemy trap door ice wind crystal crystals enemies hplow`, `!` = «не».

## Добавить свой супер-тайл
1. Значение в `TileType` → 2. класс `: GroundBehavior` (OnEnter / OnExit / OnTurnEnd / IsWalkable)
→ 3. строчка в `TileBehaviors.Map` → 4. спрайт в `BiomeTileset`. Визуал обновится сам через `TileVisual`.

## Добавить врага
Класс `: IEnemyBrain` с методом `Plan(world, self)` → строчка в `EnemyBrains.Create`. Без Random —
враги предсказуемые, решение видно игроку заранее.
