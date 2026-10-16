using UnityEngine;

/// <summary>
/// Звуки интерфейса. В сцене кнопки вызывают SoundClick и SoundItemClick —
/// раньше таких методов не было, и вызовы молча пропадали.
/// Звук через PlayOneShot: быстрые клики больше не «глотают» звук.
/// Важно: объект SoundManager в сцене выключен — включи его, чтобы звуки заработали.
/// </summary>
public class SoundButton : MonoBehaviour
{
    public AudioClip sound;
    [Tooltip("Клик по кнопкам меню (пусто = sound)")] public AudioClip clickSound;
    [Tooltip("Взять / убрать карточку (пусто = sound)")] public AudioClip itemSound;
    public AudioSource _source;

    private static SoundButton instance;

    private void Awake()
    {
        // Копию из перезагруженной сцены НЕ уничтожаем: на неё ссылаются OnClick кнопок этой сцены.
        // Она просто переадресует звук живому экземпляру (см. Play).
        if (instance != null && instance != this) return;

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void PlaySound() => Play(sound);

    public void SoundClick() => Play(clickSound != null ? clickSound : sound);

    public void SoundItemClick() => Play(itemSound != null ? itemSound : sound);

    void Play(AudioClip clip)
    {
        // Кнопки сцены после перезагрузки уровня ссылаются на копию из этой сцены;
        // играем через живой экземпляр, чтобы звук не пропадал.
        var target = instance != null ? instance : this;
        if (clip == null || target._source == null || !target._source.isActiveAndEnabled) return;
        target._source.PlayOneShot(clip);
    }
}
