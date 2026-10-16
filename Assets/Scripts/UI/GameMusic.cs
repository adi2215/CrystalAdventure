using System.Collections;
using UnityEngine;

/// <summary>
/// Фоновая музыка. Создаётся сама при запуске (ничего в сцену добавлять не нужно)
/// и живёт между перезагрузками уровня — трек не начинается заново на каждом уровне.
///
/// Трек: Assets/Resources/Music/CrystalTheme.ogg (оригинальный чиптюн, синтезирован кодом).
/// Чтобы поставить свой — положи файл с тем же именем в Resources/Music.
/// Громкость: GameMusic.Volume (0..1, сохраняется в PlayerPrefs "music_volume").
/// </summary>
public class GameMusic : MonoBehaviour
{
    const string VolumeKey = "music_volume";
    const string TrackPath = "Music/CrystalTheme";
    const float DefaultVolume = 0.3f;
    const float FadeInTime = 2.5f;

    public static GameMusic Instance { get; private set; }

    static float _volume = -1f;

    /// <summary>Громкость музыки 0..1 (0 — выключена).</summary>
    public static float Volume
    {
        get { if (_volume < 0f) _volume = PlayerPrefs.GetFloat(VolumeKey, DefaultVolume); return _volume; }
        set { _volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, _volume); }
    }

    AudioSource _source;
    float _fadeIn;
    float _duck = 1f;
    Coroutine _duckRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var clip = Resources.Load<AudioClip>(TrackPath);
        if (clip == null)
        {
            Debug.LogWarning("[GameMusic] Нет трека Resources/" + TrackPath);
            return;
        }
        var go = new GameObject("GameMusic");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameMusic>();
        Instance.Init(clip);
    }

    void Init(AudioClip clip)
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = clip;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.priority = 0;
        _source.volume = 0f;
        _source.Play();
    }

    void Update()
    {
        if (_source == null) return;
        _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, Time.unscaledDeltaTime / FadeInTime);
        _source.volume = Volume * _fadeIn * _duck;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Приглушить музыку на время (джингл победы и т.п.).</summary>
    public static void Duck(float level = 0.3f, float hold = 1.4f)
    {
        if (Instance == null) return;
        if (Instance._duckRoutine != null) Instance.StopCoroutine(Instance._duckRoutine);
        Instance._duckRoutine = Instance.StartCoroutine(Instance.DuckRoutine(level, hold));
    }

    IEnumerator DuckRoutine(float level, float hold)
    {
        float from = _duck;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.15f)
        {
            _duck = Mathf.Lerp(from, level, t);
            yield return null;
        }
        _duck = level;
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
        {
            _duck = Mathf.Lerp(level, 1f, t);
            yield return null;
        }
        _duck = 1f;
        _duckRoutine = null;
    }
}
