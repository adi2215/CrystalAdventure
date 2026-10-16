using System;
using System.Collections;
using CrystalAdventure.Core;
using UnityEngine;
using UnityEngine.Events;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Перерисовывает клетки, когда ядро сообщает TileVisual (ворота открылись,
    /// лёд сломан, облако исчезло...). Спрайты берутся из BiomeTileset текущего биома.
    /// Если у тебя свои объекты-блоки (как падающие блоки в MapManager) —
    /// подпишись на onTileChanged и обнови их там.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [Serializable] public class TileChangedEvent : UnityEvent<Vector3Int, string, bool> { }

        public GridMapper mapper;
        public BiomeTileset tileset;
        public GameObject breakFxPrefab;
        public TileChangedEvent onTileChanged = new TileChangedEvent();

        public void SyncAll(World w)
        {
            if (tileset == null) return;
            foreach (var kv in w.Tiles)
                mapper.tilemap.SetTile(mapper.ToTilemapCell(kv.Key), tileset.Resolve(kv.Value.Type, kv.Value.Gone, w.IsWalkable(kv.Key)));
        }

        /// <summary>Мгновенно применить изменение клетки.</summary>
        public bool Set(GameEvent e)
        {
            Enum.TryParse(e.Info, out TileType type);
            bool gone = e.Amount == 0;
            bool walkable = e.TargetId == 1;
            var cell = mapper.ToTilemapCell(e.Cell);

            if (tileset != null) mapper.tilemap.SetTile(cell, tileset.Resolve(type, gone, walkable));
            onTileChanged.Invoke(cell, e.Info, walkable);
            return gone;
        }

        public IEnumerator Apply(GameEvent e, float speed)
        {
            bool gone = Set(e);
            if (breakFxPrefab != null && gone)
            {
                var fx = Instantiate(breakFxPrefab, mapper.CellToWorld(e.Cell), Quaternion.identity);
                Destroy(fx, 2f);
            }
            yield return new WaitForSeconds(0.12f / speed);
        }
    }
}
