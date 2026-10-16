using System.Collections;
using UnityEngine;

/// <summary>
/// Музыка между сценами. Исправлено: при каждой перезагрузке уровня тот же трек
/// начинался заново, а два вызова подряд запускали две параллельные корутины громкости.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [SerializeField] private MusicLibrary musicLibrary;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private float volume = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayMusic(string trackName, float fadeDuration = 0.5f)
    {
        if (musicLibrary == null || musicSource == null) return;
        var clip = musicLibrary.GetClipFromName(trackName);
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return; // уже играет

        StopAllCoroutines();
        StartCoroutine(AnimateMusicCrossfade(clip, fadeDuration));
    }

    IEnumerator AnimateMusicCrossfade(AudioClip nextTrack, float fadeDuration)
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        float startVolume = musicSource.volume;

        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fadeDuration)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        musicSource.clip = nextTrack;
        musicSource.Play();

        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fadeDuration)
        {
            musicSource.volume = Mathf.Lerp(0f, volume, t);
            yield return null;
        }
        musicSource.volume = volume;
    }
}
