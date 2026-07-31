namespace Backrooms.MazeManager
{
    /// <summary>
    /// Input parameters for generating a Level-0 maze. A given <see cref="Seed"/> plus identical
    /// <see cref="Width"/>/<see cref="Height"/> always produces the same layout — generation is
    /// fully deterministic, which is what makes the maze testable.
    /// </summary>
    public sealed class MazeSettings
    {
        /// <summary>Number of cells along the X axis. Clamped to at least 2 on construction.</summary>
        public int Width { get; }

        /// <summary>Number of cells along the Y axis. Clamped to at least 2 on construction.</summary>
        public int Height { get; }

        /// <summary>Deterministic seed for the generator's pseudo-random choices.</summary>
        public int Seed { get; }

        /// <summary>World-space size of one cell in metres, used later when building geometry.</summary>
        public float CellSize { get; }

        /// <summary>
        /// How many open rooms to carve into the grid. Rooms break up the corridors so a floor is not
        /// wall-to-wall passages.
        /// </summary>
        public int RoomCount { get; set; } = 4;

        /// <summary>Smallest room side, in cells.</summary>
        public int RoomMinSize { get; set; } = 3;

        /// <summary>Largest room side, in cells.</summary>
        public int RoomMaxSize { get; set; } = 6;

        /// <summary>
        /// Fraction of dead ends to open into a loop, from 0 (a perfect maze, every route unique) to
        /// 1 (no dead ends at all). Loops make a floor feel navigable instead of a guessing game.
        /// </summary>
        public float BraidChance { get; set; } = 0.8f;

        /// <summary>
        /// Creates maze settings, clamping <paramref name="width"/> and <paramref name="height"/> to
        /// a minimum of 2 cells and <paramref name="cellSize"/> to a small positive value so
        /// generation always has a valid grid to work on.
        /// </summary>
        /// <param name="width">Requested grid width in cells.</param>
        /// <param name="height">Requested grid height in cells.</param>
        /// <param name="seed">Deterministic generation seed.</param>
        /// <param name="cellSize">World size of a cell in metres (default 4).</param>
        public MazeSettings(int width, int height, int seed, float cellSize = 4f)
        {
            Width = width < 2 ? 2 : width;
            Height = height < 2 ? 2 : height;
            Seed = seed;
            CellSize = cellSize < 0.1f ? 0.1f : cellSize;
        }
    }
}
