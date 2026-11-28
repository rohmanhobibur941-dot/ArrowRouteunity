using System.Collections.Generic;

namespace ArrowRoute
{
    /// <summary>Directions in grid space (y grows downwards, like the Android version).</summary>
    public enum Dir { Up = 0, Down = 1, Left = 2, Right = 3 }

    public static class DirUtil
    {
        public static readonly int[] DX = { 0, 0, -1, 1 };
        public static readonly int[] DY = { -1, 1, 0, 0 };
        public static readonly Dir[] All = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        public static int Dx(Dir d) { return DX[(int)d]; }
        public static int Dy(Dir d) { return DY[(int)d]; }
    }

    public struct Cell
    {
        public readonly int x;
        public readonly int y;
        public Cell(int x, int y) { this.x = x; this.y = y; }
    }

    /// <summary>A snake-like path of grid cells. The LAST cell is the head.</summary>
    public sealed class Arrow
    {
        public readonly int id;
        public readonly Cell[] cells;

        public Arrow(int id, Cell[] cells)
        {
            this.id = id;
            this.cells = cells;
        }

        public Cell Head { get { return cells[cells.Length - 1]; } }

        public Dir Direction
        {
            get
            {
                Cell a = cells[cells.Length - 2];
                Cell b = cells[cells.Length - 1];
                if (b.x > a.x) return Dir.Right;
                if (b.x < a.x) return Dir.Left;
                if (b.y > a.y) return Dir.Down;
                return Dir.Up;
            }
        }
    }

    /// <summary>Board silhouettes: square, circle, numbers 1 2 3, letters A B C and a few animals.</summary>
    public enum ShapeType { Square, Circle, One, LetterA, Cat, Two, LetterB, Fish, Three, LetterC, Rabbit, Bear }

    public sealed class LevelData
    {
        public int w;
        public int h;
        public bool[] mask;
        public ShapeType shape;
        public List<Arrow> arrows;
    }

    public enum GameStatus { Playing, Won, Lost }
}
