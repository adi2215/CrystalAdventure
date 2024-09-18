using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Crystal : MonoBehaviour
{
    public Animator transition;

    public float transitionTime = 1f;

    public int num;

    public Data tran;

    public GameObject targetPosition;

    public float speedFalling;

    private bool Falling = true;

    void Update()
    {
        if (Falling && transform.position != targetPosition.transform.position)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition.transform.position, Time.deltaTime * speedFalling);
            speedFalling += 0.02f;
        }

        if (transform.position == targetPosition.transform.position)
        {
            Falling = false;
        }

        if (!tran.FallingData)
        {
            GetComponent<PolygonCollider2D>().enabled = true;
            GetComponentInParent<Animator>().enabled = true;
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
