using System.Collections.Generic;
using UnityEditor;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Crystal Adventure → Уровни → Создать уровни с шипами.
    ///
    /// Шипы (Forest) поднимаются каждый 3-й ход; цифра в рисунке — фаза (0 — ходы 3, 6, 9…; 1 — 2, 5, 8…;
    /// 2 — 1, 4, 7…). Кто стоит на клетке в ход подъёма — погибает. Герой ходит каждый ход, поэтому
    /// переждать можно только «шаг туда-обратно» (+2 хода) — ритм приходится считать.
    /// Уровни дописываются в конец списка (после уровней с врагами).
    ///
    /// Оптимумы проверены перебором через ядро (CrystalCoreTests/SpikeTests):
    ///   1. «Шипы» — прямо — шип поднимется под ногами; переждать: R L, потом R × 4 (цикл на одну R) (4).
    ///   2. «Волна шипов» — фазы идут волной навстречу бегущему: одна пауза спасает внизу, но не в
    ///      коридоре вверх; нужны две: (F B) × 2, R × 4, F × 4 (7).
    /// </summary>
    public static class SpikeLevels
    {
        const ItemType F = ItemType.Forward, B = ItemType.Bottom, L = ItemType.Left, R = ItemType.Right, W = ItemType.While;

        public static EnemyLevels.Spec[] Specs => new[]
        {
            new EnemyLevels.Spec
            {
                Name = "LevelSpikes1", Title = "Шипы", ExpectedOptimal = 4,
                Rows = new[] { "..#..", "S#1#C", "..#.." },
                Main = EnemyLevels.MovesAndWhile,
                Solution = new List<SolutionCard> { EnemyLevels.C(R), EnemyLevels.C(L), EnemyLevels.C(R), EnemyLevels.C(W, 3, span: 1) },
            },
            new EnemyLevels.Spec
            {
                Name = "LevelSpikes2", Title = "Волна шипов", ExpectedOptimal = 7,
                Rows = new[] { "....C", "....0", "....1", "#...2", "S210#" },
                Main = EnemyLevels.MovesAndWhile,
                Solution = new List<SolutionCard>
                {
                    EnemyLevels.C(F), EnemyLevels.C(B), EnemyLevels.C(W, 1), EnemyLevels.C(R), EnemyLevels.C(W, 3), EnemyLevels.C(F), EnemyLevels.C(W, 3),
                },
            },
        };

        [MenuItem("Crystal Adventure/Уровни/Создать уровни с шипами")]
        static void Create() => EnemyLevels.CreateLevels(Specs, "Уровни с шипами",
            "Перезаписать их заново? Ассеты и номера уровней останутся те же.");
    }
}
