using System.Collections;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Карточка в слоте. ЛКМ — убрать карточку. У While ПКМ (или значок ×N на скобке) — число повторов.
/// Появление/удаление анимированы.
///
/// Исправлено: число повторов теперь хранится на самой карточке, а не в общем ассете Item
/// (раньше все While делили одно число, и оно сохранялось в ассете после Play в редакторе);
/// во время выполнения карточки менять нельзя; переворот не ломается от быстрых кликов.
/// </summary>
public class InventoryItems : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Image image;

    [HideInInspector] public Item item;
    [HideInInspector] public int count = 1;
    [HideInInspector] public Transform parentAfterDrag;

    public Data remove;

    public Sprite frontSprite;
    public Sprite backSprite;
    public TextMeshProUGUI countIteration;

    public const int MaxIterations = 5;

    [Header("Анимация карточки (сек)")]
    public float appearTime = 0.55f;
    public float removeTime = 0.4f;
    public float flipTime = 0.25f;

    public int Iterations { get; private set; } = 1;

    public SlotType Slot => _slot;
    public int Index => _index;
    /// <summary>Карточка улетает (анимация удаления) — слот уже считается свободным.</summary>
    public bool IsRemoving { get; private set; }

    Vector3 _baseScale = Vector3.one;
    Color _baseColor = Color.white;
    bool _baseCaptured;
    Coroutine _tint;

    /// <summary>Условие карточки: цвет клетки, на которой она срабатывает.</summary>
    public TileColor CardColor => _moveManager != null ? _moveManager.GetColor(_slot, _index) : TileColor.None;

    private bool isFront = true;
    private bool flipping;
    private MovingManager _moveManager;
    private SlotType _slot;
    private int _index = -1;

    public void InitialItem(Item newItem, MovingManager moveManager, SlotType slot, int index)
    {
        CaptureBase();
        item = newItem;
        _moveManager = moveManager;
        _slot = slot;
        _index = index;
        Iterations = Mathf.Clamp(newItem.iteration, 1, MaxIterations);

        if (image != null) image.sprite = newItem.image;
        if (countIteration != null)
        {
            countIteration.text = Iterations.ToString();
            countIteration.enabled = false;
        }
    }

    /// <summary>Покрасить карточку (краской из PaintPalette). None — смыть.</summary>
    public bool Paint(TileColor color)
    {
        if (_moveManager == null || !_moveManager.CanEdit || IsRemoving) return false;
        if (!MovingManager.CanHaveColor(item)) { Deny(); return false; }
        if (!_moveManager.SetColor(_slot, _index, color) && CardColor != color) return false;
        ApplyColorTint(color);
        PaintPalette.Decorate(this, color);
        PixelSfx.Play(color == TileColor.None ? Sfx.Remove : Sfx.Pop, 0.8f, 1.1f + (int)color * 0.1f, 0f);
        transform.DOKill(true);
        transform.DOPunchScale(_baseScale * 0.18f, 0.35f, 5, 0.6f);
        if (color != TileColor.None) PixelFX.BurstUI(transform as RectTransform, new[] { ColorTiles.Ui(color), Color.white }, 8, 160f, 10f, 0.5f);
        return true;
    }

    void Deny()
    {
        PixelSfx.Play(Sfx.Deny, 0.6f);
        transform.DOKill(true);
        transform.DOShakeRotation(0.4f, new Vector3(0f, 0f, 12f), 8, 0f);
    }

    /// <summary>Пока в руке краска — карточки хода мягко «зовут» тапнуть по ним.</summary>
    bool _paintPulse;

    void Update()
    {
        bool want = PaintPalette.Active && !IsRemoving && !flipping && _moveManager != null && _moveManager.CanEdit
                    && MovingManager.CanHaveColor(item);
        if (!want)
        {
            if (_paintPulse) { _paintPulse = false; if (!DOTween.IsTweening(transform)) transform.localScale = _baseScale; }
            return;
        }
        _paintPulse = true;
        float k = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 8f + _index);
        if (!DOTween.IsTweening(transform)) transform.localScale = _baseScale * k;
    }

    void ApplyColorTint(TileColor c)
    {
        CaptureBase();
        _baseColor = c == TileColor.None ? Color.white : Color.Lerp(Color.white, ColorTiles.Ui(c), 0.4f);
        if (image != null && _tint == null) image.color = _baseColor;
    }

    /// <summary>Во время выполнения: условие не выполнено, карточка пропущена — коротко «гаснет».</summary>
    public void Skipped()
    {
        if (IsRemoving) return;
        transform.DOKill(true);
        transform.DOPunchScale(-_baseScale * 0.12f, 0.25f, 2, 0.5f);
        Flash(new Color(0.45f, 0.45f, 0.52f, 1f), 1, 0.12f);
    }

    /// <summary>Старый вариант: номер слота берётся из имени родителя (MovingSlot12 → F1, слот 2).</summary>
    public void InitialItem(Item newItem, MovingManager moveManager)
    {
        int global = ParseSlotNumber();
        InitialItem(newItem, moveManager, MovingManager.GetSlotByIndex(global), MovingManager.GetLocalIndex(global));
    }

    int ParseSlotNumber()
    {
        var digits = new string(transform.parent != null ? transform.parent.name.Where(char.IsDigit).ToArray() : new char[0]);
        return int.TryParse(digits, out var n) ? n : 0;
    }

    public void ModifyItem(int number)
    {
        Iterations = Mathf.Clamp(number, 1, MaxIterations);
        if (_moveManager != null) _moveManager.SetIteration(_slot, _index, Iterations);
        if (countIteration != null) countIteration.text = Iterations.ToString();
    }

    void CaptureBase()
    {
        if (_baseCaptured) return;
        _baseScale = transform.localScale;
        if (image != null) _baseColor = image.color;
        _baseCaptured = true;
    }

    // ───────────── анимации ─────────────

    /// <summary>Появление: вылетает из нажатой кнопки и «плюхается» в слот с отскоком.</summary>
    public void PlayAppear(Vector3? fromWorld)
    {
        CaptureBase();
        transform.DOKill();
        PixelSfx.Play(Sfx.Pop, 0.8f, 1f + Mathf.Max(0, _index) * 0.05f);
        Vector3 home = transform.localPosition;

        if (fromWorld.HasValue)
        {
            var from = fromWorld.Value;
            transform.position = new Vector3(from.x, from.y, transform.position.z);
            transform.DOLocalMove(home, appearTime * 0.85f).SetEase(Ease.OutQuad);
        }
        transform.localScale = _baseScale * 0.5f;
        transform.DOScale(_baseScale, appearTime).SetEase(Ease.OutBack, 1.2f);
        transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
        transform.DOLocalRotate(Vector3.zero, appearTime).SetEase(Ease.OutCubic);
    }

    /// <summary>Удаление: сжимается с поворотом и исчезает. Слот освобождается сразу.</summary>
    public void PlayRemove(float delay = 0f)
    {
        if (IsRemoving) return;
        IsRemoving = true;
        PixelSfx.Play(Sfx.Remove, 0.7f, Random.Range(0.95f, 1.1f), 0.04f);
        CaptureBase();

        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        transform.DOKill();
        float dir = Random.value < 0.5f ? -1f : 1f;
        transform.DOLocalRotate(new Vector3(0f, 0f, 20f * dir), removeTime).SetDelay(delay).SetEase(Ease.InQuad);
        // InBack сначала чуть раздувает карточку, потом плавно схлопывает.
        transform.DOScale(Vector3.zero, removeTime).SetDelay(delay).SetEase(Ease.InBack, 1.2f)
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
    }

    /// <summary>Подсказка «эта карточка лишняя/неверная»: трясётся и мигает красным.</summary>
    public void HintWrong()
    {
        if (IsRemoving) return;
        PixelSfx.Play(Sfx.Deny, 0.7f);
        transform.DOKill(true);
        transform.DOShakeRotation(0.8f, new Vector3(0f, 0f, 16f), 8, 0f);
        Flash(new Color(1f, 0.45f, 0.45f, 1f), 3);
    }

    /// <summary>Подсказка «у этой карточки не то число повторов»: пульсирует жёлтым.</summary>
    public void HintIterations()
    {
        if (IsRemoving) return;
        transform.DOKill(true);
        transform.DOPunchScale(_baseScale * 0.2f, 0.5f, 5, 0.6f);
        Flash(new Color(1f, 0.9f, 0.4f, 1f), 3);
    }

    /// <summary>Эта карточка сейчас выполняется — мягко подпрыгивает.</summary>
    public void PulseStep()
    {
        if (IsRemoving || flipping) return;
        transform.DOKill(true);
        transform.DOPunchScale(_baseScale * 0.12f, 0.3f, 3, 0.5f);
        Flash(new Color(0.75f, 1f, 0.8f, 1f), 1, 0.12f);
    }

    /// <summary>Из-за этой карточки куб чуть не упал — мигает оранжевым и вздрагивает.</summary>
    public void Danger()
    {
        if (IsRemoving) return;
        transform.DOKill(true);
        transform.DOShakeRotation(0.6f, new Vector3(0f, 0f, 14f), 8, 0f);
        Flash(new Color(1f, 0.6f, 0.25f, 1f), 2, 0.2f);
    }

    void Flash(Color color, int times, float half = 0.18f)
    {
        if (image == null || !isActiveAndEnabled) return;
        if (_tint != null) StopCoroutine(_tint);
        _tint = StartCoroutine(FlashRoutine(color, times, half));
    }

    IEnumerator FlashRoutine(Color color, int times, float half)
    {
        var normal = _baseColor;
        for (int i = 0; i < times; i++)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / half) { image.color = Color.Lerp(normal, color, t); yield return null; }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / half) { image.color = Color.Lerp(color, normal, t); yield return null; }
        }
        image.color = normal;
        _tint = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_moveManager == null || !_moveManager.CanEdit || flipping || IsRemoving) return;

        if (eventData.button == PointerEventData.InputButton.Left && PaintPalette.Active)
        {
            // В руке краска — красим, а не удаляем.
            if (Paint(PaintPalette.Selected.Value)) PaintPalette.Used();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isFront)
            {
                if (_moveManager.RemoveCommandAt(_slot, _index))
                    PlayRemove();
            }
            else
            {
                ModifyItem(Iterations < MaxIterations ? Iterations + 1 : 1);
                transform.DOKill(true);
                transform.DOPunchScale(_baseScale * 0.1f, 0.35f, 4, 0.6f);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Right && item != null && item.type == ItemType.While)
        {
            // Повторы цикла теперь видны на значке скобки (×2…×6); ПКМ — тоже листает их.
            CycleIterations();
        }
    }

    /// <summary>While: следующее число повторов (по кругу).</summary>
    public void CycleIterations()
    {
        if (_moveManager == null || !_moveManager.CanEdit || IsRemoving) return;
        ModifyItem(Iterations < MaxIterations ? Iterations + 1 : 1);
        PixelSfx.Play(Sfx.Blip, 0.8f, 1f + Iterations * 0.08f, 0f);
        transform.DOKill(true);
        transform.DOPunchScale(_baseScale * 0.1f, 0.35f, 4, 0.6f);
    }

    private void FlipCard()
    {
        flipping = true;
        transform.DOKill(true);
        transform.localScale = _baseScale;
        transform.DOScaleX(0, flipTime).SetEase(Ease.InQuad).OnComplete(() =>
        {
            isFront = !isFront;
            if (image != null) image.sprite = isFront ? (item != null && item.image != null ? item.image : frontSprite) : backSprite;
            if (countIteration != null) countIteration.enabled = !isFront;
            transform.DOScaleX(_baseScale.x, flipTime).SetEase(Ease.OutQuad).OnComplete(() => flipping = false);
        });
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
