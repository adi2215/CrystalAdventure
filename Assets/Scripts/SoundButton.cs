using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundButton : MonoBehaviour
{
    public AudioClip sound;
    public AudioClip soundItems;
    public AudioSource _source;

    public void SoundClick()
    {
        
    }

    public void SoundItemClick()
    {
        _source.PlayOneShot(soundItems);
    }
}
