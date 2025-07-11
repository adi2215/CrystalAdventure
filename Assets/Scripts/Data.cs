using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DataTrigger")]
public class Data : ScriptableObject
{
    public bool trans = false;

    public bool menuclose = false;

    public bool music = true;

    public bool burrons;

    public bool wecanPlay;

    public int RemoveBlock;
    
    public int NextLevel;

    public bool RemoveBool = false;

    public bool FallingCrystal = true;

    public bool FallingCube = true;

    public bool FallingMap = true;

    public bool DialogManager = false;

    public int commandUsed = 0;

    public int currentLevel;
}
