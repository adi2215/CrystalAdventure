using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Программа с клавиатуры и геймпада (Steam, Steam Deck) — мышь и тач работают как раньше.
///
/// Клавиатура:
///   W / ↑ — Вперёд, A / ← — Влево, D / → — Вправо, S / ↓ — Назад (карточка в открытую панель);
///   F1 / F2 — карточка функции, L — цикл While;
///   Backspace — убрать последнюю карточку (или выпустить краску из руки);
///   Tab / 1 2 3 — вкладка Main / F1 / F2;
///   Enter / Space — Play, во время и после прогона — Rewind;  H — подсказка.
/// Геймпад:
///   крестовина — ходы; A — Play / Rewind; B — убрать последнюю; Y — F1, X — F2, RT — While;
///   LB / RB — соседняя вкладка; Select (View) — подсказка.
///
/// Карточки добавляются нажатием настоящей кнопки команды (её onClick) — работают звук, анимация,
/// туториал и проверка «кнопка есть на этом уровне». Не срабатывает, пока идёт сюжет, открыты карта
/// уровней / настройки или показано окно победы. Создаётся сам на сцене с MovingManager.
/// Клавиатура — через старый Input (в проекте включены оба способа ввода), геймпад — через Input System.
/// </summary>
public class ProgramKeys : MonoBehaviour
{
    MovingManager _program;
    PanelController _panels;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

    static void Ensure()
    {
        if (FindObjectOfType<ProgramKeys>() != null) return;
        var program = FindObjectOfType<MovingManager>();
        if (program == null) return;
        program.gameObject.AddComponent<ProgramKeys>();
    }

    void Start()
    {
        _program = GetComponent<MovingManager>();
        _panels = FindObjectOfType<PanelController>(true);
    }

    bool Blocked()
    {
        if (_program == null) return true;
        if (StoryDirector.IsBlocking) return true;
        var map = FindObjectOfType<LevelMapMenu>();
        if (map != null && map.IsOpen) return true;
        var settings = FindObjectOfType<SettingsPanel>();
        if (settings != null && settings.IsOpen) return true;
        if (ActiveBlockManager.Instance != null && ActiveBlockManager.Instance.LevelComplete) return true;
        if (Levelloader.IsLoading) return true;
        return false;
    }

    void Update()
    {
        if (Blocked()) return;

        // Кнопка, нажатая мышью, остаётся «выбранной» в EventSystem: Space / Enter / крестовина тогда
        // ещё раз нажали бы её (лишняя карточка). Снимаем выбор — клавиши работают только через этот скрипт.
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<Button>() != null)
            es.SetSelectedGameObject(null);

        // ── ходы ──
        if (Down(KeyCode.W, KeyCode.UpArrow) || Pad(PadKey.Up)) AddCard(ItemType.Forward);
        if (Down(KeyCode.A, KeyCode.LeftArrow) || Pad(PadKey.Left)) AddCard(ItemType.Left);
        if (Down(KeyCode.D, KeyCode.RightArrow) || Pad(PadKey.Right)) AddCard(ItemType.Right);
        if (Down(KeyCode.S, KeyCode.DownArrow) || Pad(PadKey.Down)) AddCard(ItemType.Bottom);

        // ── функции и цикл ──
        if (Input.GetKeyDown(KeyCode.F1) || Pad(PadKey.North)) AddCard(ItemType.FunctionOne);
        if (Input.GetKeyDown(KeyCode.F2) || Pad(PadKey.West)) AddCard(ItemType.FunctionTwo);
        if (Input.GetKeyDown(KeyCode.L) || Pad(PadKey.RightTrigger)) AddCard(ItemType.While);

        // ── правка ──
        if (Input.GetKeyDown(KeyCode.Backspace) || Pad(PadKey.East)) RemoveLast();

        // ── вкладки ──
        if (Input.GetKeyDown(KeyCode.Alpha1)) OpenTab(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) OpenTab(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) OpenTab(2);
        if (Input.GetKeyDown(KeyCode.Tab) || Pad(PadKey.RightShoulder)) StepTab(+1);
        if (Pad(PadKey.LeftShoulder)) StepTab(-1);

        // ── запуск ──
        if (Down(KeyCode.Return, KeyCode.Space) || Input.GetKeyDown(KeyCode.KeypadEnter) || Pad(PadKey.South)) PlayOrRewind();

        // ── подсказка ──
        if (Input.GetKeyDown(KeyCode.H) || Pad(PadKey.Select))
        {
            var hint = FindObjectOfType<HintSystem>();
            if (hint != null) hint.ShowHint();
        }
    }

    static bool Down(KeyCode a, KeyCode b) => Input.GetKeyDown(a) || Input.GetKeyDown(b);

    // ───────────── действия ─────────────

    int CurrentPanel()
    {
        if (_panels == null || _panels.panels == null) return 0;
        for (int i = 0; i < _panels.panels.Length; i++)
            if (_panels.panels[i] != null && _panels.panels[i].activeInHierarchy) return i;
        return 0;
    }

    SpawnScript SpawnFor(int panel)
    {
        if (_panels == null || _panels.spawnMethods == null || panel < 0 || panel >= _panels.spawnMethods.Length) return null;
        return _panels.spawnMethods[panel];
    }

    /// <summary>Нажать кнопку карточки в открытой панели (если такая карточка есть на этом уровне).</summary>
    void AddCard(ItemType type)
    {
        if (!_program.CanEdit) return;
        var spawn = SpawnFor(CurrentPanel());
        var go = spawn != null ? spawn.FindButton(type) : null;
        if (go == null || !go.activeInHierarchy) return;
        var button = go.GetComponent<Button>();
        if (button == null || !button.interactable) return;
        var juice = go.GetComponent<ButtonJuice>();
        if (juice != null) juice.Kick();
        button.onClick.Invoke();
    }

    /// <summary>Убрать последнюю карточку открытой панели; если в руке краска — сначала выпустить её.</summary>
    void RemoveLast()
    {
        if (!_program.CanEdit) return;
        if (PaintPalette.Active) { PaintPalette.Select(null); return; }
        var spawn = SpawnFor(CurrentPanel());
        if (spawn == null) return;
        var slot = spawn.type;
        var last = FindObjectsOfType<InventoryItems>()
            .Where(c => c.Slot == slot && c.Index >= 0 && !c.IsRemoving)
            .OrderByDescending(c => c.Index)
            .FirstOrDefault();
        if (last != null && _program.RemoveCommandAt(slot, last.Index)) last.PlayRemove();
    }

    void OpenTab(int index)
    {
        if (_panels == null || _panels.buttonPanels == null || index < 0 || index >= _panels.buttonPanels.Length) return;
        var tab = _panels.buttonPanels[index];
        if (tab == null || !tab.activeInHierarchy) return;
        var button = tab.GetComponent<Button>();
        if (button != null && button.interactable) button.onClick.Invoke();
    }

    void StepTab(int dir)
    {
        if (_panels == null || _panels.buttonPanels == null) return;
        int n = _panels.buttonPanels.Length, cur = CurrentPanel();
        for (int k = 1; k <= n; k++)
        {
            int i = ((cur + dir * k) % n + n) % n;
            var tab = _panels.buttonPanels[i];
            if (tab != null && tab.activeInHierarchy) { OpenTab(i); return; }
        }
    }

    void PlayOrRewind()
    {
        if (_program.CanEdit) _program.StartPlay();
        else _program.ReversePlay();
    }

    // ───────────── геймпад ─────────────

    enum PadKey { Up, Down, Left, Right, South, East, West, North, LeftShoulder, RightShoulder, RightTrigger, Select }

    static bool Pad(PadKey key)
    {
#if ENABLE_INPUT_SYSTEM
        var g = Gamepad.current;
        if (g == null) return false;
        switch (key)
        {
            case PadKey.Up: return g.dpad.up.wasPressedThisFrame;
            case PadKey.Down: return g.dpad.down.wasPressedThisFrame;
            case PadKey.Left: return g.dpad.left.wasPressedThisFrame;
            case PadKey.Right: return g.dpad.right.wasPressedThisFrame;
            case PadKey.South: return g.buttonSouth.wasPressedThisFrame;
            case PadKey.East: return g.buttonEast.wasPressedThisFrame;
            case PadKey.West: return g.buttonWest.wasPressedThisFrame;
            case PadKey.North: return g.buttonNorth.wasPressedThisFrame;
            case PadKey.LeftShoulder: return g.leftShoulder.wasPressedThisFrame;
            case PadKey.RightShoulder: return g.rightShoulder.wasPressedThisFrame;
            case PadKey.RightTrigger: return g.rightTrigger.wasPressedThisFrame;
            case PadKey.Select: return g.selectButton.wasPressedThisFrame;
        }
#endif
        return false;
    }
}
