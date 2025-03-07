using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Scriptable object/Item")]
public class Item : ScriptableObject
{
    public Sprite image;
    public ItemType type;
    public int iteration;
}

public enum ItemType {
    Forward,
    Left,
    Right,
    Bottom,
    FunctionOne,
    FunctionTwo,
    While
}