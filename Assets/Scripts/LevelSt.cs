using System.Collections.Generic;
using System.IO;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable object/Level")]
public class LevelSt : ScriptableObject
{
    public string levelName;
    
    public int[,] tileMatrix;

    public Vector3 tilemapPos;

    public Vector3 characterPos;

    public Vector3 crystalPos;

    public List<Item> commands;

    public int maxFu;

}
