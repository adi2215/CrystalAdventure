using System;
using System.Collections.Generic;
using System.Text;

namespace CrystalAdventure.Core
{
    /// <summary>
    /// Текстовая запись программы. Нужна для тестов, эталонных решений уровней
    /// и подсказок Luma («Recommended first block: ...»).
    ///
    ///   def A { F F A }
    ///   repeat 3 { call A R }
    ///   while !crystals_done { if enemy { A } else { F } }
    ///
    /// Действия: F B L R (шаг вверх/вниз/влево/вправо по сетке), TL TR (поворот),
    ///           A (атака), SA (супер-атака), S (щит), I (interact), BI (break ice),
    ///           SP (spell), W (change wind), WAIT.
    /// Условия:  path enemy trap door ice wind crystal crystals enemies hplow,
    ///           «!» перед условием = «не».
    /// </summary>
    public static class ProgramText
    {
        static readonly Dictionary<string, ActionType> Actions = new Dictionary<string, ActionType>(StringComparer.OrdinalIgnoreCase)
        {
            { "F", ActionType.MoveForward }, { "B", ActionType.MoveBack },
            { "L", ActionType.MoveLeft }, { "R", ActionType.MoveRight },
            { "TL", ActionType.TurnLeft }, { "TR", ActionType.TurnRight },
            { "A", ActionType.Attack }, { "SA", ActionType.SuperAttack },
            { "S", ActionType.Shield }, { "I", ActionType.Interact },
            { "BI", ActionType.BreakIce }, { "SP", ActionType.Spell },
            { "W", ActionType.ChangeWind }, { "WAIT", ActionType.Wait },
        };

        static readonly Dictionary<string, ConditionType> Conditions = new Dictionary<string, ConditionType>(StringComparer.OrdinalIgnoreCase)
        {
            { "path", ConditionType.PathClear }, { "enemy", ConditionType.EnemyAhead },
            { "trap", ConditionType.TrapAhead }, { "door", ConditionType.DoorClosed },
            { "ice", ConditionType.IceAhead }, { "wind", ConditionType.WindActive },
            { "crystal", ConditionType.CrystalNear }, { "crystals", ConditionType.CrystalNotCollected },
            { "enemies", ConditionType.EnemyAlive }, { "hplow", ConditionType.HpLow },
        };

        public static ProgramDef Parse(string text)
        {
            var tokens = Tokenize(text ?? "");
            int i = 0;
            var program = new ProgramDef();
            while (i < tokens.Count)
            {
                if (Eq(tokens[i], "def"))
                {
                    i++;
                    string name = Expect(tokens, ref i, "function name");
                    program.Functions[name] = ParseBlockList(tokens, ref i);
                }
                else program.Main.Add(ParseStatement(tokens, ref i));
            }
            program.AssignIds();
            return program;
        }

        static List<Block> ParseBlockList(List<string> t, ref int i)
        {
            if (i >= t.Count || t[i] != "{") throw new FormatException("Expected '{'");
            i++;
            var list = new List<Block>();
            while (i < t.Count && t[i] != "}") list.Add(ParseStatement(t, ref i));
            if (i >= t.Count) throw new FormatException("Missing '}'");
            i++;
            return list;
        }

        static Block ParseStatement(List<string> t, ref int i)
        {
            var tok = t[i++];
            if (Actions.TryGetValue(tok, out var a)) return new ActionBlock(a);

            if (Eq(tok, "repeat"))
            {
                var n = int.Parse(Expect(t, ref i, "repeat count"));
                return new RepeatBlock { Count = n, Body = ParseBlockList(t, ref i) };
            }
            if (Eq(tok, "while"))
            {
                var c = ParseCondition(t, ref i);
                return new WhileBlock { Condition = c, Body = ParseBlockList(t, ref i) };
            }
            if (Eq(tok, "if"))
            {
                var c = ParseCondition(t, ref i);
                var b = new IfBlock { Condition = c, Then = ParseBlockList(t, ref i) };
                if (i < t.Count && Eq(t[i], "else")) { i++; b.Else = ParseBlockList(t, ref i); }
                return b;
            }
            if (Eq(tok, "call")) return new CallBlock(Expect(t, ref i, "function name"));

            throw new FormatException($"Unknown token '{tok}'");
        }

        static Condition ParseCondition(List<string> t, ref int i)
        {
            var tok = Expect(t, ref i, "condition");
            bool neg = false;
            if (tok == "!" || Eq(tok, "not")) { neg = true; tok = Expect(t, ref i, "condition"); }
            else if (tok.StartsWith("!")) { neg = true; tok = tok.Substring(1); }
            if (!Conditions.TryGetValue(tok, out var c)) throw new FormatException($"Unknown condition '{tok}'");
            return new Condition(c, neg);
        }

        static string Expect(List<string> t, ref int i, string what)
        {
            if (i >= t.Count) throw new FormatException($"Expected {what}");
            return t[i++];
        }

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        static List<string> Tokenize(string s)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            void Flush() { if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); } }
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch) || ch == ';' || ch == ',') { Flush(); continue; }
                if (ch == '{' || ch == '}') { Flush(); list.Add(ch.ToString()); continue; }
                sb.Append(ch);
            }
            Flush();
            return list;
        }

        /// <summary>Обратно в текст (для подсказок и логов).</summary>
        public static string Print(ProgramDef p)
        {
            var sb = new StringBuilder();
            foreach (var kv in p.Functions) { sb.Append("def ").Append(kv.Key).Append(" { "); PrintList(sb, kv.Value); sb.Append("} "); }
            PrintList(sb, p.Main);
            return sb.ToString().Trim();
        }

        static void PrintList(StringBuilder sb, List<Block> list)
        {
            foreach (var b in list)
            {
                switch (b)
                {
                    case ActionBlock a:
                        foreach (var kv in Actions) if (kv.Value == a.Action) { sb.Append(kv.Key).Append(' '); break; }
                        break;
                    case RepeatBlock r: sb.Append("repeat ").Append(r.Count).Append(" { "); PrintList(sb, r.Body); sb.Append("} "); break;
                    case WhileBlock w: sb.Append("while ").Append(Cond(w.Condition)).Append(" { "); PrintList(sb, w.Body); sb.Append("} "); break;
                    case IfBlock f:
                        sb.Append("if ").Append(Cond(f.Condition)).Append(" { "); PrintList(sb, f.Then); sb.Append("} ");
                        if (f.Else.Count > 0) { sb.Append("else { "); PrintList(sb, f.Else); sb.Append("} "); }
                        break;
                    case CallBlock c: sb.Append("call ").Append(c.Function).Append(' '); break;
                }
            }
        }

        static string Cond(Condition c)
        {
            foreach (var kv in Conditions) if (kv.Value == c.Type) return (c.Negate ? "!" : "") + kv.Key;
            return c.Type.ToString();
        }
    }
}
