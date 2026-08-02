using UnityEngine;

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
        /// wall-to-wall passages. Defaults to one room per 64 cells, so room <i>density</i> stays
        /// constant as the grid grows rather than a big floor being all corridor.
        /// </summary>
        public int RoomCount { get; set; } = 4;

        /// <summary>
        /// How many stairwells down to the next floor the grid carries. More than one is what makes a
        /// large floor playable: a single exit on a 32-cell grid is a long blind search, whereas three
        /// means there is usually one within reach of wherever the player ends up.
        /// </summary>
        public int StairCount { get; set; } = 3;

        /// <summary>
        /// Whether there is a floor above this one to climb to. False on the first floor, which the
        /// player noclipped into rather than walked down to — so it carries no ways up at all.
        /// </summary>
        /// <remarks>
        /// Suppressed here at the layout rather than at the geometry, so one decision covers every
        /// consequence: no riser is built, no hole is cut in the ceiling, no cell is reserved from
        /// furniture, and nothing can be found by a search for the nearest way up. A staircase that
        /// climbs to nowhere is worse than no staircase — the player walks to it and it does not work.
        /// </remarks>
        public bool HasFloorAbove { get; set; } = true;

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
            RoomCount = Mathf.Max(3, Width * Height / 64);
        }
    }
}
