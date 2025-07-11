using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelState : MonoBehaviour
{
    [SerializeField] private GameObject[] Stars;
    [SerializeField] private GameObject Zamok;
    public int levelNumber;

    public void LevelComplete() => Zamok.SetActive(false);

    public void SetUnlocked(bool active) => Zamok.SetActive(!active);
}
