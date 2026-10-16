using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Показывает, что враги сделают в следующем ходу: красные клетки — удар,
    /// стрелка — шаг. Игрок видит это ещё ДО нажатия «Старт» и строит программу с учётом врагов.
    /// </summary>
    public class IntentOverlay : MonoBehaviour
    {
        public GridMapper mapper;
        [Tooltip("Спрайт-маркер «сюда ударят» (красный ромб размером с клетку)")]
        public SpriteRenderer dangerPrefab;
        [Tooltip("Стрелка «сюда пойдёт» (смотрит вправо)")]
        public SpriteRenderer arrowPrefab;
        public int sortingOrder = 5;

        readonly Dictionary<int, List<GameObject>> _byEntity = new Dictionary<int, List<GameObject>>();
        readonly Stack<GameObject> _dangerPool = new Stack<GameObject>();
        readonly Stack<GameObject> _arrowPool = new Stack<GameObject>();

        public void Show(GameEvent planned, Cell entityPos)
        {
            Clear(planned.EntityId);
            var list = new List<GameObject>();

            if (planned.Area != null && dangerPrefab != null)
            {
                foreach (var c in planned.Area)
                {
                    var go = Take(_dangerPool, dangerPrefab);
                    go.transform.position = mapper.CellToWorld(c) + new Vector3(0f, -0.1f, 0.01f);
                    list.Add(go);
                }
            }
            else if (planned.Info == IntentKind.Move.ToString() && arrowPrefab != null)
            {
                var go = Take(_arrowPool, arrowPrefab);
                var from = mapper.CellToWorld(entityPos);
                var off = GridMapper.DirToWorld(planned.Dir);
                go.transform.position = from + off * 0.6f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(off.y, off.x) * Mathf.Rad2Deg);
                list.Add(go);
            }
            _byEntity[planned.EntityId] = list;
        }

        public void Clear(int entityId)
        {
            if (!_byEntity.TryGetValue(entityId, out var list)) return;
            foreach (var go in list)
            {
                go.SetActive(false);
                (go.name.StartsWith("Arrow") ? _arrowPool : _dangerPool).Push(go);
            }
            _byEntity.Remove(entityId);
        }

        public void ClearAll()
        {
            foreach (var id in new List<int>(_byEntity.Keys)) Clear(id);
        }

        GameObject Take(Stack<GameObject> pool, SpriteRenderer prefab)
        {
            GameObject go;
            if (pool.Count > 0) go = pool.Pop();
            else
            {
                var sr = Instantiate(prefab, transform);
                sr.sortingOrder = sortingOrder;
                go = sr.gameObject;
                go.name = (prefab == arrowPrefab ? "Arrow" : "Danger") + "_" + go.GetInstanceID();
            }
            go.SetActive(true);
            return go;
        }
    }
}
