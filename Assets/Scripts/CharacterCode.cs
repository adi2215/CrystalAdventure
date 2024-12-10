using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCode : MonoBehaviour
{
    public OverlayTile activeTile;

    private SpriteRenderer order;
    private Rigidbody2D rb;

    private WayTiles pathFinder;
    public float speed;
    public MovingManager List;
    public bool star1 = false;

    public Data tran;

    private List<OverlayTile> path = new List<OverlayTile>();

    public CursorR tile;

    public GameObject anim;

    public GameObject Enemy;

    public Button button;

    private bool Falling;

    public GameObject targetPosition;
    
    public float speedFalling;

    private void Start()
    {
        pathFinder = new WayTiles();
        rb = GetComponent<Rigidbody2D>();
        order = GetComponent<SpriteRenderer>();
    }
    /*0.005 -0.67 1*/
    void Update()
    {
        if (tran.FallingCube && transform.position != targetPosition.transform.position && !tran.FallingMap)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition.transform.position, Time.deltaTime * speedFalling);
            speedFalling += 0.02f;
        }

        if (transform.position == targetPosition.transform.position)
        {
            tran.FallingCube = false;
        }
    }

    void LateUpdate()
    {
        if (!star1 && List.play)
        {
            path = pathFinder.FindWays(tile.tile, List);
            star1 = true;
        }

        if (path.Count == 0)
        {
            List.play = false;
            button.GetComponent<Button>().enabled = true;
        }

        else if (path.Count > 0 && List.play)
        {
            Debug.Log(List.ButtonIDCheck.Count);
            if (List.ButtonIDCheck[0] != "Attack")
            {
                MoveAlong();
            }
            else if (List.ButtonIDCheck[0] == "Attack")
            {
                path.RemoveAt(0);
                speed = 0;
                List.ButtonIDCheck.RemoveAt(0);
                StartCoroutine(AttackAnim());
            }
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.tag == "Hero")
        {
            Destroy(other.gameObject);
            Invoke(nameof(TransitionScene), 0.5f);
        }
    }

    private void TransitionScene()
    {
        tran.trans = true;
    }

    private void MoveAlong()
    {  
        var step = speed * Time.deltaTime;
        var zIndex = path[0].transform.position.z;
        transform.position = Vector2.MoveTowards(transform.position, path[0].transform.position, step);
        transform.position = new Vector3(transform.position.x, transform.position.y, zIndex);

        if (Vector2.Distance(transform.position, path[0].transform.position) < 0.0001f)
        {
            OverlayTile currentTile = path[0];
            PositionCharacterOnLine(path[0]);
            path.RemoveAt(0);
            List.ButtonIDCheck.RemoveAt(0);

            if (currentTile.typeSprite == "CloudsTileMap_0")
            {
                StartCoroutine(FallingCube());
            }
        }
    }

    IEnumerator FallingCube()
    {
        order.spriteSortPoint = SpriteSortPoint.Center;
        yield return new WaitForSeconds(0.2f);
        transform.position = new Vector3(transform.position.x, transform.position.y, -0.7f);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = 0.5f;
        rb.gravityScale = 0.2f;
    }

    IEnumerator AttackAnim()
    {
        yield return new WaitForSeconds(0.5f);
        anim.SetActive(true);
        yield return new WaitForSeconds(1.2f);
        Enemy.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        speed = 1f;
    }
    
    private void PositionCharacterOnLine(OverlayTile tile)
    {
        transform.position = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
        GetComponent<SpriteRenderer>().sortingOrder = tile.GetComponent<SpriteRenderer>().sortingOrder;
        activeTile = tile;
    }
}
