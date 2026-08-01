using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Pure-logic submodule that collects a maze's closed cell sides into maximal
    /// <see cref="WallRun"/>s — continuous stretches of wall surface facing into a single space.
    /// It touches no Unity scene objects, so run planning is unit-testable independently of the
    /// furniture that gets laid along it.
    /// </summary>
    /// <remarks>
    /// Two rules decide where a run stops, and both matter visually. A run cannot cross a
    /// perpendicular wall — the two cells either side must be mutually reachable, or a sideboard laid
    /// across the join would grow straight through the wall between two different rooms. And a run
    /// ends wherever the wall line itself is broken, which is a doorway; the run records that so
    /// furniture can stand clear of the threshold instead of half-blocking it.
    /// </remarks>
    internal sealed class WallRunPlanner
    {
        /// <summary>
        /// Collects the wall runs of a layout, longest-possible first.
        /// </summary>
        /// <param name="layout">The maze to scan.</param>
        /// <param name="excluded">
        /// Cells whose wall surfaces must be left bare — spawn and the stairs, which need to stay
        /// readable. May be <c>null</c>. Excluded cells break runs but never suppress the doorway
        /// flags, which describe the geometry rather than the dressing.
        /// </param>
        /// <returns>Every wall run on the floor, in a deterministic order.</returns>
        public List<WallRun> Plan(MazeLayout layout, HashSet<Vector2Int> excluded = null)
        {
            var runs = new List<WallRun>();

            foreach (Direction side in Directions.All)
            {
                bool alongX = side == Direction.North || side == Direction.South;
                Direction step = alongX ? Direction.East : Direction.North;
                int lines = alongX ? layout.Height : layout.Width;
                int span = alongX ? layout.Width : layout.Height;

                for (int line = 0; line < lines; line++)
                {
                    ScanLine(layout, excluded, runs, side, step, alongX, line, span);
                }
            }

            return runs;
        }

        /// <summary>
        /// Walks one row or column collecting the maximal runs of wall surface on a single side.
        /// </summary>
        /// <param name="layout">The maze being scanned.</param>
        /// <param name="excluded">Cells to leave bare, or <c>null</c>.</param>
        /// <param name="runs">Collects the runs found.</param>
        /// <param name="side">Which side of the cells the wall is on.</param>
        /// <param name="step">Direction the scan advances in.</param>
        /// <param name="alongX">Whether the scan advances along world X.</param>
        /// <param name="line">Index of the row (or column) being scanned.</param>
        /// <param name="span">How many cells the row (or column) holds.</param>
        private static void ScanLine(MazeLayout layout, HashSet<Vector2Int> excluded,
            List<WallRun> runs, Direction side, Direction step, bool alongX, int line, int span)
        {
            int i = 0;
            while (i < span)
            {
                if (!Eligible(layout, excluded, Cell(alongX, line, i), side))
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i + 1 < span
                       && Eligible(layout, excluded, Cell(alongX, line, i + 1), side)
                       && Reaches(layout, Cell(alongX, line, i), step))
                {
                    i++;
                }

                runs.Add(Build(layout, side, step, alongX, Cell(alongX, line, start), i - start + 1));
                i++;
            }
        }

        /// <summary>
        /// Assembles the run description for a span of cells, resolving its world extent and whether
        /// each end abuts a doorway rather than a corner.
        /// </summary>
        /// <param name="layout">The maze being scanned.</param>
        /// <param name="side">Which side of the cells the wall is on.</param>
        /// <param name="step">Direction the run advances in.</param>
        /// <param name="alongX">Whether the run advances along world X.</param>
        /// <param name="startCell">First cell of the run.</param>
        /// <param name="cells">How many cells the run spans.</param>
        /// <returns>The completed run.</returns>
        private static WallRun Build(MazeLayout layout, Direction side, Direction step, bool alongX,
            Vector2Int startCell, int cells)
        {
            float cellSize = layout.CellSize;
            Vector2Int endCell = startCell + Directions.Delta(step) * (cells - 1);

            Vector3 origin = alongX
                ? new Vector3(startCell.x * cellSize, 0f,
                    (side == Direction.North ? startCell.y + 1 : startCell.y) * cellSize)
                : new Vector3((side == Direction.East ? startCell.x + 1 : startCell.x) * cellSize,
                    0f, startCell.y * cellSize);

            return new WallRun(origin, alongX, cells * cellSize, side, startCell, cells,
                IsDoorway(layout, startCell, Directions.Opposite(step), side),
                IsDoorway(layout, endCell, step, side));
        }

        /// <summary>
        /// Whether the wall line is broken by an opening just past the given end of a run, as opposed
        /// to turning a corner or leaving the grid. Only a break the player can actually walk through
        /// from this side counts.
        /// </summary>
        /// <param name="layout">The maze being scanned.</param>
        /// <param name="cell">The end cell of the run.</param>
        /// <param name="outward">Direction pointing off the end of the run.</param>
        /// <param name="side">Which side of the cells the wall is on.</param>
        /// <returns><c>true</c> if a doorway abuts this end of the run.</returns>
        private static bool IsDoorway(MazeLayout layout, Vector2Int cell, Direction outward,
            Direction side)
        {
            if (!Reaches(layout, cell, outward)) return false;
            Vector2Int next = cell + Directions.Delta(outward);
            return layout.CanMove(next.x, next.y, side);
        }

        /// <summary>
        /// Whether a cell presents a dressable wall surface on the given side.
        /// </summary>
        /// <param name="layout">The maze being scanned.</param>
        /// <param name="excluded">Cells to leave bare, or <c>null</c>.</param>
        /// <param name="cell">Cell to test.</param>
        /// <param name="side">Side to test.</param>
        /// <returns><c>true</c> if the side is walled and the cell may be dressed.</returns>
        private static bool Eligible(MazeLayout layout, HashSet<Vector2Int> excluded, Vector2Int cell,
            Direction side)
        {
            if (layout.CanMove(cell.x, cell.y, side)) return false;
            return excluded == null || !excluded.Contains(cell);
        }

        /// <summary>
        /// Whether a player standing in a cell can walk one step in a direction.
        /// </summary>
        /// <param name="layout">The maze being scanned.</param>
        /// <param name="cell">Cell to step from.</param>
        /// <param name="dir">Direction to step in.</param>
        /// <returns><c>true</c> if the step is possible.</returns>
        private static bool Reaches(MazeLayout layout, Vector2Int cell, Direction dir)
            => layout.CanMove(cell.x, cell.y, dir);

        /// <summary>
        /// The cell at a scan position, mapping the (line, index) scan coordinates onto the grid.
        /// </summary>
        /// <param name="alongX">Whether the scan advances along world X.</param>
        /// <param name="line">Row or column index.</param>
        /// <param name="index">Position along the scan.</param>
        /// <returns>The grid cell.</returns>
        private static Vector2Int Cell(bool alongX, int line, int index)
            => alongX ? new Vector2Int(index, line) : new Vector2Int(line, index);
    }
}
