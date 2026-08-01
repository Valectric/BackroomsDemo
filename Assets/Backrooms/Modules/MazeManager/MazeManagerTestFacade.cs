using System.Collections.Generic;
using Backrooms.MazeManager.Internal;
using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// Test seam for the MazeManager module. Its constructor takes an internal router, so only the
    /// production <see cref="MazeFacade"/> can create one (via <see cref="MazeFacade.GetTestFacade"/>).
    /// Not intended for production use — only for automated testing. Lets tests generate layouts by
    /// seed and inspect them without any scene geometry.
    /// </summary>
    public sealed class MazeManagerTestFacade
    {
        private readonly MazeRouter _router;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        internal MazeManagerTestFacade(MazeRouter router)
        {
            _router = router;
        }

        /// <summary>
        /// Generates a maze layout for the given settings — the same path production uses, exposed
        /// directly so tests can assert on the layout without a scene.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout Generate(MazeSettings settings) => _router.Generate(settings);

        /// <summary>
        /// Plans the wall segments for a layout without building any scene geometry, so tests can
        /// assert wall placement directly.
        /// </summary>
        /// <param name="layout">The layout to plan walls for.</param>
        /// <returns>The planned wall segments.</returns>
        public List<WallSegment> PlanWalls(MazeLayout layout)
            => _router.PlanWalls(layout, layout.CellSize);

        /// <summary>
        /// Collects the layout's continuous wall runs without building any scene geometry, so tests
        /// can assert on how furniture will be laid out before any of it is instantiated.
        /// </summary>
        /// <param name="layout">The layout to scan.</param>
        /// <param name="excluded">Cells whose walls must be left bare, or <c>null</c> for none.</param>
        /// <returns>The planned wall runs.</returns>
        public List<WallRun> PlanWallRuns(MazeLayout layout, HashSet<Vector2Int> excluded = null)
            => _router.PlanWallRuns(layout, excluded);
    }
}
