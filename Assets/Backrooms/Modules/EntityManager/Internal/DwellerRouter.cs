using System.Collections.Generic;
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

        /// <summary>The route the Dweller is currently patrolling, and how far along it is.</summary>
        private List<Vector2Int> _patrol = new List<Vector2Int>();
        private int _patrolIndex;

        /// <summary>The maze the Dweller is roaming, or <c>null</c> before it is placed.</summary>
        public MazeLayout Layout { get; private set; }

        /// <summary>The cell the Dweller occupies.</summary>
        public Vector2Int Cell { get; private set; }

        /// <summary>The cell the Dweller is currently moving into.</summary>
        public Vector2Int TargetCell { get; private set; }

        /// <summary>What the Dweller is currently doing.</summary>
        public DwellerState State { get; private set; } = DwellerState.Patrol;

        /// <summary>How many cells away the Dweller notices the player.</summary>
        public int SenseRangeCells { get; set; } = 12;

        /// <summary>Shortest patrol trip, in cells of grid separation.</summary>
        public int PatrolSpanCells { get; set; } = 18;

        /// <summary>Whether the Dweller is actively hunting the player right now.</summary>
        public bool IsChasing => State == DwellerState.Chase;

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
            _patrol.Clear();
            _patrolIndex = 0;
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
        /// Puts the Dweller's grid position where its body actually is.
        /// </summary>
        /// <remarks>
        /// A charge leaves the grid and moves in a straight line, so the pathing has to be told where
        /// it ended up. Without this the next path step is computed from the cell it occupied before
        /// the charge and it walks back the way it came.
        /// </remarks>
        /// <param name="cell">The cell the body now occupies.</param>
        public void SnapTo(Vector2Int cell)
        {
            Cell = cell;
            TargetCell = cell;
        }

        /// <summary>
        /// Picks the next cell to move into, chasing the player or wandering.
        /// </summary>
        /// <param name="playerCell">The player's cell.</param>
        /// <returns>The chosen destination cell.</returns>
        public Vector2Int ChooseNextCell(Vector2Int playerCell)
        {
            if (Layout == null) return Cell;

            if (State == DwellerState.Chase)
            {
                // Drop the patrol route on sight. Resuming a route planned from a cell the Dweller
                // has since left would walk it through walls.
                _patrol.Clear();
                _patrolIndex = 0;
                TargetCell = _brain.StepToward(Layout, Cell, playerCell);
                return TargetCell;
            }

            TargetCell = NextPatrolCell();
            return TargetCell;
        }

        /// <summary>
        /// The next cell along the current patrol route, planning a fresh one when the last is spent.
        /// Falls back to a single wander step on a grid where no route could be planned.
        /// </summary>
        /// <returns>The cell to move into.</returns>
        private Vector2Int NextPatrolCell()
        {
            if (_patrolIndex >= _patrol.Count)
            {
                _patrol = _brain.PlanPatrol(Layout, Cell, PatrolSpanCells) ?? new List<Vector2Int>();
                _patrolIndex = 0;
            }

            return _patrolIndex < _patrol.Count
                ? _patrol[_patrolIndex++]
                : _brain.StepWander(Layout, Cell, _cameFrom);
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
        /// Takes the Dweller off the floor entirely. Clearing the state matters as much as hiding the
        /// body: a Dweller parked in <see cref="DwellerState.Caught"/> would keep reporting that it
        /// had caught the player, and end the next run the instant it began.
        /// </summary>
        public void Deactivate()
        {
            Layout = null;
            State = DwellerState.Patrol;
            _patrol.Clear();
            _patrolIndex = 0;
        }

        /// <summary>
        /// Path distance in cells from the Dweller to a cell, or -1 if unreachable.
        /// </summary>
        /// <param name="cell">The cell to measure to.</param>
        /// <returns>Distance in cells, or -1.</returns>
        public int DistanceTo(Vector2Int cell)
            => Layout == null ? -1 : _brain.CellDistance(Layout, Cell, cell);
    }
}
