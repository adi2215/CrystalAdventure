using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Crystal Adventure → Уровни → Расставить уровни по главам.
    ///
    /// Уровни дописывались в конец списка TilemapData по мере создания, поэтому главы перемешались:
    /// лёгкие черновики (№16–18) стояли в Winter, лесной босс и шипы — тоже, враги — между ними.
    /// Кнопка ставит уровни по порядку <see cref="Order"/> — план на 90 уровней (3 главы по 30, в конце босс):
    /// Forest (обучение → F1 → циклы → условия → шипы → враги → лесной босс), Winter (лёд, плиты, F2, пушки → ледяной ромб),
    /// Sky (облака, ветер, порталы → небесный кит). Новые уровни: LevelForest1–7, LevelWinter5–28, LevelSky4–29.
    /// Главу закрывает уровень с боссом (см. LevelChapters), поэтому главы могут быть разной длины.
    ///
    /// Звёзды не теряются: сохранение хранит имя уровня (SaveManager), номер пересчитывается сам.
    /// Data.NextLevel тоже переносится на тот же уровень.
    /// Уровни, которых нет в <see cref="Order"/>, остаются в конце списка — допиши их имена сюда.
    /// </summary>
    public static class LevelOrder
    {
        /// <summary>Порядок уровней в игре (имена ассетов LevelSt): план на 90 уровней, 3 главы по 30.
        /// Уровней, которых ещё нет в проекте, кнопка просто пропускает — новые встают на место сами.</summary>
        public static readonly string[] Order =
        {
            // ── Forest (30) ──
            "LevelOne", "LevelTwo", "LevelThree", "LevelFive", "LevelFourth", "LevelSix",
            "LevelSeven", "LevelThirteen", "LevelEight", "LevelNine", "LevelFourteen", "LevelForest1",
            "LevelLoop", "LevelForest2", "LevelForest3", "LevelIf", "LevelForest4", "LevelForest5",
            "LevelTwelve", "LevelFifteen", "LevelTen", "LevelEleven", "LevelSixteen", "LevelSpikes1",
            "LevelSpikes2", "LevelForest6", "LevelEnemy1", "LevelEnemy2", "LevelForest7", "LevelBossForest",
            // ── Winter (30) ──
            "LevelWinter1", "LevelWinter5", "LevelWinter6", "LevelWinter3", "LevelWinter7", "LevelWinter2",
            "LevelWinter8", "LevelWinter9", "LevelWinter4", "LevelWinter10", "LevelWinter11", "LevelWinter12",
            "LevelWinter13", "LevelWinter14", "LevelWinter15", "LevelEnemy3", "LevelWinter16", "LevelWinter17",
            "LevelWinter18", "LevelWinter19", "LevelWinter20", "LevelWinter21", "LevelWinter22", "LevelWinter23",
            "LevelWinter24", "LevelWinter25", "LevelWinter26", "LevelWinter27", "LevelWinter28", "LevelBossWinter",
            // ── Sky (30) ──
            "LevelSky1", "LevelSky4", "LevelSky5", "LevelSky6", "LevelSky7", "LevelSky2",
            "LevelSky8", "LevelSky9", "LevelSky10", "LevelSky11", "LevelSky3", "LevelSky12",
            "LevelSky13", "LevelSky14", "LevelSky15", "LevelSky16", "LevelSky17", "LevelSky18",
            "LevelSky19", "LevelSky20", "LevelSky21", "LevelSky22", "LevelSky23", "LevelSky24",
            "LevelSky25", "LevelSky26", "LevelSky27", "LevelSky28", "LevelSky29", "LevelBossSky",
        };

        [MenuItem("Crystal Adventure/Уровни/Расставить уровни по главам")]
        static void Run()
        {
            var data = Object.FindObjectOfType<TilemapData>();
            if (data == null || data.tilemapData == null)
            {
                Debug.LogError("[Crystal] Открой сцену CollectorLevel (там список уровней TilemapData) и нажми ещё раз.");
                return;
            }

            var old = data.tilemapData.ToList();
            var sorted = Sort(old, out var missing, out var extra);

            var sb = new StringBuilder();
            if (sorted.SequenceEqual(old))
            {
                sb.AppendLine("  ✓  Уровни уже стоят по главам — ничего не меняю.");
            }
            else
            {
                if (!EditorUtility.DisplayDialog("Порядок уровней",
                        "Расставить уровни по главам Forest → Winter → Sky?\nЗвёзды в сохранениях останутся у своих уровней.",
                        "Расставить", "Отмена"))
                    return;

                Undo.RecordObject(data, "Sort levels by chapter");
                data.tilemapData = sorted.ToArray();
                EditorUtility.SetDirty(data);

                // Data.NextLevel — на тот же уровень, что и раньше.
                if (data.data != null && data.data.NextLevel >= 0 && data.data.NextLevel < old.Count)
                {
                    int moved = sorted.IndexOf(old[data.data.NextLevel]);
                    if (moved >= 0 && moved != data.data.NextLevel)
                    {
                        Undo.RecordObject(data.data, "Sort levels by chapter");
                        data.data.NextLevel = moved;
                        EditorUtility.SetDirty(data.data);
                        AssetDatabase.SaveAssets();
                    }
                }
                EditorSceneManager.MarkSceneDirty(data.gameObject.scene);
                sb.AppendLine("  !  Порядок изменён — сохрани сцену (Ctrl+S).");
            }

            int chapter = -1;
            for (int i = 0; i < sorted.Count; i++)
            {
                int ch = LevelChapters.ChapterOf(sorted, i);
                if (ch != chapter) { chapter = ch; sb.AppendLine($"── {LevelChapters.Names[ch]} ──"); }
                int was = old.IndexOf(sorted[i]);
                sb.AppendLine($"  №{i + 1,-3} {(sorted[i] != null ? sorted[i].name : "(пусто)")}" + (was != i ? $"   (был №{was + 1})" : ""));
            }
            if (missing.Count > 0)
                sb.AppendLine($"  …  Из плана ещё не сделано {missing.Count}: {string.Join(", ", missing)}");
            foreach (var e in extra) sb.AppendLine($"  ?  {(e != null ? e.name : "(пусто)")} — нет в LevelOrder.Order, оставлен в конце. Допиши его имя в нужную главу.");
            Debug.Log("[Crystal] Порядок уровней\n" + sb);
        }

        /// <summary>Отсортировать по <see cref="Order"/>; неизвестные уровни — в конце, в прежнем порядке.</summary>
        public static List<LevelSt> Sort(IList<LevelSt> levels, out List<string> missing, out List<LevelSt> extra)
        {
            var sorted = new List<LevelSt>();
            missing = new List<string>();
            foreach (var name in Order)
            {
                var level = levels.FirstOrDefault(l => l != null && l.name == name);
                if (level != null) sorted.Add(level);
                else missing.Add(name);
            }
            extra = levels.Where(l => !sorted.Contains(l)).ToList();
            sorted.AddRange(extra);
            return sorted;
        }
    }
}
