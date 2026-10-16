using UnityEngine;

public class Crystal : MonoBehaviour
{
    public float transitionTime = 1f;

    public int num;

    public Data tran;

    public Vector3 targetPosition;

    public float speedFalling;

    /// <summary>Клетка, на которой лежит кристалл (нужна, чтобы вернуть его после Rewind).</summary>
    [HideInInspector] public OverlayTile attachedTile;

    private bool objAttached = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (objAttached || !other.CompareTag("Cube")) return;

        var tile = other.GetComponent<OverlayTile>();
        if (tile == null) return;

        tile.attachedObject = gameObject;
        tile.block = SuperBlocks.Crystal;
        attachedTile = tile;
        objAttached = true;
    }
}
