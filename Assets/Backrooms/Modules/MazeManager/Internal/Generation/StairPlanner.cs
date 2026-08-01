using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Generation
{
    /// <summary>
    /// Chooses where a floor's stairwells go — the ones down to the next floor, and the ones the
    /// player arrives out of. Pure logic over the carved grid, so placement can be measured without
    /// building anything.
    /// </summary>
    /// <remarks>
    /// A floor carries as many up stairwells as down ones, and the player always emerges from one of
    /// the up ones. That is what makes descending read as movement through a building rather than as
    /// a teleport: you came down a staircase, so there is a staircase behind you.
    /// </remarks>
    internal sealed class StairPlanner
    {
        /// <summary>
        /// Closest a down stairwell may sit to where the player arrives, in cells of walking.
        /// </summary>
        /// <remarks>
        /// Optimising coverage alone puts a way down a few steps from the arrival point, which makes
        /// the floor a formality — and, measured, collapsed the Dweller encounter rate from 96% to
        /// 28%, since a floor nobody has to cross is a floor nobody meets anything on.
        /// </remarks>
        private const int MinStairsFromSpawn = 16;

        /// <summary>How many relocations the coverage search tries.</summary>
        private const int ImproveAttempts = 1500;

        /// <summary>
        /// Where a floor's stairwells go.
        /// </summary>
        public readonly struct Plan
        {
            /// <summary>Cells the player can arrive out of.</summary>
            public readonly Vector2Int[] Up;

            /// <summary>Cells that lead down to the next floor.</summary>
            public readonly Vector2Int[] Down;

            /// <summary>The up stairwell the player actually emerges from on this floor.</summary>
            public readonly Vector2Int Arrival;

            /// <summary>
            /// Creates a plan.
            /// </summary>
            /// <param name="up">Cells the player can arrive out of.</param>
            /// <param name="down">Cells that lead down.</param>
            /// <param name="arrival">The up stairwell the player emerges from.</param>
            public Plan(Vector2Int[] up, Vector2Int[] down, Vector2Int arrival)
            {
                Up = up;
                Down = down;
                Arrival = arrival;
            }
        }

        /// <summary>
        /// Plans a floor's stairwells: the up ones first, then where the player emerges, then the
        /// down ones placed to cover the floor from there.
        /// </summary>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="count">How many stairwells of each direction.</param>
        /// <param name="rng">Seeded generator, so a floor is laid out the same every time.</param>
        /// <returns>The plan.</returns>
        public Plan PlanFloor(MazeCell[] cells, int w, int h, int count, System.Random rng)
        {
            count = Mathf.Clamp(count, 1, Mathf.Max(1, w * h / 4));

            // Spread the up stairwells over the floor first. They are where the player can appear,
            // so they decide everything downstream.
            Vector2Int[] up = Spread(cells, w, h, count, rng, null, Vector2Int.zero, 0);
            Vector2Int arrival = up[rng.Next(up.Length)];

            var blocked = new HashSet<Vector2Int>(up);
            Vector2Int[] down = Spread(cells, w, h, count, rng, blocked, arrival, MinStairsFromSpawn);

            return new Plan(up, down, arrival);
        }

        /// <summary>
        /// Picks cells spread over the floor: furthest-point selection to seed, then a local search
        /// that minimises the longest walk anyone faces to the nearest of them.
        /// </summary>
        /// <remarks>
        /// Three sharper-sounding strategies measured worse and are worth not re-attempting. Pure
        /// furthest-point selection drives the choices to the extremes of the floor, close to the
        /// worst shape for covering it. Repeatedly relocating onto the single worst-served cell
        /// stalls on its first pass, because with only three of them vacating any one opens a hole
        /// at least as large as the one it fills. And biasing candidates towards badly-served cells
        /// is backwards: the best site is usually a well-connected middle cell.
        /// </remarks>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="count">How many cells to choose.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <param name="blocked">Cells that may not be chosen, or <c>null</c>.</param>
        /// <param name="origin">Cell to keep clear of, or <see cref="Vector2Int.zero"/> for none.</param>
        /// <param name="minFromOrigin">How far chosen cells must be from the origin, in cells.</param>
        /// <returns>The chosen cells.</returns>
        private Vector2Int[] Spread(MazeCell[] cells, int w, int h, int count, System.Random rng,
            HashSet<Vector2Int> blocked, Vector2Int origin, int minFromOrigin)
        {
            var taken = new List<Vector2Int>(count);
            int[] fromOrigin = Distances(cells, w, h, new[] { origin });
            int[] distance = fromOrigin;

            while (taken.Count < count)
            {
                Vector2Int pick = Furthest(distance, w, h, fromOrigin, minFromOrigin, blocked, taken,
                    origin, rng);
                taken.Add(pick);
                if (taken.Count < count) distance = Distances(cells, w, h, taken);
            }

            Improve(cells, w, h, fromOrigin, minFromOrigin, blocked, taken, origin, rng);
            return taken.ToArray();
        }

        /// <summary>
        /// The eligible cell furthest from whatever the distance field was seeded with.
        /// </summary>
        /// <param name="distance">Distance field over the grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="fromOrigin">Distance from the origin to every cell.</param>
        /// <param name="minFromOrigin">How far a cell must be from the origin.</param>
        /// <param name="blocked">Cells that may not be chosen, or <c>null</c>.</param>
        /// <param name="taken">Cells already chosen.</param>
        /// <param name="origin">Cell to keep clear of.</param>
        /// <param name="rng">Seeded generator, used only to break ties.</param>
        /// <returns>The furthest eligible cell.</returns>
        private static Vector2Int Furthest(int[] distance, int w, int h, int[] fromOrigin,
            int minFromOrigin, HashSet<Vector2Int> blocked, List<Vector2Int> taken,
            Vector2Int origin, System.Random rng)
        {
            var best = new List<Vector2Int>();
            int bestDistance = -1;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!Eligible(cell, w, fromOrigin, minFromOrigin, blocked, taken, origin)) continue;

                    int d = distance[y * w + x];
                    if (d == int.MaxValue || d < bestDistance) continue;

                    // Keep every cell tied for furthest and pick among them, or the same corner of
                    // the scan order wins on every floor.
                    if (d > bestDistance)
                    {
                        bestDistance = d;
                        best.Clear();
                    }

                    best.Add(cell);
                }
            }

            return best.Count == 0 ? Fallback(w, h, taken, origin) : best[rng.Next(best.Count)];
        }

        /// <summary>
        /// Relocates one chosen cell at a time, keeping any move that shortens the longest walk.
        /// </summary>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="fromOrigin">Distance from the origin to every cell.</param>
        /// <param name="minFromOrigin">How far a cell must be from the origin.</param>
        /// <param name="blocked">Cells that may not be chosen, or <c>null</c>.</param>
        /// <param name="taken">Chosen cells, improved in place.</param>
        /// <param name="origin">Cell to keep clear of.</param>
        /// <param name="rng">Seeded generator.</param>
        private static void Improve(MazeCell[] cells, int w, int h, int[] fromOrigin,
            int minFromOrigin, HashSet<Vector2Int> blocked, List<Vector2Int> taken,
            Vector2Int origin, System.Random rng)
        {
            if (taken.Count < 2) return;

            int best = Worst(Distances(cells, w, h, taken));

            for (int attempt = 0; attempt < ImproveAttempts && best > 1; attempt++)
            {
                var candidate = new Vector2Int(rng.Next(w), rng.Next(h));
                if (!Eligible(candidate, w, fromOrigin, minFromOrigin, blocked, taken, origin)) continue;

                int slot = rng.Next(taken.Count);
                Vector2Int original = taken[slot];
                taken[slot] = candidate;

                int score = Worst(Distances(cells, w, h, taken));
                if (score < best) best = score;
                else taken[slot] = original;
            }
        }

        /// <summary>
        /// Whether a cell may hold a stairwell.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="fromOrigin">Distance from the origin to every cell.</param>
        /// <param name="minFromOrigin">How far a cell must be from the origin.</param>
        /// <param name="blocked">Cells that may not be chosen, or <c>null</c>.</param>
        /// <param name="taken">Cells already chosen.</param>
        /// <param name="origin">Cell to keep clear of.</param>
        /// <returns><c>true</c> if the cell is eligible.</returns>
        private static bool Eligible(Vector2Int cell, int w, int[] fromOrigin, int minFromOrigin,
            HashSet<Vector2Int> blocked, List<Vector2Int> taken, Vector2Int origin)
        {
            if (minFromOrigin > 0 && cell == origin) return false;
            if (taken.Contains(cell)) return false;
            if (blocked != null && blocked.Contains(cell)) return false;
            if (minFromOrigin <= 0) return true;

            int away = fromOrigin[cell.y * w + cell.x];
            return away != int.MaxValue && away >= minFromOrigin;
        }

        /// <summary>
        /// A cell to use when nothing is eligible, so generation always returns the count asked for.
        /// </summary>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="taken">Cells already chosen.</param>
        /// <param name="origin">Cell to keep clear of.</param>
        /// <returns>Any cell not already used.</returns>
        private static Vector2Int Fallback(int w, int h, List<Vector2Int> taken, Vector2Int origin)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell != origin && !taken.Contains(cell)) return cell;
                }
            }

            return origin;
        }

        /// <summary>
        /// The longest walk in a distance field, ignoring anything unreachable.
        /// </summary>
        /// <param name="distance">Distance field over the grid.</param>
        /// <returns>The largest finite distance.</returns>
        private static int Worst(int[] distance)
        {
            int worst = 0;
            foreach (int d in distance)
            {
                if (d != int.MaxValue && d > worst) worst = d;
            }

            return worst;
        }

        /// <summary>
        /// Walking distance from a set of source cells to every cell, through open passages only.
        /// </summary>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="sources">Cells to measure from.</param>
        /// <returns>Distance per cell, row-major; <see cref="int.MaxValue"/> if unreachable.</returns>
        private static int[] Distances(MazeCell[] cells, int w, int h, IEnumerable<Vector2Int> sources)
        {
            var distance = new int[w * h];
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;

            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int source in sources)
            {
                distance[source.y * w + source.x] = 0;
                queue.Enqueue(source);
            }

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                int here = distance[cur.y * w + cur.x];

                foreach (Direction dir in Directions.All)
                {
                    if (!cells[cur.y * w + cur.x].IsOpen(dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    int nx = cur.x + d.x;
                    int ny = cur.y + d.y;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (distance[ny * w + nx] != int.MaxValue) continue;

                    distance[ny * w + nx] = here + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            return distance;
        }
    }
}
