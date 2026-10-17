using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrystalAdventure.Core;
using CrystalAdventure.UnityLayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Фаза 2 переноса на CrystalGameController — проверка БЕЗ Play Mode и без правок сцен.
    ///
    /// Crystal Adventure → Ядро → Проверить совпадение со старой системой:
    ///   для каждого старого уровня (LevelSt) берёт эталонное решение («Решить все уровни»),
    ///   прогоняет его через новое ядро и через эталон старых правил (LegacyReplay) и сравнивает:
    ///   победа, число шагов, где остановился герой, сколько раз упёрся в край, карточки и звёзды.
    ///   Пока здесь не все уровни ✓ — сцены на ядро не переводим.
    ///
    /// Crystal Adventure → Ядро → Создать LevelDefinition из LevelSt:
    ///   создаёт (или обновляет) ассет LevelDefinition для каждого уровня в папке CoreLevels.
    ///   Враги, tileOverrides и биом при повторном запуске не трогаются.
    /// </summary>
    public static class LegacyParityCheck
    {
        const string CoreLevelsFolder = "Assets/Scenes/GameScenes/CoreLevels";

        [MenuItem("Crystal Adventure/Ядро/Проверить совпадение со старой системой")]
        public static void CheckAll()
        {
            var levels = GameLevels(out var fromScene);
            var tilemap = SceneTilemap();
            var sb = new StringBuilder();
            int ok = 0, bad = 0, skipped = 0;

            for (int i = 0; i < levels.Count; i++)
            {
                var lvl = levels[i];
                string title = $"{(fromScene ? $"№{i + 1} " : "")}{(lvl != null ? lvl.name : "<пусто>")}";
                if (lvl == null) { skipped++; sb.AppendLine($"  ?  {title}: уровень не назначен"); continue; }

                var line = Check(lvl, tilemap, out var verdict);
                if (verdict == Verdict.Ok) ok++;
                else if (verdict == Verdict.Bad) bad++;
                else skipped++;
                sb.AppendLine($"  {Icon(verdict)}  {title}: {line}");
            }

            string source = fromScene ? "по списку TilemapData открытой сцены" : "все LevelSt проекта (открой CollectorLevel, чтобы взять порядок уровней игры)";
            string header = $"[Crystal] Совпадение ядра со старой системой: {ok} ✓, {bad} ✗, {skipped} без проверки — {source}";
            if (bad > 0) Debug.LogError(header + "\n" + sb);
            else if (skipped > 0) Debug.LogWarning(header + "\n" + sb);
            else Debug.Log(header + "\n" + sb);

            EditorUtility.DisplayDialog("Совпадение со старой системой",
                $"✓ совпадает: {ok}\n✗ расходится: {bad}\n? без проверки: {skipped}\n\nПодробности — в Console.", "OK");
        }

        public enum Verdict { Ok, Bad, Skipped }

        /// <summary>Проверка одного уровня. Возвращает строку отчёта.</summary>
        public static string Check(LevelSt lvl, Tilemap tilemap, out Verdict verdict)
        {
            verdict = Verdict.Skipped;
            if (!LegacyLevelImport.TryFindStartCell(lvl, tilemap, out var start, out var startDist))
                return "нет клеток — нечего проверять";
            if (!lvl.HasSolution)
                return "нет эталонного решения — сначала «Crystal Adventure → Редактор уровней → Решить все уровни»";

            var problems = new List<string>();
            if (startDist > LegacyLevelImport.StartMatchDistance)
                problems.Add($"старт героя не точно на клетке (ближайшая {start}, отклонение {startDist:0.###})");

            LevelSetup setup;
            ProgramDef program;
            try
            {
                setup = LegacyLevelImport.ToSetup(lvl, start);
                program = ItemProgramAdapter.FromSolution(lvl);
            }
            catch (System.Exception e)
            {
                verdict = Verdict.Bad;
                return "ошибка импорта — " + e.Message;
            }

            var core = Simulation.Run(setup, program);
            if (lvl.HasEnemies)
            {
                // Враги есть только в ядре — старых правил для них нет. Проверяем, что решение проходит уровень.
                if (!core.Won) problems.Add($"ядро: {core.Outcome} — {core.Message}");
                if (core.BlocksUsed != lvl.optimalCards) problems.Add($"карточек: ядро {core.BlocksUsed}, оптимум уровня {lvl.optimalCards}");
                verdict = problems.Count == 0 ? Verdict.Ok : Verdict.Bad;
                string s = $"с врагами: шагов {core.Turns.Count}, карточек {core.BlocksUsed}, ★{core.Stars}, здоровье {core.Final.Hero.Hp} | {ProgramText.Print(program)}";
                return problems.Count == 0 ? s : string.Join("; ", problems) + " | " + s;
            }
            var old = LegacyReplay.Run(lvl, start, slot => ItemProgramAdapter.SolutionCards(lvl, slot));
            var heroCell = new Vector2Int(core.Final.Hero.Pos.X, core.Final.Hero.Pos.Y);

            if (!old.Won) problems.Add("решение не проходит уровень даже по старым правилам — пересчитай решение");
            if (!core.Won) problems.Add($"ядро: {core.Outcome} — {core.Message}");
            if (core.Won != old.Won) problems.Add($"победа: ядро {core.Won}, старая {old.Won}");
            if (core.Turns.Count != old.Steps) problems.Add($"шагов: ядро {core.Turns.Count}, старая {old.Steps}");
            if (heroCell != old.Final) problems.Add($"герой остановился: ядро {heroCell}, старая {old.Final}");
            if (core.EdgeHits != old.EdgeHits) problems.Add($"край: ядро {core.EdgeHits}, старая {old.EdgeHits}");
            if (core.BlocksUsed != lvl.optimalCards) problems.Add($"карточек: ядро {core.BlocksUsed}, оптимум уровня {lvl.optimalCards}");

            string summary = $"шагов {core.Turns.Count}, карточек {core.BlocksUsed}, ★{core.Stars}" +
                             (old.Skipped > 0 ? $", пропусков по цвету {old.Skipped}" : "") +
                             (core.EdgeHits > 0 ? $", край {core.EdgeHits}" : "") +
                             $" | {ProgramText.Print(program)}";
            verdict = problems.Count == 0 ? Verdict.Ok : Verdict.Bad;
            return problems.Count == 0 ? summary : string.Join("; ", problems) + " | " + summary;
        }

        [MenuItem("Crystal Adventure/Ядро/Создать LevelDefinition из LevelSt")]
        public static void CreateDefinitions()
        {
            var levels = GameLevels(out var fromScene).Where(l => l != null).Distinct().ToList();
            if (levels.Count == 0) { Debug.LogWarning("[Crystal] Не нашёл ни одного LevelSt"); return; }
            if (!fromScene && !EditorUtility.DisplayDialog("Создать LevelDefinition",
                    $"Сцена с TilemapData не открыта — возьму все LevelSt проекта ({levels.Count} шт.) по имени. Продолжить?", "Да", "Отмена"))
                return;

            EnsureFolder(CoreLevelsFolder);
            var tilemap = SceneTilemap();
            var sb = new StringBuilder();
            int created = 0, updated = 0;

            foreach (var lvl in levels)
            {
                if (!LegacyLevelImport.TryFindStartCell(lvl, tilemap, out var start, out var dist))
                {
                    sb.AppendLine($"  ?  {lvl.name}: нет клеток — пропущен");
                    continue;
                }

                string path = $"{CoreLevelsFolder}/{lvl.name}_Core.asset";
                var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                bool isNew = def == null;
                if (isNew) def = ScriptableObject.CreateInstance<LevelDefinition>();
                else Undo.RecordObject(def, "Update LevelDefinition");

                LegacyLevelImport.FillDefinition(def, lvl, start);

                if (isNew) { AssetDatabase.CreateAsset(def, path); created++; }
                else { EditorUtility.SetDirty(def); updated++; }

                sb.AppendLine($"  {(isNew ? "+" : "~")}  {lvl.name}: старт {start}" +
                              (dist > LegacyLevelImport.StartMatchDistance ? $" (⚠ отклонение {dist:0.###})" : "") +
                              $", оптимум {def.optimalBlocks}" +
                              (string.IsNullOrEmpty(def.referenceSolution) ? ", решения нет" : ""));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Crystal] LevelDefinition: создано {created}, обновлено {updated} в {CoreLevelsFolder}\n{sb}");
        }

        // ------------------------------------------------------------------ вспомогательное

        /// <summary>Уровни в порядке игры (TilemapData открытой сцены), иначе все LevelSt проекта по имени.</summary>
        static List<LevelSt> GameLevels(out bool fromScene)
        {
            var data = Object.FindObjectOfType<TilemapData>();
            if (data != null && data.tilemapData != null && data.tilemapData.Length > 0)
            {
                fromScene = true;
                return data.tilemapData.ToList();
            }

            fromScene = false;
            return AssetDatabase.FindAssets("t:LevelSt")
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelSt>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(l => l != null)
                .OrderBy(l => l.name)
                .ToList();
        }

        /// <summary>Tilemap игровой сцены, если она открыта (точнее формулы для уровней с высотой).</summary>
        static Tilemap SceneTilemap()
        {
            var data = Object.FindObjectOfType<TilemapData>();
            return data != null ? data.tilemap : null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static string Icon(Verdict v) => v == Verdict.Ok ? "✓" : v == Verdict.Bad ? "✗" : "?";
    }
}
