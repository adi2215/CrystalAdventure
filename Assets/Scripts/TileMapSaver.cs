using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;

public class TilemapSaver : MonoBehaviour
{
    /*public Tilemap tilemap;
    public TilemapData tilemapData;

    [ContextMenu("Save Tilemap Positions")]
    public void SaveTilePositions()
    {
        tilemapData.tilePositions = new List<Vector3Int>();

        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                tilemapData.tilePositions.Add(pos);
            }
        }

        EditorUtility.SetDirty(tilemapData);
        AssetDatabase.SaveAssets();
        Debug.Log("Tilemap data saved.");
    }*/
}


/*using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TilemapData", menuName = "Custom/Tilemap Data")]
public class TilemapData : ScriptableObject
{
    public List<Vector3Int> tilePositions;
}
*/

/*public void LoadTilemapFromData(Tile tile)
{
    tilemap.ClearAllTiles();
    foreach (Vector3Int pos in tilemapData.tilePositions)
    {
        tilemap.SetTile(pos, tile); // Один тип тайла, можно добавить больше логики
    }
}
*/

/*[System.Serializable]
public class TileInfo
{
    public Vector3Int position;
    public TileBase tile;
}

public class TilemapData : ScriptableObject
{
    public List<TileInfo> tiles;
}
*/