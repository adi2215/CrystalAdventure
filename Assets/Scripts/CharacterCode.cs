using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCode : MonoBehaviour
{
    public OverlayTile activeTile;

    private OverlayTile _tile;

    private SpriteRenderer order;
    private Rigidbody2D rb;

    private WayTiles pathFinder;
    public float speed;
    public MovingManager List;
    public bool star1 = false;

    public Data tran;

    private List<OverlayTile> path = new List<OverlayTile>();

    /*public GameObject anim;

    public GameObject Enemy;*/

    public Button button;

    public Vector3 targetPosition;
    public GameObject finishLevel;
    
    public float speedFalling;

    private bool isPaused = false;

    private float pauseTimer = 0f;

    private bool beginingTile = false;

    private void Start()
    {
        pathFinder = new WayTiles();
        rb = GetComponent<Rigidbody2D>();
        order = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (tran.FallingCube == false)
            return;
        else
        {
            if (tran.FallingCube && transform.position != targetPosition && !tran.FallingMap)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * speedFalling);
                speedFalling += 0.02f;
            }

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                tran.FallingCube = false;
            }
        }
    }

    void LateUpdate()
    {
        if (!star1 && List.play)
        {
            path = pathFinder.FindWays(_tile, List);
            star1 = true;
            tran.commandUsed = List.Commands[SlotType.Main].Count;
        }

        if (path.Count == 0)
        {
            List.play = false;
            button.GetComponent<Button>().enabled = true;
        }

        else if (path.Count > 0 && List.play)
        {
            //Debug.Log(List.ButtonIDCheck.Count);

            MoveAlong();

            /*else if (List.SaveCommands[SlotType.Main][0].type.ToString() == "Attack")
            {
                path.RemoveAt(0);
                speed = 0;
                List.SaveCommands[SlotType.Main].RemoveAt(0);
                StartCoroutine(AttackAnim());
            }*/
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.tag == "Hero")
        {
            Destroy(other.gameObject);
            Invoke(nameof(TransitionScene), 0.5f);
        }

        if (other.gameObject.tag == "Cube" && !beginingTile)
        {
            Debug.Log(other.gameObject);
            _tile = other.gameObject.GetComponent<OverlayTile>();
            Debug.Log(_tile.gameObject.transform.position);
            beginingTile = true;
        }
    }

    private void TransitionScene()
    {
        finishLevel.SetActive(true);
    }

    private void MoveAlong()
    {  
        if (isPaused)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
            }
            return;
        }

        var step = speed * Time.deltaTime;
        var zIndex = path[0].transform.position.z;
        transform.position = Vector2.MoveTowards(transform.position, path[0].transform.position, step);
        transform.position = new Vector3(transform.position.x, transform.position.y, zIndex);

        if (Vector2.Distance(transform.position, path[0].transform.position) < 0.0001f)
        {
            OverlayTile currentTile = path[0];
            PositionCharacterOnLine(path[0]);
            path.RemoveAt(0);
            List.SaveCommands[SlotType.Main].RemoveAt(0);
            isPaused = true;
            pauseTimer = 0.1f;

            /*if (currentTile.typeSprite == "CloudsTileMap_0")
            {
                StartCoroutine(FallingCube());
            }*/
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

    /*IEnumerator AttackAnim()
    {
        yield return new WaitForSeconds(0.5f);
        anim.SetActive(true);
        yield return new WaitForSeconds(1.2f);
        Enemy.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        speed = 1f;
    }*/


    private void PositionCharacterOnLine(OverlayTile tile)
    {
        transform.position = new Vector3(tile.transform.position.x, tile.transform.position.y, tile.transform.position.z + 0.001f);
        GetComponent<SpriteRenderer>().sortingOrder = tile.GetComponent<SpriteRenderer>().sortingOrder;
        activeTile = tile;
        _tile = activeTile;
    }
}
