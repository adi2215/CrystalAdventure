using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalAdventure.Core
{
    /// <summary>Действие героя = ровно один ход.</summary>
    public enum ActionType
    {
        MoveForward, MoveBack, MoveLeft, MoveRight,
        TurnLeft, TurnRight,
        Attack, SuperAttack, Shield, Interact,
        BreakIce, Spell, ChangeWind, Wait,
    }

    /// <summary>
    /// Программа игрока — это дерево блоков (AST), а не список строк.
    /// UI собирает дерево, интерпретатор его исполняет. Так циклы, условия
    /// и функции работают одинаково в любой глубине вложенности.
    /// </summary>
    public abstract class Block
    {
        public int Id; // уникален в программе → по нему UI подсвечивает текущий блок
        public object Tag; // ссылка на карточку в UI (ядру не нужна)
        public virtual IEnumerable<Block> Children => Enumerable.Empty<Block>();
    }

    public sealed class ActionBlock : Block
    {
        public ActionType Action;
        /// <summary>
        /// Условие самой карточки (покрашенная карточка старой игры): если не выполнено,
        /// действие пропускается и ход мира не тратится. null — выполняется всегда.
        /// </summary>
        public Condition Guard;
        public ActionBlock(ActionType a) { Action = a; }
    }

    /// <summary>Повторить N раз (рекомендую ввести ДО While — это проще для новичка).</summary>
    public sealed class RepeatBlock : Block
    {
        public int Count;
        public List<Block> Body = new List<Block>();
        public override IEnumerable<Block> Children => Body;
    }

    public sealed class WhileBlock : Block
    {
        public Condition Condition;
        public List<Block> Body = new List<Block>();
        public override IEnumerable<Block> Children => Body;
    }

    public sealed class IfBlock : Block
    {
        public Condition Condition;
        public List<Block> Then = new List<Block>();
        public List<Block> Else = new List<Block>();
        public override IEnumerable<Block> Children => Then.Concat(Else);
    }

    public sealed class CallBlock : Block
    {
        public string Function;
        public CallBlock(string fn) { Function = fn; }
    }

    public sealed class ProgramDef
    {
        public List<Block> Main = new List<Block>();
        public Dictionary<string, List<Block>> Functions = new Dictionary<string, List<Block>>();

        /// <summary>
        /// Считать ли объявление функции («def») отдельным блоком. В игре панель функции —
        /// не карточка, и звёзды считаются по карточкам (MovingManager.CountCards), поэтому по умолчанию false.
        /// </summary>
        public bool CountFunctionDefs = false;

        /// <summary>
        /// Сколько блоков поставил игрок — для звёзд.
        ///
        /// Исправлено: раньше каждая функция добавляла +1, даже пустая. Адаптер старого UI всегда
        /// создаёт F1 и F2, поэтому любая программа из карточек считалась на 2 блока больше.
        /// </summary>
        public int CountBlocks() =>
            Count(Main) + Functions.Values.Sum(Count) +
            (CountFunctionDefs ? Functions.Values.Count(f => f != null && f.Count > 0) : 0);

        static int Count(IEnumerable<Block> blocks) => blocks == null ? 0 : blocks.Sum(b => 1 + Count(b.Children));

        public Block Find(int id)
        {
            Block found = null;
            void Walk(IEnumerable<Block> list) { foreach (var b in list) { if (found != null) return; if (b.Id == id) { found = b; return; } Walk(b.Children); } }
            Walk(Main);
            foreach (var f in Functions.Values) if (found == null) Walk(f);
            return found;
        }

        /// <summary>Раздать блокам уникальные Id (вызывай после сборки из UI).</summary>
        public void AssignIds()
        {
            int next = 1;
            void Walk(IEnumerable<Block> list) { foreach (var b in list) { b.Id = next++; Walk(b.Children); } }
            Walk(Main);
            foreach (var f in Functions.Values) Walk(f);
        }
    }

    /// <summary>Удобная сборка программ (для тестов, обучения, подсказок Ela).</summary>
    public sealed class ProgramBuilder
    {
        readonly List<Block> _target;
        readonly ProgramDef _program;

        public ProgramBuilder() : this(new ProgramDef(), null) { }
        ProgramBuilder(ProgramDef p, List<Block> target) { _program = p; _target = target ?? p.Main; }

        public ProgramBuilder Do(params ActionType[] actions)
        {
            foreach (var a in actions) _target.Add(new ActionBlock(a));
            return this;
        }

        public ProgramBuilder Repeat(int count, Action<ProgramBuilder> body)
        {
            var b = new RepeatBlock { Count = count };
            body(new ProgramBuilder(_program, b.Body));
            _target.Add(b);
            return this;
        }

        public ProgramBuilder While(Condition c, Action<ProgramBuilder> body)
        {
            var b = new WhileBlock { Condition = c };
            body(new ProgramBuilder(_program, b.Body));
            _target.Add(b);
            return this;
        }

        public ProgramBuilder If(Condition c, Action<ProgramBuilder> then, Action<ProgramBuilder> otherwise = null)
        {
            var b = new IfBlock { Condition = c };
            then(new ProgramBuilder(_program, b.Then));
            otherwise?.Invoke(new ProgramBuilder(_program, b.Else));
            _target.Add(b);
            return this;
        }

        public ProgramBuilder Call(string fn) { _target.Add(new CallBlock(fn)); return this; }

        public ProgramBuilder Function(string name, Action<ProgramBuilder> body)
        {
            var list = new List<Block>();
            body(new ProgramBuilder(_program, list));
            _program.Functions[name] = list;
            return this;
        }

        public ProgramDef Build() { _program.AssignIds(); return _program; }
    }
}
