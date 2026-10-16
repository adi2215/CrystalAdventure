using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Диалоги перед уровнем. Пока диалог открыт, Play не запускается (MovingManager проверяет data.DialogManager).
///
/// Исправлено: флаг data.DialogManager живёт в ассете Data и оставался true, если остановить игру
/// посреди диалога в редакторе — теперь сбрасывается при старте; пустой диалог / неверный actorId
/// больше не роняют игру; быстрые клики не наслаивают анимации окна.
/// </summary>
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

    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        if (data != null) data.DialogManager = false;
        if (backGroundBox != null) backGroundBox.localScale = Vector3.zero;
    }

    void OnEnable()
    {
        inputActions.ActionManager.Enable();
        inputActions.ActionManager.SkipDialog.performed += OnNext;
    }

    void OnDisable()
    {
        inputActions.ActionManager.SkipDialog.performed -= OnNext;
        inputActions.ActionManager.Disable();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void OnNext(InputAction.CallbackContext context)
    {
        if (isActive) NextMessage();
    }

    public void OpenDialogue(DialogTrigger dialog)
    {
        if (dialog == null || dialog.messages == null || dialog.messages.Length == 0) return;

        currentMessages = dialog.messages;
        currentActors = dialog.actors ?? new Actor[0];
        activeMessage = 0;
        SetActive(true);
        DisplayMessage();

        if (backGroundBox != null)
        {
            LeanTween.cancel(backGroundBox.gameObject);
            backGroundBox.LeanScale(Vector3.one, 0.5f).setEaseInOutExpo();
        }
    }

    void DisplayMessage()
    {
        var message = currentMessages[activeMessage];
        if (messageText != null) messageText.text = message.message;

        if (message.actorId >= 0 && message.actorId < currentActors.Length)
        {
            var actor = currentActors[message.actorId];
            if (actorName != null) actorName.text = actor.name;
            if (actorImage != null) actorImage.sprite = actor.sprite;
        }
    }

    public void NextMessage()
    {
        if (!isActive || currentMessages == null) return;

        activeMessage++;
        if (activeMessage < currentMessages.Length)
        {
            DisplayMessage();
            return;
        }

        SetActive(false);
        if (backGroundBox != null)
        {
            LeanTween.cancel(backGroundBox.gameObject);
            backGroundBox.LeanScale(Vector3.zero, 0.5f).setEaseInOutExpo();
        }
    }

    void SetActive(bool value)
    {
        isActive = value;
        if (data != null) data.DialogManager = value;
    }
}
