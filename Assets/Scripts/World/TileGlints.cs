using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Редкие блики на льду: раз в 1–2 секунды на случайной ледяной клетке вспыхивает маленькая искорка.
/// Совсем немного — чтобы лёд «жил», но не отвлекал от головоломки.
/// Создаётся сам на сцене с картой (MapManager); на уровнях без льда ничего не делает.
/// </summary>
public class TileGlints : MonoBehaviour
{
    static readonly Color[] Glint = { Color.white, new Color(0.85f, 0.97f, 1f, 1f) };
    readonly List<Vector2Int> _ice = new List<Vector2Int>();
    float _next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

    static void Ensure()
    {
        if (FindObjectOfType<TileGlints>() != null || FindObjectOfType<MapManager>() == null) return;
        new GameObject("TileGlints").AddComponent<TileGlints>();
    }

    void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + Random.Range(1f, 2f);

        var mm = MapManager.Instance;
        if (mm == null || mm.map == null) return;
        _ice.Clear();
        _ice.AddRange(SpecialTiles.CellsOf(SuperBlocks.Ice));
        if (_ice.Count == 0) return;

        var cell = _ice[Random.Range(0, _ice.Count)];
        if (!mm.map.TryGetValue(new Vector3Int(cell.x, cell.y, 0), out var tile) || tile == null) return;
        if (!SpecialTiles.TryGetBlock(cell, out var block)) return;
        var sr = block.GetComponent<SpriteRenderer>();
        var pos = tile.transform.position + new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(-0.01f, 0.03f), 0f);
        PixelFX.Burst(pos, Glint, 1, 0.01f, 0.02f, 0f,
                      sr != null ? sr.sortingLayerID : 0, sr != null ? sr.sortingOrder + 3 : 0, null, 0.9f, 0.5f);
    }
}
