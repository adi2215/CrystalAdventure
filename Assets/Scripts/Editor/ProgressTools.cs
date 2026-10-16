using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Crystal Adventure → Сбросить прогресс / Открыть папку сохранений.</summary>
public static class ProgressTools
{
    static string SavePath => Application.persistentDataPath + "/levels.json";

    [MenuItem("Crystal Adventure/Сбросить прогресс")]
    static void ResetProgress()
    {
        if (!EditorUtility.DisplayDialog("Сбросить прогресс",
                "Удалить сохранение (звёзды и пройденные уровни)? Туториалы снова будут показываться.", "Сбросить", "Отмена"))
            return;

        foreach (var path in new[] { SavePath, SavePath + ".tmp" })
            if (File.Exists(path)) File.Delete(path);

        Debug.Log("[Прогресс] Сохранение удалено: " + SavePath);
    }

    [MenuItem("Crystal Adventure/Открыть папку сохранений")]
    static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);
}
