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
        /// Generates the maze layout for the given settings. Spawn is the bottom-left cell (0,0) and
        /// the exit is the top-right cell (Width-1, Height-1). Deterministic for a fixed seed and
        /// size.
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
            var exit = new Vector2Int(w - 1, h - 1);

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

            return new MazeLayout(w, h, cells, spawn, exit);
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
