using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonsManager : MonoBehaviour
{
    public GameObject[] Magnets;
    public GameObject[] Chosen;
    public GameObject[] Correct;
    public Data incorrect;
    public Button playBut;


    public void PlayButton()
    {
        //gameObject.GetComponent<Renderer>().enabled = true;
        /*if (gameObject.GetComponent<Renderer>().enabled == false)
        {
            return;
        }*/
        for (int i = 0; i < 4; ++i)
        {
            if (Chosen[i] != Correct[i])
            {
                incorrect.burrons = false;
                //PopUp.CallPopUp("Wrong combination..", 2);
                //Debug.Log("BAD");
                foreach (var x in Chosen)
                {
                    x.GetComponent<DraggableItem>().GetComponent<RectTransform>().anchoredPosition = x.GetComponent<DraggableItem>().currentPosition;
                }
                for (int j = 0; j < 4; ++j)
                {
                    Chosen[j] = null;
                }
                return;
            }
        }
        incorrect.burrons = true;
        //PopUp.CallPopUp("Right combination!", 2);
        //Debug.Log("GOOD");
    }


    void Update()
    {
        foreach (var x in Chosen)
        {
            if (x == null)
            {
                playBut.GetComponent<Button>().enabled = false;
                return;
            }
        }
        playBut.GetComponent<Button>().enabled = true;
    }
}
