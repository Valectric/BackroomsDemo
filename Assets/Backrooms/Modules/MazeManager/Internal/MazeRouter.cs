using System.Collections.Generic;
using Backrooms.MazeManager.Internal.Generation;
using Backrooms.MazeManager.Internal.Geometry;
using UnityEngine;

namespace Backrooms.MazeManager.Internal
{
    /// <summary>
    /// Internal coordinator for the MazeManager module. Pure single-line wiring: it forwards
    /// generation to the <see cref="MazeGenerator"/> submodule, wall planning to the
    /// <see cref="MazeWallPlanner"/>, and geometry building to the
    /// <see cref="MazeGeometryBuilder"/>.
    /// </summary>
    internal sealed class MazeRouter
    {
        private readonly MazeGenerator _generator = new MazeGenerator();
        private readonly MazeWallPlanner _planner = new MazeWallPlanner();
        private readonly WallRunPlanner _runPlanner = new WallRunPlanner();
        private readonly MazeGeometryBuilder _geometry = new MazeGeometryBuilder();

        /// <summary>
        /// Generates a maze layout for the given settings by delegating to the generator submodule.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout Generate(MazeSettings settings) => _generator.Generate(settings);

        /// <summary>
        /// Plans the wall segments for a layout by delegating to the wall planner.
        /// </summary>
        /// <param name="layout">The layout to plan walls for.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <returns>The planned wall segments.</returns>
        public List<WallSegment> PlanWalls(MazeLayout layout, float cellSize)
            => _planner.Plan(layout, cellSize);

        /// <summary>
        /// Collects the layout's continuous wall runs by delegating to the wall-run planner.
        /// </summary>
        /// <param name="layout">The layout to scan.</param>
        /// <param name="excluded">Cells whose walls must be left bare, or <c>null</c>.</param>
        /// <returns>The planned wall runs.</returns>
        public List<WallRun> PlanWallRuns(MazeLayout layout, HashSet<Vector2Int> excluded)
            => _runPlanner.Plan(layout, excluded);

        /// <summary>
        /// Builds scene geometry for a layout by delegating to the geometry builder.
        /// </summary>
        /// <param name="layout">The layout to realise.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="lightSpacingCells">Ceiling-light spacing in cells.</param>
        /// <param name="parent">Parent transform for the generated root.</param>
        /// <param name="theme">Palette for the floor being built.</param>
        /// <returns>The generated geometry root GameObject.</returns>
        public GameObject BuildGeometry(MazeLayout layout, float wallHeight, int lightSpacingCells,
            Transform parent, FloorTheme theme)
            => _geometry.Build(layout, layout.CellSize, wallHeight, lightSpacingCells, parent, theme);
    }
}
