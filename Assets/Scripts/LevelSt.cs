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

    public Vector3 crystalPos;

    public Panels[] panels;

    public DialogTrigger dialog;
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
}
