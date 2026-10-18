using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEditor;

namespace CrystalAdventure.EditorTools
{
    /// <summary>
    /// Crystal Adventure → Уровни → Создать уровни неба.
    ///
    /// Клетки главы Sky (рисунок — EnemyLevels.TileFor):
    ///   'O' облако — сошёл с него, и оно растворяется; возвращается через пару ходов;
    ///   '>' '<' '^' 'v' ветер — в конце хода сдвигает стоящего на клетку по стрелке (если там есть куда);
    ///   'T' / 't' телепорты — встал на один, оказался на втором с тем же номером пары.
    /// Уровни дописываются в конец списка (после уровней с шипами). Оптимумы проверены перебором
    /// через ядро (CrystalCoreTests/SkyTests):
    ///   1. «Облачный мост» — вернуться по облакам нельзя (они растворились): сначала ближний кристалл,
    ///      потом через мост: F, B, R × 4 (цикл на одну R) (4).
    ///   2. «Попутный ветер» — встречный ветер внизу не пускает; наверху попутный несёт: F R R B (4).
    ///   3. «Телепорт» — до правого кристалла не дойти, только через телепорт: F, B, R × 3 (цикл на одну R) (4).
    ///   4. «Небесный кит» — босс неба (SkyBrain): летает над полем по кругу, под ним можно ходить, но каждый
    ///      3-й ход бомба крестом ровно под его тенью. Обход по краю в любую сторону попадает под бомбу;
    ///      решение — «лесенка» наискосок под китом: R × 4, (L F) × 4, R × 4 (7).
    /// </summary>
    public static class SkyLevels
    {
        const ItemType F = ItemType.Forward, B = ItemType.Bottom, L = ItemType.Left, R = ItemType.Right, W = ItemType.While;

        public static EnemyLevels.Spec[] Specs => new[]
        {
            new EnemyLevels.Spec
            {
                Name = "LevelSky1", Title = "Облачный мост", ExpectedOptimal = 4,
                Rows = new[] { "COOO#", "SOOOC" },
                Main = EnemyLevels.MovesAndWhile,
                Solution = new List<SolutionCard> { EnemyLevels.C(F), EnemyLevels.C(B), EnemyLevels.C(R), EnemyLevels.C(W, 3, span: 1) },
            },
            new EnemyLevels.Spec
            {
                Name = "LevelSky2", Title = "Попутный ветер", ExpectedOptimal = 4,
                Rows = new[] { "#>>>#", "S#<<C" },
                Main = EnemyLevels.MovesAndWhile,
                Solution = new List<SolutionCard> { EnemyLevels.C(F), EnemyLevels.C(R), EnemyLevels.C(R), EnemyLevels.C(B) },
            },
            new EnemyLevels.Spec
            {
                Name = "LevelSky3", Title = "Телепорт", ExpectedOptimal = 4,
                Rows = new[] { "C#.TC", "S#T.#" },
                Main = EnemyLevels.MovesAndWhile,
                Solution = new List<SolutionCard> { EnemyLevels.C(F), EnemyLevels.C(B), EnemyLevels.C(R), EnemyLevels.C(W, 2, span: 1) },
            },
            new EnemyLevels.Spec
            {
                Name = "LevelBossSky", Title = "Небесный кит", ExpectedOptimal = 7,
                Rows = new[] { "C###C", "#####", "#####", "#####", "S###C" },
                Enemies = new[] { EnemyLevels.E(3, 1, EnemyBrains.SkyBossArchetype, L, "LLFFRRBB") },
                Main = EnemyLevels.MovesAndWhile,
                Solution = new List<SolutionCard>
                {
                    EnemyLevels.C(R), EnemyLevels.C(W, 3), EnemyLevels.C(L), EnemyLevels.C(F), EnemyLevels.C(W, 3), EnemyLevels.C(R), EnemyLevels.C(W, 3),
                },
            },
        };

        [MenuItem("Crystal Adventure/Уровни/Создать уровни неба")]
        static void Create() => EnemyLevels.CreateLevels(Specs, "Уровни неба",
            "Перезаписать их заново? Ассеты и номера уровней останутся те же.");
    }
}
