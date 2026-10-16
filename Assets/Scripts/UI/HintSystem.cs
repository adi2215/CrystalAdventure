using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Кнопка «Подсказка». Два режима (поле mode):
///
/// Ghost (по умолчанию) — полупрозрачный «призрак» куба проходит примерно половину
/// эталонного решения, над ним всплывают нужные карточки; к ~40% пути призрак тает.
///
/// Buttons — сравнивает программу игрока с решением и показывает ОДИН следующий шаг:
///   • неверная карточка — трясётся и мигает красным (убери её);
///   • у While не то число повторов или не та скобка цикла — мигает жёлтым;
///   • не хватает карточки — пульсирует нужная кнопка команды
///     (или вкладка Main/F1, если кнопка на другой вкладке);
///   • программа совпадает с решением — пульсирует Play.
/// Если куб уже едет — пульсирует Rewind.
///
/// Создать кнопку в сцене: меню Crystal Adventure → Добавить кнопку подсказки.
/// </summary>
public class HintSystem : MonoBehaviour
{
    /// <summary>Сколько подсказок взято на текущем уровне (сбрасывается при загрузке уровня).</summary>
    public static int HintsUsed { get; private set; }
    public static void ResetForLevel() => HintsUsed = 0;

    public enum Mode { Ghost, Buttons }

    public MovingManager moving;
    [Tooltip("Необязательно — найдутся сами")] public PanelController panels;

    [Header("Режим подсказки")]
    public Mode mode = Mode.Ghost;

    [Header("Призрак")]
    [Range(0.1f, 1f)] [Tooltip("Какую часть решения показать")] public float showFraction = 0.5f;
    [Range(0f, 1f)] [Tooltip("С какой части пути призрак начинает таять")] public float fadeStart = 0.4f;
    [Tooltip("Секунд на один шаг призрака")] public float ghostStepTime = 0.55f;
    [Range(0.1f, 1f)] public float ghostAlpha = 0.55f;
    public Color ghostTint = new Color(0.65f, 0.9f, 1f, 1f);

    static readonly SlotType[] Order = { SlotType.Main, SlotType.FunctionOne, SlotType.FunctionTwo };

    void Awake()
    {
        if (moving == null) moving = FindObjectOfType<MovingManager>();
        if (panels == null) panels = FindObjectOfType<PanelController>();
    }

    public void ShowHint()
    {
        if (moving == null) return;

        if (moving.State != MovingManager.RunState.Idle)
        {
            Attention(moving.Rewind);
            return;
        }

        var level = TilemapData.CurrentLevel;
        if (level == null || !level.HasSolution)
        {
            Debug.LogWarning("[Подсказка] У уровня нет решения. Редактор уровней → «Решить все уровни».");
            var self = ButtonJuice.Ensure(gameObject);
            if (self != null) self.Deny();
            return;
        }

        if (mode == Mode.Ghost)
        {
            var settings = new HintGhost.Settings
            {
                ShowFraction = showFraction,
                FadeStart = Mathf.Min(fadeStart, showFraction),
                StepTime = Mathf.Max(0.15f, ghostStepTime),
                Alpha = ghostAlpha,
                Tint = ghostTint,
            };
            if (HintGhost.Play(moving.character, level, CardIcon(level), settings)) HintsUsed++;
            return;
        }

        HintsUsed++;

        foreach (var slot in Order)
        {
            var solution = level.GetSolution(slot);
            var player = PlayerCards(slot);

            int n = Mathf.Max(solution.Count, player.Count);
            for (int i = 0; i < n; i++)
            {
                if (i >= player.Count)
                {
                    PointToButton(slot, solution[i].type);
                    return;
                }

                var (index, item, iterations) = player[i];
                if (i >= solution.Count || item.type != solution[i].type)
                {
                    var card = FindCard(slot, index);
                    if (card != null) card.HintWrong();
                    return;
                }
                if (MovingManager.CanHaveColor(item) && moving.GetColor(slot, index) != solution[i].color)
                {
                    // Карточка та, но не то условие (цвет) — мигает жёлтым.
                    var card = FindCard(slot, index);
                    if (card != null) card.HintIterations();
                    return;
                }
                if (item.type == ItemType.While &&
                    (iterations != Mathf.Max(1, solution[i].iterations) ||
                     moving.CardsInSpan(slot, index) != SolutionCard.ResolveSpan(solution, i)))
                {
                    var card = FindCard(slot, index);
                    if (card != null) card.HintIterations();
                    LoopBrackets.For(slot)?.HintLoop(index);
                    return;
                }
            }
        }

        Attention(moving.Play); // программа как в решении — жми Play
    }

    /// <summary>Картинка карточки по типу — берём из карточек, доступных на уровне.</summary>
    static System.Func<ItemType, Sprite> CardIcon(LevelSt level)
    {
        var sprites = new Dictionary<ItemType, Sprite>();
        if (level.panels != null)
            foreach (var panel in level.panels)
                if (panel?.commands != null)
                    foreach (var item in panel.commands)
                        if (item != null && item.image != null && !sprites.ContainsKey(item.type)) sprites[item.type] = item.image;
        return t => sprites.TryGetValue(t, out var s) ? s : null;
    }

    /// <summary>Карточки игрока в панели по порядку (пустые слоты пропускаются, как при выполнении).</summary>
    List<(int index, Item item, int iterations)> PlayerCards(SlotType slot)
    {
        var list = new List<(int, Item, int)>();
        var cards = moving.Commands[slot];
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null) list.Add((i, cards[i], moving.GetIteration(slot, i)));
        return list;
    }

    InventoryItems FindCard(SlotType slot, int index)
    {
        InventoryManager inv = slot == SlotType.Main ? moving.Items : slot == SlotType.FunctionOne ? moving.Items1 : moving.Items2;
        return inv != null ? inv.FindCard(index) : null;
    }

    void PointToButton(SlotType slot, ItemType type)
    {
        var spawns = FindObjectsOfType<SpawnScript>(true);
        var spawn = spawns.FirstOrDefault(s => s.type == slot && s.FindButton(type) != null);
        var button = spawn != null ? spawn.FindButton(type) : null;

        if (button != null && button.activeInHierarchy)
        {
            Attention(button);
            return;
        }

        // Кнопка на другой вкладке — подсвечиваем вкладку (Main / F1 / F2).
        if (panels != null && spawn != null && panels.spawnMethods != null && panels.buttonPanels != null)
        {
            int i = System.Array.IndexOf(panels.spawnMethods, spawn);
            if (i >= 0 && i < panels.buttonPanels.Length && panels.buttonPanels[i] != null)
            {
                Attention(panels.buttonPanels[i]);
                return;
            }
        }
        if (button != null) Attention(button);
    }

    static void Attention(GameObject go)
    {
        var juice = ButtonJuice.Ensure(go);
        if (juice != null) juice.Attention();
    }
}
