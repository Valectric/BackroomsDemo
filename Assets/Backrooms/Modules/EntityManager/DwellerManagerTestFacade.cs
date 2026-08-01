using System.Collections.Generic;
using Backrooms.EntityManager.Internal;
using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.EntityManager
{
    /// <summary>
    /// Test seam for the EntityManager module. Its constructor takes the internal router, so only the
    /// production <see cref="DwellerFacade"/> can create one. Not intended for production use — only
    /// for automated testing. Lets tests drive and inspect a Dweller's decisions on a grid without
    /// running the scene, which keeps its pathing and state machine deterministic to verify.
    /// </summary>
    public sealed class DwellerManagerTestFacade
    {
        private readonly DwellerRouter _router;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        internal DwellerManagerTestFacade(DwellerRouter router)
        {
            _router = router;
        }

        /// <summary>What the Dweller is currently doing.</summary>
        public DwellerState State => _router.State;

        /// <summary>The cell the Dweller occupies.</summary>
        public Vector2Int Cell => _router.Cell;

        /// <summary>The cell the Dweller is moving into.</summary>
        public Vector2Int TargetCell => _router.TargetCell;

        /// <summary>
        /// Places the Dweller on a floor without any scene objects.
        /// </summary>
        /// <param name="layout">The maze it roams.</param>
        /// <param name="startCell">Cell to start in.</param>
        /// <param name="seed">Seed for deterministic wandering.</param>
        public void Place(MazeLayout layout, Vector2Int startCell, int seed)
            => _router.Place(layout, startCell, seed);

        /// <summary>
        /// Sets how many cells away the Dweller notices the player.
        /// </summary>
        /// <param name="cells">Sense range in cells.</param>
        public void SetSenseRange(int cells) => _router.SenseRangeCells = cells;

        /// <summary>
        /// Sets how far a patrol trip travels before a new one is planned.
        /// </summary>
        /// <param name="cells">Shortest patrol trip, in cells of grid separation.</param>
        public void SetPatrolSpan(int cells) => _router.PatrolSpanCells = cells;

        /// <summary>Whether the Dweller is actively hunting the player.</summary>
        public bool IsChasing => _router.IsChasing;

        /// <summary>
        /// Recomputes the Dweller's state against a player position.
        /// </summary>
        /// <param name="playerCell">The player's cell.</param>
        public void UpdateState(Vector2Int playerCell) => _router.UpdateState(playerCell);

        /// <summary>
        /// Advances the Dweller one whole cell, as if it had finished walking there.
        /// </summary>
        /// <param name="playerCell">The player's cell.</param>
        /// <returns>The cell the Dweller moved into.</returns>
        public Vector2Int StepOneCell(Vector2Int playerCell)
        {
            _router.UpdateState(playerCell);
            _router.ChooseNextCell(playerCell);
            _router.ArriveAtTarget();
            return _router.Cell;
        }

        /// <summary>
        /// Path distance in cells from the Dweller to a cell, or -1 if unreachable.
        /// </summary>
        /// <param name="cell">Cell to measure to.</param>
        /// <returns>Distance in cells, or -1.</returns>
        public int DistanceTo(Vector2Int cell) => _router.DistanceTo(cell);

        /// <summary>
        /// The cells the Dweller would walk through to reach a target, excluding its own cell.
        /// </summary>
        /// <param name="layout">The maze being traversed.</param>
        /// <param name="from">Starting cell.</param>
        /// <param name="to">Destination cell.</param>
        /// <returns>The path, or <c>null</c> if unreachable.</returns>
        public List<Vector2Int> PathBetween(MazeLayout layout, Vector2Int from, Vector2Int to)
            => new Internal.Behaviour.DwellerBrain(0).FindPath(layout, from, to);
    }
}
