namespace CrystalAdventure.Core
{
    /// <summary>
    /// Фазы хода. View проигрывает фазы по очереди. Внутри фазы события РАЗНЫХ
    /// сущностей идут одновременно (герой и враги шагают синхронно),
    /// а события ОДНОЙ сущности — друг за другом (скольжение по льду клетка за клеткой).
    /// </summary>
    public enum EventPhase { Prepare = 0, Move = 1, Effects = 2, Combat = 3, Tiles = 4, Resolve = 5, Plan = 6 }

    public enum GameEventType
    {
        BlockStarted,   // интерпретатор взял блок (подсветить в UI)
        Turned,
        ShieldRaised,
        Moved,
        Bumped,         // хотел пойти, но упёрся (стена, враг, конфликт клеток)
        Attacked,
        SuperAttacked,
        Damaged,
        Blocked,        // урон поглощён щитом / бронёй
        ArmorBroken,
        Died,
        Fell,           // упал в пропасть
        Slid,
        Bounced,
        Pushed,
        Teleported,
        TrapFired,
        TileChanged,    // рассыпалась клетка, сломан лёд, снят барьер...
        TileVisual,     // клетка выглядит иначе: Info = тип, Amount = 1 если проходима/видна (для View)
        ChannelChanged, // рычаг / кнопка
        WindChanged,
        SpellCast,
        CrystalCollected,
        IntentPlanned,  // новое намерение врага (телеграф)
        Spawned,        // босс призвал миньона
        Guarded,        // босс закрыл ядро
        Frozen,         // клетки превратились в лёд
        Won,
        Lost,
        Skipped,        // карточка с условием по цвету пропущена: герой стоит, ход мира не тратится
        ArmorGained,    // враг надел ледяную броню (босс в новой фазе) — View показывает её сразу
    }

    public sealed class GameEvent
    {
        public GameEventType Type;
        public EventPhase Phase;
        public int EntityId = -1;
        public int TargetId = -1;
        public Cell Cell;      // клетка события / откуда
        public Cell To;        // куда (движение)
        public Dir Dir;
        public int Amount;
        public Cell[] Area;
        public string Info;
        /// <summary>Вид удара (Intent.Style) — у IntentPlanned и Attacked; цель удара тогда в To.</summary>
        public string Style;

        public GameEvent(GameEventType type, EventPhase phase) { Type = type; Phase = phase; }

        public override string ToString() =>
            $"[{Phase}] {Type} e={EntityId} {Cell}->{To} {Dir} {Amount} {Info}";
    }
}
