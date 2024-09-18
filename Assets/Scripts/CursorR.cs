using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorR : MonoBehaviour
{
    public OverlayTile tile;

    public bool star = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.tag == "Cube" && !star)
        {
            Debug.Log(other.gameObject);
            tile = other.gameObject.GetComponent<OverlayTile>();
            Debug.Log(tile.gridLocation);
            star = true;
        }
    }
}
