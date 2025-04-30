using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackRound : MonoBehaviour
{
    public AudioSource Backmusic;
    public Data musicWork;


    private void Awake()
    {
        if (musicWork.music)
        {
            DontDestroyOnLoad(Backmusic);
        }
        else
        {
            Backmusic.enabled = false;
        }
    }
}
