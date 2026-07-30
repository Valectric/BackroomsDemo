using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// The generated maze: a grid of <see cref="MazeCell"/> with a spawn cell and an exit cell.
    /// This is the public, read-only result a <see cref="MazeFacade"/> hands out. Construction is
    /// internal so only the generator can build one; consumers and tests read it through the query
    /// methods below.
    /// </summary>
    public sealed class MazeLayout
    {
        private readonly MazeCell[] _cells;

        /// <summary>Grid width in cells.</summary>
        public int Width { get; }

        /// <summary>Grid height in cells.</summary>
        public int Height { get; }

        /// <summary>The cell the player starts in.</summary>
        public Vector2Int Spawn { get; }

        /// <summary>The cell that ends the run (the exit) when reached.</summary>
        public Vector2Int Exit { get; }

        /// <summary>World size of one cell in metres, as used when building geometry.</summary>
        public float CellSize { get; }

        /// <summary>
        /// Builds a layout from a flat cell array in row-major order (index = y * width + x).
        /// Internal: only <see cref="Backrooms.MazeManager.Internal.Generation.MazeGenerator"/>
        /// constructs layouts.
        /// </summary>
        /// <param name="width">Grid width in cells.</param>
        /// <param name="height">Grid height in cells.</param>
        /// <param name="cells">Row-major cell array of length width*height.</param>
        /// <param name="spawn">Spawn cell coordinate.</param>
        /// <param name="exit">Exit cell coordinate.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        internal MazeLayout(int width, int height, MazeCell[] cells, Vector2Int spawn, Vector2Int exit,
            float cellSize)
        {
            Width = width;
            Height = height;
            _cells = cells;
            Spawn = spawn;
            Exit = exit;
            CellSize = cellSize;
        }

        /// <summary>
        /// World-space centre of a cell at floor level. The grid maps to the world XZ plane
        /// (grid X → world X, grid Y → world Z).
        /// </summary>
        /// <param name="cell">Cell coordinate.</param>
        /// <returns>World position at floor level.</returns>
        public Vector3 CellCenterToWorld(Vector2Int cell)
        {
            float half = CellSize * 0.5f;
            return new Vector3(cell.x * CellSize + half, 0f, cell.y * CellSize + half);
        }

        /// <summary>
        /// The grid cell containing a world position, clamped into the grid.
        /// </summary>
        /// <param name="world">World-space position.</param>
        /// <returns>The containing cell coordinate.</returns>
        public Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(world.x / CellSize), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(world.z / CellSize), 0, Height - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Whether the given coordinate is inside the grid.
        /// </summary>
        /// <param name="x">Cell X coordinate.</param>
        /// <param name="y">Cell Y coordinate.</param>
        /// <returns><c>true</c> if in bounds, otherwise <c>false</c>.</returns>
        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>
        /// Returns the cell at the given coordinate. Assumes the coordinate is in bounds (guard with
        /// <see cref="InBounds"/> first).
        /// </summary>
        /// <param name="x">Cell X coordinate.</param>
        /// <param name="y">Cell Y coordinate.</param>
        /// <returns>The cell at (x,y).</returns>
        public MazeCell CellAt(int x, int y) => _cells[y * Width + x];

        /// <summary>
        /// Whether a player standing at (x,y) can move one step in the given direction — the source
        /// cell must have an open passage there and the destination must be in bounds.
        /// </summary>
        /// <param name="x">Source cell X coordinate.</param>
        /// <param name="y">Source cell Y coordinate.</param>
        /// <param name="dir">Direction to test.</param>
        /// <returns><c>true</c> if movement is possible, otherwise <c>false</c>.</returns>
        public bool CanMove(int x, int y, Direction dir)
        {
            if (!InBounds(x, y)) return false;
            if (!CellAt(x, y).IsOpen(dir)) return false;
            Vector2Int d = Directions.Delta(dir);
            return InBounds(x + d.x, y + d.y);
        }
    }
}
