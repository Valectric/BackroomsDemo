using System.Collections.Generic;
using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.RelicManager.Internal.Placement
{
    /// <summary>
    /// Pure-logic submodule that decides which cells hold a relic. No Unity scene access, so the
    /// placement rule can be asserted directly.
    /// </summary>
    /// <remarks>
    /// The rule is one sentence: a relic goes where the stairwells are not. Concretely, at the cell
    /// with the longest walk to the nearest way down — which is exactly the cell the stairwell
    /// placement works hardest to avoid creating, and therefore the part of the floor a player
    /// heading for the exit would never otherwise see. That is what makes it a decision: the relic is
    /// always a genuine detour rather than something collected in passing.
    /// </remarks>
    internal sealed class RelicPlanner
    {
        /// <summary>
        /// Chooses the relic cells for a floor.
        /// </summary>
        /// <param name="layout">The floor being dressed.</param>
        /// <param name="count">How many relics to place.</param>
        /// <param name="rng">Seeded generator, used only to break ties.</param>
        /// <returns>The chosen cells, furthest-from-the-stairs first.</returns>
        public List<Vector2Int> Plan(MazeLayout layout, int count, System.Random rng)
        {
            var chosen = new List<Vector2Int>();
            if (layout == null || count <= 0) return chosen;

            // Seeded from the stairwells and the spawn together: a relic tucked behind the player's
            // starting corner is no more of a detour than one next to a stairwell.
            var sources = new List<Vector2Int>(layout.Stairs) { layout.Spawn };
            int[] distance = Distances(layout, sources);

            while (chosen.Count < count)
            {
                Vector2Int pick = FurthestCell(layout, distance, chosen, rng);
                if (pick == layout.Spawn) break;

                chosen.Add(pick);
                if (chosen.Count >= count) break;

                // Fold the relic just placed into the distance field, so a second relic lands in a
                // different part of the floor rather than next to the first.
                sources.Add(pick);
                distance = Distances(layout, sources);
            }

            return chosen;
        }

        /// <summary>
        /// The cell furthest from everything the distance field was seeded with.
        /// </summary>
        /// <param name="layout">The floor being measured.</param>
        /// <param name="distance">Distance field over the grid.</param>
        /// <param name="taken">Cells already chosen.</param>
        /// <param name="rng">Seeded generator, used only to break ties.</param>
        /// <returns>The furthest eligible cell, or the spawn if there is none.</returns>
        private static Vector2Int FurthestCell(MazeLayout layout, int[] distance,
            List<Vector2Int> taken, System.Random rng)
        {
            var best = new List<Vector2Int>();
            int bestDistance = 0;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell == layout.Spawn || layout.IsStairs(cell) || taken.Contains(cell)) continue;

                    int d = distance[y * layout.Width + x];
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

            return best.Count == 0 ? layout.Spawn : best[rng.Next(best.Count)];
        }

        /// <summary>
        /// Walking distance from a set of cells to every cell, through open passages only.
        /// </summary>
        /// <param name="layout">The floor being measured.</param>
        /// <param name="sources">Cells to measure from.</param>
        /// <returns>Distance per cell, indexed row-major; <see cref="int.MaxValue"/> if unreachable.</returns>
        private static int[] Distances(MazeLayout layout, IEnumerable<Vector2Int> sources)
        {
            var distance = new int[layout.Width * layout.Height];
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;

            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int source in sources)
            {
                distance[source.y * layout.Width + source.x] = 0;
                queue.Enqueue(source);
            }

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                int here = distance[cur.y * layout.Width + cur.x];

                foreach (Direction dir in Directions.All)
                {
                    if (!layout.CanMove(cur.x, cur.y, dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    var next = new Vector2Int(cur.x + d.x, cur.y + d.y);
                    int index = next.y * layout.Width + next.x;
                    if (distance[index] != int.MaxValue) continue;

                    distance[index] = here + 1;
                    queue.Enqueue(next);
                }
            }

            return distance;
        }
    }
}
