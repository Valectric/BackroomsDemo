using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// A compass direction on the maze grid. North is +Y, East is +X, South is -Y, West is -X.
    /// Used to describe passages between adjacent cells and to walk the grid.
    /// </summary>
    public enum Direction
    {
        /// <summary>Towards +Y.</summary>
        North = 0,

        /// <summary>Towards +X.</summary>
        East = 1,

        /// <summary>Towards -Y.</summary>
        South = 2,

        /// <summary>Towards -X.</summary>
        West = 3
    }

    /// <summary>
    /// Grid helpers for <see cref="Direction"/>: the full set, per-direction cell deltas, and the
    /// opposite direction. Pure, allocation-free helpers shared by generation and by tests.
    /// </summary>
    public static class Directions
    {
        /// <summary>All four directions in enum order (North, East, South, West).</summary>
        public static readonly Direction[] All =
        {
            Direction.North, Direction.East, Direction.South, Direction.West
        };

        /// <summary>
        /// The integer grid offset for a direction: North=(0,1), East=(1,0), South=(0,-1),
        /// West=(-1,0).
        /// </summary>
        /// <param name="dir">The direction to translate into a cell offset.</param>
        /// <returns>The (x,y) step for that direction.</returns>
        public static Vector2Int Delta(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return new Vector2Int(0, 1);
                case Direction.East: return new Vector2Int(1, 0);
                case Direction.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        /// <summary>The reverse of a direction (North↔South, East↔West).</summary>
        /// <param name="dir">The direction to reverse.</param>
        /// <returns>The opposite direction.</returns>
        public static Direction Opposite(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return Direction.South;
                case Direction.East: return Direction.West;
                case Direction.South: return Direction.North;
                default: return Direction.East;
            }
        }
    }

    /// <summary>
    /// One cell of the maze grid. Each of the four booleans is <c>true</c> when the cell has an open
    /// passage in that direction (i.e. no wall), meaning a player can walk from this cell into the
    /// neighbouring cell. Passages are always symmetric: if cell A is open North, the cell to its
    /// North is open South.
    /// </summary>
    public struct MazeCell
    {
        /// <summary>Open passage towards +Y.</summary>
        public bool NorthOpen;

        /// <summary>Open passage towards +X.</summary>
        public bool EastOpen;

        /// <summary>Open passage towards -Y.</summary>
        public bool SouthOpen;

        /// <summary>Open passage towards -X.</summary>
        public bool WestOpen;

        /// <summary>
        /// Whether this cell has an open passage in the given direction.
        /// </summary>
        /// <param name="dir">The direction to query.</param>
        /// <returns><c>true</c> if the passage is open, otherwise <c>false</c>.</returns>
        public bool IsOpen(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return NorthOpen;
                case Direction.East: return EastOpen;
                case Direction.South: return SouthOpen;
                default: return WestOpen;
            }
        }

        /// <summary>
        /// Opens the passage in the given direction on this cell (sets the matching boolean).
        /// </summary>
        /// <param name="dir">The direction to open.</param>
        public void Open(Direction dir)
        {
            switch (dir)
            {
                case Direction.North: NorthOpen = true; break;
                case Direction.East: EastOpen = true; break;
                case Direction.South: SouthOpen = true; break;
                default: WestOpen = true; break;
            }
        }
    }
}
