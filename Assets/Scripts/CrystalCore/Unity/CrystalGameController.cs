using System;
using System.Collections;
using System.Collections.Generic;
using CrystalAdventure.Core;
using UnityEngine;
using UnityEngine.Events;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Точка входа уровня. Заменяет связку CharacterCode.LateUpdate + WayTiles + MovingManager.StartPlay.
    ///
    ///   Play()  → собрать программу из карточек → посчитать прогон целиком → проиграть анимацию
    ///   ResetLevel() → пересобрать уровень с нуля (никаких «откатов» списков)
    ///   SetSpeed(1/2/4), SkipToEnd()
    /// </summary>
    public class CrystalGameController : MonoBehaviour
    {
        [Header("Уровни")]
        public List<LevelDefinition> levels = new List<LevelDefinition>();
        [Tooltip("Старый Data — берём из него NextLevel")]
        public Data data;
        [Tooltip("Если задан — играем его, а не levels[data.NextLevel]")]
        public LevelDefinition overrideLevel;

        [Header("Сцена")]
        public GridMapper mapper;
        public EntityPrefabs prefabs;
        public TurnPlayer player;
        public IntentOverlay overlay;
        public TileView tileView;
        public Transform entitiesRoot;

        [Header("Старый UI карточек")]
        public MovingManager commandSource;
        [Tooltip("Сохранять звёзды самому. Выключи, если окно победы — старый FinishingLevel: он сохраняет сам (с учётом подсказок и края), иначе запись будет двойной.")]
        public bool saveStars = true;

        [Header("События для UI")]
        public SlotEvent onCardHighlight = new SlotEvent();
        public IntEvent onWin = new IntEvent();          // звёзды
        public StringEvent onLose = new StringEvent();   // причина
        public UnityEvent onPlayStarted = new UnityEvent();
        public UnityEvent onReset = new UnityEvent();

        [Serializable] public class SlotEvent : UnityEvent<SlotType, int> { }
        [Serializable] public class IntEvent : UnityEvent<int> { }
        [Serializable] public class StringEvent : UnityEvent<string> { }

        public LevelDefinition CurrentLevel { get; private set; }
        public SimulationResult LastResult { get; private set; }
        public bool IsPlaying { get; private set; }

        LevelSetup _setup;
        ProgramDef _program;
        readonly List<EntityView> _spawned = new List<EntityView>();

        void Start()
        {
            player.Bind(mapper, overlay, tileView, SpawnFromEvent);
            player.TurnStarted += OnTurnStarted;
            LoadLevel();
        }

        // ------------------------------------------------------------------ уровень

        public void LoadLevel()
        {
            CurrentLevel = overrideLevel != null ? overrideLevel : PickLevel();
            if (CurrentLevel == null) { Debug.LogError("[Crystal] Нет LevelDefinition для этого уровня"); return; }

            _setup = CurrentLevel.ToSetup();
            mapper.SetHeights(CurrentLevel.Heights());
            BuildViews();
        }

        LevelDefinition PickLevel()
        {
            if (levels.Count == 0) return null;
            int i = data != null ? data.NextLevel : 0;
            return levels[Mathf.Clamp(i, 0, levels.Count - 1)];
        }

        void BuildViews()
        {
            StopAllCoroutines();
            player.Stop();
            IsPlaying = false;
            foreach (var v in _spawned) if (v != null) Destroy(v.gameObject);
            _spawned.Clear();
            player.ClearViews();
            if (overlay != null) overlay.ClearAll();

            var world = _setup.Build();
            if (tileView != null) tileView.SyncAll(world);
            foreach (var e in world.Entities) Spawn(e.Id, e, e.Pos, e.Facing);

            // Намерения врагов видны сразу, до старта.
            player.ShowInitial(TurnResolver.PlanOnly(world));
        }

        EntityView Spawn(int id, Entity e, Cell cell, Dir facing)
        {
            var prefab = prefabs != null ? prefabs.For(e) : null;
            if (prefab == null) { Debug.LogWarning($"[Crystal] Нет префаба для {e.Kind} {e.Archetype}"); return null; }
            var view = Instantiate(prefab, entitiesRoot != null ? entitiesRoot : transform);
            view.name = $"{e.Kind}_{e.Archetype}_{id}";
            view.Init(id, mapper, cell, facing);
            player.Register(view, cell);
            _spawned.Add(view);
            return view;
        }

        EntityView SpawnFromEvent(GameEvent ev)
        {
            // Призванный миньон: берём его данные из финального мира прогона.
            var e = LastResult?.Final.EntityById(ev.EntityId);
            if (e == null) return null;
            var view = Spawn(ev.EntityId, e, ev.Cell, ev.Dir);
            if (view != null) view.transform.localScale = Vector3.zero; // PopIn вырастит
            return view;
        }

        // ------------------------------------------------------------------ кнопки

        public void Play()
        {
            if (IsPlaying || _setup == null) return;
            // С повторами, скобками циклов и цветами каждой карточки — как MovingManager.BuildQueue.
            _program = ItemProgramAdapter.FromMovingManager(commandSource);
            Play(_program);
        }

        /// <summary>Запустить произвольную программу (подсказки Luma, демо, тесты).</summary>
        public void Play(ProgramDef program)
        {
            BuildViews(); // каждый запуск — с чистого уровня
            _program = program;
            LastResult = Simulation.Run(_setup, program);
            if (data != null) data.commandUsed = LastResult.BlocksUsed;
            StartCoroutine(PlayRoutine());
        }

        IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            onPlayStarted.Invoke();
            yield return player.Play(LastResult);
            IsPlaying = false;

            if (LastResult.Won)
            {
                if (saveStars && data != null) SaveManager.SaveLevelResult(data.NextLevel, LastResult.Stars);
                onWin.Invoke(LastResult.Stars);
            }
            else onLose.Invoke(LastResult.Message);
        }

        public void ResetLevel()
        {
            player.instant = false;
            BuildViews();
            onReset.Invoke();
        }

        public void SetSpeed(float speed) { player.speed = Mathf.Max(0.25f, speed); player.instant = false; }
        public void SkipToEnd() => player.instant = true;

        void OnTurnStarted(TurnRecord turn)
        {
            var block = _program?.Find(turn.BlockId);
            if (block?.Tag is SlotRef slot) onCardHighlight.Invoke(slot.Slot, slot.Index);
        }
    }
}
