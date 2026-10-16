// Сигнатуры существующих скриптов игры (Item, LevelSt, SolutionCard, MovingManager, TilemapData, Data, SaveManager) — как в проекте.
// Логику, от которой зависят тесты (LevelSt.GetSolution, SolutionCard.ResolveSpan, повторы/скобки/цвета
// MovingManager), повторяем один в один с Assets/Scripts.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Item : ScriptableObject { public Sprite image; public ItemType type; public int iteration; }
public enum ItemType { Forward, Left, Right, Bottom, FunctionOne, FunctionTwo, While, Ifandelse, Null }
public enum SlotType { Main, FunctionOne, FunctionTwo }

public class MovingManager : MonoBehaviour
{
    public Dictionary<SlotType, List<Item>> Commands = new Dictionary<SlotType, List<Item>>();
    // В игре это приватные списки; в заглушке открыты, чтобы тест мог их заполнить.
    public Dictionary<SlotType, List<int>> Iterations = new Dictionary<SlotType, List<int>>();
    public Dictionary<SlotType, List<int>> Spans = new Dictionary<SlotType, List<int>>();
    public Dictionary<SlotType, List<TileColor>> Colors = new Dictionary<SlotType, List<TileColor>>();

    public int GetIteration(SlotType type, int index) =>
        Iterations.TryGetValue(type, out var l) && index >= 0 && index < l.Count ? l[index] : 1;
    public int GetSpan(SlotType type, int index) =>
        Spans.TryGetValue(type, out var l) && index >= 0 && index < l.Count ? l[index] : 0;
    public TileColor GetColor(SlotType type, int index) =>
        Colors.TryGetValue(type, out var l) && index >= 0 && index < l.Count ? l[index] : TileColor.None;
}

public class LevelSt : ScriptableObject
{
    public List<TileInfo> tiles;
    public Vector3 characterPos;
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
    public int iterations = 1;
    public int span;
    public TileColor color;

    public static int ResolveSpan(List<SolutionCard> cards, int index)
    {
        int max = 0;
        for (int j = index - 1; j >= 0 && cards[j].type != ItemType.While; j--) max++;
        int span = cards[index].span;
        return span <= 0 ? max : System.Math.Min(span, max);
    }
}

[System.Serializable] public class TileInfo { public Vector3Int position; public TileBase tile; public SuperBlocks superTileType = SuperBlocks.None; public TileColor color = TileColor.None; }
public enum TileColor { None, Red, Blue, Yellow }
public enum SuperBlocks { None, Mushroom, Crystal }
public class TilemapData : MonoBehaviour { public LevelSt[] tilemapData; public Tilemap tilemap; }
public class Data : ScriptableObject { public int NextLevel; public int commandUsed; }
public static class SaveManager { public static void SaveLevelResult(int levelNumber, int starsEarned) { } }
