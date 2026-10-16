using System.Collections;
using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Визуал одной сущности (герой, враг, босс, кристалл). Ничего не решает —
    /// только проигрывает то, что уже посчитало ядро. Все длительности делятся на speed.
    /// Если на объекте есть Animator, дергаются триггеры Move / Attack / Hurt / Die / Shield.
    /// </summary>
    public class EntityView : MonoBehaviour
    {
        public SpriteRenderer body;
        public Animator animator;
        [Tooltip("Спрайт смотрит вправо по умолчанию? Тогда для West/North он отражается")]
        public bool flipForWest = true;
        public GameObject shieldFx;

        [Header("Длительности при x1")]
        public float moveTime = 0.28f;
        public float hopHeight = 0.12f;
        public float attackTime = 0.22f;
        public float hurtTime = 0.25f;
        public float dieTime = 0.35f;

        [HideInInspector] public int entityId;
        [HideInInspector] public GridMapper mapper;

        Vector3 _baseScale = Vector3.one;
        Color _baseColor = Color.white;

        void Awake()
        {
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _baseScale = transform.localScale;
            if (body != null) _baseColor = body.color;
            if (shieldFx != null) shieldFx.SetActive(false);
        }

        public void Init(int id, GridMapper gridMapper, Cell cell, Dir facing)
        {
            entityId = id;
            mapper = gridMapper;
            Snap(cell);
            Face(facing);
        }

        public void Snap(Cell cell)
        {
            transform.position = mapper.CellToWorld(cell);
            if (body != null) body.sortingOrder = mapper.SortingOrderFor(cell);
        }

        public void Face(Dir d)
        {
            if (body != null && flipForWest) body.flipX = d == Dir.West || d == Dir.North;
        }

        void Trigger(string name)
        {
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(name);
        }

        // ----------------------------------------------------------------- анимации

        public IEnumerator MoveTo(Cell to, Dir dir, float speed, float hop = 1f, float timeScale = 1f)
        {
            Face(dir);
            Trigger("Move");
            var from = transform.position;
            var target = mapper.CellToWorld(to);
            float dur = moveTime * timeScale / speed;
            // Рисуем поверх на время шага, если идём «вперёд к камере».
            if (body != null) body.sortingOrder = Mathf.Max(body.sortingOrder, mapper.SortingOrderFor(to));
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                var p = Vector3.Lerp(from, target, k);
                p.y += Mathf.Sin(k * Mathf.PI) * hopHeight * hop;
                transform.position = p;
                yield return null;
            }
            Snap(to);
            yield return Squash(0.12f / speed);
        }

        public IEnumerator Bump(Dir dir, float speed)
        {
            Face(dir);
            var start = transform.position;
            var push = start + GridMapper.DirToWorld(dir) * 0.25f;
            float dur = moveTime * 0.5f / speed;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                transform.position = Vector3.Lerp(start, push, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            transform.position = start;
        }

        public IEnumerator Attack(Dir dir, float speed, bool super)
        {
            Face(dir);
            Trigger(super ? "SuperAttack" : "Attack");
            var start = transform.position;
            var lunge = start + GridMapper.DirToWorld(dir) * (super ? 0.45f : 0.3f);
            float dur = attackTime * (super ? 1.6f : 1f) / speed;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                transform.position = Vector3.Lerp(start, lunge, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            transform.position = start;
        }

        public IEnumerator Hurt(float speed)
        {
            Trigger("Hurt");
            float dur = hurtTime / speed;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                if (body != null) body.color = Color.Lerp(Color.white * 3f, _baseColor, t);
                transform.localScale = _baseScale * (1f + 0.15f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (body != null) body.color = _baseColor;
            transform.localScale = _baseScale;
        }

        public IEnumerator Blocked(float speed)
        {
            float dur = hurtTime / speed;
            var blue = new Color(0.6f, 0.85f, 1f, _baseColor.a);
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                if (body != null) body.color = Color.Lerp(blue, _baseColor, t);
                yield return null;
            }
            if (body != null) body.color = _baseColor;
        }

        public void SetShield(bool on)
        {
            if (shieldFx != null) shieldFx.SetActive(on);
            if (on) Trigger("Shield");
        }

        public IEnumerator Die(float speed, bool fall)
        {
            Trigger("Die");
            float dur = dieTime / speed;
            var start = transform.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                transform.localScale = _baseScale * (1f - t);
                if (fall) transform.position = start + Vector3.down * (t * t * 1.5f);
                if (body != null) { var c = _baseColor; c.a = 1f - t; body.color = c; }
                yield return null;
            }
            gameObject.SetActive(false);
        }

        public IEnumerator Teleport(Cell to, float speed)
        {
            float half = moveTime * 0.5f / speed;
            for (float t = 0; t < 1f; t += Time.deltaTime / half) { transform.localScale = _baseScale * (1f - t); yield return null; }
            Snap(to);
            for (float t = 0; t < 1f; t += Time.deltaTime / half) { transform.localScale = _baseScale * t; yield return null; }
            transform.localScale = _baseScale;
        }

        public IEnumerator PopIn(float speed)
        {
            float dur = moveTime / speed;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                transform.localScale = _baseScale * Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        IEnumerator Squash(float dur)
        {
            if (dur <= 0f) yield break;
            var squashed = new Vector3(_baseScale.x * 1.12f, _baseScale.y * 0.88f, _baseScale.z);
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                transform.localScale = Vector3.Lerp(squashed, _baseScale, t);
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        /// <summary>Сбросить всё временное (для режима «мгновенно»).</summary>
        public void ResetVisual()
        {
            transform.localScale = _baseScale;
            if (body != null) body.color = _baseColor;
        }
    }
}
