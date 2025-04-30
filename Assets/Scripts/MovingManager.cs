using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using TMPro;

public class MovingManager : MonoBehaviour
{
    public Dictionary<SlotType, List<Item>> Commands = new Dictionary<SlotType, List<Item>>()
    {
            { SlotType.Main, new List<Item> {} },
            { SlotType.FunctionOne, new List<Item> {} },
            { SlotType.FunctionTwo, new List<Item> {} }
    };
    public Dictionary<SlotType, List<Item>> SaveCommands = new Dictionary<SlotType, List<Item>>()
    {
            { SlotType.Main, new List<Item> {} },
            { SlotType.FunctionOne, new List<Item> {} },
            { SlotType.FunctionTwo, new List<Item> {} }
    };

    public List<Item> ReverseID = new List<Item>{};
    public List<Item> WhileSlot = new List<Item>{};

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

    private string selectedOption;

    public static MovingManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        else
        {
            Destroy(gameObject);
        }
    }

    public void MoveCharacter(Item command, SlotType type)
    {
        if (Commands[type].Count >= 8 && !Commands[type].Contains(null))
            return;

        if (Commands[type].Contains(null))
        {
            Debug.Log("gg");
            Commands[type][Commands[type].IndexOf(null)] = command;
            SaveCommands[type][SaveCommands[type].IndexOf(null)] = command;
        }
        else
        {
            Commands[type].Add(command);
            SaveCommands[type].Add(command);
        }
    }

    /*public void While()
    {
        if (ButtonID.Count >= 8 && !ButtonID.Contains(null))
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
    }*/

    public void RemoveCommand(int indexCommand)
    {
        if (indexCommand < 8)
        {
            Commands[SlotType.Main][indexCommand] = null;
            SaveCommands[SlotType.Main][indexCommand] = null;
        }

        else if (indexCommand > 8 && indexCommand < 18)
        {
            Commands[SlotType.FunctionOne][indexCommand % 10] = null;
        }

        else if (indexCommand > 18 && indexCommand < 28)
        {
            Commands[SlotType.FunctionTwo][indexCommand % 10] = null;
        }
    }

    public void ClearButtons()
    {
        character.star1 = false;

        Commands[SlotType.Main].Clear();
        SaveCommands[SlotType.Main].Clear();
        ReverseID.Clear();

        Items.GetDestroyItem();
        Items1.GetDestroyItem();
        Items2.GetDestroyItem();

        button.GetComponent<Button>().enabled = true;

        Play.SetActive(true);
        Rewind.SetActive(false);

        /*FunctionSlotOne.Clear();
        FunctionSlotOneReverse.Clear();*/
        
        character.transform.position = new Vector3(currentTile.transform.position.x, currentTile.transform.position.y, currentTile.transform.position.z + 0.7f);
    }

    public void StartPlay()
    {
        ReverseID.Clear();
        SaveCommands[SlotType.FunctionOne].Clear();
        ReverseID.AddRange(SaveCommands[SlotType.Main]);
        SaveCommands[SlotType.FunctionOne].AddRange(Commands[SlotType.FunctionOne]);

        for (int i = 0; i < SaveCommands[SlotType.Main].Count; i++)
        {
            /*Debug.Log(FunctionSlotOne.Count);
            Debug.Log(ReverseID.Count);*/
            if (Commands[SlotType.Main][i].type.ToString() == "While")
            {
                int repeatCount = Commands[SlotType.Main][i].iteration;
                if (i > 0) // Если перед "While" есть команды
                {
                    List<Item> pastCommands = Commands[SlotType.Main].GetRange(0, i); // Берём команды до "While"

                    // Создаём список pastCommands, повторённый repeatCount раз
                    List<Item> repeatedCommands = Enumerable.Repeat(pastCommands, repeatCount)
                                                            .SelectMany(x => x)
                                                            .ToList();

                    // Вставляем всё одним вызовом InsertRange()
                    Commands[SlotType.Main].InsertRange(i, repeatedCommands);
                    SaveCommands[SlotType.Main].InsertRange(i, repeatedCommands);

                    // Удаляем "While" по новой позиции
                    Commands[SlotType.Main].RemoveAt(i + repeatedCommands.Count);
                    SaveCommands[SlotType.Main].RemoveAt(i + repeatedCommands.Count);
                }
                else
                {
                    // Если "While" первый в списке, просто удаляем его
                    Commands[SlotType.Main].RemoveAt(i);
                    SaveCommands[SlotType.Main].RemoveAt(i);
                }
            }

            /*if (Commands[SlotType.Main][i] == "While")
            {
                if (i > 0)
                {
                    List<string> pastCommands = Commands[SlotType.Main].GetRange(0, i);

                    Commands[SlotType.Main].InsertRange(i, pastCommands);
                    SaveCommands[SlotType.Main].InsertRange(i, pastCommands); // Вставляем их на место "While"
                    Commands[SlotType.Main].RemoveAt(i + pastCommands.Count);
                    SaveCommands[SlotType.Main].RemoveAt(i + pastCommands.Count); // Удаляем "While", который сдвинулся дальше
                }
                else
                {
                    Commands[SlotType.Main].RemoveAt(i);
                    SaveCommands[SlotType.Main].RemoveAt(i); // Если "While" первый, просто удаляем его
                }
            }*/

            //Debug.Log(String.Join(", ", ButtonID));

            if (Commands[SlotType.Main][i].type.ToString() == "FunctionOne")
            {
                Debug.Log("Found 'Function' at index: " + i);

                Commands[SlotType.Main].RemoveAt(i);
                SaveCommands[SlotType.Main].RemoveAt(i);

                if (Commands[SlotType.FunctionOne].Count > 0)
                {
                    Commands[SlotType.Main].InsertRange(i, Commands[SlotType.FunctionOne]);
                    SaveCommands[SlotType.Main].InsertRange(i, Commands[SlotType.FunctionOne]);

                    i += Commands[SlotType.FunctionOne].Count - 1;
                }

                else
                {
                    Debug.LogWarning("FunctionSlot is empty. Skipping insertion.");
                    i--;
                }
            }

            Debug.Log("Updated ButtonID: " + String.Join(", ", Commands[SlotType.Main]));
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
        Commands[SlotType.FunctionOne].Clear();
        Commands[SlotType.Main].AddRange(ReverseID);
        SaveCommands[SlotType.Main].AddRange(ReverseID);
        Commands[SlotType.FunctionOne].AddRange(SaveCommands[SlotType.FunctionOne]);
        character.star1 = false;
        character.transform.position = new Vector3(currentTile.transform.position.x, currentTile.transform.position.y, currentTile.transform.position.z + 0.7f);
        Play.SetActive(true);
        Rewind.SetActive(false);
    }
}

public enum SlotType
{
    Main,
    FunctionOne,
    FunctionTwo
}
