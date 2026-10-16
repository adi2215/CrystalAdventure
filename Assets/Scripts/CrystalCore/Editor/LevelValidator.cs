using System.Text;

using CrystalAdventure.UnityLayer;
using UnityEditor;
using UnityEngine;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Меню Crystal Adventure → Validate All Levels.
    /// Прогоняет эталонное решение каждого уровня через ядро и пишет в консоль,
    /// проходится ли он и на сколько звёзд. С 90 уровнями это экономит часы ручной проверки.
    /// </summary>
    public static class LevelValidator
    {
        [MenuItem("Crystal Adventure/Validate All Levels")]
        public static void ValidateAll()
        {
            var guids = AssetDatabase.FindAssets("t:LevelDefinition");
            int ok = 0, bad = 0, missing = 0;
            var report = new StringBuilder();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null) continue;

                if (string.IsNullOrWhiteSpace(level.referenceSolution))
                {
                    missing++;
                    report.AppendLine($"  ?  {level.name}: нет эталонного решения");
                    continue;
                }

                try
                {
                    var result = CrystalAdventure.Core.Simulation.Run(level.ToSetup(), level.referenceSolution);
                    if (result.Won)
                    {
                        ok++;
                        report.AppendLine($"  ✓  {level.name}: {result.Stars}★, блоков {result.BlocksUsed}, ходов {result.Turns.Count}");
                    }
                    else
                    {
                        bad++;
                        report.AppendLine($"  ✗  {level.name}: {result.Outcome} — {result.Message} (ход {result.Turns.Count})");
                    }
                }
                catch (System.Exception e)
                {
                    bad++;
                    report.AppendLine($"  ✗  {level.name}: ошибка — {e.Message}");
                }
            }

            var header = $"[Crystal] Уровни: {ok} ок, {bad} не проходятся, {missing} без решения";
            if (bad > 0) Debug.LogError(header + "\n" + report);
            else Debug.Log(header + "\n" + report);
        }
    }
}
