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

            Vector2Int[] stairs = ChooseStairs(w, h, spawn, settings.StairCount, rng);
            return new MazeLayout(w, h, cells, spawn, stairs, settings.CellSize);
        }

        /// <summary>
        /// Picks the cells that get a stairwell down. Two properties matter and neither survives
        /// picking at random: a stairwell must be far enough from the spawn that the floor is not
        /// over the moment it starts, and the stairwells must be far enough from each other that they
        /// cover different parts of the grid instead of clustering into one shortcut.
        /// </summary>
        /// <remarks>
        /// The spacing requirement is relaxed in steps rather than enforced absolutely, because a
        /// small grid cannot satisfy it and the generator must still return the requested count. In
        /// the worst case the last pass accepts anything that is not the spawn.
        /// </remarks>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="spawn">Cell the player starts in.</param>
        /// <param name="count">How many stairwells to place.</param>
        /// <param name="rng">Seeded generator, so a floor's stairs land in the same cells every time.</param>
        /// <returns>The chosen cells, at least one and at most <paramref name="count"/>.</returns>
        private static Vector2Int[] ChooseStairs(int w, int h, Vector2Int spawn, int count,
            System.Random rng)
        {
            count = Mathf.Clamp(count, 1, w * h - 1);
            float span = Mathf.Max(w, h);

            var candidates = new List<Vector2Int>(w * h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x != spawn.x || y != spawn.y) candidates.Add(new Vector2Int(x, y));
                }
            }

            Shuffle(candidates, rng);

            var chosen = new List<Vector2Int>(count);
            foreach (float relax in Relaxations)
            {
                float minFromSpawn = span * 0.5f * relax;
                float minApart = span * 0.45f * relax;

                foreach (Vector2Int cell in candidates)
                {
                    if (chosen.Count == count) break;
                    if (chosen.Contains(cell)) continue;
                    if (Vector2Int.Distance(cell, spawn) < minFromSpawn) continue;
                    if (!FarFromAll(cell, chosen, minApart)) continue;
                    chosen.Add(cell);
                }

                if (chosen.Count == count) break;
            }

            return chosen.ToArray();
        }

        /// <summary>
        /// Spacing multipliers tried in turn when placing stairwells, from the full requirement down
        /// to none at all.
        /// </summary>
        private static readonly float[] Relaxations = { 1f, 0.7f, 0.45f, 0.2f, 0f };

        /// <summary>
        /// Whether a cell is at least a given distance from every cell already chosen.
        /// </summary>
        /// <param name="cell">Cell under consideration.</param>
        /// <param name="chosen">Cells already accepted.</param>
        /// <param name="minDistance">Required separation in cells.</param>
        /// <returns><c>true</c> if the cell clears them all.</returns>
        private static bool FarFromAll(Vector2Int cell, List<Vector2Int> chosen, float minDistance)
        {
            foreach (Vector2Int other in chosen)
            {
                if (Vector2Int.Distance(cell, other) < minDistance) return false;
            }

            return true;
        }

        /// <summary>
        /// Shuffles a list in place with a seeded generator, so the order is random but reproducible.
        /// </summary>
        /// <param name="items">List to shuffle.</param>
        /// <param name="rng">Seeded generator.</param>
        private static void Shuffle(List<Vector2Int> items, System.Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
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
