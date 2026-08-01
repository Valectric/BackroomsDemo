using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Generation
{
    /// <summary>
    /// Submodule that turns a <see cref="MazeSettings"/> into a <see cref="MazeLayout"/> using an
    /// iterative recursive-backtracker (depth-first) algorithm seeded by a deterministic
    /// <see cref="System.Random"/>. The result is a "perfect" maze: every cell is reachable from
    /// every other cell by exactly one path, so the exit is always reachable from the spawn.
    /// Pure C# — no Unity scene access.
    /// </summary>
    internal sealed class MazeGenerator
    {
        /// <summary>
        /// Generates the maze layout for the given settings. Spawn is the bottom-left cell (0,0);
        /// the stairwells down to the next floor are scattered across the grid. Deterministic for a
        /// fixed seed and size.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>A fully-connected maze layout.</returns>
        public MazeLayout Generate(MazeSettings settings)
        {
            int w = settings.Width;
            int h = settings.Height;
            var cells = new MazeCell[w * h];
            var visited = new bool[w * h];
            var rng = new System.Random(settings.Seed);

            var spawn = new Vector2Int(0, 0);

            var stack = new Stack<Vector2Int>();
            visited[Index(spawn.x, spawn.y, w)] = true;
            stack.Push(spawn);

            var candidates = new List<Direction>(4);

            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Peek();
                candidates.Clear();

                foreach (Direction dir in Directions.All)
                {
                    Vector2Int d = Directions.Delta(dir);
                    int nx = cur.x + d.x;
                    int ny = cur.y + d.y;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (visited[Index(nx, ny, w)]) continue;
                    candidates.Add(dir);
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                Direction chosen = candidates[rng.Next(candidates.Count)];
                Vector2Int delta = Directions.Delta(chosen);
                var next = new Vector2Int(cur.x + delta.x, cur.y + delta.y);

                // Carve a symmetric passage between the current cell and the chosen neighbour.
                cells[Index(cur.x, cur.y, w)].Open(chosen);
                cells[Index(next.x, next.y, w)].Open(Directions.Opposite(chosen));

                visited[Index(next.x, next.y, w)] = true;
                stack.Push(next);
            }

            // The backtracker leaves a perfect maze: fully connected, but every route unique and
            // riddled with dead ends. Both passes below only ever OPEN walls, so connectivity is
            // preserved by construction while the floor gains rooms and loops.
            CarveRooms(cells, w, h, settings, rng);
            Braid(cells, w, h, settings, rng);

            Vector2Int[] stairs = ChooseStairs(cells, w, h, spawn, settings.StairCount, rng);
            return new MazeLayout(w, h, cells, spawn, stairs, settings.CellSize);
        }

        /// <summary>
        /// Picks the cells that get a stairwell down, by greedy furthest-point selection: the first
        /// stairwell goes far from the spawn, and each one after it goes wherever is currently the
        /// longest walk from any stairwell already placed.
        /// </summary>
        /// <remarks>
        /// The previous rule enforced a minimum <i>separation</i> between stairwells, which is not the
        /// same property as covering the floor and does not imply it. Three stairwells can sit a
        /// comfortable distance apart and still leave a whole quadrant stranded: measured over 30
        /// seeds it left the worst cell a mean of 33 cells from any way down, and 47 cells at its
        /// worst — 188 metres of walking on a 96 metre floor. Choosing each stairwell at the current
        /// furthest point attacks exactly that number, and distances are measured <i>through</i> the
        /// maze rather than across it, so a stairwell on the far side of a wall does not count as near.
        /// </remarks>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="spawn">Cell the player starts in.</param>
        /// <param name="count">How many stairwells to place.</param>
        /// <param name="rng">Seeded generator, so a floor's stairs land in the same cells every time.</param>
        /// <returns>The chosen cells.</returns>
        private static Vector2Int[] ChooseStairs(MazeCell[] cells, int w, int h, Vector2Int spawn,
            int count, System.Random rng)
        {
            count = Mathf.Clamp(count, 1, w * h - 1);

            // Seed with furthest-point selection, then improve it. Furthest-point alone drives the
            // stairwells to the extremes of the floor, which is close to the worst possible shape for
            // covering it — measured, it was worse than the separation rule it replaced. It is only a
            // starting position.
            var chosen = new List<Vector2Int>(count);
            int[] fromSpawn = Distances(cells, w, h, new[] { spawn });
            int[] distance = fromSpawn;

            while (chosen.Count < count)
            {
                Vector2Int pick = FurthestCell(distance, w, h, spawn, fromSpawn, chosen, rng);
                chosen.Add(pick);
                if (chosen.Count < count) distance = Distances(cells, w, h, chosen);
            }

            Improve(cells, w, h, spawn, fromSpawn, chosen, rng);
            return chosen.ToArray();
        }

        /// <summary>
        /// Tries relocating one stairwell at a time to a uniformly random cell, keeping any move that
        /// shortens the longest walk anyone can face.
        /// </summary>
        /// <remarks>
        /// Three sharper-sounding strategies measured worse and are worth not re-attempting. Pure
        /// furthest-point selection drives the stairwells to the extremes of the floor, which is close
        /// to the worst shape for covering it. Repeatedly relocating a stairwell onto the single
        /// worst-served cell stalls immediately: with only three of them, vacating any one opens a
        /// hole at least as large as the one it fills, so no single move ever improves and the search
        /// gives up on its first pass. And sampling candidates biased towards badly-served cells is
        /// backwards: the best site for a stairwell is usually a well-connected middle cell, not a
        /// stranded one, so the bias mostly rejects the cells worth trying.
        /// </remarks>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="spawn">Cell the player starts in.</param>
        /// <param name="fromSpawn">Walking distance from the spawn to every cell.</param>
        /// <param name="chosen">Stairwell cells, improved in place.</param>
        /// <param name="rng">Seeded generator.</param>
        private static void Improve(MazeCell[] cells, int w, int h, Vector2Int spawn, int[] fromSpawn,
            List<Vector2Int> chosen, System.Random rng)
        {
            if (chosen.Count < 2) return;

            int[] field = Distances(cells, w, h, chosen);
            int best = WorstWalk(field);

            for (int attempt = 0; attempt < ImproveAttempts && best > 1; attempt++)
            {
                var candidate = new Vector2Int(rng.Next(w), rng.Next(h));
                if (candidate == spawn || chosen.Contains(candidate)) continue;

                // Coverage alone will happily drop a way down a few steps from where the player
                // arrives, which makes the floor a formality. Optimising for the worst walk has to be
                // bounded by keeping the best walk honest.
                if (fromSpawn[Index(candidate.x, candidate.y, w)] < MinStairsFromSpawn) continue;

                int slot = rng.Next(chosen.Count);
                Vector2Int original = chosen[slot];
                chosen[slot] = candidate;

                int[] trial = Distances(cells, w, h, chosen);
                int score = WorstWalk(trial);
                if (score < best)
                {
                    best = score;
                    field = trial;
                }
                else
                {
                    chosen[slot] = original;
                }
            }
        }

        /// <summary>
        /// Closest a stairwell may sit to the spawn, in cells of walking. Without this the coverage
        /// search puts one next to the player and the floor is over before it starts — which showed
        /// up as the Dweller encounter rate collapsing from 96% to 28%, since a floor nobody has to
        /// cross is a floor nobody meets anything on.
        /// </summary>
        private const int MinStairsFromSpawn = 16;

        /// <summary>
        /// How many relocations to try. Each costs one grid-wide breadth-first search, a few hundred
        /// operations on a 24x24 floor, so the whole search is well under a millisecond.
        /// </summary>
        private const int ImproveAttempts = 1500;

        /// <summary>
        /// The longest walk in a distance field, ignoring anything unreachable.
        /// </summary>
        /// <param name="distance">Distance field over the grid.</param>
        /// <returns>The largest finite distance.</returns>
        private static int WorstWalk(int[] distance)
        {
            int worst = 0;
            foreach (int d in distance)
            {
                if (d != int.MaxValue && d > worst) worst = d;
            }

            return worst;
        }

        /// <summary>
        /// The cell at the greatest distance from whatever the distance field was seeded with,
        /// breaking ties with the seeded generator so the choice is varied but reproducible.
        /// </summary>
        /// <param name="distance">Distance field over the grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="spawn">Cell the player starts in, which may never hold a stairwell.</param>
        /// <param name="fromSpawn">Walking distance from the spawn to every cell.</param>
        /// <param name="taken">Cells already chosen.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <returns>The furthest eligible cell.</returns>
        private static Vector2Int FurthestCell(int[] distance, int w, int h, Vector2Int spawn,
            int[] fromSpawn, List<Vector2Int> taken, System.Random rng)
        {
            var best = new List<Vector2Int>();
            int bestDistance = -1;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell == spawn || taken.Contains(cell)) continue;

                    // The same doorstep guard the local search uses. Without it here, the second
                    // stairwell lands next to the player: the field is reseeded from the first
                    // stairwell alone, and the cell furthest from that is the spawn's own corner.
                    if (fromSpawn[Index(x, y, w)] < MinStairsFromSpawn) continue;

                    int d = distance[Index(x, y, w)];
                    if (d < bestDistance) continue;

                    // Keep every cell tied for furthest, then pick among them. Taking the first would
                    // hand every floor to the same corner of the scan order.
                    if (d > bestDistance)
                    {
                        bestDistance = d;
                        best.Clear();
                    }

                    best.Add(cell);
                }
            }

            return best.Count == 0 ? spawn : best[rng.Next(best.Count)];
        }

        /// <summary>
        /// Walking distance from a set of source cells to every cell, through open passages only.
        /// </summary>
        /// <param name="cells">The carved grid.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="sources">Cells to measure from.</param>
        /// <returns>Distance per cell, indexed row-major; <see cref="int.MaxValue"/> if unreachable.</returns>
        private static int[] Distances(MazeCell[] cells, int w, int h, IEnumerable<Vector2Int> sources)
        {
            var distance = new int[w * h];
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;

            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int source in sources)
            {
                distance[Index(source.x, source.y, w)] = 0;
                queue.Enqueue(source);
            }

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                int here = distance[Index(cur.x, cur.y, w)];

                foreach (Direction dir in Directions.All)
                {
                    if (!cells[Index(cur.x, cur.y, w)].IsOpen(dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    int nx = cur.x + d.x;
                    int ny = cur.y + d.y;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (distance[Index(nx, ny, w)] != int.MaxValue) continue;

                    distance[Index(nx, ny, w)] = here + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            return distance;
        }

        /// <summary>
        /// Opens rectangular rooms in the grid by removing every wall inside them, turning warrens of
        /// corridors into open halls the player can actually orient in.
        /// </summary>
        /// <param name="cells">Cell array being modified.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="settings">Room count and size limits.</param>
        /// <param name="rng">Seeded generator, so rooms land in the same place every time.</param>
        private static void CarveRooms(MazeCell[] cells, int w, int h, MazeSettings settings,
            System.Random rng)
        {
            int minSize = Mathf.Max(2, settings.RoomMinSize);
            int maxSize = Mathf.Max(minSize, settings.RoomMaxSize);

            for (int room = 0; room < settings.RoomCount; room++)
            {
                int roomW = Mathf.Min(rng.Next(minSize, maxSize + 1), w);
                int roomH = Mathf.Min(rng.Next(minSize, maxSize + 1), h);
                int x0 = rng.Next(0, Mathf.Max(1, w - roomW + 1));
                int y0 = rng.Next(0, Mathf.Max(1, h - roomH + 1));

                for (int y = y0; y < y0 + roomH; y++)
                {
                    for (int x = x0; x < x0 + roomW; x++)
                    {
                        // Open eastward and northward inside the room, keeping passages symmetric.
                        if (x + 1 < x0 + roomW) OpenBetween(cells, w, x, y, Direction.East);
                        if (y + 1 < y0 + roomH) OpenBetween(cells, w, x, y, Direction.North);
                    }
                }
            }
        }

        /// <summary>
        /// Removes dead ends by opening one extra wall on each, which turns the tree of corridors
        /// into a network with loops. A player who takes a wrong turn can come back a different way
        /// instead of retracing every step.
        /// </summary>
        /// <param name="cells">Cell array being modified.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="settings">Braid chance.</param>
        /// <param name="rng">Seeded generator.</param>
        private static void Braid(MazeCell[] cells, int w, int h, MazeSettings settings,
            System.Random rng)
        {
            float chance = Mathf.Clamp01(settings.BraidChance);
            var candidates = new List<Direction>(4);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    MazeCell cell = cells[Index(x, y, w)];
                    int openSides = 0;
                    foreach (Direction dir in Directions.All)
                    {
                        if (cell.IsOpen(dir)) openSides++;
                    }

                    if (openSides != 1) continue;
                    if (rng.NextDouble() > chance) continue;

                    candidates.Clear();
                    foreach (Direction dir in Directions.All)
                    {
                        if (cell.IsOpen(dir)) continue;
                        Vector2Int d = Directions.Delta(dir);
                        int nx = x + d.x;
                        int ny = y + d.y;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        candidates.Add(dir);
                    }

                    if (candidates.Count == 0) continue;
                    OpenBetween(cells, w, x, y, candidates[rng.Next(candidates.Count)]);
                }
            }
        }

        /// <summary>
        /// Opens a symmetric passage between a cell and its neighbour in a direction.
        /// </summary>
        /// <param name="cells">Cell array being modified.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="x">Cell X coordinate.</param>
        /// <param name="y">Cell Y coordinate.</param>
        /// <param name="dir">Direction to open.</param>
        private static void OpenBetween(MazeCell[] cells, int w, int x, int y, Direction dir)
        {
            Vector2Int d = Directions.Delta(dir);
            cells[Index(x, y, w)].Open(dir);
            cells[Index(x + d.x, y + d.y, w)].Open(Directions.Opposite(dir));
        }

        /// <summary>
        /// Row-major flat index for a grid coordinate.
        /// </summary>
        /// <param name="x">Cell X coordinate.</param>
        /// <param name="y">Cell Y coordinate.</param>
        /// <param name="width">Grid width.</param>
        /// <returns>The flat array index.</returns>
        private static int Index(int x, int y, int width) => y * width + x;
    }
}
