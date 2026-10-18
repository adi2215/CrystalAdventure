using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Строит карту уровня: загружает Tilemap из LevelSt, создаёт overlay-клетки и
/// «падающие» блоки вступления.
///
/// Исправлено: Debug.Log в тройном цикле по всем ячейкам (сильно тормозил загрузку)
/// и в Update; жёстко зашитое «% 8» уровней; падение, если на клетке не Tile.
/// </summary>
public class MapManager : MonoBehaviour
{
    private static MapManager _instance;
    public static MapManager Instance => _instance;

    public OverlayTile overlayTilePrefab;
    public GameObject overlayContainer;

    public Dictionary<Vector3Int, OverlayTile> map;

    public Tilemap tilemap;

    private readonly List<Vector3Int> Tiles = new();
    private readonly List<GameObject> Obj = new();

    private bool MoveBlock = true;

    public GameObject objectPrefab;
    public GameObject Objec;

    [SerializeField] private AnimationCurve curve;

    public Data data;

    [Tooltip("Через сколько секунд после старта анимации блоков куб может падать на поле")]
    public float mapIntroTime = 1f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        data.FallingCube = true;
        data.FallingMap = true;
        TileAppears();
    }

    public void TileAppears()
    {
        var tileMap = GetComponentInChildren<Tilemap>();
        var tileLoad = tileMap.GetComponent<TilemapData>();

        int levelCount = tileLoad.tilemapData != null ? tileLoad.tilemapData.Length : 0;
        if (levelCount == 0)
        {
            Debug.LogError("[MapManager] В TilemapData нет уровней");
            return;
        }
        // Номер уровня по кругу, как раньше (% 8), но по реальному числу уровней.
        int levelIndex = ((data.NextLevel % levelCount) + levelCount) % levelCount;
        tileLoad.LoadTilemapFromData(levelIndex);

        map = new Dictionary<Vector3Int, OverlayTile>();
        Tiles.Clear();
        Obj.Clear();

        var renderer = tileMap.GetComponent<TilemapRenderer>();
        int sortingOrder = renderer != null ? renderer.sortingOrder : 0;
        BoundsInt bounds = tileMap.cellBounds;

        // Сверху вниз по слоям: на одну (x, y) берём самый верхний блок.
        for (int z = bounds.max.z; z >= bounds.min.z; z--)
        {
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    var tileLocation = new Vector3Int(x, y, z);
                    var tileKey = new Vector3Int(x, y, 0);
                    if (map.ContainsKey(tileKey) || !tileMap.HasTile(tileLocation)) continue;

                    var tile = tileMap.GetTile<Tile>(tileLocation);
                    var cellWorldPosition = tileMap.GetCellCenterWorld(tileLocation);

                    var overlayTile = Instantiate(overlayTilePrefab, overlayContainer.transform);
                    overlayTile.transform.position = new Vector3(cellWorldPosition.x, cellWorldPosition.y + 0.12f, cellWorldPosition.z + 1);
                    overlayTile.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
                    overlayTile.typeSprite = tile != null && tile.sprite != null ? tile.sprite.name : "";
                    overlayTile.gridLocation = tileLocation;

                    var block = Instantiate(objectPrefab, Objec.transform);
                    block.transform.position = new Vector3(cellWorldPosition.x, cellWorldPosition.y - 2, cellWorldPosition.z + 1);
                    block.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;

                    Tiles.Add(tileLocation);
                    Obj.Add(block);
                    map.Add(tileKey, overlayTile);
                }
            }
        }

        MoveBlock = false;
    }

    void Update()
    {
        if (MoveBlock) return;
        MoveBlock = true; // один раз запускаем анимацию блоков

        for (int i = 0; i < Tiles.Count; i++)
        {
            var currentTile = tilemap.GetTile<Tile>(Tiles[i]);
            var sr = Obj[i].GetComponent<SpriteRenderer>();
            if (currentTile != null)
            {
                sr.sprite = currentTile.sprite;
                if (currentTile.sprite != null &&
                    (currentTile.sprite.name == "CloudsTileMap_0" || currentTile.sprite.name == "EmptyTile"))
                    sr.color = new Color(0, 0, 0, 0);
            }
            // Цветная клетка (условия): красим сам видимый блок — Tilemap под ним не виден.
            var tileColor = ColorTiles.At(Tiles[i].x, Tiles[i].y);
            if (tileColor != TileColor.None && sr.color.a > 0f) ColorTilePulse.Attach(sr, tileColor);

            var sine = Obj[i].GetComponent<Sine>();
            if (sine != null) sine.FunctionPos(false, tilemap.GetCellCenterWorld(Tiles[i]), i);
        }

        // Свет (материал с нормалями), камни под островом, тени, летающие обломки.
        IslandDecor.Build(Tiles, Obj, tilemap, transform.root);

        // Лёд, рассыпающиеся плиты и снег главы Winter — после света, чтобы картинка взяла тот же материал.
        for (int i = 0; i < Tiles.Count; i++) SpecialTiles.Decorate(Tiles[i], Obj[i]);

        StartCoroutine(StartMove());
    }

    private IEnumerator StartMove()
    {
        yield return new WaitForSeconds(mapIntroTime);
        data.FallingMap = false;
        // Враги появляются, когда остров собран (уровни с LevelSt.enemies).
        EnemyLayer.Setup(TilemapData.CurrentLevel);
    }
}
