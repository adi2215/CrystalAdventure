using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverlayTile : MonoBehaviour
{
    public int G;
    public int H;

    public int F { get {return G + H; } }

    public bool isBlocked;

    public string typeSprite;

    public OverlayTile previous;

    public Vector3Int gridLocation;

    public GameObject attachedObject;

    public SuperBlocks block = SuperBlocks.None;


    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HideTile();
        }
    } 

    public void ShowTile()
    {
        gameObject.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 1);
    }

    public void HideTile()
    {
        gameObject.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0);
    }
}
