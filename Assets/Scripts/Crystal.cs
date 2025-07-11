using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Crystal : MonoBehaviour
{
    public float transitionTime = 1f;

    public int num;

    public Data tran;

    public Vector3 targetPosition;

    public float speedFalling;

    void Update()
    {
        if (tran.FallingCrystal && transform.position != targetPosition && !tran.FallingMap)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition + new Vector3(0, 0.12f, 0), Time.deltaTime * speedFalling);
            speedFalling += 0.02f;
        }

        if (Vector3.Distance(transform.position, targetPosition + new Vector3(0, 0.12f, 0)) < 0.01f)
        {
            tran.FallingCrystal = false;
        }

        if (!tran.FallingCrystal && !tran.FallingCube && !tran.FallingMap)
        {
            GetComponent<PolygonCollider2D>().enabled = true;
            GetComponentInParent<Animator>().enabled = true;
            GetComponent<Crystal>().enabled = false;
        }
    }

    /*public void LoadNextLeve()
    {
        StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex + num));
        tran.music = false;
        tran.menuclose = false;
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        transition.SetTrigger("Start");

        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(levelIndex);
    }*/
}
