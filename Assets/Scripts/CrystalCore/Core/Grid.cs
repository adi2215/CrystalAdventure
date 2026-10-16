using System;

namespace CrystalAdventure.Core
{
    /// <summary>Клетка на логической сетке. Не зависит от Unity — поэтому ядро можно тестировать без сцены.</summary>
    [Serializable]
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y) { X = x; Y = y; }

        public Cell Step(Dir dir, int distance = 1)
        {
            var o = dir.Offset();
            return new Cell(X + o.X * distance, Y + o.Y * distance);
        }

        public int ManhattanTo(Cell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static Cell operator -(Cell a, Cell b) => new Cell(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Cell a, Cell b) => !(a == b);
        public bool Equals(Cell other) => this == other;
        public override bool Equals(object obj) => obj is Cell c && this == c;
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>Абсолютное направление на сетке.</summary>
    public enum Dir { North = 0, East = 1, South = 2, West = 3 }

    /// <summary>Направление относительно того, куда смотрит персонаж.</summary>
    public enum RelDir { Forward = 0, Right = 1, Back = 2, Left = 3 }

    public static class DirExtensions
    {
        public static Cell Offset(this Dir d)
        {
            switch (d)
            {
                case Dir.North: return new Cell(0, 1);
                case Dir.East: return new Cell(1, 0);
                case Dir.South: return new Cell(0, -1);
                default: return new Cell(-1, 0);
            }
        }

        public static Dir RotateCW(this Dir d) => (Dir)(((int)d + 1) % 4);
        public static Dir RotateCCW(this Dir d) => (Dir)(((int)d + 3) % 4);
        public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) % 4);
        public static Dir Apply(this Dir facing, RelDir rel) => (Dir)(((int)facing + (int)rel) % 4);

        /// <summary>Направление «примерно в сторону цели» (сначала по большей оси). Детерминированно.</summary>
        public static Dir Towards(Cell from, Cell to)
        {
            int dx = to.X - from.X, dy = to.Y - from.Y;
            if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0) return dx > 0 ? Dir.East : Dir.West;
            return dy > 0 ? Dir.North : Dir.South;
        }
    }
}
