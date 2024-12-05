using System.IO;
using UnityEngine;

public class MatrixStriver : MonoBehaviour
{
    private string savePath => Application.persistentDataPath + "/matrix.json";
    
    private int[,] matrix = new int[,]
    {
        { 1, 0, 0, 1 },
        { 1, 1, 0, 1 },
        { 0, 1, 1, 0 }
    };

    [System.Serializable]
    public class MatrixWrapper
    {
        public int[] data;
        public int rows;
        public int columns;
    }

    public void SaveMatrixToJson()
    {
        MatrixWrapper wrapper = new MatrixWrapper
        {
            data = FlattenMatrix(matrix),
            rows = matrix.GetLength(0),
            columns = matrix.GetLength(1)
        };

        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(savePath, json);
        Debug.Log("Matrix saved to: " + savePath);
    }

    public void LoadMatrixFromJson()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogError("Save file not found!");
            return;
        }

        string json = File.ReadAllText(savePath);
        MatrixWrapper wrapper = JsonUtility.FromJson<MatrixWrapper>(json);

        matrix = UnflattenMatrix(wrapper.data, wrapper.rows, wrapper.columns);
        Debug.Log("Matrix loaded!");
    }

    private int[] FlattenMatrix(int[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        int[] flatArray = new int[rows * columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                flatArray[i * columns + j] = matrix[i, j];
            }
        }

        return flatArray;
    }

    private int[,] UnflattenMatrix(int[] flatArray, int rows, int columns)
    {
        int[,] matrix = new int[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                matrix[i, j] = flatArray[i * columns + j];
            }
        }

        return matrix;
    }
}
