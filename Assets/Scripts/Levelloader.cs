using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Levelloader : MonoBehaviour
{
    public Animator transition;

    public float transitionTime = 1f;

    public Data tran;

    void Update()
    {
        if (tran.trans)
        {
            LoadNextLeve();
            tran.trans = false;
        }
    }

    public void LoadNextLeve()
    {
        StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex));
        tran.music = false;
    }

    public void LoadLevelIndex(int index)
    {
        StartCoroutine(LoadLevel(index + 1));
        tran.music = false;
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        if (levelIndex == 0)
        {
            tran.menuclose = false;
        }

        if (SceneManager.GetActiveScene().name == "Menu")
            tran.NextLevel = levelIndex;
        else
            tran.NextLevel++;

        transition.SetTrigger("Start");

        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(0);
    }

}
