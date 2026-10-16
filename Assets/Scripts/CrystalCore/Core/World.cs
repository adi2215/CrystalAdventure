using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalAdventure.Core
{
    [Serializable]
    public sealed class LevelRules
    {
        public int HeroHp = 3;
        public int MaxStepHeight = 1;          // на сколько блоков можно шагнуть вверх/вниз
        public bool AutoPickupCrystals = true; // false = кристалл надо взять блоком Interact (с середины Леса)
        public bool RequireAllEnemiesDefeated = false;
        public int SuperAttacks = 1;           // сколько Super Attack на уровень
        public int MaxTurns = 200;             // защита от бесконечных программ
        public bool WindActive = false;
        public Dir WindDir = Dir.East;

        /// <summary>
        /// Гриб «как в игре сейчас»: кто СТОИТ на грибе, тот следующим шагом летит на 2 клетки —
        /// туда, куда велит карточка. Наступить на гриб — обычный шаг, без отскока.
        /// false — новый вариант: отскок на 2 клетки вперёд сразу при входе на гриб.
        /// Автор выбрал старое поведение, поэтому по умолчанию true.
        /// </summary>
        public bool ClassicMushroom = true;

        /// <summary>
        /// Кристалл подбирается, только когда герой ПРИШЁЛ на клетку в этом ходу (как в старой игре:
        /// кристалл на стартовой клетке не считается, пока герой не уйдёт и не вернётся).
        /// </summary>
        public bool PickupOnArrival = true;
    }

    /// <summary>
    /// Полное состояние уровня. Никаких MonoBehaviour, корутин и Time.deltaTime —
    /// только данные. Всё, что «видно» на экране, строится из событий.
    /// </summary>
    public sealed class World
    {
        public readonly Dictionary<Cell, TileState> Tiles = new Dictionary<Cell, TileState>();
        public readonly List<Entity> Entities = new List<Entity>();
        public LevelRules Rules = new LevelRules();
        public int Turn;
        public Dir WindDir;
        public bool WindActive;

        readonly HashSet<int> _leverChannels = new HashSet<int>();
        readonly HashSet<int> _plateChannels = new HashSet<int>();
        int _nextId = 1;

        public Entity Hero { get; private set; }

        // ---------- построение ----------

        public TileState SetTile(Cell c, TileType type, int height = 0)
        {
            var t = new TileState { Type = type, Height = height };
            if (type == TileType.MagicPlatform) t.Gone = true;
            Tiles[c] = t;
            return t;
        }

        public Entity Spawn(EntityKind kind, Cell pos, Dir facing = Dir.East, int hp = 1, string archetype = null, IEnemyBrain brain = null)
        {
            var e = new Entity
            {
                Id = _nextId++, Kind = kind, Pos = pos, Facing = facing,
                Hp = hp, MaxHp = hp, Archetype = archetype ?? kind.ToString(), Brain = brain,
            };
            if (kind == EntityKind.Hero)
            {
                Hero = e;
                e.Hp = e.MaxHp = Rules.HeroHp;
                e.SuperAttacksLeft = Rules.SuperAttacks;
            }
            Entities.Add(e);
            return e;
        }

        public void ApplyRules(LevelRules rules)
        {
            Rules = rules;
            WindActive = rules.WindActive;
            WindDir = rules.WindDir;
        }

        // ---------- запросы ----------

        public TileState GetTile(Cell c) => Tiles.TryGetValue(c, out var t) ? t : null;

        public Entity EntityById(int id) => Entities.FirstOrDefault(e => e.Id == id);

        /// <summary>Твёрдая (не кристалл) живая сущность на клетке.</summary>
        public Entity SolidAt(Cell c)
        {
            foreach (var e in Entities) if (e.IsSolid && e.Pos == c) return e;
            return null;
        }

        public Entity EntityAt(Cell c)
        {
            foreach (var e in Entities) if (e.Alive && e.Pos == c) return e;
            return null;
        }

        public Entity CrystalAt(Cell c)
        {
            foreach (var e in Entities) if (e.Alive && e.Kind == EntityKind.Crystal && e.Pos == c) return e;
            return null;
        }

        public IEnumerable<Entity> Hostiles => Entities.Where(e => e.Alive && e.IsHostile);
        public IEnumerable<Entity> Crystals => Entities.Where(e => e.Alive && e.Kind == EntityKind.Crystal);

        public bool IsWalkable(Cell c)
        {
            var t = GetTile(c);
            return t != null && TileBehaviors.Get(t.Type).IsWalkable(this, c, t);
        }

        /// <summary>Статическая проверка: можно ли шагнуть с from на to (без учёта других движущихся).</summary>
        public bool CanStep(Cell from, Cell to, bool ignoreHeight = false)
        {
            if (!IsWalkable(to)) return false;
            if (ignoreHeight) return true;
            var a = GetTile(from);
            var b = GetTile(to);
            int ha = a?.Height ?? 0, hb = b.Height;
            return Math.Abs(hb - ha) <= Rules.MaxStepHeight;
        }

        /// <summary>Можно ли сущности войти в клетку прямо сейчас (клетка свободна).</summary>
        public bool CanEnter(Entity e, Cell from, Cell to, bool ignoreHeight = false)
        {
            if (!CanStep(from, to, ignoreHeight)) return false;
            var other = SolidAt(to);
            return other == null || other == e;
        }

        // ---------- каналы (рычаги, кнопки, ворота) ----------

        public bool IsChannelOn(int channel) =>
            channel != 0 && (_leverChannels.Contains(channel) || _plateChannels.Contains(channel));

        public void ToggleLever(int channel)
        {
            if (!_leverChannels.Remove(channel)) _leverChannels.Add(channel);
        }

        /// <summary>Пересчитать нажатые кнопки. Возвращает true, если что-то изменилось.</summary>
        public bool RecomputePlates()
        {
            var now = new HashSet<int>();
            foreach (var kv in Tiles)
                if (kv.Value.Type == TileType.PressurePlate && SolidAt(kv.Key) != null)
                    now.Add(kv.Value.Channel);
            bool changed = !now.SetEquals(_plateChannels);
            _plateChannels.Clear();
            _plateChannels.UnionWith(now);
            return changed;
        }
    }
}
