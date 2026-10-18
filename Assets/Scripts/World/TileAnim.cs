using UnityEngine;

/// <summary>
/// Простая покадровая анимация спрайта клетки (стрелка ветра, вихрь телепорта — World/SpecialTiles).
/// Кадры сменяются по реальному времени, поэтому крутятся и между прогонами.
/// </summary>
public class TileAnim : MonoBehaviour
{
    public Sprite[] Frames;
    public float Fps = 4f;

    SpriteRenderer _sr;
    float _phase;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _phase = Random.value * 10f; // соседние клетки не мигают в такт
    }

    void Update()
    {
        if (_sr == null || Frames == null || Frames.Length == 0) return;
        int i = (int)((Time.unscaledTime + _phase) * Fps) % Frames.Length;
        if (_sr.sprite != Frames[i]) _sr.sprite = Frames[i];
    }
}
