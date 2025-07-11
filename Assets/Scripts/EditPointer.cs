using System.Linq;
using UnityEngine;

public class EditPointer : MonoBehaviour
{
    public TilemapData dataTileMap;
    private void LateUpdate()
    {
        var tileHit = GetTile();

        if (tileHit.HasValue)
        {
            GameObject tile = tileHit.Value.collider.gameObject;
            transform.position = tile.transform.position;

            if (Input.GetMouseButtonDown(0))
            {
                OverlayTile _curTile = tile.GetComponent<OverlayTile>();
                _curTile.ShowTile();
                dataTileMap.PosCharacter(_curTile);
            }

            if (Input.GetMouseButtonDown(1))
            {
                OverlayTile _curTile = tile.GetComponent<OverlayTile>();
                _curTile.ShowTile();
                dataTileMap.PosFinish(_curTile);
            }
        }
    }

    public RaycastHit2D? GetTile()
    {
        Vector3 mPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mPos2D = new Vector2(mPos.x, mPos.y);

        RaycastHit2D[] hits = Physics2D.RaycastAll(mPos2D, Vector2.zero);

        if (hits.Length > 0)
        {
            return hits.OrderByDescending(i => i.collider.transform.position.z).First();
        }
        return null;
    }
}
