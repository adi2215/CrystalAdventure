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

    public Panels[] panels;

    public DialogTrigger dialog;
}

[System.Serializable]
public class Panels {
    public string panelName;
    public Item[] commands;
}
