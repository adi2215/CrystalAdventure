using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Scriptable object/Level")]
public class LevelSt : ScriptableObject
{
    public List<TileInfo> tiles;

    /*public string levelName;
    
    public int[,] tileMatrix;*/

    public Vector3 characterPos;

    public Panels[] panels;

    public DialogTrigger dialog;

    [Tooltip("Пошаговый туториал (Create → Crystal Adventure → Tutorial). Показывается, пока уровень не пройден; старый диалог тогда не открывается.")]
    public TutorialScript tutorial;

    [Header("Враги (ядро CrystalCore)")]
    [Tooltip("Враги уровня. Archetype: Walker (синий куб по маршруту), BossForest / BossWinter (большие кубы по маршруту), FrostTurret (пушка). Касание кубов — смерть.")]
    public List<LevelEnemy> enemies = new List<LevelEnemy>();
    [Tooltip("Здоровье героя на уровне с врагами (пока у героя одна жизнь)")] [Min(1)] public int heroHp = 1;
    [Tooltip("Сколько карточек «Супер-атака» сработает за уровень")] [Min(0)] public int superAttacks = 1;

    public bool HasEnemies => enemies != null && enemies.Count > 0;

    [Header("Решение — заполняет Crystal Adventure → Редактор уровней")]
    [Tooltip("Минимум карточек для прохождения. 0 — не посчитано (тогда звёзды по общим порогам).")]
    public int optimalCards;
    public List<SolutionCard> solutionMain = new List<SolutionCard>();
    public List<SolutionCard> solutionF1 = new List<SolutionCard>();
    public List<SolutionCard> solutionF2 = new List<SolutionCard>();

    public bool HasSolution => optimalCards > 0 && solutionMain != null && solutionMain.Count > 0;

    public List<SolutionCard> GetSolution(SlotType slot)
    {
        switch (slot)
        {
            case SlotType.FunctionOne: return solutionF1 ?? new List<SolutionCard>();
            case SlotType.FunctionTwo: return solutionF2 ?? new List<SolutionCard>();
            default: return solutionMain ?? new List<SolutionCard>();
        }
    }
}

[System.Serializable]
public class SolutionCard
{
    public ItemType type;
    [Tooltip("Только для While: сколько раз повторить ещё")] public int iterations = 1;
    [Tooltip("Только для While: сколько карточек перед ним в цикле. 0 — все до предыдущего While")] public int span;
    [Tooltip("Карточка хода работает только на клетке этого цвета (None — всегда)")] public TileColor color;

    /// <summary>Сколько карточек реально охватывает цикл, стоящий на позиции index (0 → все до предыдущего While).</summary>
    public static int ResolveSpan(List<SolutionCard> cards, int index)
    {
        int max = 0;
        for (int j = index - 1; j >= 0 && cards[j].type != ItemType.While; j--) max++;
        int span = cards[index].span;
        return span <= 0 ? max : System.Math.Min(span, max);
    }
}

/// <summary>Враг на старте уровня. facing — куда смотрит, теми же стрелками, что карточки (Forward = вверх по сетке).</summary>
[System.Serializable]
public class LevelEnemy
{
    public Vector2Int cell;
    public string archetype = "Walker";
    public ItemType facing = ItemType.Left;
    [Tooltip("Маршрут по кругу: F B L R — шаг вверх/вниз/влево/вправо по сетке (как карточки), W — стоять. Например RRLL.")]
    public string path = "";
}

[System.Serializable]
public class Panels
{
    public string panelName;
    public Item[] commands;
}

[System.Serializable]
public class TileInfo
{
    public Vector3Int position;
    public TileBase tile;
    public SuperBlocks superTileType = SuperBlocks.None;
    [Tooltip("Цвет клетки для условий: покрашенная карточка срабатывает только на клетке своего цвета")]
    public TileColor color = TileColor.None;
}

/// <summary>Цвет клетки / условие карточки.</summary>
public enum TileColor
{
    None,
    Red,
    Blue,
    Yellow,
}

/// <summary>
/// Особая клетка. Значения не переставлять — они хранятся в ассетах уровней числом.
/// Ice и Crumbling — клетки главы Winter (см. World/SpecialTiles и ядро CrystalCore).
/// </summary>
public enum SuperBlocks
{
    None,
    Mushroom,
    Crystal,
    Ice,        // лёд: встал — скользишь дальше в ту же сторону до обычной клетки или края
    Crumbling,  // рассыпающаяся плита: по ней можно пройти один раз — после ухода она падает
}
