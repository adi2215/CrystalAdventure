using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Программа игрока (карточки) и запуск/остановка её выполнения.
///
/// Раньше команды «съедались» из списков во время игры, а Rewind восстанавливал их
/// из копий — при быстрых нажатиях Play/Rewind копии расходились и игра ломалась.
/// Теперь:
///   • списки карточек (Commands) НЕ меняются во время игры;
///   • при Play из них строится отдельная очередь команд для куба;
///   • есть явное состояние Idle / Playing / Finished, и каждая кнопка
///     работает только в «своём» состоянии — спамить можно сколько угодно;
///   • кнопки Play/Rewind и панель-блокировка всегда показываются по состоянию.
/// </summary>
public class MovingManager : MonoBehaviour
{
    public enum RunState { Idle, Playing, Finished }

    public const int MaxCommandsPerSlot = 8;
    const int MaxQueueLength = 300;   // защита от «бесконечных» While внутри функций
    const int MaxCallDepth = 6;       // F1 → F2 → F1 ...
    const float ButtonCooldown = 0.15f;

    public Dictionary<SlotType, List<Item>> Commands = new()
    {
        { SlotType.Main, new List<Item>() },
        { SlotType.FunctionOne, new List<Item>() },
        { SlotType.FunctionTwo, new List<Item>() }
    };

    // Число повторов у каждой карточки While — своё (раньше писалось в общий ассет Item).
    readonly Dictionary<SlotType, List<int>> _iterations = new()
    {
        { SlotType.Main, new List<int>() },
        { SlotType.FunctionOne, new List<int>() },
        { SlotType.FunctionTwo, new List<int>() }
    };

    // Сколько слотов ПЕРЕД карточкой While охватывает её скобка (тянется за стрелку).
    readonly Dictionary<SlotType, List<int>> _spans = new()
    {
        { SlotType.Main, new List<int>() },
        { SlotType.FunctionOne, new List<int>() },
        { SlotType.FunctionTwo, new List<int>() }
    };

    // Условие карточки хода: срабатывает только на клетке этого цвета.
    readonly Dictionary<SlotType, List<TileColor>> _colors = new()
    {
        { SlotType.Main, new List<TileColor>() },
        { SlotType.FunctionOne, new List<TileColor>() },
        { SlotType.FunctionTwo, new List<TileColor>() }
    };

    /// <summary>Программа изменилась (карточку положили/убрали, сменили повторы или скобку цикла).</summary>
    public event System.Action<SlotType> ProgramChanged;

    public InventoryManager Items;
    public InventoryManager Items1;
    public InventoryManager Items2;

    [Tooltip("Только для чтения: true, пока куб выполняет программу")]
    public bool play = false;

    public GameObject Rewind;
    public GameObject Play;

    [Tooltip("Панель, которая закрывает карточки во время выполнения (в сцене это 'Panel 1')")]
    public GameObject inputBlocker;

    public CharacterCode character;

    [Header("Новое ядро (перенос на CrystalCore)")]
    [Tooltip("Куда идти, решает новое ядро CrystalCore, а показывает всё тот же куб и старый UI. Выключено — старый WayTiles (запасной путь).")]
    public bool useCrystalCore = true;
    [Tooltip("Писать в Console итог каждого запуска через ядро (только в редакторе)")]
    public bool logCoreRuns = true;

    public RunState State { get; private set; } = RunState.Idle;
    public bool IsPlaying => State == RunState.Playing;
    /// <summary>Можно ли сейчас менять карточки.</summary>
    public bool CanEdit => State == RunState.Idle;

    float _lastButtonTime = -10f;

    Data GameData => character != null ? character.tran : null;

    private void Start()
    {
        InstallJuice();
        InstallLoopBrackets();
        PaintPalette.Attach(this);
        SpeedControl.Attach(this); // кнопка ×1 / ×2 / ×4
        SyncButtons();
    }

    /// <summary>Над каждой панелью программы — слой со скобками циклов.</summary>
    void InstallLoopBrackets()
    {
        LoopBrackets.Attach(this, Items, SlotType.Main);
        LoopBrackets.Attach(this, Items1, SlotType.FunctionOne);
        LoopBrackets.Attach(this, Items2, SlotType.FunctionTwo);
    }

    /// <summary>
    /// «Живые» кнопки: Play, Rewind и все кнопки, которые вызывают MovingManager,
    /// PanelController (вкладки Main/F1/F2) или HintSystem. Кнопки команд оживляет SpawnScript.
    /// </summary>
    void InstallJuice()
    {
        _playJuice = ButtonJuice.Ensure(Play);
        ButtonJuice.Ensure(Rewind);
        foreach (var button in FindObjectsOfType<UnityEngine.UI.Button>(true))
        {
            var e = button.onClick;
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
            {
                var target = e.GetPersistentTarget(i);
                if (target is MovingManager || target is PanelController || target is HintSystem)
                {
                    ButtonJuice.Ensure(button.gameObject);
                    break;
                }
            }
        }
    }

    ButtonJuice _playJuice;

    // Состояние кнопок выставляется каждый кадр ПОСЛЕ всех OnClick —
    // поэтому лишние SetActive из сцены больше не могут их «перепутать».
    private void LateUpdate() => SyncButtons();

    void SyncButtons()
    {
        play = State == RunState.Playing;
        SetActiveSafe(Play, State == RunState.Idle);
        SetActiveSafe(Rewind, State != RunState.Idle);
        SetActiveSafe(inputBlocker, State != RunState.Idle);
        // Play «дышит», когда программа собрана и её можно запускать.
        if (_playJuice != null) _playJuice.breathe = State == RunState.Idle && CountCards() > 0;
    }

    static void SetActiveSafe(GameObject go, bool value)
    {
        if (go != null && go.activeSelf != value) go.SetActive(value);
    }

    bool Cooldown()
    {
        if (Time.unscaledTime - _lastButtonTime < ButtonCooldown) return true;
        _lastButtonTime = Time.unscaledTime;
        return false;
    }

    // ============================================================
    //                       КАРТОЧКИ
    // ============================================================

    /// <summary>Положить карточку в первый свободный слот (старый API).</summary>
    public bool AddCommand(Item command, SlotType type)
    {
        var list = Commands[type];
        int index = list.IndexOf(null);
        if (index == -1) index = list.Count;
        return SetCommand(type, index, command, command != null ? command.iteration : 1);
    }

    /// <summary>Положить карточку в конкретный слот (как в UI).</summary>
    public bool SetCommand(SlotType type, int index, Item command, int iteration)
    {
        if (!CanEdit || command == null) return false;
        if (index < 0 || index >= MaxCommandsPerSlot) return false;

        var list = Commands[type];
        var iters = _iterations[type];
        var spans = _spans[type];
        var colors = _colors[type];
        while (colors.Count <= index) colors.Add(TileColor.None);
        while (list.Count <= index) list.Add(null);
        while (iters.Count <= index) iters.Add(1);
        while (spans.Count <= index) spans.Add(0);

        if (list[index] != null) return false; // слот занят
        list[index] = command;
        iters[index] = Mathf.Max(1, iteration);
        colors[index] = TileColor.None;
        // Новый цикл сразу охватывает всё, что перед ним (как раньше), — потом скобку можно сузить.
        spans[index] = command.type == ItemType.While ? MaxSpan(type, index) : 0;
        ClampSpans(type);
        ProgramChanged?.Invoke(type);
        return true;
    }

    // ───────────── условие (цвет клетки) ─────────────

    /// <summary>Можно ли покрасить эту карточку: только ходы.</summary>
    public static bool CanHaveColor(Item item) => item != null && WayTiles.Direction(item.type) != Vector3Int.zero;

    public TileColor GetColor(SlotType type, int index)
    {
        var colors = _colors[type];
        return index >= 0 && index < colors.Count ? colors[index] : TileColor.None;
    }

    public bool SetColor(SlotType type, int index, TileColor color)
    {
        if (!CanEdit) return false;
        var list = Commands[type];
        if (index < 0 || index >= list.Count || !CanHaveColor(list[index])) return false;
        var colors = _colors[type];
        while (colors.Count <= index) colors.Add(TileColor.None);
        if (colors[index] == color) return false;
        colors[index] = color;
        ProgramChanged?.Invoke(type);
        return true;
    }

    // ───────────── скобка цикла ─────────────

    /// <summary>Сколько слотов перед While охвачено циклом.</summary>
    public int GetSpan(SlotType type, int index)
    {
        var spans = _spans[type];
        return index >= 0 && index < spans.Count ? spans[index] : 0;
    }

    /// <summary>Максимум для скобки: до начала панели или до предыдущего While (циклы не вкладываются).</summary>
    public int MaxSpan(SlotType type, int index)
    {
        var list = Commands[type];
        int start = 0;
        for (int j = Mathf.Min(index, list.Count) - 1; j >= 0; j--)
            if (list[j] != null && list[j].type == ItemType.While) { start = j + 1; break; }
        return Mathf.Max(0, index - start);
    }

    public bool SetSpan(SlotType type, int index, int span)
    {
        if (!CanEdit) return false;
        var list = Commands[type];
        if (index < 0 || index >= list.Count || list[index] == null || list[index].type != ItemType.While) return false;
        var spans = _spans[type];
        while (spans.Count <= index) spans.Add(0);
        span = Mathf.Clamp(span, 0, MaxSpan(type, index));
        if (spans[index] == span) return false;
        spans[index] = span;
        ProgramChanged?.Invoke(type);
        return true;
    }

    /// <summary>Сколько карточек (не пустых слотов) внутри скобки — для сравнения с решением.</summary>
    public int CardsInSpan(SlotType type, int index)
    {
        var list = Commands[type];
        int n = 0;
        for (int j = Mathf.Max(0, index - GetSpan(type, index)); j < index && j < list.Count; j++)
            if (list[j] != null) n++;
        return n;
    }

    void ClampSpans(SlotType type)
    {
        var list = Commands[type];
        var spans = _spans[type];
        for (int i = 0; i < list.Count && i < spans.Count; i++)
            if (list[i] != null && list[i].type == ItemType.While)
                spans[i] = Mathf.Clamp(spans[i], 0, MaxSpan(type, i));
    }

    public int GetIteration(SlotType type, int index)
    {
        var iters = _iterations[type];
        return index >= 0 && index < iters.Count ? iters[index] : 1;
    }

    public void SetIteration(SlotType type, int index, int iteration)
    {
        var iters = _iterations[type];
        if (index < 0 || index >= iters.Count) return;
        iters[index] = Mathf.Max(1, iteration);
        ProgramChanged?.Invoke(type);
    }

    /// <summary>Старый API: общий индекс слота (MovingSlot0..7 / 10..17 / 20..27).</summary>
    public void RemoveCommand(int index)
    {
        RemoveCommandAt(GetSlotByIndex(index), GetLocalIndex(index));
    }

    public bool RemoveCommandAt(SlotType slot, int localIndex)
    {
        if (!CanEdit) return false;
        var list = Commands[slot];
        if (localIndex < 0 || localIndex >= list.Count) return false;
        list[localIndex] = null;
        ClampSpans(slot);
        ProgramChanged?.Invoke(slot);
        return true;
    }

    public static SlotType GetSlotByIndex(int index)
    {
        if (index < 10) return SlotType.Main;
        if (index < 20) return SlotType.FunctionOne;
        return SlotType.FunctionTwo;
    }

    public static int GetLocalIndex(int index) => index % 10;

    // ============================================================
    //                       ОЧИСТИТЬ ВСЁ
    // ============================================================

    public void ClearButtons()
    {
        if (Cooldown()) return;

        StopRunAndReset();

        foreach (var list in Commands.Values) list.Clear();
        foreach (var list in _iterations.Values) list.Clear();
        foreach (var list in _spans.Values) list.Clear();
        foreach (var list in _colors.Values) list.Clear();
        ProgramChanged?.Invoke(SlotType.Main);
        ProgramChanged?.Invoke(SlotType.FunctionOne);
        ProgramChanged?.Invoke(SlotType.FunctionTwo);

        if (Items != null) Items.GetDestroyItem();
        if (Items1 != null) Items1.GetDestroyItem();
        if (Items2 != null) Items2.GetDestroyItem();

        SyncButtons();
    }

    // ============================================================
    //                         PLAY
    // ============================================================

    public void StartPlay()
    {
        if (State != RunState.Idle) return;     // уже играет / уже доиграл — сначала Rewind
        if (Cooldown()) return;
        if (character == null || !character.IsReady) return; // куб ещё падает на поле
        var data = GameData;
        if (data != null && data.DialogManager) return;       // открыт диалог

        var queue = BuildQueue(out _runOrigins);
        if (queue.Count == 0) return;            // пустая программа — нечего запускать

        if (data != null) data.commandUsed = CountCards();

        HintGhost.StopActive();
        LastRunEdgeHits = 0;
        State = RunState.Playing;
        SyncButtons();
        if (useCrystalCore && TryPlanWithCore(out var plan))
            character.RunPlan(queue, plan, OnRunFinished, OnStep, OnEdge, OnSkip);
        else
            character.RunCommands(queue, OnRunFinished, OnStep, OnEdge, CanRunStep, OnSkip);
    }

    /// <summary>
    /// Посчитать прогон новым ядром. Не вышло (нет уровня / старта / ошибка) — false, и игра
    /// идёт по-старому, чтобы флаг useCrystalCore никогда не ломал прохождение.
    /// </summary>
    bool TryPlanWithCore(out List<CrystalAdventure.UnityLayer.CorePlannedStep> plan)
    {
        plan = null;
        var level = TilemapData.CurrentLevel;
        var start = character != null ? character.StartTile : null;
        if (level == null || start == null) return false;
        try
        {
            if (!CrystalAdventure.UnityLayer.CoreRunBridge.TryPlan(this, level, new Vector2Int(start.gridLocation.x, start.gridLocation.y), out plan, out var result))
                return false;
#if UNITY_EDITOR
            if (logCoreRuns)
                Debug.Log($"[Crystal] Ядро: {level.name} — {result.Outcome}, ходов {result.Turns.Count}, карточек {result.BlocksUsed}, край {result.EdgeHits}");
#endif
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Crystal] Ядро не посчитало прогон, играю по-старому: " + e.Message);
            plan = null;
            return false;
        }
    }

    void OnRunFinished()
    {
        if (State == RunState.Playing) State = RunState.Finished;
        SyncButtons();
    }

    /// <summary>Все кристаллы собраны — дальше только окно победы или Rewind.</summary>
    public void OnLevelComplete()
    {
        if (character != null)
        {
            character.StopRun();
            character.Celebrate();
        }
        State = RunState.Finished;
        SyncButtons();
    }

    /// <summary>
    /// Герой коснулся врага (уровни с врагами): куб уже исчез телепортом — сбрасываем уровень, как Rewind,
    /// и куб проявляется на старте. Карточки остаются — можно сразу поправить программу.
    /// </summary>
    public void OnHeroDefeated()
    {
        if (State != RunState.Playing) return;
        StopRunAndReset();
        SyncButtons();
        if (character != null) character.PlayTeleportIn();
    }

    // ============================================================
    //                         REWIND
    // ============================================================

    public void ReversePlay()
    {
        if (State == RunState.Idle) return;
        if (Cooldown()) return;
        StopRunAndReset();
        SyncButtons();
    }

    void StopRunAndReset()
    {
        HintGhost.StopActive();
        if (character != null)
        {
            character.star1 = false;
            character.StopAndReset();
        }
        if (ActiveBlockManager.Instance != null) ActiveBlockManager.Instance.ResetLevelObjects();
        State = RunState.Idle;
    }

    // ============================================================
    //                 ПОСТРОЕНИЕ ОЧЕРЕДИ КОМАНД
    // ============================================================

    /// <summary>Какая карточка породила шаг: сама карточка и (для функций) карточка вызова в Main.</summary>
    public struct CardRef
    {
        public SlotType Slot;
        public int Index;
        public SlotType CallerSlot;
        public int CallerIndex;   // -1 — шаг напрямую из Main
        // Цикл, внутри которого шаг (для подсветки скобки): -1 — не в цикле.
        public SlotType LoopSlot;
        public int LoopIndex;
        public int LoopPass;      // 0 — первый проход, 1.. — повторы
        public int LoopTotal;     // всего проходов
        public TileColor Color;   // условие карточки (None — всегда)
    }

    struct QueueStep
    {
        public Item Item;
        public CardRef Card;
    }

    /// <summary>
    /// Разворачивает программу в плоский список шагов, НЕ трогая карточки.
    /// While(N) — повторить ещё N раз карточки под его скобкой (span слотов перед ним).
    /// F1/F2 — подставить содержимое функции (функции могут вызывать друг друга до MaxCallDepth).
    /// </summary>
    public List<Item> BuildQueue() => BuildQueue(out _);

    /// <summary>То же, плюс для каждого шага — карточка, из которой он взялся (для подсветки).</summary>
    public List<Item> BuildQueue(out List<CardRef> origins)
    {
        var steps = new List<QueueStep>();
        Expand(SlotType.Main, 0, steps, -1);
        var result = new List<Item>(steps.Count);
        origins = new List<CardRef>(steps.Count);
        foreach (var s in steps) { result.Add(s.Item); origins.Add(s.Card); }
        return result;
    }

    void Expand(SlotType slot, int depth, List<QueueStep> output, int callerIndex)
    {
        var list = Commands[slot];
        var local = new List<QueueStep>();
        var startOf = new int[list.Count + 1];   // с какого шага начинается каждый слот

        for (int i = 0; i < list.Count; i++)
        {
            startOf[i] = local.Count;
            var cmd = list[i];
            if (cmd == null) continue;
            // Для шагов внутри функций «вызывающая» карточка — та, что стоит в Main.
            int caller = slot == SlotType.Main ? i : callerIndex;

            switch (cmd.type)
            {
                case ItemType.While:
                {
                    int n = i < _iterations[slot].Count ? _iterations[slot][i] : cmd.iteration;
                    int span = Mathf.Clamp(GetSpan(slot, i), 0, MaxSpan(slot, i));
                    int from = startOf[i - span];
                    var body = local.GetRange(from, local.Count - from);
                    if (body.Count == 0) break;
                    int total = n + 1;
                    // Первый проход уже в local — помечаем его как проход 1 этого цикла.
                    for (int k = from; k < local.Count; k++)
                    {
                        var q = local[k];
                        if (q.Card.LoopIndex < 0) { q.Card.LoopSlot = slot; q.Card.LoopIndex = i; q.Card.LoopPass = 0; q.Card.LoopTotal = total; }
                        local[k] = q;
                    }
                    for (int r = 0; r < n && local.Count + body.Count <= MaxQueueLength; r++)
                        foreach (var b in body)
                        {
                            var q = b;
                            q.Card.LoopSlot = slot; q.Card.LoopIndex = i; q.Card.LoopPass = r + 1; q.Card.LoopTotal = total;
                            local.Add(q);
                        }
                    break;
                }
                case ItemType.FunctionOne:
                    if (depth < MaxCallDepth) Expand(SlotType.FunctionOne, depth + 1, local, caller);
                    break;
                case ItemType.FunctionTwo:
                    if (depth < MaxCallDepth) Expand(SlotType.FunctionTwo, depth + 1, local, caller);
                    break;
                case ItemType.Ifandelse:
                case ItemType.Null:
                    break;
                default:
                    local.Add(new QueueStep
                    {
                        Item = cmd,
                        Card = new CardRef { Slot = slot, Index = i, CallerSlot = SlotType.Main, CallerIndex = slot == SlotType.Main ? -1 : caller, LoopIndex = -1, Color = GetColor(slot, i) },
                    });
                    break;
            }

            if (local.Count > MaxQueueLength) local.RemoveRange(MaxQueueLength, local.Count - MaxQueueLength);
        }

        output.AddRange(local);
        if (output.Count > MaxQueueLength) output.RemoveRange(MaxQueueLength, output.Count - MaxQueueLength);
    }

    // ============================================================
    //            ПОДСВЕТКА КАРТОЧЕК ВО ВРЕМЯ ВЫПОЛНЕНИЯ
    // ============================================================

    List<CardRef> _runOrigins;

    InventoryManager PanelOf(SlotType slot) => slot == SlotType.Main ? Items : slot == SlotType.FunctionOne ? Items1 : Items2;

    /// <summary>Карточка шага: сама (если её панель видна) или карточка вызова функции в Main.</summary>
    InventoryItems CardForStep(int step)
    {
        if (_runOrigins == null || step < 0 || step >= _runOrigins.Count) return null;
        var o = _runOrigins[step];
        var inv = PanelOf(o.Slot);
        var card = inv != null ? inv.FindCard(o.Index) : null;
        if (card != null && card.gameObject.activeInHierarchy) return card;
        if (o.CallerIndex >= 0)
        {
            var main = PanelOf(o.CallerSlot);
            var callerCard = main != null ? main.FindCard(o.CallerIndex) : null;
            if (callerCard != null) return callerCard;
        }
        return card;
    }

    void OnStep(int step)
    {
        var card = CardForStep(step);
        if (card != null) card.PulseStep();
        if (_runOrigins != null && step >= 0 && step < _runOrigins.Count)
        {
            var o = _runOrigins[step];
            if (o.LoopIndex >= 0) LoopBrackets.For(o.LoopSlot)?.OnLoopStep(o.LoopIndex, o.LoopPass, o.LoopTotal);
        }
    }

    /// <summary>Условие шага: покрашенная карточка срабатывает только на клетке своего цвета.</summary>
    bool CanRunStep(int step, OverlayTile tile)
    {
        if (_runOrigins == null || step < 0 || step >= _runOrigins.Count) return true;
        return ColorTiles.Passes(_runOrigins[step].Color, tile);
    }

    /// <summary>Карточка пропущена (не тот цвет) — она коротко «гаснет».</summary>
    void OnSkip(int step)
    {
        var card = CardForStep(step);
        if (card != null) card.Skipped();
        if (_runOrigins != null && step >= 0 && step < _runOrigins.Count)
        {
            var o = _runOrigins[step];
            if (o.LoopIndex >= 0) LoopBrackets.For(o.LoopSlot)?.OnLoopStep(o.LoopIndex, o.LoopPass, o.LoopTotal);
        }
    }

    /// <summary>Сколько раз куб «чуть не упал» в последнем запуске (для окна победы).</summary>
    public static int LastRunEdgeHits { get; private set; }

    void OnEdge(int step)
    {
        LastRunEdgeHits++;
        var card = CardForStep(step);
        if (card != null) card.Danger();
    }

    public int CountCards()
    {
        int n = 0;
        foreach (var list in Commands.Values)
            foreach (var item in list)
                if (item != null) n++;
        return n;
    }

    public void DebugCommandsState()
    {
        foreach (var kv in Commands)
        {
            var parts = new List<string>();
            for (int i = 0; i < kv.Value.Count; i++)
                parts.Add(kv.Value[i] == null ? $"[{i}] -" : $"[{i}] {kv.Value[i].type}");
            Debug.Log($"{kv.Key}: {string.Join(", ", parts)}");
        }
        Debug.Log($"State: {State}, queue: {BuildQueue().Count} steps");
    }
}

public enum SlotType
{
    Main,
    FunctionOne,
    FunctionTwo
}
