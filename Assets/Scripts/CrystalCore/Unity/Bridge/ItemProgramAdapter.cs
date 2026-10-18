using System;
using System.Collections.Generic;
using CrystalAdventure.Core;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>Где в старом UI лежит карточка — чтобы подсветить её во время проигрывания.</summary>
    public sealed class SlotRef
    {
        public SlotType Slot;
        public int Index;
        public SlotRef(SlotType slot, int index) { Slot = slot; Index = index; }
    }

    /// <summary>
    /// Одна карточка в слоте, без привязки к Item / SolutionCard — так один и тот же код
    /// переводит и карточки игрока (MovingManager), и эталонное решение уровня (LevelSt.solution*).
    /// </summary>
    public struct CardSpec
    {
        public bool Empty;      // пустой слот
        public ItemType Type;
        public int Iterations;  // While: сколько раз повторить ЕЩЁ (всего Iterations + 1 проходов)
        public int Span;        // While: сколько слотов перед ним под скобкой; -1 — все до предыдущего While
        public int Color;       // ход: 0 — всегда, иначе срабатывает только на клетке этого цвета (TileColor)

        public static CardSpec None => new CardSpec { Empty = true };
    }

    /// <summary>
    /// Переводит текущий UI (MovingManager.Commands: Main / FunctionOne / FunctionTwo из карточек Item)
    /// в программу нового ядра. Старые списки при этом НЕ меняются — раньше они
    /// «съедались» во время игры, и из-за этого ломался перезапуск.
    ///
    /// Правила те же, что в MovingManager.BuildQueue и LevelSolver:
    ///   • While(N) — карточки под его скобкой (span слотов перед ним) выполняются ещё N раз → RepeatBlock(N + 1);
    ///   • покрашенная карточка хода срабатывает только на клетке своего цвета → ActionBlock.Guard;
    ///   • F1 / F2 — вызов функции.
    ///
    /// Исправлено: раньше адаптер брал число повторов из общего ассета Item (а не с карточки),
    /// всегда оборачивал в цикл ВСЁ перед While (скобку игрок мог сузить) и терял цвет карточки.
    ///
    /// Новые карточки (Attack, Shield, SuperAttack, Interact, BreakIce, Spell, ChangeWind, Wait,
    /// TurnLeft, TurnRight) подхватятся автоматически, если добавить такие же имена в enum ItemType.
    /// </summary>
    public static class ItemProgramAdapter
    {
        public const string FunctionOneName = "F1";
        public const string FunctionTwoName = "F2";

        static readonly SlotType[] Slots = { SlotType.Main, SlotType.FunctionOne, SlotType.FunctionTwo };

        /// <summary>Программа из карточек игрока — с повторами, скобками и цветами каждой карточки.</summary>
        public static ProgramDef FromMovingManager(MovingManager moving)
        {
            if (moving == null) return FromCommands(null);
            return FromCommands(moving.Commands, moving.GetIteration, moving.GetSpan, (s, i) => (int)moving.GetColor(s, i));
        }

        /// <param name="iterationOf">Сколько раз повторить ЕЩЁ для While. По умолчанию item.iteration.</param>
        /// <param name="spanOf">Сколько слотов перед While под его скобкой. По умолчанию — все до предыдущего While.</param>
        /// <param name="colorOf">Цвет-условие карточки хода (0 — без условия). По умолчанию без цвета.</param>
        public static ProgramDef FromCommands(Dictionary<SlotType, List<Item>> commands,
                                              Func<SlotType, int, int> iterationOf = null,
                                              Func<SlotType, int, int> spanOf = null,
                                              Func<SlotType, int, int> colorOf = null)
        {
            return FromCards(slot =>
            {
                var items = Get(commands, slot);
                var cards = new List<CardSpec>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null) { cards.Add(CardSpec.None); continue; }
                    cards.Add(new CardSpec
                    {
                        Type = item.type,
                        Iterations = iterationOf != null ? iterationOf(slot, i) : item.iteration,
                        Span = spanOf != null ? spanOf(slot, i) : -1,
                        Color = colorOf != null ? colorOf(slot, i) : 0,
                    });
                }
                return cards;
            });
        }

        /// <summary>
        /// Эталонное решение уровня (заполняет «Решить все уровни»). У SolutionCard span считается
        /// в карточках, 0 — «все до предыдущего While» (SolutionCard.ResolveSpan); пустых слотов в решении нет.
        /// </summary>
        public static ProgramDef FromSolution(LevelSt level) => FromCards(slot => SolutionCards(level, slot));

        /// <summary>Карточки эталонного решения одной панели.</summary>
        public static IList<CardSpec> SolutionCards(LevelSt level, SlotType slot)
        {
            var list = level != null ? level.GetSolution(slot) : null;
            var cards = new List<CardSpec>();
            if (list == null) return cards;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                cards.Add(new CardSpec
                {
                    Type = c.type,
                    Iterations = c.iterations,
                    Span = c.type == ItemType.While ? SolutionCard.ResolveSpan(list, i) : 0,
                    Color = (int)c.color,
                });
            }
            return cards;
        }

        /// <summary>Общая сборка: panel(slot) отдаёт карточки панели по слотам.</summary>
        public static ProgramDef FromCards(Func<SlotType, IList<CardSpec>> panel)
        {
            var program = new ProgramDef { CountFunctionDefs = false }; // панель функции — не карточка
            foreach (var slot in Slots)
            {
                var body = Convert(slot, panel(slot) ?? new List<CardSpec>());
                if (slot == SlotType.Main) program.Main = body;
                else program.Functions[slot == SlotType.FunctionOne ? FunctionOneName : FunctionTwoName] = body;
            }
            program.AssignIds();
            return program;
        }

        static Block Tag(Block b, SlotType slot, int index) { b.Tag = new SlotRef(slot, index); return b; }

        static List<Item> Get(Dictionary<SlotType, List<Item>> d, SlotType s) =>
            d != null && d.TryGetValue(s, out var l) && l != null ? l : new List<Item>();

        /// <summary>Блок вместе с индексом слота, из которого он взялся (для скобки цикла).</summary>
        struct Placed
        {
            public int Index;
            public Block Block;
        }

        static List<Block> Convert(SlotType slot, IList<CardSpec> cards)
        {
            var result = new List<Placed>();
            int lastWhile = -1; // циклы не вкладываются: скобка не заходит за предыдущий While

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.Empty) continue;

                Block block = null;
                switch (card.Type)
                {
                    case ItemType.Forward: block = Move(ActionType.MoveForward, card.Color); break;
                    case ItemType.Bottom: block = Move(ActionType.MoveBack, card.Color); break;
                    case ItemType.Left: block = Move(ActionType.MoveLeft, card.Color); break;
                    case ItemType.Right: block = Move(ActionType.MoveRight, card.Color); break;
                    case ItemType.FunctionOne: block = new CallBlock(FunctionOneName); break;
                    case ItemType.FunctionTwo: block = new CallBlock(FunctionTwoName); break;

                    case ItemType.While:
                    {
                        // Как MovingManager.BuildQueue: повторить ЕЩЁ n раз карточки под скобкой.
                        int n = card.Iterations;
                        int from = card.Span < 0 ? lastWhile + 1 : Math.Max(lastWhile + 1, i - card.Span);
                        lastWhile = i;
                        int start = result.FindIndex(p => p.Index >= from);
                        if (n <= 0 || start < 0) break; // пустая скобка — цикл ничего не делает

                        var repeat = new RepeatBlock { Count = n + 1 };
                        for (int k = start; k < result.Count; k++) repeat.Body.Add(result[k].Block);
                        result.RemoveRange(start, result.Count - start);
                        result.Add(new Placed { Index = i, Block = Tag(repeat, slot, i) });
                        continue;
                    }

                    case ItemType.Ifandelse:
                    case ItemType.Null:
                        break;

                    default:
                        // Новые типы карточек: имя ItemType совпадает с ActionType.
                        if (Enum.TryParse(card.Type.ToString(), out ActionType action))
                            block = new ActionBlock(action);
                        break;
                }

                if (block != null) result.Add(new Placed { Index = i, Block = Tag(block, slot, i) });
            }

            var list = new List<Block>(result.Count);
            foreach (var p in result) list.Add(p.Block);
            return list;
        }

        static Block Move(ActionType action, int color) =>
            new ActionBlock(action) { Guard = color > 0 ? Condition.OnColor(color) : null };
    }
}
