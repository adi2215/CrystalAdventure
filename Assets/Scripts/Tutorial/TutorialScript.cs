using UnityEngine;

/// <summary>
/// Пошаговый туториал уровня. Назначается в LevelSt → tutorial.
/// Create → Crystal Adventure → Tutorial.
///
/// Каждый шаг: реплика персонажа + что подсветить + чего ждать от игрока.
/// Показывается, пока уровень ни разу не пройден.
/// </summary>
[CreateAssetMenu(menuName = "Crystal Adventure/Tutorial")]
public class TutorialScript : ScriptableObject
{
    public string speakerName = "Ela";
    [Tooltip("Портрет в окне реплики. Пусто — берётся из окна диалога в сцене.")]
    public Sprite portrait;
    public TutorialStep[] steps;
}

public enum TutorialTarget
{
    None,           // без подсветки
    Hero,           // куб
    Crystal,        // ближайший кристалл
    CommandButton,  // кнопка карточки (card) на панели (slot)
    ProgramPanel,   // ряд слотов программы (slot)
    Tab,            // вкладка Main / F1 / F2 (slot)
    PlayButton,
    ClearButton,
    HintButton,
    LevelsButton,
    LoopArrow,      // скобка цикла (slot) целиком, рука показывает, куда тянуть стрелку
    LoopBadge,      // значок ×N на скобке цикла (slot)
    ColorTile,      // первая цветная клетка на карте
    PaintButton,    // кнопка-краска первого цвета уровня
    ProgramCard,    // карточка card, уже лежащая в панели slot
}

public enum TutorialWait
{
    Tap,            // клик/тап — дальше
    AddCard,        // игрок кладёт карточку card в панель slot (count штук)
    OpenTab,        // игрок открывает вкладку slot
    PressPlay,      // игрок нажимает Play
    LevelComplete,  // ждём победы (подсветка и затемнение скрыты)
    LoopSpan,       // скобка первого цикла в панели slot охватывает ровно count карточек
    LoopRepeats,    // первый цикл в панели slot выполняется ×count раз
    PaintCard,      // карточка card в панели slot покрашена (любой цвет)
    PaintSelected,  // игрок взял краску (тапнул кнопку-краску)
}

[System.Serializable]
public class TutorialStep
{
    [TextArea(2, 4)] public string text;
    public TutorialTarget target;
    [Tooltip("Для CommandButton / AddCard")] public ItemType card;
    [Tooltip("Для CommandButton / ProgramPanel / Tab / AddCard / OpenTab")] public SlotType slot;
    public TutorialWait wait;
    [Tooltip("AddCard: сколько карточек положить; LoopSpan: сколько карточек в скобке; LoopRepeats: ×сколько")] [Min(1)] public int count = 1;
    [Tooltip("Затемнять экран вокруг подсветки")] public bool dim = true;
}
