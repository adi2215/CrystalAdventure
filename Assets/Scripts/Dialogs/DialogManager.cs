using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogManager : MonoBehaviour
{
    public Image actorImage;
    public Text actorName;
    public Text messageText;
    public RectTransform backGroundBox;

    public bool isActive = false;
    public Data data;

    private Message[] currentMessages;
    private Actor[] currentActors;
    private int activeMessage = 0;
    private DialogTrigger current_dialog;

    public void OpenDialogue(DialogTrigger dialog)
    {
        current_dialog = dialog;
        currentMessages = current_dialog.messages;
        currentActors = current_dialog.actors;
        activeMessage = 0;
        data.DialogManager = true;
        DisplayMessage();
        backGroundBox.LeanScale(new Vector3(1f, 1f, 1f), 0.5f).setEaseInOutExpo();
    }

    void DisplayMessage()
    {
        Message messageToDisplay = currentMessages[activeMessage];
        messageText.text = messageToDisplay.message;

        Actor actorToDisplay = currentActors[messageToDisplay.actorId];
        actorName.text = actorToDisplay.name;
        actorImage.sprite = actorToDisplay.sprite;
    }

    public void NextMessage()
    {
        activeMessage++;
        if (activeMessage < currentMessages.Length)
        {
            DisplayMessage();
        }
        else
        {
            backGroundBox.LeanScale(Vector3.zero, 0.5f).setEaseInOutExpo();
            data.DialogManager = false;
            //StartCoroutine(LastScene());
        }
    }

    /*public IEnumerator LastScene()
    {
        yield return new WaitForSeconds(2f);
        Application.Quit();
    } */

    void Start()
    {
        backGroundBox.transform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && data.DialogManager == true)
        {
            NextMessage();
        }
    }
}
