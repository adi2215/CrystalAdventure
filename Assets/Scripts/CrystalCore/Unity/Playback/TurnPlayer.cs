using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Проигрывает уже посчитанный прогон (SimulationResult).
    /// Фазы хода идут по очереди, внутри фазы все сущности анимируются ОДНОВРЕМЕННО.
    /// Скорость: x1 / x2 / x4 или «мгновенно» — меняется прямо во время проигрывания.
    /// </summary>
    public class TurnPlayer : MonoBehaviour
    {
        [Range(0.25f, 8f)] public float speed = 1f;
        public bool instant;
        [Tooltip("Пауза между ходами при x1")] public float turnGap = 0.1f;

        public event Action<TurnRecord> TurnStarted;
        public event Action<GameEvent> EventPlayed; // звук, тряска камеры, счётчики UI

        GridMapper _mapper;
        IntentOverlay _overlay;
        TileView _tiles;
        Func<GameEvent, EntityView> _spawn;
        readonly Dictionary<int, EntityView> _views = new Dictionary<int, EntityView>();
        readonly Dictionary<int, Cell> _cells = new Dictionary<int, Cell>();
        readonly HashSet<int> _shielded = new HashSet<int>();
        int _running;

        static readonly EventPhase[] PhaseOrder =
            { EventPhase.Prepare, EventPhase.Move, EventPhase.Effects, EventPhase.Combat, EventPhase.Tiles, EventPhase.Resolve, EventPhase.Plan };

        public void Bind(GridMapper mapper, IntentOverlay overlay, TileView tiles, Func<GameEvent, EntityView> spawn)
        {
            _mapper = mapper; _overlay = overlay; _tiles = tiles; _spawn = spawn;
        }

        public void Register(EntityView view, Cell cell)
        {
            _views[view.entityId] = view;
            _cells[view.entityId] = cell;
        }

        /// <summary>Оборвать проигрывание (кнопка Reset посреди анимации).</summary>
        public void Stop()
        {
            StopAllCoroutines();
            _running = 0;
        }

        public void ClearViews()
        {
            _views.Clear(); _cells.Clear(); _shielded.Clear();
        }

        /// <summary>Показать намерения врагов до старта.</summary>
        public void ShowInitial(IEnumerable<GameEvent> initial)
        {
            foreach (var e in initial) ApplyInstant(e);
        }

        public IEnumerator Play(SimulationResult result)
        {
            foreach (var turn in result.Turns)
            {
                TurnStarted?.Invoke(turn);
                foreach (var id in _shielded) if (_views.TryGetValue(id, out var v)) v.SetShield(false);
                _shielded.Clear();
                if (_overlay != null) _overlay.ClearAll();

                foreach (var phase in PhaseOrder)
                {
                    var events = turn.Events.Where(e => e.Phase == phase).ToList();
                    if (events.Count == 0) continue;

                    if (instant)
                    {
                        foreach (var e in events) ApplyInstant(e);
                        continue;
                    }

                    // Разные сущности — параллельно, события одной сущности — по порядку.
                    foreach (var group in events.GroupBy(GroupKey))
                    {
                        _running++;
                        StartCoroutine(Track(RunGroup(group.ToList())));
                    }
                    while (_running > 0) yield return null;
                }

                if (!instant) yield return new WaitForSeconds(turnGap / speed);
                else yield return null; // не вешаем кадр на длинных программах
            }
        }

        static int GroupKey(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.TileVisual:
                case GameEventType.ChannelChanged:
                case GameEventType.WindChanged:
                case GameEventType.Won:
                case GameEventType.Lost:
                    return -1;
                default:
                    return e.EntityId;
            }
        }

        IEnumerator Track(IEnumerator routine)
        {
            yield return routine;
            _running--;
        }

        IEnumerator RunGroup(List<GameEvent> events)
        {
            foreach (var e in events)
            {
                if (instant) { ApplyInstant(e); continue; }
                yield return Animate(e);
                EventPlayed?.Invoke(e);
            }
        }

        EntityView View(int id) => _views.TryGetValue(id, out var v) ? v : null;

        IEnumerator Animate(GameEvent e)
        {
            var v = View(e.EntityId);
            switch (e.Type)
            {
                case GameEventType.Moved:
                    _cells[e.EntityId] = e.To;
                    // "jump" — прыжок с гриба / на гриб: выше и дольше, как старый CharacterCode.JumpToTile.
                    if (v != null) yield return e.Info == "jump" ? v.MoveTo(e.To, e.Dir, speed, 3f, 1.6f) : v.MoveTo(e.To, e.Dir, speed);
                    break;
                case GameEventType.Slid:
                    _cells[e.EntityId] = e.To;
                    if (v != null) yield return v.MoveTo(e.To, e.Dir, speed, hop: 0f, timeScale: 0.6f);
                    break;
                case GameEventType.Pushed:
                    _cells[e.EntityId] = e.To;
                    if (v != null) yield return v.MoveTo(e.To, e.Dir, speed, hop: 0.3f, timeScale: 0.8f);
                    break;
                case GameEventType.Bounced:
                    _cells[e.EntityId] = e.To;
                    if (v != null) yield return v.MoveTo(e.To, e.Dir, speed, hop: 4f, timeScale: 1.6f);
                    break;
                case GameEventType.Teleported:
                    _cells[e.EntityId] = e.To;
                    if (v != null) yield return v.Teleport(e.To, speed);
                    break;
                case GameEventType.Bumped:
                    if (v != null && e.Info != null && e.Info.StartsWith("no")) break; // «нечего делать» — без анимации
                    if (v != null) yield return v.Bump(e.Dir, speed);
                    break;
                case GameEventType.Turned:
                    if (v != null) v.Face(e.Dir);
                    break;
                case GameEventType.ShieldRaised:
                    _shielded.Add(e.EntityId);
                    if (v != null) v.SetShield(true);
                    break;
                case GameEventType.Attacked:
                case GameEventType.SuperAttacked:
                    if (v != null) yield return v.Attack(e.Dir, speed, e.Type == GameEventType.SuperAttacked);
                    break;
                case GameEventType.Damaged:
                    yield return new WaitForSeconds(0.1f / speed); // удар долетает чуть позже замаха
                    if (v != null) yield return v.Hurt(speed);
                    break;
                case GameEventType.Blocked:
                    yield return new WaitForSeconds(0.1f / speed);
                    if (v != null) yield return v.Blocked(speed);
                    break;
                case GameEventType.Fell:
                    if (v != null) yield return v.Die(speed, fall: true);
                    break;
                case GameEventType.Died:
                    if (v != null && v.gameObject.activeSelf) yield return v.Die(speed, fall: false);
                    if (_overlay != null) _overlay.Clear(e.EntityId);
                    break;
                case GameEventType.CrystalCollected:
                    var crystal = View(e.TargetId);
                    if (crystal != null) yield return crystal.Die(speed, fall: false);
                    break;
                case GameEventType.Spawned:
                    var spawned = _spawn?.Invoke(e);
                    if (spawned != null) { Register(spawned, e.Cell); yield return spawned.PopIn(speed); }
                    break;
                case GameEventType.TileVisual:
                    if (_tiles != null) yield return _tiles.Apply(e, speed);
                    break;
                case GameEventType.IntentPlanned:
                    if (_overlay != null && _cells.TryGetValue(e.EntityId, out var c)) _overlay.Show(e, c);
                    break;
            }
        }

        void ApplyInstant(GameEvent e)
        {
            var v = View(e.EntityId);
            switch (e.Type)
            {
                case GameEventType.Moved:
                case GameEventType.Slid:
                case GameEventType.Pushed:
                case GameEventType.Bounced:
                case GameEventType.Teleported:
                    _cells[e.EntityId] = e.To;
                    if (v != null) { v.Snap(e.To); v.Face(e.Dir); }
                    break;
                case GameEventType.Turned:
                    if (v != null) v.Face(e.Dir);
                    break;
                case GameEventType.Died:
                case GameEventType.Fell:
                    if (v != null) v.gameObject.SetActive(false);
                    if (_overlay != null) _overlay.Clear(e.EntityId);
                    break;
                case GameEventType.CrystalCollected:
                    var crystal = View(e.TargetId);
                    if (crystal != null) crystal.gameObject.SetActive(false);
                    break;
                case GameEventType.Spawned:
                    var spawned = _spawn?.Invoke(e);
                    if (spawned != null) Register(spawned, e.Cell);
                    break;
                case GameEventType.TileVisual:
                    if (_tiles != null) _tiles.Set(e);
                    break;
                case GameEventType.IntentPlanned:
                    if (_overlay != null && _cells.TryGetValue(e.EntityId, out var c)) _overlay.Show(e, c);
                    break;
            }
            if (v != null) v.ResetVisual();
            EventPlayed?.Invoke(e);
        }
    }
}
