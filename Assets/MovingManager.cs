using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using TMPro;

public class MovingManager : MonoBehaviour
{
    public List<string> ButtonID = new List<string>{};
    public List<string> ButtonIDCheck = new List<string>{};
    public List<string> FunctionSlot = new List<string>{};
    public List<string> FunctionSlotReverse = new List<string>{};
    public List<string> ReverseID = new List<string>{};
    public List<string> WhileSlot = new List<string>{};

    public InventoryManager Items;
    public InventoryManager Items1;
    public InventoryManager Items2;

    public CursorR currentTile;

    public bool play = false;

    public Button button;

    public GameObject Rewind;

    public GameObject Play;

    public CharacterCode character;

    public Data plays;

    public CharacterCode satr;

    public int but, func;

    public  TMP_Dropdown dropdown;

    private string selectedOption;

    private void Start()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }
    }

    public void MoveCharacter(String move)
    {
        if (ButtonID.Count >= but && !ButtonID.Contains(null))
            return;

        if (ButtonID.Contains(null))
        {
            Debug.Log("gg");
            ButtonID[ButtonID.IndexOf(null)] = move;
            ButtonIDCheck[ButtonIDCheck.IndexOf(null)] = move;
        }
        else
        {
            ButtonID.Add(move);
            ButtonIDCheck.Add(move);
        }
        Debug.Log(ButtonID.BinarySearch(null));
    }

    public void RightFu()
    {   
        if (FunctionSlot.Count >= func && !FunctionSlot.Contains(null))
            return;

        FunctionSlot.Add("Right");
    }

    public void LeftFu()
    {   
        if (FunctionSlot.Count >= func && !FunctionSlot.Contains(null))
            return;

        FunctionSlot.Add("Left");
    }

    public void ForwardFu()
    {   
        if (FunctionSlot.Count >= func && !FunctionSlot.Contains(null))
            return;

        FunctionSlot.Add("Forward");
    }

    public void RightWh()
    {   
        WhileSlot.Add("Right");
    }

    public void LeftWh()
    {   
        WhileSlot.Add("Left");
    }

    public void ForwardWh()
    {   
        WhileSlot.Add("Forward");
    }

    public void FunctionWh()
    {
        WhileSlot.Add("Function");
    }

    public void Function()
    {
        if (ButtonID.Count >= but && !ButtonID.Contains(null))
            return;

        if (ButtonID.Contains(null))
        {
            Debug.Log("gg");
            ButtonID[ButtonID.IndexOf(null)] = "Function";
            ButtonIDCheck[ButtonIDCheck.IndexOf(null)] = "Function";
        }
        else
        {
            Debug.Log("ff");
            ButtonID.Add("Function");
            ButtonIDCheck.Add("Function");
        }
    }

    public void While()
    {
        if (ButtonID.Count >= but && !ButtonID.Contains(null))
            return;

        if (ButtonID.Contains(null))
        {
            Debug.Log("gg");
            ButtonID[ButtonID.IndexOf(null)] = "While";
            ButtonIDCheck[ButtonIDCheck.IndexOf(null)] = "While";
        }
        else
        {
            Debug.Log("ff");
            ButtonID.Add("While");
            ButtonIDCheck.Add("While");
        }
    }


    private void Update()
    {
        if (plays.RemoveBool)
        {
            if (plays.RemoveBlock < 10)
            {
                ButtonID[plays.RemoveBlock] = null;
                ButtonIDCheck[plays.RemoveBlock] = null;
            }

            else if (plays.RemoveBlock == 14)
            {
                WhileSlot[0] = null;
            }

            else
            {
                FunctionSlot[(plays.RemoveBlock % 10)] = null;
            }
        }
        plays.RemoveBool = false;
    }

    public void ClearButtons()
    {
        character.star1 = false;
        ButtonID.Clear();
        ButtonIDCheck.Clear();
        ReverseID.Clear();
        Items.GetDestroyItem();
        Items1.GetDestroyItem();
        Items2.GetDestroyItem();
        button.GetComponent<Button>().enabled = true;
        Play.SetActive(true);
        Rewind.SetActive(false);
        FunctionSlot.Clear();
        FunctionSlotReverse.Clear();
        character.transform.position = new Vector3(currentTile.transform.position.x, currentTile.transform.position.y, currentTile.transform.position.z + 0.7f);
    }

    void OnDropdownValueChanged(int index)
    {
        selectedOption = dropdown.options[index].text;
        
        Debug.Log("Выбрано: " + selectedOption);
    }

    public void StartPlay()
    {
        ReverseID.Clear();
        FunctionSlotReverse.Clear();
        ReverseID.AddRange(ButtonID);
        FunctionSlotReverse.AddRange(FunctionSlot);
        for (int i = 0; i < ButtonIDCheck.Count; i++)
        {
            Debug.Log(FunctionSlot.Count);
            Debug.Log(ReverseID.Count);

            if (ButtonID[i] == "While")
            {
                Debug.Log("Выбрано: " + selectedOption);
                ButtonID.RemoveAt(i);
                ButtonIDCheck.RemoveAt(i);
                Debug.Log(String.Join(", ", WhileSlot));
                for (int j = 0; j < Int32.Parse(selectedOption); j++)
                {
                    ButtonID.InsertRange(i, WhileSlot);
                    ButtonIDCheck.InsertRange(i, WhileSlot);
                }
            }

            Debug.Log(String.Join(", ", ButtonID));

            if (ButtonID[i] == "Function")
            {
                Debug.Log("Times");
                ButtonID.RemoveAt(i);
                ButtonIDCheck.RemoveAt(i);
                ButtonID.InsertRange(i, FunctionSlot);
                ButtonIDCheck.InsertRange(i, FunctionSlot);
                i += FunctionSlot.Count - 1;

                Debug.Log(String.Join(", ", ButtonID));
            }
        }
        /*for(int i = 0; i < ButtonID.Count; i++)
        {
            Debug.Log(i);
            Debug.Log(ButtonID[i]);
        }*/
        play = true;
        button.GetComponent<Button>().enabled = false;
        Play.SetActive(false);
        Rewind.SetActive(true);
    }

    public void ReversePlay()
    {
        FunctionSlot.Clear();
        ButtonID.AddRange(ReverseID);
        ButtonIDCheck.AddRange(ReverseID);
        FunctionSlot.AddRange(FunctionSlotReverse);
        character.star1 = false;
        character.transform.position = new Vector3(currentTile.transform.position.x, currentTile.transform.position.y, currentTile.transform.position.z + 0.7f);
        Play.SetActive(true);
        Rewind.SetActive(false);
    }

    public void StartPlayCourse2()
    {
        if (plays.burrons)
        {
            play = true;
            ButtonID = new List<string>{"Forward", "Attack", "Forward", "Left", "Left", "Left", "Bottom"};
            ButtonIDCheck = new List<string>{"Forward", "Attack", "Forward", "Left", "Left", "Left", "Bottom"};
            button.GetComponent<Button>().enabled = false;
            Debug.Log(ButtonID.Count);
        }
    }

    public void StartPlayCourse3()
    {
        play = true;
        satr.star1 = false;
        currentTile.star = false;
        /*for(int i = 0; i < ButtonID.Count; i++)
        {
            Debug.Log(i);
            Debug.Log(ButtonID[i]);
        }*/
    }
}
