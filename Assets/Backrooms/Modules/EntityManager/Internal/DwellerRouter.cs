using Backrooms.EntityManager.Internal.Behaviour;
using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.EntityManager.Internal
{
    /// <summary>
    /// Internal coordinator for the EntityManager module. Owns the Dweller's current state and cell,
    /// and forwards every decision to the brain submodule.
    /// </summary>
    internal sealed class DwellerRouter
    {
        private DwellerBrain _brain = new DwellerBrain(0);
        private Vector2Int _cameFrom;

        /// <summary>The maze the Dweller is roaming, or <c>null</c> before it is placed.</summary>
        public MazeLayout Layout { get; private set; }

        /// <summary>The cell the Dweller occupies.</summary>
        public Vector2Int Cell { get; private set; }

        /// <summary>The cell the Dweller is currently moving into.</summary>
        public Vector2Int TargetCell { get; private set; }

        /// <summary>What the Dweller is currently doing.</summary>
        public DwellerState State { get; private set; } = DwellerState.Patrol;

        /// <summary>How many cells away the Dweller notices the player.</summary>
        public int SenseRangeCells { get; set; } = 5;

        /// <summary>
        /// Places the Dweller on a floor and resets its behaviour.
        /// </summary>
        /// <param name="layout">The maze it roams.</param>
        /// <param name="startCell">Cell to start in.</param>
        /// <param name="seed">Seed for deterministic wandering.</param>
        public void Place(MazeLayout layout, Vector2Int startCell, int seed)
        {
            Layout = layout;
            Cell = startCell;
            TargetCell = startCell;
            _cameFrom = startCell;
            State = DwellerState.Patrol;
            _brain = new DwellerBrain(seed);
        }

        /// <summary>
        /// Recomputes the Dweller's state from how far the player is.
        /// </summary>
        /// <param name="playerCell">The player's cell.</param>
        public void UpdateState(Vector2Int playerCell)
        {
            if (Layout == null) return;
            int distance = _brain.CellDistance(Layout, Cell, playerCell);
            State = _brain.NextState(State, distance, SenseRangeCells);
        }

        /// <summary>
        /// Picks the next cell to move into, chasing the player or wandering.
        /// </summary>
        /// <param name="playerCell">The player's cell.</param>
        /// <returns>The chosen destination cell.</returns>
        public Vector2Int ChooseNextCell(Vector2Int playerCell)
        {
            if (Layout == null) return Cell;

            TargetCell = State == DwellerState.Chase
                ? _brain.StepToward(Layout, Cell, playerCell)
                : _brain.StepWander(Layout, Cell, _cameFrom);

            return TargetCell;
        }

        /// <summary>
        /// Records that the Dweller finished moving into its target cell.
        /// </summary>
        public void ArriveAtTarget()
        {
            _cameFrom = Cell;
            Cell = TargetCell;
        }

        /// <summary>
        /// Marks the player as caught, which ends the run.
        /// </summary>
        public void MarkCaught() => State = DwellerState.Caught;

        /// <summary>
        /// Path distance in cells from the Dweller to a cell, or -1 if unreachable.
        /// </summary>
        /// <param name="cell">The cell to measure to.</param>
        /// <returns>Distance in cells, or -1.</returns>
        public int DistanceTo(Vector2Int cell)
            => Layout == null ? -1 : _brain.CellDistance(Layout, Cell, cell);
    }
}
