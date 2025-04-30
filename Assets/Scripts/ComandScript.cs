using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class ComandScript : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textComand;
    [SerializeField] private TMP_InputField inputField;

    List<string> coordinatesA = new List<string>()
    {
        "A1", "A2", "A3"
    };

     List<string> coordinatesB = new List<string>()
     {
        "B1", "B2", "B3"
     };

    List<string> words = new List<string>()
    {
        "destroy", "move", "reverse"
    };

    List<string> moves = new List<string>(){};

    public MovingManager list;

    public Color color;

    private string checkText;
    
    delegate char OnValidateInput(string text, int charIndex, char addedChar);

    /*public void ShowText()
    {
        textComand.text = "> " + Texting(inputField.text.ToLower(), color);

        inputField.text = "";

    }*/

    /*public void TypeText(string textComand)
    {
         inputField.onValidateInput = (string text, int charIndex, char addedChar) => {
            return ValidateChar(text, addedChar);
        };
    }*/

    /*private string Texting(string text, Color color)
    {
        string [] tokens = text.Split(' ');
        List<string> coor = new List<string>(text.Split(' '));
        string output = text;
        string nice = "";
        if (coor.Count <= 3)
        {
            switch (tokens[0])
            {
                case "destroy":
                    foreach (var x in coordinatesA)
                    {
                        if (coor[1] == x)
                        {
                            foreach (var y in coordinatesA)
                            {
                                if (coor[2] == y)
                                {
                                    output = "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + tokens[0] + "</color>" + ' ' + coor[1] + ' ' + coor[2];
                                    break;
                                }
                                else
                                {
                                    return output;
                                }
                            }
                        }
                        else
                        {
                            return output;
                        }
                    }
                    break;
            
                case "move":
                    for (int i = 1; i < coor.Count; i++)
                    {
                        if (String.Equals(coor[i], "f"))
                        {
                            Debug.Log("True");
                            list.ButtonID.Add("Forward");
                            list.ButtonIDCheck.Add("Forward");
                            moves.Add("Forward");
                            nice += " f";
                        }

                        else if (coor[i].Equals("r"))
                        {
                            list.ButtonID.Add("Right");
                            list.ButtonIDCheck.Add("Right");
                            moves.Add("Right");
                            nice += " r";
                        }

                        else if (coor[i].Equals("l"))
                        {
                            list.ButtonID.Add("Left");
                            list.ButtonIDCheck.Add("Left");
                            nice += " l";
                        }
                    }
                    output = "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + tokens[0] + "</color>" + nice;
                    break;
            }
            //return output;
        }

        return output;
    }*/


    /*private char ValidateChar(string validateChar, char addedChar)
    {
            if (Char.IsUpper(addedChar))
            {
                addedChar = char.ToLower(addedChar);
            }
            return addedChar;
    }*/
}
