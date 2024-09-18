using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class MouseController : MonoBehaviour
{
    public GameObject characterPrefab;
    public CharacterCode character;
    public GameObject characterContainer;

    private WayTiles pathFinder;
    public float speed;

    public MovingManager List;

    private int o = 0;

    private bool play = false;

    private List<OverlayTile> path = new List<OverlayTile>();

    private void Start()
    {
        pathFinder = new WayTiles();
    }

    public void StartPlay()
    {
        play = true;
    }

    void LateUpdate()
    {
        var focusedTileHit = GetFocusedOnTile();

        if (play)
        {
            path = pathFinder.FindWays(character.activeTile, List);

            if (path.Count > 0)
            {
                var step = speed * Time.deltaTime;
                var zIndex = path[0].transform.position.z;
                character.transform.position = Vector2.MoveTowards(character.transform.position, path[0].transform.position, step);
                character.transform.position = new Vector3(character.transform.position.x, character.transform.position.y, zIndex);

                if (Vector2.Distance(character.transform.position, path[0].transform.position) < 0.0001f)
                {
                    PositionCharacterOnLine(path[0]);
                    path.RemoveAt(0);
                }
            }
        }
        
        if (focusedTileHit.HasValue)
        {
            OverlayTile overlayTile = focusedTileHit.Value.collider.gameObject.GetComponent<OverlayTile>();
            transform.position = overlayTile.transform.position;
            gameObject.GetComponent<SpriteRenderer>().sortingOrder = overlayTile.GetComponent<SpriteRenderer>().sortingOrder;
            if (Input.GetMouseButton(1) && o == 0)
            {
                o++;
                PositionCharacterOnLine(overlayTile);
            }
        }
    }

    public RaycastHit2D? GetFocusedOnTile()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mousePos2d = new Vector2(mousePos.x, mousePos.y);

        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos2d, Vector2.zero);

        if (hits.Length > 0)
        {
            return hits.OrderByDescending(i => i.collider.transform.position.z).First();
        }

        return null;
    }

    private void PositionCharacterOnLine(OverlayTile tile)
    {
        character.transform.position = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
        character.GetComponent<SpriteRenderer>().sortingOrder = tile.GetComponent<SpriteRenderer>().sortingOrder;
        character.activeTile = tile;
    }
}
