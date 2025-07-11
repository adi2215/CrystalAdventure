using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEditor;

public class TilemapData : MonoBehaviour
{
    public TileBase emptyTile;
    public TileBase[] tiles;
    public int[,] tileMatrix;
    public LevelSt[] tilemapData;
    public MapManager map;
    public DialogManager dialogs;
    public PanelController panels;
    public CharacterCode character;
    public Crystal crystal;
    public Data data;

    //private string filePath => Application.dataPath + "/" + tilemapData[data.NextLevel].levelName + ".txt";

    public Tilemap tilemap;
    public LevelSt tilemapDataUpdate;
    private Vector3 tileCharacter;
    private Vector3 tileFinish;

    public void PosCharacter(OverlayTile tile)
    {
        tileCharacter = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
    }

    public void PosFinish(OverlayTile tile)
    {
        tileFinish = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
    }

    [ContextMenu("Save Tilemap Positions")]
    public void SaveTilePositions()
    {
        tilemapDataUpdate.tiles = new List<TileInfo>();

        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                TileBase tile = tilemap.GetTile(pos);
                TileInfo info = new TileInfo
                {
                    position = pos,
                    tile = tile
                };
                tilemapDataUpdate.tiles.Add(info);
            }
        }

        tilemapDataUpdate.characterPos = tileCharacter;
        tilemapDataUpdate.crystalPos = tileFinish;

        EditorUtility.SetDirty(tilemapDataUpdate);
        AssetDatabase.SaveAssets();

        Debug.Log("Tilemap saved to ScriptableObject!");
    }

    public void LoadTilemapFromData(int loadLevelIndex)
    {
        LevelSt level = tilemapData[loadLevelIndex];

        tilemap.ClearAllTiles();
        foreach (TileInfo info in level.tiles)
        {
            tilemap.SetTile(info.position, info.tile);
        }

        character.targetPosition = level.characterPos;
        crystal.targetPosition = level.crystalPos;

        if (level.dialog != null)
        {
            dialogs.OpenDialogue(level.dialog);
        }

        panels.CheckPanels(level.panels);
    }

    /*public void Start()
    {
        UpdateTilemapData();
        RestoreTilemap(this);
    }*/

    /*public void RestoreTilemap(TilemapData tilemapData)
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

    /*public void UpdateTilemapData()
    {
        if (tilemapData == null)
        {
            Debug.LogError("TilemapData ScriptableObject is not assigned!");
            return;
        }

        tileMatrix = StringToMatrix(File.ReadAllText(filePath));

        Debug.Log("Data saved to: " + filePath);
    }*/

    /*private int[,] StringToMatrix(string matrixString)
    {
        string[] rows = matrixString.Split('\n');

        // Получаем количество строк и столбцов
        int rowCount = rows.Length - 1;
        int columnCount = rows[0].Split(' ').Length - 1;

        // Создаем пустую матрицу
        int[,] matrix = new int[rowCount, columnCount];

        // Заполняем матрицу
        for (int i = 0; i < rowCount; i++)
        {
            string[] elements = rows[i].Trim().Split(' '); // Разделяем строку по пробелам
            Debug.Log(elements[0]);

            for (int j = 0; j < columnCount; j++)
            {
                matrix[i, j] = int.Parse(elements[j]); // Преобразуем строку в целое число
                Debug.Log(matrix[i, j]);
            }
        }

        return matrix; // Возвращаем полученную матрицу
    }

    public void PrintMatrixToConsole()
    {
        if (tilemapData == null)
        {
            Debug.LogError("TilemapData ScriptableObject is not assigned!");
            return;
        }

        Debug.Log("Level Name: " + tilemapData[data.NextLevel].levelName);
        Debug.Log("Matrix:");

        if (tileMatrix == null)
        {
            Debug.LogWarning("Matrix is null!");
            return;
        }

        string matrixAsString = MatrixToString(tileMatrix);

        string dataToSave = matrixAsString;

        File.WriteAllText(filePath, dataToSave);

        Debug.Log(matrixAsString);
    }

    private string MatrixToString(int[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        string result = "";

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                result += matrix[i, j] + " ";
            }
            result += "\n";
        }

        return result;
    }*/
}
