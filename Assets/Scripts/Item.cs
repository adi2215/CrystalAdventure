using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Scriptable object/Item")]
public class Item : ScriptableObject
{
    public Sprite image;
    public ItemType type;
    public int iteration;
}

/// <summary>
/// Тип карточки. Значения хранятся в ассетах числом — новые добавлять ТОЛЬКО в конец.
/// Attack / Shield / SuperAttack называются так же, как ActionType ядра, — ItemProgramAdapter
/// переводит их сам. Иконки рисует UI/CardIcons, кнопки на панели создаёт SpawnScript.
/// </summary>
public enum ItemType {
    Forward,
    Left,
    Right,
    Bottom,
    FunctionOne,
    FunctionTwo,
    While,
    Ifandelse, 
    Null,
    Attack,       // удар по клетке, куда герой последний раз шагнул (или упёрся)
    Shield,       // щит на этот ход: удары врагов не ранят
    SuperAttack,  // сильный удар: пробивает ледяную броню (их мало — LevelRules.SuperAttacks)
}