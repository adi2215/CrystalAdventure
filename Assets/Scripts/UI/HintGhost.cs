using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подсказка-«призрак»: полупрозрачная копия куба проходит начало решения уровня,
/// а над ней всплывают карточки, которые для этого нужны. Примерно к 40% пути призрак
/// начинает таять и к половине пути исчезает — дальше игрок думает сам.
///
/// Создаётся из кода (HintSystem), руками ничего настраивать не нужно.
/// </summary>
public class HintGhost : MonoBehaviour
{
    public struct Step
    {
        public ItemType Move;     // куда шагнуть
        public ItemType? Card;    // карточка Main, с которой начинается этот шаг (показать крупно)
        public TileColor Color;   // условие карточки (None — всегда)
    }

    public sealed class Settings
    {
        public float ShowFraction = 0.5f;   // какую часть решения показать
        public float FadeStart = 0.4f;      // с какой части пути начинать таять
        public float StepTime = 0.55f;      // секунд на шаг (медленнее настоящего куба)
        public float Alpha = 0.55f;
        public Color Tint = new Color(0.65f, 0.9f, 1f, 1f);
        public float IconSize = 0.22f;      // ширина всплывающей карточки в единицах мира
    }

    static HintGhost _active;

    CharacterCode _hero;
    List<Step> _steps;
    int _shownSteps;
    Settings _settings;
    Func<ItemType, Sprite> _icon;
    HashSet<Vector2Int> _crystals;

    SpriteRenderer[] _parts;
    int[] _orderOffsets;
    SpriteRenderer _main;
    Sprite _frontSprite;
    float _alpha = 1f;
    readonly List<GameObject> _icons = new List<GameObject>();

    public static bool IsPlaying => _active != null;

    public static void StopActive()
    {
        if (_active != null) _active.Finish(immediate: true);
    }

    /// <summary>Запустить призрака по решению уровня. false — показать нечего.</summary>
    public static bool Play(CharacterCode hero, LevelSt level, Func<ItemType, Sprite> icon, Settings settings)
    {
        StopActive();
        if (hero == null || level == null || !level.HasSolution) return false;
        var map = MapManager.Instance != null ? MapManager.Instance.map : null;
        var start = hero.StartTile != null ? hero.StartTile : hero.activeTile;
        if (map == null || start == null) return false;

        var crystals = new HashSet<Vector2Int>();
        if (level.tiles != null)
            foreach (var t in level.tiles)
                if (t != null && t.superTileType == SuperBlocks.Crystal) crystals.Add(new Vector2Int(t.position.x, t.position.y));

        var steps = ApplyConditions(Expand(level), start, map);
        int untilWin = StepsUntilWin(steps, start, map, crystals);
        if (untilWin <= 0) return false;
        steps.RemoveRange(untilWin, steps.Count - untilWin);

        var go = new GameObject("HintGhost");
        var ghost = go.AddComponent<HintGhost>();
        ghost._hero = hero;
        ghost._steps = steps;
        ghost._settings = settings ?? new Settings();
        ghost._shownSteps = Mathf.Clamp(Mathf.CeilToInt(steps.Count * ghost._settings.ShowFraction), 1, steps.Count);
        ghost._icon = icon;
        ghost._crystals = crystals;
        ghost.BuildBody(start);
        _active = ghost;
        PixelSfx.Play(Sfx.Ghost, 0.8f);
        ghost.StartCoroutine(ghost.Run(start, map));
        return true;
    }

    // ───────────── программа → шаги ─────────────

    /// <summary>Те же правила, что MovingManager.BuildQueue: While повторяет карточки под своей скобкой.</summary>
    public static List<Step> Expand(LevelSt level)
    {
        var steps = new List<Step>();
        var main = level.GetSolution(SlotType.Main);
        var startOf = new List<int>();
        for (int ci = 0; ci < main.Count; ci++)
        {
            var card = main[ci];
            int before = steps.Count;
            startOf.Add(before);
            switch (card.type)
            {
                case ItemType.FunctionOne:
                    steps.AddRange(ExpandFunction(level.GetSolution(SlotType.FunctionOne)));
                    break;
                case ItemType.FunctionTwo:
                    steps.AddRange(ExpandFunction(level.GetSolution(SlotType.FunctionTwo)));
                    break;
                case ItemType.While:
                {
                    int from = startOf[ci - SolutionCard.ResolveSpan(main, ci)];
                    var body = steps.GetRange(from, steps.Count - from).ConvertAll(s => new Step { Move = s.Move, Color = s.Color });
                    for (int r = 0; r < Mathf.Max(1, card.iterations) && steps.Count < 300; r++) steps.AddRange(body);
                    break;
                }
                default:
                    if (WayTiles.Direction(card.type) != Vector3Int.zero) steps.Add(new Step { Move = card.type, Color = card.color });
                    break;
            }
            if (steps.Count > before)
            {
                var first = steps[before];
                first.Card = card.type;
                steps[before] = first;
            }
            if (steps.Count >= 300) break;
        }
        return steps;
    }

    public static List<Step> ExpandFunction(List<SolutionCard> cards)
    {
        var local = new List<Step>();
        var startOf = new List<int>();
        for (int ci = 0; ci < cards.Count; ci++)
        {
            var c = cards[ci];
            startOf.Add(local.Count);
            if (c.type == ItemType.While)
            {
                int from = startOf[ci - SolutionCard.ResolveSpan(cards, ci)];
                var body = local.GetRange(from, local.Count - from);
                for (int r = 0; r < Mathf.Max(1, c.iterations) && local.Count < 300; r++) local.AddRange(body);
            }
            else if (WayTiles.Direction(c.type) != Vector3Int.zero) local.Add(new Step { Move = c.type, Color = c.color });
        }
        return local;
    }

    /// <summary>Условия (цвет клетки): убираем шаги, которые на своём месте были бы пропущены.</summary>
    public static List<Step> ApplyConditions(List<Step> steps, OverlayTile start, Dictionary<Vector3Int, OverlayTile> map)
    {
        bool any = false;
        foreach (var s in steps) if (s.Color != TileColor.None) { any = true; break; }
        if (!any) return steps;

        var result = new List<Step>(steps.Count);
        var tile = start;
        ItemType? pendingCard = null;
        foreach (var s in steps)
        {
            if (!ColorTiles.Passes(s.Color, tile))
            {
                if (s.Card.HasValue) pendingCard = s.Card;   // подпись карточки переносим на следующий шаг
                continue;
            }
            var step = s;
            if (!step.Card.HasValue && pendingCard.HasValue) step.Card = pendingCard;
            pendingCard = null;
            result.Add(step);
            var r = WayTiles.Resolve(tile, s.Move, map);
            if (r.target != null) tile = r.target;
        }
        return result;
    }

    static int StepsUntilWin(List<Step> steps, OverlayTile start, Dictionary<Vector3Int, OverlayTile> map, HashSet<Vector2Int> crystals)
    {
        var left = new HashSet<Vector2Int>(crystals);
        var tile = start;
        for (int i = 0; i < steps.Count; i++)
        {
            var s = WayTiles.Resolve(tile, steps[i].Move, map);
            if (s.target != null)
            {
                tile = s.target;
                left.Remove(new Vector2Int(tile.gridLocation.x, tile.gridLocation.y));
                if (left.Count == 0) return i + 1;
            }
        }
        return steps.Count; // решение без победы (не должно быть) — покажем как есть
    }

    // ───────────── внешний вид ─────────────

    void BuildBody(OverlayTile start)
    {
        var heroRenderers = _hero.GetComponentsInChildren<SpriteRenderer>();
        var main = _hero.MainRenderer;
        int baseOrder = main != null ? main.sortingOrder : 0;
        _parts = new SpriteRenderer[heroRenderers.Length];
        _orderOffsets = new int[heroRenderers.Length];

        transform.position = CharacterCode.TopOf(start);
        for (int i = 0; i < heroRenderers.Length; i++)
        {
            var src = heroRenderers[i];
            var part = new GameObject("Part").AddComponent<SpriteRenderer>();
            part.transform.SetParent(transform, false);
            part.transform.localPosition = src.transform.position - _hero.transform.position;
            part.transform.localScale = src.transform.lossyScale;
            part.sprite = src.sprite;
            part.sharedMaterial = src.sharedMaterial;
            part.sortingLayerID = src.sortingLayerID;
            _orderOffsets[i] = src.sortingOrder - baseOrder;
            _parts[i] = part;
            if (src == main) _main = part;
        }
        if (_main == null && _parts.Length > 0) _main = _parts[0];
        _frontSprite = _main != null ? _main.sprite : null;
        SetOrder(start);
        SetAlpha(0f);
    }

    void SetOrder(OverlayTile tile)
    {
        var tr = tile != null ? tile.GetComponent<SpriteRenderer>() : null;
        int order = (tr != null ? tr.sortingOrder : 0) + 1;
        for (int i = 0; i < _parts.Length; i++) _parts[i].sortingOrder = order + _orderOffsets[i];
    }

    void SetAlpha(float a)
    {
        _alpha = a;
        var c = _settings.Tint;
        c.a = _settings.Alpha * a;
        foreach (var p in _parts) if (p != null) p.color = c;
    }

    /// <summary>s — пройдено шагов (дробно). Тает с FadeStart/ShowFraction показанной части до конца.</summary>
    float AlphaAt(float s)
    {
        float fadeFrom = _shownSteps * Mathf.Clamp01(_settings.FadeStart / Mathf.Max(0.01f, _settings.ShowFraction));
        if (s <= fadeFrom) return 1f;
        return Mathf.Clamp01(1f - (s - fadeFrom) / Mathf.Max(0.01f, _shownSteps - fadeFrom));
    }

    // ───────────── движение ─────────────

    IEnumerator Run(OverlayTile start, Dictionary<Vector3Int, OverlayTile> map)
    {
        // Появление на старте.
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f) { SetAlpha(t); yield return null; }
        SetAlpha(1f);
        yield return new WaitForSeconds(0.15f);

        var tile = start;
        for (int i = 0; i < _shownSteps; i++)
        {
            var step = _steps[i];
            CharacterCode.ApplyFacing(_parts, _main, _frontSprite, _hero.backSprite, step.Move);
            ShowCard(step);

            var res = WayTiles.Resolve(tile, step.Move, map);
            Vector3 from = transform.position;
            if (res.target == null)
            {
                // упёрся — лёгкий толчок на месте
                var d = WayTiles.Direction(step.Move);
                var offset = new Vector3((d.x - d.y) * 0.5f, (d.x + d.y) * 0.25f, 0f) * 0.12f;
                for (float t = 0f; t < 1f; t += Time.deltaTime / (_settings.StepTime * 0.6f))
                {
                    transform.position = from + offset * Mathf.Sin(t * Mathf.PI);
                    SetAlpha(AlphaAt(i + t));
                    yield return null;
                }
                transform.position = from;
            }
            else
            {
                Vector3 to = CharacterCode.TopOf(res.target);
                bool jump = res.fromMushroom || res.onMushroom;
                float dur = _settings.StepTime * (jump ? 1.3f : 1f);
                if (to.y > from.y) SetOrder(res.target); // уходим «назад» — порядок сразу
                for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t);
                    float h = jump ? 4f * 0.3f * k * (1f - k) : 0f;
                    transform.position = Vector3.Lerp(from, to, k) + Vector3.up * h;
                    SetAlpha(AlphaAt(i + t));
                    yield return null;
                }
                transform.position = to;
                SetOrder(res.target);
                tile = res.target;
            }
            if (_alpha <= 0.01f) break;
            yield return new WaitForSeconds(0.08f);
        }

        Finish(immediate: false);
    }

    void ShowCard(Step step)
    {
        if (_icon == null) return;
        bool big = step.Card.HasValue;
        var sprite = _icon(big ? step.Card.Value : step.Move);
        if (sprite == null) return;

        var go = new GameObject("HintCard");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = _main != null ? _main.sortingLayerID : 0;
        sr.sortingOrder = (_main != null ? _main.sortingOrder : 0) + 50;
        float width = sprite.bounds.size.x > 0.0001f ? sprite.bounds.size.x : 1f;
        float scale = (big ? _settings.IconSize : _settings.IconSize * 0.6f) / width;
        // Карточка всплывает чуть сбоку от призрака, мелкие — чуть ниже крупных.
        go.transform.position = transform.position + new Vector3(big ? 0.22f : 0.18f, big ? 0.3f : 0.18f, 0f);
        _icons.Add(go);
        var floating = go.AddComponent<FloatingHintCard>();
        floating.Init(sr, scale, Mathf.Max(0.35f, _alpha));
    }

    void Finish(bool immediate)
    {
        if (_active == this) _active = null;
        StopAllCoroutines();
        if (immediate)
        {
            foreach (var icon in _icons) if (icon != null) Destroy(icon);
            _icons.Clear();
            Destroy(gameObject);
        }
        else StartCoroutine(FadeOutAndDie()); // карточки доплывут и исчезнут сами
    }

    IEnumerator FadeOutAndDie()
    {
        float a0 = _alpha;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f) { SetAlpha(a0 * (1f - t)); yield return null; }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_active == this) _active = null;
    }
}
