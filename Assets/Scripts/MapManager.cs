using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


public class MapManager : MonoBehaviour
{
    private static MapManager _instance;

    public static MapManager Instance { get {return _instance; } }

    public OverlayTile overlayTilePrefab;

    public GameObject overlayContainer;

    public Dictionary<Vector2Int, OverlayTile> map;

    public Tilemap tilemap;

    List<Vector3Int> Tiles = new List<Vector3Int>();

    private bool MoveBlock = true;

    public GameObject objectPrefab;

    public GameObject Objec;

    List<GameObject> Obj = new List<GameObject>();

    [SerializeField] private AnimationCurve curve;

    [SerializeField] private Animator anim;

    private float _current, _target;
    
    bool DontOn = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }

        else
        {
            _instance = this;
        }
    }
    
    public void TileAppears()
    {
        var TileMap = gameObject.GetComponentInChildren<Tilemap>();
        map = new Dictionary<Vector2Int, OverlayTile>();
        BoundsInt bounds = TileMap.cellBounds;
        Debug.Log(bounds);

        for (int z = bounds.max.z; z >= bounds.min.z; z--)
        {
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    var TileLocation = new Vector3Int(x, y, z); 
                    var tileKey = new Vector2Int(x, y);

                    Debug.Log($"Anchor of Tilemap: {TileMap.HasTile(TileLocation)}, {TileLocation}");

                    if (TileMap.HasTile(TileLocation) && !map.ContainsKey(tileKey))
                    {
                        Debug.Log("feg");
                        // ADD array
                        Tiles.Add(TileLocation);
                        var ovarlayTile = Instantiate(overlayTilePrefab, overlayContainer.transform);
                        var blocks = Instantiate(objectPrefab, Objec.transform);
                        var cellWorldPosition = TileMap.GetCellCenterWorld(TileLocation);
                        
                        ovarlayTile.transform.position = new Vector3(cellWorldPosition.x, cellWorldPosition.y + (float)0.12, cellWorldPosition.z+1);
                        blocks.transform.position = new Vector3(cellWorldPosition.x, cellWorldPosition.y - 2, cellWorldPosition.z+1);
                        ovarlayTile.GetComponent<SpriteRenderer>().sortingOrder = TileMap.GetComponent<TilemapRenderer>().sortingOrder;
                        ovarlayTile.typeSprite = TileMap.GetTile<Tile>(TileLocation).sprite.name;
                        blocks.GetComponent<SpriteRenderer>().sortingOrder = TileMap.GetComponent<TilemapRenderer>().sortingOrder;
                        ovarlayTile.gridLocation = TileLocation;
                        Obj.Add(blocks);
                        map.Add(tileKey, ovarlayTile);
                    }

                }
            }
        }

        MoveBlock = false;
    }

    void Update()
    {
        /*if (!DontOn)
        {
            StartMove();
            //Debug.Log("LOL");
        } */

        if (MoveBlock)
            return;

        //_current = Mathf.MoveTowards(_current, _target, 0.14f * Time.deltaTime);
        //Obj[i].transform.position = Vector3.Lerp(Obj[i].transform.position, cellPosition, 0.1f * curve.Evaluate(_current));
        //Debug.Log(curve.Evaluate(_current));
        for (int i = 0; i < Tiles.Count; i++)
        {
            Tile currentTile = tilemap.GetTile<Tile>(Tiles[i]);
            Vector3 cellPosition = tilemap.GetCellCenterWorld(Tiles[i]);
            Obj[i].GetComponent<SpriteRenderer>().sprite = currentTile.sprite;
            if (currentTile.sprite.name == "CloudsTileMap_0" || currentTile.sprite.name == "EmptyTile")
            {
                Obj[i].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 0);
            }
            Obj[i].GetComponent<Sine>().FunctionPos(MoveBlock, cellPosition, i);
            Debug.Log("LOL");
        }
        Debug.Log("NUB");

        MoveBlock = true;
    }

    void StartMove()
    {
        MoveBlock = false;
        DontOn = true;
    }
}

