using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MushroomObj : MonoBehaviour
{
    public float bounceForce = 2f;
    public float bounceDelay = 0.5f;

    private bool objAttached = false;


    private void Awake()
    {
        // Раньше originalScale никогда не задавался (= 0,0,0) и Squash() сжимал гриб в точку.
        originalScale = transform.localScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (objAttached || !other.CompareTag("Cube")) return;
        var tile = other.GetComponent<OverlayTile>();
        if (tile == null) return;
        tile.attachedObject = gameObject;
        tile.block = SuperBlocks.Mushroom;
        objAttached = true;
    }

    public float topOffset = 0.1f;

    public Vector3 GetTopPosition()
    {
        return transform.position + Vector3.up * topOffset;
    }

    public float squashAmount = 0.3f; 
    public float squashDuration = 0.2f; 

    private Vector3 originalScale;
    public void Squash()
    {
        StopAllCoroutines();
        StartCoroutine(SquashCoroutine());
    }

    private IEnumerator SquashCoroutine()
    {
        // сжатие вниз
        Vector3 squashed = new Vector3(originalScale.x + squashAmount, originalScale.y - squashAmount, originalScale.z);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / squashDuration;
            transform.localScale = Vector3.Lerp(originalScale, squashed, t);
            yield return null;
        }

        // возврат обратно
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / squashDuration;
            transform.localScale = Vector3.Lerp(squashed, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }
}
