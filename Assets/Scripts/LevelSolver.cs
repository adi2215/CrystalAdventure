using System;
using System.Collections.Generic;

/// <summary>
/// Находит программу с минимальным числом карточек для старой механики
/// (ходы F/L/R/B, гриб = прыжок на 2 клетки, While = «повторить ещё N раз карточки под скобкой»,
/// функции F1/F2 подставляют свои карточки).
///
/// Правила один в один как в MovingManager.BuildQueue + WayTiles.Resolve:
///   • клетки — колонки (x, y) Tilemap; ходить можно на любую клетку с тайлом;
///   • стоишь на грибе — следующий шаг на 2 клетки; нет клетки — куб стоит, ход потрачен;
///   • кристалл собирается, когда куб ВСТАЁТ на клетку (стартовая клетка не считается);
///   • как только собраны все кристаллы — победа, остальные команды не выполняются.
///
/// Без зависимостей от Unity — работает и в редакторе, и в тестах.
/// </summary>
public static class LevelSolver
{
    public const int MaxCardsPerPanel = 8;
    public const int MaxWhileIterations = 5;
    const int MaxQueue = 300;

    public struct Card
    {
        public ItemType Type;
        public int Iterations; // только для While: сколько раз повторить ещё
        public int Span;       // только для While: сколько карточек перед ним в цикле (0 — все до предыдущего While)
        public TileColor Color; // только для ходов: срабатывает лишь на клетке этого цвета
        public Card(ItemType type, int iterations = 1, int span = 0, TileColor color = TileColor.None) { Type = type; Iterations = iterations; Span = span; Color = color; }
        public override string ToString() => Type == ItemType.While ? (Span > 0 ? $"While×{Iterations + 1}[{Span}]" : $"While×{Iterations + 1}")
            : Color != TileColor.None ? $"{Type}@{Color}" : Type.ToString();
    }

    public sealed class Problem
    {
        public List<(int x, int y)> Tiles = new List<(int x, int y)>();
        public List<(int x, int y)> Mushrooms = new List<(int x, int y)>();
        public List<(int x, int y)> Crystals = new List<(int x, int y)>();
        public Dictionary<(int x, int y), TileColor> Colors = new Dictionary<(int x, int y), TileColor>();
        public (int x, int y) Start;
        public ItemType[] MainCards = new ItemType[0];
        public ItemType[] F1Cards;   // null — панели функции 1 нет
        public ItemType[] F2Cards;   // null — панели функции 2 нет
        public long NodeBudget = 30_000_000;
    }

    public sealed class Result
    {
        public bool Solvable;
        public bool ProvenOptimal;      // false — бюджет перебора кончился, найдено «не хуже чем»
        public int Cards;               // всего карточек в решении
        public int ShortestMoves;       // длина кратчайшего пути ходами
        public List<Card> Main = new List<Card>();
        public List<Card> F1 = new List<Card>();
        public List<Card> F2 = new List<Card>();
        public string Message = "";

        public override string ToString()
        {
            if (!Solvable) return Message;
            string s = $"{Cards} карт.: Main [{string.Join(", ", Main)}]";
            if (F1.Count > 0) s += $"  F1 [{string.Join(", ", F1)}]";
            if (F2.Count > 0) s += $"  F2 [{string.Join(", ", F2)}]";
            if (!ProvenOptimal) s += "  (может быть и короче — перебор не завершён)";
            return s;
        }
    }

    // ───────────────────────────── карта ─────────────────────────────

    sealed class Map
    {
        public readonly Dictionary<long, int> Index = new Dictionary<long, int>();
        public (int x, int y)[] Cells;
        public bool[] Mushroom;
        public int[] Color;            // TileColor клетки
        public readonly List<TileColor> UsedColors = new List<TileColor>();
        public int[] CrystalBit;       // -1 — нет кристалла
        public int Full;
        public int Start;
        // Переход: [клетка * 4 + направление] → клетка (или та же, если упёрся)
        public int[] Next;

        public static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public Map(Problem p)
        {
            var cells = new List<(int x, int y)>();
            foreach (var t in p.Tiles)
                if (!Index.ContainsKey(Key(t.x, t.y))) { Index[Key(t.x, t.y)] = cells.Count; cells.Add(t); }
            Cells = cells.ToArray();
            Mushroom = new bool[Cells.Length];
            Color = new int[Cells.Length];
            foreach (var kv in p.Colors)
                if (kv.Value != TileColor.None && Index.TryGetValue(Key(kv.Key.x, kv.Key.y), out var ci))
                {
                    Color[ci] = (int)kv.Value;
                    if (!UsedColors.Contains(kv.Value)) UsedColors.Add(kv.Value);
                }
            UsedColors.Sort();
            CrystalBit = new int[Cells.Length];
            for (int i = 0; i < CrystalBit.Length; i++) CrystalBit[i] = -1;

            foreach (var m in p.Mushrooms)
                if (Index.TryGetValue(Key(m.x, m.y), out var i)) Mushroom[i] = true;
            int bit = 0;
            foreach (var c in p.Crystals)
                if (Index.TryGetValue(Key(c.x, c.y), out var i) && CrystalBit[i] < 0) CrystalBit[i] = bit++;
            Full = (1 << bit) - 1;
            Start = Index.TryGetValue(Key(p.Start.x, p.Start.y), out var s) ? s : -1;

            Next = new int[Cells.Length * 4];
            for (int i = 0; i < Cells.Length; i++)
                for (int d = 0; d < 4; d++)
                {
                    var (dx, dy) = Dir(d);
                    int dist = Mushroom[i] ? 2 : 1;
                    Next[i * 4 + d] = Index.TryGetValue(Key(Cells[i].x + dx * dist, Cells[i].y + dy * dist), out var j) ? j : i;
                }
        }
    }

    static int DirIndex(ItemType t)
    {
        switch (t)
        {
            case ItemType.Forward: return 0;
            case ItemType.Right: return 1;
            case ItemType.Left: return 2;
            case ItemType.Bottom: return 3;
            default: return -1;
        }
    }

    static (int dx, int dy) Dir(int d)
    {
        switch (d)
        {
            case 0: return (0, 1);
            case 1: return (1, 0);
            case 2: return (-1, 0);
            default: return (0, -1);
        }
    }

    static ItemType DirType(int d) => d == 0 ? ItemType.Forward : d == 1 ? ItemType.Right : d == 2 ? ItemType.Left : ItemType.Bottom;

    struct Sim
    {
        public int Pos, Mask;
        public bool Won;
    }

    /// <summary>Шаг-«атом»: направление (0..3) + 4 × цвет условия (0 — без условия).</summary>
    static int Atom(int dir, TileColor color) => dir | ((int)color << 2);
    static int Atom(Card c) => Atom(DirIndex(c.Type), c.Color);

    static void Step(Map m, ref Sim s, int atom)
    {
        if (s.Won) return;
        int cond = atom >> 2;
        if (cond != 0 && m.Color[s.Pos] != cond) return;   // не тот цвет — карточка пропущена
        int dir = atom & 3;
        int to = m.Next[s.Pos * 4 + dir];
        if (to == s.Pos) return; // упёрся
        s.Pos = to;
        int b = m.CrystalBit[to];
        if (b >= 0) s.Mask |= 1 << b;
        if (s.Mask == m.Full) s.Won = true;
    }

    // ───────────────────────────── поиск ─────────────────────────────

    public static Result Solve(Problem p)
    {
        var res = new Result();
        var map = new Map(p);
        if (map.Start < 0) { res.Message = "Старт героя не стоит на клетке уровня"; return res; }
        if (map.Full == 0) { res.Message = "На уровне нет кристаллов"; return res; }

        var mainMoves = MovesOf(p.MainCards);
        if (mainMoves.Count == 0 && !Has(p.MainCards, ItemType.FunctionOne) && !Has(p.MainCards, ItemType.FunctionTwo))
        { res.Message = "На панели Main нет карточек движения"; return res; }

        // 1) Кратчайший путь ходами (BFS по (клетка, собранные кристаллы)).
        var allMoves = new HashSet<int>(mainMoves);
        if (p.F1Cards != null) allMoves.UnionWith(MovesOf(p.F1Cards));
        if (p.F2Cards != null) allMoves.UnionWith(MovesOf(p.F2Cards));
        var path = ShortestPath(map, allMoves);
        if (path == null) { res.Message = "Уровень не проходится: кристаллы недостижимы"; return res; }
        res.ShortestMoves = path.Count;

        // Простое решение «по карточке на шаг», если хватает слотов Main и все ходы есть на Main.
        bool plainOk = path.Count <= MaxCardsPerPanel && path.TrueForAll(mainMoves.Contains);
        if (plainOk)
        {
            res.Solvable = true;
            res.Cards = path.Count;
            foreach (var d in path) res.Main.Add(new Card(DirType(d)));
        }

        // 2) Ищем программу короче с функциями и While (перебор с углублением).
        var search = new Search(p, map);
        int limit = plainOk ? path.Count - 1 : MaxCardsPerPanel * 3;
        bool exhausted = true;
        for (int k = 1; k <= limit; k++)
        {
            if (search.TryCards(k, out var sol))
            {
                res.Solvable = true;
                res.Cards = k;
                res.Main = sol.main; res.F1 = sol.f1; res.F2 = sol.f2;
                break;
            }
            if (search.OutOfBudget) { exhausted = false; break; }
        }

        res.ProvenOptimal = res.Solvable && exhausted;
        if (!res.Solvable)
            res.Message = exhausted
                ? $"Не проходится в лимит {MaxCardsPerPanel} карточек на панель (кратчайший путь — {path.Count} ходов). Дай функции или While."
                : $"Решение не найдено за отведённое время (кратчайший путь — {path.Count} ходов).";
        return res;
    }

    static bool Has(ItemType[] cards, ItemType t) => cards != null && Array.IndexOf(cards, t) >= 0;

    static List<int> MovesOf(ItemType[] cards)
    {
        var list = new List<int>();
        if (cards == null) return list;
        foreach (var c in cards)
        {
            int d = DirIndex(c);
            if (d >= 0 && !list.Contains(d)) list.Add(d);
        }
        return list;
    }

    static List<int> ShortestPath(Map m, HashSet<int> moves)
    {
        var start = (m.Start, 0);
        var prev = new Dictionary<(int, int), ((int, int) from, int dir)>();
        var q = new Queue<(int pos, int mask)>();
        q.Enqueue(start);
        prev[start] = ((-1, -1), -1);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var d in moves)
            {
                var s = new Sim { Pos = cur.pos, Mask = cur.mask };
                Step(m, ref s, d);
                var nk = (s.Pos, s.Mask);
                if (prev.ContainsKey(nk)) continue;
                prev[nk] = (cur, d);
                if (s.Won)
                {
                    var path = new List<int>();
                    var k = nk;
                    while (prev[k].dir >= 0) { path.Add(prev[k].dir); k = prev[k].from; }
                    path.Reverse();
                    return path;
                }
                q.Enqueue(nk);
            }
        }
        return null;
    }

    sealed class Search
    {
        readonly Problem p;
        readonly Map map;
        readonly List<Card> mainAlphabet = new List<Card>();
        readonly List<Card> f1Alphabet, f2Alphabet;
        readonly bool mainHasWhile;
        readonly List<TileColor> colors;
        long nodes;
        public bool OutOfBudget => nodes > p.NodeBudget;

        // Для текущей попытки
        List<int> f1Exp, f2Exp;
        List<Card> f1Cards, f2Cards;
        readonly Dictionary<(int, int, ulong, int), int> seen = new Dictionary<(int, int, ulong, int), int>();
        readonly List<Card> mainStack = new List<Card>();

        public Search(Problem p, Map map)
        {
            this.p = p; this.map = map;
            mainHasWhile = Has(p.MainCards, ItemType.While);
            colors = map.UsedColors;
            foreach (var d in MovesOf(p.MainCards))
            {
                mainAlphabet.Add(new Card(DirType(d)));
                foreach (var c in colors) mainAlphabet.Add(new Card(DirType(d), color: c));
            }
            if (Has(p.MainCards, ItemType.FunctionOne) && p.F1Cards != null) mainAlphabet.Add(new Card(ItemType.FunctionOne));
            if (Has(p.MainCards, ItemType.FunctionTwo) && p.F2Cards != null) mainAlphabet.Add(new Card(ItemType.FunctionTwo));
            if (mainHasWhile)
                for (int n = 1; n <= MaxWhileIterations; n++) mainAlphabet.Add(new Card(ItemType.While, n));
            f1Alphabet = FunctionAlphabet(p.F1Cards, colors);
            f2Alphabet = FunctionAlphabet(p.F2Cards, colors);
        }

        static List<Card> FunctionAlphabet(ItemType[] cards, List<TileColor> colors)
        {
            if (cards == null) return null;
            var a = new List<Card>();
            foreach (var d in MovesOf(cards))
            {
                a.Add(new Card(DirType(d)));
                foreach (var c in colors) a.Add(new Card(DirType(d), color: c));
            }
            if (Has(cards, ItemType.While))
                for (int n = 1; n <= MaxWhileIterations; n++) a.Add(new Card(ItemType.While, n));
            return a.Count > 0 ? a : null;
        }

        public bool TryCards(int k, out (List<Card> main, List<Card> f1, List<Card> f2) sol)
        {
            sol = default;
            bool useF1 = mainAlphabet.Exists(c => c.Type == ItemType.FunctionOne);
            bool useF2 = mainAlphabet.Exists(c => c.Type == ItemType.FunctionTwo);
            int maxF1 = useF1 ? Math.Min(MaxCardsPerPanel, k - 1) : 0;

            for (int m1 = 0; m1 <= maxF1; m1++)
            {
                int maxF2 = useF2 ? Math.Min(MaxCardsPerPanel, k - 1 - m1) : 0;
                for (int m2 = 0; m2 <= maxF2; m2++)
                {
                    int mainLen = k - m1 - m2;
                    if (mainLen < 1 || mainLen > MaxCardsPerPanel) continue;

                    foreach (var f1 in Functions(f1Alphabet, m1))
                    {
                        foreach (var f2 in Functions(f2Alphabet, m2))
                        {
                            if (OutOfBudget) return false;
                            f1Cards = f1.cards; f1Exp = f1.exp;
                            f2Cards = f2.cards; f2Exp = f2.exp;
                            seen.Clear();
                            mainStack.Clear();
                            var s = new Sim { Pos = map.Start };
                            if (Dfs(s, new List<int>(), new List<int>(), 0, 0UL, mainLen, m1 > 0, m2 > 0))
                            {
                                sol = (new List<Card>(mainStack), new List<Card>(f1Cards), new List<Card>(f2Cards));
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        IEnumerable<(List<Card> cards, List<int> exp)> Functions(List<Card> alphabet, int len)
        {
            if (len == 0) { yield return (new List<Card>(), new List<int>()); yield break; }
            if (alphabet == null) yield break;
            var seenExp = new HashSet<string>();
            var idx = new int[len];
            while (true)
            {
                var cards = new List<Card>(len);
                for (int i = 0; i < len; i++) cards.Add(alphabet[idx[i]]);
                var exp = ExpandFunction(cards);
                // While в начале функции ничего не повторяет — такие варианты пропускаем.
                if (exp != null && exp.Count > 0 && cards[0].Type != ItemType.While && seenExp.Add(string.Join(",", exp)))
                    yield return (cards, exp);

                int pos = len - 1;
                while (pos >= 0 && ++idx[pos] == alphabet.Count) { idx[pos] = 0; pos--; }
                if (pos < 0) yield break;
            }
        }

        /// <summary>Функции перебираются только с «полной» скобкой (span 0) — иначе перебор взрывается.</summary>
        static List<int> ExpandFunction(List<Card> cards)
        {
            var local = new List<int>();
            int segStart = 0;
            foreach (var c in cards)
            {
                if (c.Type == ItemType.While)
                {
                    var body = local.GetRange(segStart, local.Count - segStart);
                    for (int r = 0; r < c.Iterations && local.Count + body.Count <= MaxQueue; r++) local.AddRange(body);
                    segStart = local.Count;
                }
                else local.Add(Atom(c));
                if (local.Count > MaxQueue) return null;
            }
            return local;
        }

        /// <summary>
        /// seg — шаги после последнего While, starts — где в seg начинается каждая карточка
        /// (скобка нового While может охватить 1..starts.Count последних карточек).
        /// </summary>
        bool Dfs(Sim s, List<int> seg, List<int> starts, int total, ulong hash, int left, bool mustCallF1, bool mustCallF2)
        {
            nodes++;
            if (s.Won) return !mustCallF1 && !mustCallF2;
            if (left == 0 || OutOfBudget) return false;

            // Отсечение повторов: в эту же клетку с теми же кристаллами (и тем же хвостом для While)
            // уже приходили, имея не меньше карточек в запасе.
            int flags = (mustCallF1 ? 1 : 0) | (mustCallF2 ? 2 : 0);
            var key = (s.Pos, s.Mask, mainHasWhile ? hash : 0UL, flags);
            if (seen.TryGetValue(key, out var bestLeft) && bestLeft >= left) return false;
            seen[key] = left;

            foreach (var card in mainAlphabet)
            {
                if (card.Type == ItemType.While)
                {
                    if (starts.Count == 0) continue;
                    for (int span = starts.Count; span >= 1; span--)
                    {
                        int from = starts[starts.Count - span];
                        int bodyLen = seg.Count - from;
                        if (bodyLen == 0 || total + bodyLen * card.Iterations > MaxQueue) continue;
                        var ns = s;
                        for (int r = 0; r < card.Iterations && !ns.Won; r++)
                            for (int k = from; k < seg.Count && !ns.Won; k++) Step(map, ref ns, seg[k]);
                        mainStack.Add(new Card(ItemType.While, card.Iterations, span));
                        // После цикла хвост пустой: следующий While не может залезть за этот.
                        if (Dfs(ns, new List<int>(), new List<int>(), total + bodyLen * card.Iterations, 0x9E37UL, left - 1, mustCallF1, mustCallF2)) return true;
                        mainStack.RemoveAt(mainStack.Count - 1);
                    }
                    continue;
                }

                var ns2 = s;
                bool f1 = mustCallF1, f2 = mustCallF2;
                List<int> moves;
                switch (card.Type)
                {
                    case ItemType.FunctionOne: moves = f1Exp; f1 = false; break;
                    case ItemType.FunctionTwo: moves = f2Exp; f2 = false; break;
                    default: moves = new List<int> { Atom(card) }; break;
                }
                if (total + moves.Count > MaxQueue) continue;

                var nseg = seg;
                var nstarts = starts;
                ulong nh = hash;
                if (mainHasWhile)
                {
                    nseg = new List<int>(seg);
                    nstarts = new List<int>(starts) { seg.Count };
                    nh = nh * 31 + 7; // граница карточки
                }
                foreach (var d in moves)
                {
                    Step(map, ref ns2, d);
                    if (mainHasWhile) { nseg.Add(d); nh = nh * 37 + (ulong)d + 1; }
                }

                mainStack.Add(card);
                if (Dfs(ns2, nseg, nstarts, total + moves.Count, nh, left - 1, f1, f2)) return true;
                mainStack.RemoveAt(mainStack.Count - 1);
            }
            return false;
        }
    }
}
