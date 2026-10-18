using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Грибы, кристаллы и другие активные объекты уровня.
///
/// Раньше собранный кристалл уничтожался (Destroy) и после Rewind не возвращался,
/// а счётчик собранных не сбрасывался — можно было «собрать» уровень по частям
/// за несколько попыток, а окно победы появлялось даже после Rewind.
/// Теперь кристалл просто прячется и возвращается при ResetLevelObjects().
/// </summary>
public class ActiveBlockManager : MonoBehaviour
{
    public static ActiveBlockManager Instance;

    public CharacterCode heroPlayer;

    public int crystalObjectCount = 0;
    public int collectedCrystalObject = 0;

    public GameObject mushroomBlock;
    public GameObject crystalBlock;

    [Tooltip("Пауза между сбором последнего кристалла и окном победы")]
    public float finishDelay = 1f;

    private readonly Dictionary<Vector3Int, TileBase> activeTileMap = new();
    private readonly Dictionary<Vector3Int, int> tileStepCounts = new();
    public HashSet<Vector3Int> triggeredFinishTiles = new HashSet<Vector3Int>();

    private readonly List<Crystal> crystals = new();
    private readonly List<Crystal> collected = new();
    private Coroutine finishing;

    public Tilemap tilemap;

    public bool LevelComplete { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Вызывается перед загрузкой уровня.</summary>
    public void ResetLevelTileData()
    {
        triggeredFinishTiles.Clear();
        activeTileMap.Clear();
        tileStepCounts.Clear();
        crystals.Clear();
        collected.Clear();
        collectedCrystalObject = 0;
        crystalObjectCount = 0;
        LevelComplete = false;
        if (finishing != null) { StopCoroutine(finishing); finishing = null; }
    }

    public void RegisterTile(TileInfo pos)
    {
        if (tilemap == null) return;

        if (pos.superTileType == SuperBlocks.Mushroom)
        {
            if (mushroomBlock == null) return;
            Vector3 c = tilemap.GetCellCenterWorld(pos.position);
            // Дочерний объект карты — покачивается вместе с островом.
            var mushroomObject = Instantiate(mushroomBlock, new Vector3(c.x, c.y + 0.15f, c.z + 1), Quaternion.identity, transform);
            if (mushroomObject.TryGetComponent<MushroomObj>(out var m)) m.enabled = true;
            return;
        }

        if (pos.superTileType == SuperBlocks.Crystal)
        {
            if (crystalBlock == null) return;
            Vector3 c = tilemap.GetCellCenterWorld(pos.position);
            var crystalObject = Instantiate(crystalBlock, new Vector3(c.x, c.y + 0.15f, c.z + 1), Quaternion.identity, transform);
            if (crystalObject.TryGetComponent<Crystal>(out var crystal))
            {
                crystal.enabled = true;
                crystals.Add(crystal);
            }
            CrystalJuice.Attach(crystalObject); // парит, светится, блестит
            crystalObjectCount++;
            return;
        }

        // Лёд и рассыпающиеся плиты рисует и возвращает World/SpecialTiles.
        if (pos.superTileType == SuperBlocks.Ice || pos.superTileType == SuperBlocks.Crumbling || pos.superTileType == SuperBlocks.Spikes ||
            pos.superTileType == SuperBlocks.Cloud || pos.superTileType == SuperBlocks.Wind || pos.superTileType == SuperBlocks.Teleport) return;

        activeTileMap[pos.position] = pos.tile;
        tileStepCounts[pos.position] = 0;
    }

    // ============================================================
    //                         КРИСТАЛЛЫ
    // ============================================================

    /// <summary>Куб встал на клетку: если там кристалл — забрать.</summary>
    public bool TryCollectCrystal(OverlayTile tile)
    {
        if (LevelComplete || tile == null || tile.attachedObject == null) return false;
        if (!tile.attachedObject.activeSelf) return false;
        if (!tile.attachedObject.TryGetComponent<Crystal>(out var crystal)) return false;

        crystal.attachedTile = tile;
        crystal.gameObject.SetActive(false);
        tile.attachedObject = null;
        tile.block = SuperBlocks.None;

        collected.Add(crystal);
        collectedCrystalObject = collected.Count;
        CheckCrystals();
        return true;
    }

    /// <summary>
    /// На уровнях с врагами победу решает ядро (кристаллы + повержен босс), а не счётчик кристаллов:
    /// EnemyLayer включает этот флаг и сам вызывает ForceVictory() в нужный ход.
    /// </summary>
    public bool victoryByCore;

    public void CheckCrystals()
    {
        if (victoryByCore) return;
        if (LevelComplete || crystalObjectCount == 0) return;
        if (collectedCrystalObject < crystalObjectCount) return;

        LevelComplete = true;
        if (heroPlayer != null && heroPlayer.List != null) heroPlayer.List.OnLevelComplete();
        finishing = StartCoroutine(FinishingCrystal());
    }

    /// <summary>Победа по решению ядра (уровни с врагами): то же, что после последнего кристалла.</summary>
    public void ForceVictory()
    {
        if (LevelComplete) return;
        LevelComplete = true;
        if (heroPlayer != null && heroPlayer.List != null) heroPlayer.List.OnLevelComplete();
        finishing = StartCoroutine(FinishingCrystal());
    }

    public IEnumerator FinishingCrystal()
    {
        yield return new WaitForSeconds(finishDelay);
        finishing = null;
        if (heroPlayer != null) heroPlayer.TransitionScene();
    }

    /// <summary>Rewind: вернуть собранные кристаллы и отменить победу, если окно ещё не открылось.</summary>
    public void ResetLevelObjects()
    {
        if (finishing != null) { StopCoroutine(finishing); finishing = null; }
        LevelComplete = false;

        foreach (var crystal in collected)
        {
            if (crystal == null) continue;
            crystal.gameObject.SetActive(true);
            if (crystal.attachedTile != null)
            {
                crystal.attachedTile.attachedObject = crystal.gameObject;
                crystal.attachedTile.block = SuperBlocks.Crystal;
            }
        }
        collected.Clear();
        collectedCrystalObject = 0;
        SpecialTiles.ResetAll(); // упавшие плиты и замороженные клетки — на место
        EnemyLayer.ResetAll();   // враги — на старт
    }

    // ============================================================
    //                    АКТИВНЫЕ ТАЙЛЫ (заготовка)
    // ============================================================

    public int GetTileTickCount(Vector3Int pos) => tileStepCounts.TryGetValue(pos, out var count) ? count : 0;

    public void SetTileTickCount(Vector3Int pos, int value) => tileStepCounts[pos] = value;

    public void RemoveTileAt(Vector3Int pos)
    {
        if (tilemap != null) tilemap.SetTile(pos, null);
        activeTileMap.Remove(pos);
        tileStepCounts.Remove(pos);
    }

    public void TryTriggerTile(Vector3Int position, GameObject player) { }

    public void TickAllTiles() { }
}
