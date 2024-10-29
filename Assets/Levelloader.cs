using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Levelloader : MonoBehaviour
{
    public Animator transition;

    public float transitionTime = 1f;

    public int num;

    public Data tran;

    void Update()
    {
        if (tran.trans)
        {
            LoadNextLeve(num);
            tran.trans = false;
        }
    }

    public void LoadNextLeve(int num)
    {
        StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex + num));
        tran.music = false;
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        if (levelIndex == 0)
        {
            tran.menuclose = false;
        }

        transition.SetTrigger("Start");

        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(levelIndex);
    }

}
