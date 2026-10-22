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
    /// Кнопка ставит уровни по порядку <see cref="Order"/>: Forest (обучение → шипы → враги → лесной босс),
    /// Winter (лёд и плиты → ледяные пушки → ледяной ромб), Sky (облака, ветер, телепорты → небесный кит).
    /// Главу закрывает уровень с боссом (см. LevelChapters), поэтому главы могут быть разной длины.
    ///
    /// Звёзды не теряются: сохранение хранит имя уровня (SaveManager), номер пересчитывается сам.
    /// Data.NextLevel тоже переносится на тот же уровень.
    /// Уровни, которых нет в <see cref="Order"/>, остаются в конце списка — допиши их имена сюда.
    /// </summary>
    public static class LevelOrder
    {
        /// <summary>Порядок уровней в игре (имена ассетов LevelSt).</summary>
        public static readonly string[] Order =
        {
            // ── Forest ── обучение по порядку названий, затем шипы, враги и босс
            "LevelOne", "LevelTwo", "LevelThree", "LevelFourth", "LevelFive", "LevelSix", "LevelSeven",
            "LevelEight",   // туториал функций
            "LevelNine",
            "LevelLoop",    // туториал циклов
            "LevelIf",      // туториал условий
            "LevelTen", "LevelEleven", "LevelTwelve", "LevelThirteen", "LevelFourteen", "LevelFifteen", "LevelSixteen",
            "LevelSpikes1", "LevelSpikes2",
            "LevelEnemy1", "LevelEnemy2",
            "LevelBossForest",
            // ── Winter ──
            "LevelWinter1", "LevelWinter2", "LevelWinter3", "LevelWinter4",
            "LevelEnemy3",  // ледяные пушки
            "LevelBossWinter",
            // ── Sky ──
            "LevelSky1", "LevelSky2", "LevelSky3",
            "LevelBossSky",
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
            foreach (var m in missing) sb.AppendLine($"  ?  {m} — нет в списке уровней игры (пропущен).");
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
