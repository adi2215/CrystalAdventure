// Сигнатуры существующих скриптов игры (Item, LevelSt, MovingManager, Data, SaveManager) — как в проекте.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Item : ScriptableObject { public Sprite image; public ItemType type; public int iteration; }
public enum ItemType { Forward, Left, Right, Bottom, FunctionOne, FunctionTwo, While, Ifandelse, Null }
public enum SlotType { Main, FunctionOne, FunctionTwo }
public class MovingManager : MonoBehaviour { public Dictionary<SlotType, List<Item>> Commands = new Dictionary<SlotType, List<Item>>(); }
public class LevelSt : ScriptableObject { public List<TileInfo> tiles; public Vector3 characterPos; }
[System.Serializable] public class TileInfo { public Vector3Int position; public TileBase tile; public SuperBlocks superTileType = SuperBlocks.None; }
public enum SuperBlocks { None, Mushroom, Crystal }
public class Data : ScriptableObject { public int NextLevel; public int commandUsed; }
public static class SaveManager { public static void SaveLevelResult(int levelNumber, int starsEarned) { } }
