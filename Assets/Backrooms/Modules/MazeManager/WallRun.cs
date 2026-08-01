using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// One continuous stretch of wall surface facing into a room, in world space. A run is the unit
    /// furniture is laid along: a single cell edge is only 4m of wall, and dressing each edge
    /// separately puts every piece on a 4m lattice, which is what makes a generated level read as a
    /// grid rather than a place.
    /// </summary>
    /// <remarks>
    /// A run is per-<i>side</i>, not per-wall: the wall between two cells presents two independent
    /// surfaces, one into each cell, and they can be dressed differently. A run only spans cells that
    /// are mutually reachable, so it never crosses a perpendicular wall, and it stops wherever the
    /// wall line itself is broken by an opening.
    /// </remarks>
    public struct WallRun
    {
        /// <summary>
        /// World-space start of the run at floor level, on the wall plane. The run extends from here
        /// along <see cref="Along"/> for <see cref="Length"/> metres.
        /// </summary>
        public Vector3 Start;

        /// <summary><c>true</c> when the run extends along the world X axis, <c>false</c> along Z.</summary>
        public bool AlongX;

        /// <summary>Length of the run in metres.</summary>
        public float Length;

        /// <summary>Which side of its cells the wall is on. The room is on the opposite side.</summary>
        public Direction Side;

        /// <summary>Grid cell the run starts at.</summary>
        public Vector2Int StartCell;

        /// <summary>How many cells the run spans.</summary>
        public int Cells;

        /// <summary>
        /// <c>true</c> when the run ends at <see cref="Start"/> because the wall line is broken by an
        /// opening the player walks through, rather than by a corner. Furniture must stand clear of it.
        /// </summary>
        public bool DoorwayAtStart;

        /// <summary>
        /// <c>true</c> when the far end of the run abuts an opening the player walks through, rather
        /// than a corner.
        /// </summary>
        public bool DoorwayAtEnd;

        /// <summary>
        /// Creates a wall run.
        /// </summary>
        /// <param name="start">World-space start at floor level, on the wall plane.</param>
        /// <param name="alongX">Whether the run extends along the world X axis.</param>
        /// <param name="length">Run length in metres.</param>
        /// <param name="side">Which side of its cells the wall is on.</param>
        /// <param name="startCell">Grid cell the run starts at.</param>
        /// <param name="cells">How many cells the run spans.</param>
        /// <param name="doorwayAtStart">Whether the near end abuts an opening.</param>
        /// <param name="doorwayAtEnd">Whether the far end abuts an opening.</param>
        public WallRun(Vector3 start, bool alongX, float length, Direction side, Vector2Int startCell,
            int cells, bool doorwayAtStart, bool doorwayAtEnd)
        {
            Start = start;
            AlongX = alongX;
            Length = length;
            Side = side;
            StartCell = startCell;
            Cells = cells;
            DoorwayAtStart = doorwayAtStart;
            DoorwayAtEnd = doorwayAtEnd;
        }

        /// <summary>Unit vector the run extends along, in world space.</summary>
        public Vector3 Along => AlongX ? Vector3.right : Vector3.forward;

        /// <summary>Unit vector pointing away from the wall, into the room it faces.</summary>
        public Vector3 IntoRoom
        {
            get
            {
                Vector2Int d = Directions.Delta(Side);
                return new Vector3(-d.x, 0f, -d.y);
            }
        }

        /// <summary>World-space far end of the run at floor level.</summary>
        public Vector3 End => Start + Along * Length;

        /// <summary>
        /// A point a given distance along the run, on the wall plane at floor level.
        /// </summary>
        /// <param name="distance">Distance from <see cref="Start"/> in metres.</param>
        /// <returns>The world position on the wall plane.</returns>
        public Vector3 PointAt(float distance) => Start + Along * distance;

        /// <summary>
        /// The grid cell at a given index along the run.
        /// </summary>
        /// <param name="index">Zero-based index, less than <see cref="Cells"/>.</param>
        /// <returns>The cell coordinate.</returns>
        public Vector2Int CellAt(int index)
            => AlongX
                ? new Vector2Int(StartCell.x + index, StartCell.y)
                : new Vector2Int(StartCell.x, StartCell.y + index);
    }
}
