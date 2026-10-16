using System.Collections.Generic;

namespace CrystalAdventure.Core
{
    public enum RunnerStatus { Running, Finished, Error }

    /// <summary>
    /// Пошаговый интерпретатор. Каждый вызов Next() крутит управляющие блоки
    /// (циклы, условия, вызовы функций) до тех пор, пока не встретит ДЕЙСТВИЕ,
    /// и возвращает его. Одно действие = один ход мира.
    ///
    /// Стек кадров вместо рекурсии C#: программу можно остановить в любой момент,
    /// показать текущий блок и продолжить (кнопки Step / Pause).
    /// Условия While/If проверяются по миру ПОСЛЕ предыдущего хода.
    /// </summary>
    public sealed class ProgramRunner
    {
        public const int MaxControlStepsPerAction = 2000; // while(true) без действий
        public const int MaxCallDepth = 32;               // рекурсия функций

        sealed class Frame
        {
            public List<Block> Body;
            public int Index;
            public Block Owner;     // RepeatBlock / WhileBlock / CallBlock / null
            public int RepeatLeft;
        }

        readonly ProgramDef _program;
        readonly Stack<Frame> _stack = new Stack<Frame>();

        public RunnerStatus Status { get; private set; } = RunnerStatus.Running;
        public string Error { get; private set; }
        public int CurrentBlockId { get; private set; }
        public int ActionsExecuted { get; private set; }
        /// <summary>Последний Next() вернул действие, условие которого не выполнено: его надо пропустить, а не играть.</summary>
        public bool Skipped { get; private set; }

        public ProgramRunner(ProgramDef program)
        {
            _program = program;
            _stack.Push(new Frame { Body = program.Main });
        }

        public bool Next(World world, out ActionType action)
        {
            action = ActionType.Wait;
            Skipped = false;
            if (Status != RunnerStatus.Running) return false;

            for (int guard = 0; guard < MaxControlStepsPerAction; guard++)
            {
                if (_stack.Count == 0) { Status = RunnerStatus.Finished; return false; }

                var f = _stack.Peek();
                if (f.Index >= f.Body.Count)
                {
                    if (f.Owner is RepeatBlock && --f.RepeatLeft > 0) { f.Index = 0; continue; }
                    if (f.Owner is WhileBlock w && w.Condition.Evaluate(world)) { f.Index = 0; continue; }
                    _stack.Pop();
                    continue;
                }

                var block = f.Body[f.Index++];
                switch (block)
                {
                    case ActionBlock a:
                        CurrentBlockId = a.Id;
                        action = a.Action;
                        // Условие проверяется по миру ПЕРЕД этим действием (где герой стоит сейчас).
                        Skipped = a.Guard != null && !a.Guard.Evaluate(world);
                        if (!Skipped) ActionsExecuted++;
                        return true;

                    case RepeatBlock r:
                        if (r.Count > 0 && r.Body.Count > 0)
                            _stack.Push(new Frame { Body = r.Body, Owner = r, RepeatLeft = r.Count });
                        break;

                    case WhileBlock wb:
                        CurrentBlockId = wb.Id;
                        if (wb.Condition.Evaluate(world))
                        {
                            if (wb.Body.Count == 0) return Fail("Пустой цикл While никогда не закончится");
                            _stack.Push(new Frame { Body = wb.Body, Owner = wb });
                        }
                        break;

                    case IfBlock ib:
                        CurrentBlockId = ib.Id;
                        var branch = ib.Condition.Evaluate(world) ? ib.Then : ib.Else;
                        if (branch.Count > 0) _stack.Push(new Frame { Body = branch, Owner = ib });
                        break;

                    case CallBlock c:
                        if (!_program.Functions.TryGetValue(c.Function, out var body))
                            return Fail($"Функция {c.Function} не создана");
                        if (_stack.Count >= MaxCallDepth)
                            return Fail("Слишком глубокая рекурсия функций");
                        if (body.Count > 0) _stack.Push(new Frame { Body = body, Owner = c });
                        break;
                }
            }
            return Fail("Цикл крутится без действий — бесконечный цикл?");
        }

        bool Fail(string message)
        {
            Status = RunnerStatus.Error;
            Error = message;
            return false;
        }
    }
}
