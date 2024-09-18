using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ClickButton : MonoBehaviour
{
    public float speed = 12f;
    public Vector3 target;
    public Vector3 target2;
    private bool moveIng = false;
    private bool moveExit = false;
    public GameObject code;

    private void FixedUpdate()
    {
        if (moveIng && !moveExit && transform.position != target)
            transform.position = Vector3.Lerp(transform.position, target, speed * Time.fixedDeltaTime);
        else if (!moveIng && moveExit && transform.position != target2)
            transform.position = Vector3.Lerp(transform.position, target2, speed * Time.fixedDeltaTime);
        //moveIng = false;
        //Invoke(nameof(Stoping), 1);
    }

    private void Stoping()
    {
        code.GetComponent<ClickButton>().enabled = false;
    }
    

    public void clickButton()
    {
        moveIng = true;
        moveExit = false;
    }

    public void ClickExitButton()
    {
        moveIng = false;
        moveExit = true;
    }
}
