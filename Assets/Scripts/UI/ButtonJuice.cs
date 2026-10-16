using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// «Живая» кнопка: лёгкое покачивание, увеличение при наведении,
/// сжатие при нажатии и пружинистый отскок после клика, пульсация для подсказки.
///
/// Вешать руками не нужно: SpawnScript и MovingManager добавляют компонент сами
/// (кнопкам команд, Play, Rewind, очистке и вкладкам). Можно и вручную — тогда
/// настройки из инспектора сохраняются.
///
/// Анимация считается в Update без твинов, поэтому её нельзя «сломать» быстрыми кликами:
/// масштаб и поворот каждый кадр собираются заново от исходных значений.
/// </summary>
[DisallowMultipleComponent]
public class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>Последняя нажатая «живая» кнопка — откуда вылетает карточка.</summary>
    public static ButtonJuice LastPressed { get; private set; }
    static float _lastPressedTime = -10f;

    [Header("Покачивание")]
    public bool idleWobble;
    [Tooltip("Градусы в каждую сторону")] public float wobbleAngle = 2f;
    public float wobbleSpeed = 1.4f;
    [Tooltip("Плавное «дыхание» масштаба (например, у Play, когда программа готова)")]
    public bool breathe;

    [Header("Нажатие")]
    [Tooltip("Мягкость: меньше — медленнее и плавнее пружина")] [Range(0.3f, 2f)] public float stiffness = 1f;
    public float hoverScale = 1.06f;
    public float pressScale = 0.92f;
    [Tooltip("Сила отскока после клика")] public float clickPunch = 0.12f;
    [Tooltip("Поворот при клике, градусы")] public float clickTilt = 4f;

    Vector3 _baseScale;
    Quaternion _baseRotation;
    bool _hasBase;
    float _phase;
    bool _pressed, _hovered;
    float _scaleMul = 1f;
    float _punch, _punchVel;          // пружина масштаба
    float _tilt, _tiltVel;            // пружина поворота
    float _attentionUntil;
    Selectable _selectable;

    void Awake()
    {
        CaptureBase();
        _phase = Random.value * 10f;
        _selectable = GetComponent<Selectable>();
    }

    /// <summary>Задать «нормальный» масштаб заново (например, после анимации появления из нуля).</summary>
    public void RebaseScale(Vector3 scale)
    {
        _baseScale = scale;
        _baseRotation = transform.localRotation;
        _hasBase = true;
    }

    void CaptureBase()
    {
        // Объект мог быть сжат в ноль для анимации появления — такой масштаб базой не считаем.
        if (_hasBase && _baseScale.sqrMagnitude > 1e-6f) return;
        if (transform.localScale.sqrMagnitude < 1e-6f) return;
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        _hasBase = true;
    }

    void OnDisable()
    {
        if (!_hasBase || _baseScale.sqrMagnitude < 1e-6f) return;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        _pressed = _hovered = false;
        _scaleMul = 1f;
        _punch = _punchVel = _tilt = _tiltVel = 0f;
    }

    bool Interactable => _selectable == null || _selectable.IsInteractable();

    public void OnPointerDown(PointerEventData e)
    {
        if (!Interactable) return;
        _pressed = true;
        PixelSfx.Play(Sfx.Click, 0.7f, Random.Range(0.95f, 1.08f));
        LastPressed = this;
        _lastPressedTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_pressed) return;
        _pressed = false;
        Kick();
    }

    public void OnPointerEnter(PointerEventData e) => _hovered = true;
    public void OnPointerExit(PointerEventData e) { _hovered = false; _pressed = false; }

    /// <summary>Отскок, как после клика (можно вызвать из кода).</summary>
    public void Kick(float strength = 1f)
    {
        _punchVel += clickPunch * 9f * strength;
        _tiltVel += (Random.value < 0.5f ? -1f : 1f) * clickTilt * 9f * strength;
    }

    /// <summary>«Нет» — короткое покачивание из стороны в сторону (например, слоты заполнены).</summary>
    public void Deny()
    {
        PixelSfx.Play(Sfx.Deny, 0.8f);
        _tiltVel += 150f;
    }

    /// <summary>Подсказка: кнопка пульсирует несколько секунд.</summary>
    public void Attention(float seconds = 2.5f)
    {
        _attentionUntil = Time.unscaledTime + seconds;
        Kick(0.6f);
    }

    /// <summary>Кнопка, которую нажали только что (за последнюю секунду).</summary>
    public static ButtonJuice RecentlyPressed()
    {
        if (LastPressed == null || !LastPressed.isActiveAndEnabled) return null;
        return Time.unscaledTime - _lastPressedTime < 1f ? LastPressed : null;
    }

    public static ButtonJuice Ensure(GameObject go, bool wobble = false)
    {
        if (go == null) return null;
        var j = go.GetComponent<ButtonJuice>();
        if (j == null)
        {
            j = go.AddComponent<ButtonJuice>();
            j.idleWobble = wobble;
        }
        return j;
    }

    void Update()
    {
        CaptureBase();
        if (!_hasBase || _baseScale.sqrMagnitude < 1e-6f) return; // ждём нормальный масштаб
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float t = Time.unscaledTime;

        float target = _pressed ? pressScale : (_hovered && Interactable ? hoverScale : 1f);
        _scaleMul = Mathf.Lerp(_scaleMul, target, 1f - Mathf.Exp(-10f * stiffness * dt));

        // Пружины: отскок масштаба и наклона затухают сами.
        _punchVel += (-120f * stiffness * _punch - 9f * Mathf.Sqrt(stiffness) * _punchVel) * dt;
        _punch += _punchVel * dt;
        _tiltVel += (-110f * stiffness * _tilt - 8f * Mathf.Sqrt(stiffness) * _tiltVel) * dt;
        _tilt += _tiltVel * dt;

        float extra = 1f;
        bool attention = t < _attentionUntil;
        if (attention) extra += 0.1f * (0.5f + 0.5f * Mathf.Sin(t * 6f));
        else if (breathe && !_pressed && !_hovered) extra += 0.03f * Mathf.Sin(t * 2f + _phase);

        float angle = _tilt;
        if (attention) angle += 4f * Mathf.Sin(t * 7f);
        else if (idleWobble && !_pressed) angle += wobbleAngle * Mathf.Sin(t * wobbleSpeed + _phase);

        transform.localScale = _baseScale * (_scaleMul * extra * (1f + _punch));
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
