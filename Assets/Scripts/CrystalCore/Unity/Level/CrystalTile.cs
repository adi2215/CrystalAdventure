using CrystalAdventure.Core;
using UnityEngine;

namespace CrystalAdventure.UnityLayer
{
    /// <summary>
    /// Супер-тайл, который можно рисовать кистью в Tilemap.
    /// Тип и параметры читает LevelDefinition при сборке уровня.
    /// Create → Crystal Adventure → Super Tile.
    /// </summary>
    [CreateAssetMenu(menuName = "Crystal Adventure/Super Tile")]
    public class CrystalTile : UnityEngine.Tilemaps.Tile
    {
        [Header("Поведение")]
        public TileType type = TileType.Ground;

        [Tooltip("Связь рычагов / кнопок / ворот / лиан / телепортов")]
        public int channel;

        [Tooltip("Для Wind, если не используется общий ветер уровня")]
        public Dir direction = Dir.East;
        public bool useGlobalWind = true;

        [Header("Таймеры")]
        [Tooltip("Spikes: раз в сколько ходов бьют")] public int period = 2;
        [Tooltip("Spikes: сдвиг фазы")] public int offset;
        [Tooltip("Crumbling: сколько раз можно пройти")] public int durability = 1;
        [Tooltip("VanishingCloud: через сколько ходов вернётся")] public int respawnTurns = 3;

        public TileSpec ToSpec(int x, int y, int height) => new TileSpec
        {
            X = x, Y = y, Height = height, Type = type, Channel = channel,
            Direction = direction, UseGlobalWind = useGlobalWind,
            Period = period, Offset = offset, Durability = durability, RespawnTurns = respawnTurns,
        };
    }
}
