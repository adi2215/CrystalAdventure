using UnityEngine;

public class StartMusic : MonoBehaviour
{
    public string trackName = "Muse";

    void Start()
    {
        if (MusicManager.Instance != null) MusicManager.Instance.PlayMusic(trackName);
    }
}
