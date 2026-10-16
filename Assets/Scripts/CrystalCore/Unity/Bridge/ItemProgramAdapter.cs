using System;
using System.Collections.Generic;
using CrystalAdventure.Core;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Переводит текущий UI (MovingManager.Commands: Main / FunctionOne / FunctionTwo из карточек Item)
    /// в программу нового ядра. Старые списки при этом НЕ меняются — раньше они
    /// «съедались» во время игры, и из-за этого ломался перезапуск.
    ///
    /// Новые карточки (Attack, Shield, SuperAttack, Interact, BreakIce, Spell, ChangeWind, Wait,
    /// TurnLeft, TurnRight) подхватятся автоматически, если добавить такие же имена в enum ItemType.
    /// </summary>
    /// <summary>Где в старом UI лежит карточка — чтобы подсветить её во время проигрывания.</summary>
    public sealed class SlotRef
    {
        public SlotType Slot;
        public int Index;
        public SlotRef(SlotType slot, int index) { Slot = slot; Index = index; }
    }

    public static class ItemProgramAdapter
    {
        public const string FunctionOneName = "F1";
        public const string FunctionTwoName = "F2";

        /// <param name="iterationOf">Сколько раз повторять для карточки While. По умолчанию берётся item.iteration,
        /// но лучше хранить число на самой карточке (InventoryItems), а не в общем ассете Item.</param>
        public static ProgramDef FromCommands(Dictionary<SlotType, List<Item>> commands, Func<SlotType, int, int> iterationOf = null)
        {
            var program = new ProgramDef();
            program.Main = Convert(SlotType.Main, Get(commands, SlotType.Main), iterationOf);
            program.Functions[FunctionOneName] = Convert(SlotType.FunctionOne, Get(commands, SlotType.FunctionOne), iterationOf);
            program.Functions[FunctionTwoName] = Convert(SlotType.FunctionTwo, Get(commands, SlotType.FunctionTwo), iterationOf);
            program.AssignIds();
            return program;
        }

        static Block Tag(Block b, SlotType slot, int index) { b.Tag = new SlotRef(slot, index); return b; }

        static List<Item> Get(Dictionary<SlotType, List<Item>> d, SlotType s) =>
            d != null && d.TryGetValue(s, out var l) && l != null ? l : new List<Item>();

        static List<Block> Convert(SlotType slot, List<Item> items, Func<SlotType, int, int> iterationOf)
        {
            var result = new List<Block>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue; // пустой слот

                switch (item.type)
                {
                    case ItemType.Forward: result.Add(Tag(new ActionBlock(ActionType.MoveForward), slot, i)); break;
                    case ItemType.Bottom: result.Add(Tag(new ActionBlock(ActionType.MoveBack), slot, i)); break;
                    case ItemType.Left: result.Add(Tag(new ActionBlock(ActionType.MoveLeft), slot, i)); break;
                    case ItemType.Right: result.Add(Tag(new ActionBlock(ActionType.MoveRight), slot, i)); break;
                    case ItemType.FunctionOne: result.Add(Tag(new CallBlock(FunctionOneName), slot, i)); break;
                    case ItemType.FunctionTwo: result.Add(Tag(new CallBlock(FunctionTwoName), slot, i)); break;

                    case ItemType.While:
                    {
                        // Совместимо со старым поведением: карточка повторяет всё, что стоит ПЕРЕД ней,
                        // ещё N раз (итого N+1). В новом UI лучше сделать настоящий блок Repeat с телом.
                        int n = iterationOf != null ? iterationOf(slot, i) : item.iteration;
                        if (result.Count == 0 || n <= 0) break;
                        var body = new List<Block>(result);
                        result.Clear();
                        result.Add(Tag(new RepeatBlock { Count = n + 1, Body = body }, slot, i));
                        break;
                    }

                    case ItemType.Ifandelse:
                    case ItemType.Null:
                        break;

                    default:
                        // Новые типы карточек: имя ItemType совпадает с ActionType.
                        if (Enum.TryParse(item.type.ToString(), out ActionType action))
                            result.Add(Tag(new ActionBlock(action), slot, i));
                        break;
                }
            }
            return result;
        }
    }
}
