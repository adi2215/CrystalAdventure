using System.Collections;
using UnityEngine;

/// <summary>Вспышка при сборе кристалла (см. CrystalJuice.PlayCollect).</summary>
public class CrystalCollectFX : MonoBehaviour
{
    static Sprite _ring;

    public void Play(Sprite sprite, int layer, int order) => StartCoroutine(Run(sprite, layer, order));

    IEnumerator Run(Sprite sprite, int layer, int order)
    {
        var sr = new GameObject("Crystal").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = sprite;
        if (WorldArt.Unlit != null) sr.sharedMaterial = WorldArt.Unlit;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        // спрайт кристалла с пивотом снизу — поднимаем, чтобы масштаб шёл от середины
        float half = sprite.bounds.extents.y;
        var basePos = transform.position;

        var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
        ring.transform.SetParent(transform, false);
        if (_ring == null) _ring = WorldArt.Glow(30);
        ring.sprite = _ring;
        if (WorldArt.Unlit != null) ring.sharedMaterial = WorldArt.Unlit;
        ring.sortingLayerID = layer;
        ring.sortingOrder = order - 1;

        const float time = 0.5f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            float e = 1f - (1f - t) * (1f - t);
            float s = 1f + 0.7f * e;
            sr.transform.localScale = new Vector3(s * (1f - 0.25f * Mathf.Sin(t * Mathf.PI)), s, 1f);
            sr.transform.position = basePos + Vector3.up * (0.14f * e - half * s);
            var c = Color.white;
            c.a = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
            sr.color = c;

            ring.transform.localScale = Vector3.one * (0.6f + 1.8f * e);
            ring.color = new Color(1f, 0.9f, 1f, 0.7f * (1f - t));
            yield return null;
        }
        Destroy(gameObject);
    }
}
