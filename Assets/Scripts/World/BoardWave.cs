using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Волна по полю от точки удара (молния / супер-лёд ледяного босса) — как волна в FunVideo/VideoToTilemap:
/// каждая клетка подпрыгивает по синусу с задержкой, пропорциональной расстоянию до центра.
/// Здесь клетки — не тайлы Tilemap, а видимые блоки острова (SpecialTiles), поэтому вместе с блоком
/// сдвигаются клетка-подсветка, кристалл на ней и персонажи, которые на ней стоят.
///
/// Центр удара проседает вниз, кольца вокруг поднимаются и опадают; чем дальше — тем слабее.
/// Сдвиги — на целые пиксели (0.01 мира) и только приращениями: в конце всё возвращается ровно туда,
/// где было, даже если остров в это время покачивается.
/// </summary>
public static class BoardWave
{
    sealed class Mover
    {
        public Transform T;
        public float Applied;
    }

    sealed class CellWave
    {
        public float Delay, Amp;
        public readonly List<Mover> Movers = new List<Mover>();
    }

    static readonly List<CellWave> Active = new List<CellWave>();

    /// <summary>
    /// Rewind посреди волны: корутина остановлена, сдвиги снимаем сразу. Героя не трогаем —
    /// Rewind уже поставил его на старт точной позицией.
    /// </summary>
    public static void ResetAll()
    {
        foreach (var w in Active)
            foreach (var m in w.Movers)
                if (m.T != null && m.T.GetComponent<CharacterCode>() != null) m.Applied = 0f;
        foreach (var w in Active) Shift(w, 0f);
        Active.Clear();
    }

    /// <param name="center">Клетка удара.</param>
    /// <param name="amplitude">Высота волны у центра (в единицах мира; 0.01 = 1 пиксель).</param>
    /// <param name="extra">Что ещё стоит на клетке (герой, враги) — сдвигается вместе с блоком.</param>
    public static IEnumerator Play(Vector2Int center, float amplitude, System.Func<Vector2Int, IEnumerable<Transform>> extra = null)
    {
        const float Speed = 10f;   // клеток в секунду
        const float Bounce = 0.32f; // сколько длится подскок одной клетки
        var mm = MapManager.Instance;
        var cells = new List<CellWave>();
        float maxDelay = 0f;

        foreach (var cell in SpecialTiles.AllCells)
        {
            if (SpecialTiles.IsBrokenCell(cell) || !SpecialTiles.TryGetBlock(cell, out var block)) continue;
            float dist = Vector2.Distance(cell, center);
            var w = new CellWave
            {
                Delay = dist / Speed,
                // центр проседает, кольца поднимаются; дальше — слабее
                Amp = dist < 0.5f ? -amplitude * 0.8f : amplitude / (1f + dist * 0.35f),
            };
            var seen = new HashSet<Transform>();
            void Add(Transform t)
            {
                if (t == null || !seen.Add(t)) return;
                // Дочерние объекты блока поедут вместе с ним.
                if (t != block.transform && t.IsChildOf(block.transform)) return;
                w.Movers.Add(new Mover { T = t });
            }
            Add(block.transform);
            if (mm != null && mm.map != null && mm.map.TryGetValue(new Vector3Int(cell.x, cell.y, 0), out var tile) && tile != null)
            {
                Add(tile.transform);
                if (tile.attachedObject != null) Add(tile.attachedObject.transform);
            }
            if (extra != null)
                foreach (var t in extra(cell)) Add(t);
            cells.Add(w);
            maxDelay = Mathf.Max(maxDelay, w.Delay);
        }

        Active.AddRange(cells);
        float total = maxDelay + Bounce;
        for (float time = 0f; time < total; time += Time.deltaTime)
        {
            foreach (var w in cells)
            {
                float k = (time - w.Delay) / Bounce;
                float off = k > 0f && k < 1f ? Mathf.Sin(k * Mathf.PI) * w.Amp : 0f;
                off = Mathf.Round(off * 100f) / 100f; // целые пиксели
                Shift(w, off);
            }
            yield return null;
        }
        foreach (var w in cells) Shift(w, 0f);
        foreach (var w in cells) Active.Remove(w);
    }

    static void Shift(CellWave w, float off)
    {
        foreach (var m in w.Movers)
        {
            if (m.T == null) continue;
            float d = off - m.Applied;
            if (d == 0f) continue;
            m.T.position += new Vector3(0f, d, 0f);
            m.Applied = off;
        }
    }
}
