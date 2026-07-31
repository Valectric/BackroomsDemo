using System.Collections.Generic;
using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.EntityManager.Internal.Behaviour
{
    /// <summary>
    /// Decides where a Dweller goes next. Pure logic over the maze grid — no scene access, no
    /// randomness beyond a seeded generator — so its pathing and state transitions can be tested
    /// exactly. A Dweller can only travel through open passages, exactly like the player.
    /// </summary>
    internal sealed class DwellerBrain
    {
        private readonly System.Random _rng;

        /// <summary>
        /// Creates a brain with a deterministic wander sequence.
        /// </summary>
        /// <param name="seed">Seed for wander choices.</param>
        public DwellerBrain(int seed)
        {
            _rng = new System.Random(seed);
        }

        /// <summary>
        /// Chooses the state a Dweller should be in, given how far away the player is. Once it has
        /// caught the player the state is terminal for that run.
        /// </summary>
        /// <param name="current">The Dweller's current state.</param>
        /// <param name="cellsToPlayer">Path distance to the player, in cells.</param>
        /// <param name="senseRangeCells">How many cells away the Dweller can notice the player.</param>
        /// <returns>The state the Dweller should now be in.</returns>
        public DwellerState NextState(DwellerState current, int cellsToPlayer, int senseRangeCells)
        {
            if (current == DwellerState.Caught) return DwellerState.Caught;
            if (cellsToPlayer < 0) return DwellerState.Patrol;
            return cellsToPlayer <= senseRangeCells ? DwellerState.Chase : DwellerState.Patrol;
        }

        /// <summary>
        /// The next cell to move into when chasing: the first step along the shortest open path.
        /// </summary>
        /// <param name="layout">The maze being traversed.</param>
        /// <param name="from">The Dweller's cell.</param>
        /// <param name="to">The player's cell.</param>
        /// <returns>The next cell, or <paramref name="from"/> if there is nowhere to go.</returns>
        public Vector2Int StepToward(MazeLayout layout, Vector2Int from, Vector2Int to)
        {
            List<Vector2Int> path = FindPath(layout, from, to);
            return path == null || path.Count == 0 ? from : path[0];
        }

        /// <summary>
        /// The next cell to move into when wandering: a random reachable neighbour, preferring not to
        /// double back so patrols drift along corridors instead of jittering in place.
        /// </summary>
        /// <param name="layout">The maze being traversed.</param>
        /// <param name="from">The Dweller's cell.</param>
        /// <param name="cameFrom">The cell it arrived from, to avoid immediately reversing.</param>
        /// <returns>The next cell, or <paramref name="from"/> if boxed in.</returns>
        public Vector2Int StepWander(MazeLayout layout, Vector2Int from, Vector2Int cameFrom)
        {
            var options = new List<Vector2Int>(4);
            var reversals = new List<Vector2Int>(1);

            foreach (Direction dir in Directions.All)
            {
                if (!layout.CanMove(from.x, from.y, dir)) continue;
                Vector2Int d = Directions.Delta(dir);
                var next = new Vector2Int(from.x + d.x, from.y + d.y);
                if (next == cameFrom) reversals.Add(next);
                else options.Add(next);
            }

            if (options.Count > 0) return options[_rng.Next(options.Count)];
            return reversals.Count > 0 ? reversals[0] : from;
        }

        /// <summary>
        /// Shortest path between two cells through open passages, excluding the starting cell.
        /// </summary>
        /// <param name="layout">The maze being traversed.</param>
        /// <param name="from">Starting cell.</param>
        /// <param name="to">Destination cell.</param>
        /// <returns>The path, or <c>null</c> if the destination is unreachable.</returns>
        public List<Vector2Int> FindPath(MazeLayout layout, Vector2Int from, Vector2Int to)
        {
            if (from == to) return new List<Vector2Int>();

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            bool found = false;

            while (queue.Count > 0 && !found)
            {
                Vector2Int cur = queue.Dequeue();
                foreach (Direction dir in Directions.All)
                {
                    if (!layout.CanMove(cur.x, cur.y, dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    var next = new Vector2Int(cur.x + d.x, cur.y + d.y);
                    if (!seen.Add(next)) continue;

                    cameFrom[next] = cur;
                    if (next == to)
                    {
                        found = true;
                        break;
                    }

                    queue.Enqueue(next);
                }
            }

            if (!found) return null;

            var path = new List<Vector2Int>();
            for (Vector2Int c = to; c != from; c = cameFrom[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Path distance to the player in cells, or -1 when unreachable.
        /// </summary>
        /// <param name="layout">The maze being traversed.</param>
        /// <param name="from">The Dweller's cell.</param>
        /// <param name="to">The player's cell.</param>
        /// <returns>Distance in cells, or -1.</returns>
        public int CellDistance(MazeLayout layout, Vector2Int from, Vector2Int to)
        {
            if (from == to) return 0;
            List<Vector2Int> path = FindPath(layout, from, to);
            return path?.Count ?? -1;
        }
    }
}
