using CrystalAdventure.EditorTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Туториалы для уровней, которые создаёт кнопка «Создать уровни Winter» (WinterLevels).
///
/// Лежит в Assets/Scripts/Editor, а не рядом с WinterLevels: WinterLevels компилируется и в тестах ядра
/// (CrystalCoreTests) на заглушках, где нет TutorialStep. Поэтому WinterLevels знает только ключ
/// туториала (Spec.Tutorial), а сам ассет создаётся здесь — через WinterLevels.TutorialProvider.
///
/// Ассеты: Assets/Scripts/Tutorial/Tutorials/Tutorial_*.asset. Уже существующий ассет не перезаписывается
/// (его могли поправить руками в инспекторе).
/// Тексты — по-английски (это ключ), русский перевод — в UI/Loc.
/// </summary>
[InitializeOnLoad]
public static class WinterTutorials
{
    const string Folder = "Assets/Scripts/Tutorial/Tutorials";
    const string PortraitGuid = "dc1463edfe8eb304a92a7c222c9d1b7f"; // портрет Элы, как в Tutorial_Basics

    static WinterTutorials() => WinterLevels.TutorialProvider = Create;

    static TutorialScript Create(string key)
    {
        TutorialStep[] steps;
        string name;
        switch (key)
        {
            case "F2": name = "Tutorial_TwoFunctions"; steps = TwoFunctions(); break;
            default: Debug.LogError("[Crystal] Неизвестный туториал: " + key); return null;
        }

        string path = $"{Folder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TutorialScript>(path);
        if (existing != null) return existing;

        var t = ScriptableObject.CreateInstance<TutorialScript>();
        t.speakerName = "Ela";
        t.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(PortraitGuid));
        t.steps = steps;
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    static TutorialStep Step(string text, TutorialTarget target, TutorialWait wait,
        SlotType slot = SlotType.Main, ItemType card = ItemType.Forward, int count = 1, bool dim = true) =>
        new TutorialStep { text = text, target = target, wait = wait, slot = slot, card = card, count = count, dim = dim };

    /// <summary>
    /// LevelWinter10 «Вторая функция»: дорожка-«долина» — два раза лесенкой вниз (Назад, Назад, Вправо),
    /// два раза лесенкой вверх (Вправо, Вперёд, Вперёд). В Main нет стрелок — только F1 и F2,
    /// поэтому без второй функции не обойтись. Решение туториала: F1 = вниз, F2 = вверх, Main = F1 F1 F2 F2 (10 карточек — оптимум).
    /// </summary>
    static TutorialStep[] TwoFunctions() => new[]
    {
        Step("A new card: F2! It is a second function.", TutorialTarget.CommandButton, TutorialWait.Tap, card: ItemType.FunctionTwo),
        Step("Look: this time Main has no arrows. Only F1 and F2!", TutorialTarget.ProgramPanel, TutorialWait.Tap),
        Step("The path goes down the stairs two times, then up the stairs two times.", TutorialTarget.Hero, TutorialWait.Tap),
        Step("F1 will be the way down. Open the F1 tab.", TutorialTarget.Tab, TutorialWait.OpenTab, slot: SlotType.FunctionOne),
        Step("Down the stairs: put the Back card two times...", TutorialTarget.CommandButton, TutorialWait.AddCard, SlotType.FunctionOne, ItemType.Bottom, 2),
        Step("...and one Right card.", TutorialTarget.CommandButton, TutorialWait.AddCard, SlotType.FunctionOne, ItemType.Right),
        Step("F2 will be the way up. Open the F2 tab.", TutorialTarget.Tab, TutorialWait.OpenTab, slot: SlotType.FunctionTwo),
        Step("Up the stairs: first one Right card...", TutorialTarget.CommandButton, TutorialWait.AddCard, SlotType.FunctionTwo, ItemType.Right),
        Step("...then the Forward card two times.", TutorialTarget.CommandButton, TutorialWait.AddCard, SlotType.FunctionTwo, ItemType.Forward, 2),
        Step("Both functions are ready! Go back to the Main tab.", TutorialTarget.Tab, TutorialWait.OpenTab, slot: SlotType.Main),
        Step("Down two times: put F1 two times.", TutorialTarget.CommandButton, TutorialWait.AddCard, SlotType.Main, ItemType.FunctionOne, 2),
        Step("Up two times: put F2 two times.", TutorialTarget.CommandButton, TutorialWait.AddCard, SlotType.Main, ItemType.FunctionTwo, 2),
        Step("Ten cards for the whole valley. Press Play!", TutorialTarget.PlayButton, TutorialWait.PressPlay),
    };
}
