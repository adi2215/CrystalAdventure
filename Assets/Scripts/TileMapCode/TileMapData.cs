using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

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
    public Data data;

    //private string filePath => Application.dataPath + "/" + tilemapData[data.NextLevel].levelName + ".txt";

    public Tilemap tilemap;
    public LevelSt tilemapDataUpdate;

    /// <summary>Уровень, который сейчас загружен в игре (для звёзд и подсказок).</summary>
    public static LevelSt CurrentLevel { get; private set; }
    private Vector3 tileCharacter;
    private Vector3 tileFinish;

    private bool characterPicked;

    public void PosCharacter(OverlayTile tile)
    {
        tileCharacter = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
        characterPicked = true;
    }

    public void PosFinish(OverlayTile tile)
    {
        tileFinish = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
    }

    /// <summary>
    /// Старый способ сохранения (через меню компонента). Удобнее окно
    /// Crystal Adventure → Редактор уровней.
    ///
    /// Исправлено: раньше каждое сохранение стирало кристаллы и грибы (superTileType),
    /// ставило старт героя в (0,0,0), если перед этим не кликнули по клетке,
    /// и не помечало ассет изменённым — уровень мог не записаться на диск.
    /// </summary>
    [ContextMenu("Save Tilemap Positions")]
    public void SaveTilePositions()
    {
        if (tilemapDataUpdate == null || tilemap == null)
        {
            Debug.LogError("[TilemapData] Укажи Tilemap и Tilemap Data Update (уровень, куда сохранять)");
            return;
        }

        // Запоминаем кристаллы/грибы по колонке (x, y), чтобы не потерять их при пересохранении.
        var supers = new Dictionary<Vector2Int, SuperBlocks>();
        var colors = new Dictionary<Vector2Int, TileColor>();
        if (tilemapDataUpdate.tiles != null)
            foreach (var old in tilemapDataUpdate.tiles)
            {
                if (old != null && old.superTileType != SuperBlocks.None)
                    supers[new Vector2Int(old.position.x, old.position.y)] = old.superTileType;
                if (old != null && old.color != TileColor.None)
                    colors[new Vector2Int(old.position.x, old.position.y)] = old.color;
            }

        tilemap.CompressBounds();
        var topZ = new Dictionary<Vector2Int, int>();
        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos)) continue;
            var key = new Vector2Int(pos.x, pos.y);
            if (!topZ.TryGetValue(key, out var z) || pos.z > z) topZ[key] = pos.z;
        }

        var tiles = new List<TileInfo>();
        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(pos)) continue;
            var key = new Vector2Int(pos.x, pos.y);
            var type = topZ[key] == pos.z && supers.TryGetValue(key, out var t) ? t : SuperBlocks.None;
            var color = topZ[key] == pos.z && colors.TryGetValue(key, out var c) ? c : TileColor.None;
            tiles.Add(new TileInfo { position = pos, tile = tilemap.GetTile(pos), superTileType = type, color = color });
        }

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(tilemapDataUpdate, "Save Tilemap Positions");
#endif
        tilemapDataUpdate.tiles = tiles;
        if (characterPicked)
            tilemapDataUpdate.characterPos = tileCharacter;
        else
            Debug.LogWarning("[TilemapData] Старт героя не выбран — оставлен прежний characterPos");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tilemapDataUpdate);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        int crystals = 0;
        foreach (var i in tiles) if (i.superTileType == SuperBlocks.Crystal) crystals++;
        Debug.Log($"[TilemapData] {tilemapDataUpdate.name} сохранён: тайлов {tiles.Count}, кристаллов {crystals}");
        if (crystals == 0) Debug.LogWarning($"[TilemapData] В {tilemapDataUpdate.name} нет кристаллов — уровень нельзя пройти");
    }

    public void LoadTilemapFromData(int loadLevelIndex)
    {
        if (tilemapData == null || tilemapData.Length == 0)
        {
            Debug.LogError("[TilemapData] Список уровней пуст");
            return;
        }
        loadLevelIndex = Mathf.Clamp(loadLevelIndex, 0, tilemapData.Length - 1);
        LevelSt level = tilemapData[loadLevelIndex];
        if (level == null)
        {
            Debug.LogError($"[TilemapData] Уровень {loadLevelIndex} не назначен");
            return;
        }

        CurrentLevel = level;
        HintSystem.ResetForLevel();

        var blocks = ActiveBlockManager.Instance;
        if (blocks != null) blocks.ResetLevelTileData();

        tilemap.ClearAllTiles();
        if (level.tiles != null)
        {
            foreach (TileInfo info in level.tiles)
            {
                if (info == null) continue;
                if (info.superTileType != SuperBlocks.None && blocks != null)
                    blocks.RegisterTile(info);

                tilemap.SetTile(info.position, info.tile);
            }
        }

        ColorTiles.Load(level, tilemap);   // цветные клетки (условия)
        SpecialTiles.Load(level, loadLevelIndex); // лёд, рассыпающиеся плиты, снег главы Winter

        if (character != null) character.targetPosition = level.characterPos;

        // Если у уровня есть пошаговый туториал — он сам всё расскажет, старый диалог не нужен.
        bool tutorial = TutorialDirector.ShouldShow(level, loadLevelIndex);
        if (level.dialog != null && dialogs != null && !tutorial)
            dialogs.OpenDialogue(level.dialog);

        if (panels != null) panels.CheckPanels(level.panels);
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
