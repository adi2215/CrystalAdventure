using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using Codice.Client.BaseCommands;

[CustomEditor(typeof(TilemapData))]
public class TilemapRestorer : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TilemapData tilemapData = (TilemapData)target;

        if (GUILayout.Button("Restore Tilemap"))
        {
            RestoreTilemap(tilemapData);
        }

        if (GUILayout.Button("Save Tilemap"))
        {
            SaveTilemap(tilemapData);
        }

        if (GUILayout.Button("Update TilemapData"))
        {
            tilemapData.UpdateTilemapData();
        }

        if (GUILayout.Button("Print Matrix"))
        {
            tilemapData.PrintMatrixToConsole();
        }
    }

    private void RestoreTilemap(TilemapData tilemapData)
    {
        Tilemap tilemap = tilemapData.GetComponent<Tilemap>();

        if (tilemap == null)
        {
            Debug.LogError("Tilemap component not found on the object!");
            return;
        }

        // Узнаем "origin" Tilemap
        Vector3Int origin = tilemap.origin;
        Debug.Log($"Origin of Tilemap: {origin}");

        // Узнаем размер области с тайлами
        Vector3Int size = tilemap.size;
        Debug.Log($"Size of Tilemap: {size}");

        Vector3 anc = tilemap.tileAnchor;
        Debug.Log($"Anchor of Tilemap: {anc}");

        tilemap.ClearAllTiles();

        int[,] tileMatrix = tilemapData.tileMatrix;
        TileBase[] tiles = tilemapData.tiles;

        for (int x = 0; x < tileMatrix.GetLength(0); x++)
        {
            for (int y = 0; y < tileMatrix.GetLength(1); y++)
            {
                int tileIndex = tileMatrix[x, y];

                if (tileIndex == -1)
                {
                    Vector3Int position = new Vector3Int(x, y, 0);
                    tilemap.SetTile(position, null); 
                    Debug.Log(tilemap.HasTile(position));
                }

                if (tileIndex >= 0 && tileIndex < tiles.Length)
                {
                    Vector3Int position = new Vector3Int(x, y, 0);
                    tilemap.SetTile(position, tiles[tileIndex]);
                }
            }
        }

        LevelSt level = tilemapData.tilemapData[tilemapData.data.NextLevel];

        tilemap.transform.position = level.tilemapPos;
        tilemapData.crystal.transform.position = level.crystalPos;
        tilemapData.character.transform.position = level.characterPos;
        Debug.Log(tilemap.transform.localPosition);

        if (level.dialog != null)
        {
            tilemapData.dialogs.OpenDialogue(level.dialog);
        }

        Debug.Log("Tilemap restored!");

        tilemapData.map.TileAppears();
        tilemapData.panels.CheckPanels(level.panels);
    }

    private void SaveTilemap(TilemapData tilemapData)
    {
        Tilemap tilemap = tilemapData.GetComponent<Tilemap>();

        if (tilemap == null)
        {
            Debug.LogError("Tilemap component not found!");
            return;
        }

        BoundsInt bounds = tilemap.cellBounds;
        int[,] tileMatrix = new int[bounds.size.x, bounds.size.y];

        for (int x = 0; x < bounds.size.x; x++)
        {
            for (int y = 0; y < bounds.size.y; y++)
            {
                Vector3Int position = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                TileBase tile = tilemap.GetTile(position);

                if (tile != null)
                {
                    tileMatrix[x, y] = System.Array.IndexOf(tilemapData.tiles, tile);
                }
                else
                {
                    tileMatrix[x, y] = -1; 
                }
            }
        }

        LevelSt level = tilemapData.tilemapData[tilemapData.data.NextLevel];

        level.characterPos = tilemapData.character.transform.position;
        level.crystalPos = tilemapData.crystal.transform.position; 
        level.tilemapPos = tilemap.transform.position;
        
        tilemapData.tileMatrix = tileMatrix;
        Debug.Log("Tilemap saved to matrix!");
    }
}
