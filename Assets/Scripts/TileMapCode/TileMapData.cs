using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;

public class TilemapData : MonoBehaviour
{
    public TileBase emptyTile;
    public TileBase[] tiles;
    public int[,] tileMatrix;
    public LevelSt tilemapData;
    public MapManager map;
    public GameObject character, crystal;

    private string filePath => Application.dataPath + "/" + tilemapData.levelName + ".txt";

    public void UpdateTilemapData()
    {
        if (tilemapData == null)
        {
            Debug.LogError("TilemapData ScriptableObject is not assigned!");
            return;
        }

        tileMatrix = StringToMatrix(File.ReadAllText(filePath));

        EditorUtility.SetDirty(tilemapData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Data saved to: " + filePath);
    }

    private int[,] StringToMatrix(string matrixString)
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

        Debug.Log("Level Name: " + tilemapData.levelName);
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
    }
}
