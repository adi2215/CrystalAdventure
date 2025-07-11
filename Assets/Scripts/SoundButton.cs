using UnityEngine;

public class SoundButton : MonoBehaviour
{
    public AudioClip sound;
    public AudioSource _source;

    private static SoundButton instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlaySound()
    {
        if (_source.isPlaying) return;

        _source.clip = sound;
        _source.Play();
    }
}
