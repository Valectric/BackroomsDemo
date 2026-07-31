using System.Collections.Generic;
using Backrooms.MazeManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for the deterministic Level-0 maze generator. They verify the two
    /// properties the rest of the game relies on — <b>determinism</b> (same seed → identical maze)
    /// and <b>connectivity</b> (a perfect maze where the exit, and every cell, is reachable from the
    /// spawn) — plus basic grid invariants. Connectivity is checked with an independent breadth-first
    /// search over the public layout, not by trusting the generator.
    /// </summary>
    public class MazeGenerationTests
    {
        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// Creates a fresh MazeManager module in the scene and returns its test facade, through which
        /// tests generate and inspect layouts.
        /// </summary>
        /// <returns>A test facade over a new MazeManager module.</returns>
        private static MazeManagerTestFacade NewMaze()
        {
            var go = new GameObject("MazeManager");
            var facade = go.AddComponent<MazeFacade>();
            return facade.GetTestFacade();
        }

        /// <summary>
        /// Breadth-first search from the spawn cell across open passages. Returns the set of reachable
        /// cell coordinates encoded as y * Width + x. This is the test's own, independent check of
        /// connectivity.
        /// </summary>
        /// <param name="layout">The layout to traverse.</param>
        /// <returns>The set of reachable cell indices.</returns>
        private static HashSet<int> ReachableFromSpawn(MazeLayout layout)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(layout.Spawn);
            seen.Add(layout.Spawn.y * layout.Width + layout.Spawn.x);

            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Direction dir in Directions.All)
                {
                    if (!layout.CanMove(c.x, c.y, dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    var n = new Vector2Int(c.x + d.x, c.y + d.y);
                    int key = n.y * layout.Width + n.x;
                    if (seen.Add(key)) queue.Enqueue(n);
                }
            }

            return seen;
        }

        /// <summary>
        /// Two layouts generated with the same seed and size must be cell-for-cell identical —
        /// generation is deterministic.
        /// </summary>
        [Test]
        public void SameSeed_ProducesIdenticalLayout()
        {
            var settings = new MazeSettings(16, 16, seed: 12345);
            MazeLayout a = NewMaze().Generate(settings);
            MazeLayout b = NewMaze().Generate(settings);

            Assert.AreEqual(a.Width, b.Width, "width must match");
            Assert.AreEqual(a.Height, b.Height, "height must match");
            Assert.AreEqual(a.Spawn, b.Spawn, "spawn must match");
            Assert.AreEqual(a.Exit, b.Exit, "exit must match");

            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    foreach (Direction dir in Directions.All)
                    {
                        Assert.AreEqual(
                            a.CellAt(x, y).IsOpen(dir),
                            b.CellAt(x, y).IsOpen(dir),
                            $"passage mismatch at ({x},{y}) {dir}");
                    }
                }
            }
        }

        /// <summary>
        /// Different seeds should produce different layouts (at least one passage differs). Guards
        /// against the seed being ignored.
        /// </summary>
        [Test]
        public void DifferentSeed_ProducesDifferentLayout()
        {
            MazeLayout a = NewMaze().Generate(new MazeSettings(16, 16, seed: 1));
            MazeLayout b = NewMaze().Generate(new MazeSettings(16, 16, seed: 2));

            bool anyDifference = false;
            for (int y = 0; y < a.Height && !anyDifference; y++)
            {
                for (int x = 0; x < a.Width && !anyDifference; x++)
                {
                    foreach (Direction dir in Directions.All)
                    {
                        if (a.CellAt(x, y).IsOpen(dir) != b.CellAt(x, y).IsOpen(dir))
                        {
                            anyDifference = true;
                            break;
                        }
                    }
                }
            }

            Assert.IsTrue(anyDifference, "different seeds should yield different mazes");
        }

        /// <summary>
        /// The exit must be reachable from the spawn for a spread of seeds — the core playability
        /// guarantee. Verified by independent BFS.
        /// </summary>
        [Test]
        public void ExitReachableFromSpawn_ForManySeeds()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                MazeLayout layout = NewMaze().Generate(new MazeSettings(16, 16, seed));
                HashSet<int> reachable = ReachableFromSpawn(layout);
                int exitKey = layout.Exit.y * layout.Width + layout.Exit.x;
                Assert.IsTrue(reachable.Contains(exitKey), $"exit unreachable for seed {seed}");
            }
        }

        /// <summary>
        /// The generator produces a perfect maze, so every cell must be reachable from the spawn.
        /// </summary>
        [Test]
        public void AllCells_ReachableFromSpawn()
        {
            MazeLayout layout = NewMaze().Generate(new MazeSettings(20, 12, seed: 777));
            HashSet<int> reachable = ReachableFromSpawn(layout);
            Assert.AreEqual(layout.Width * layout.Height, reachable.Count,
                "every cell should be reachable in a perfect maze");
        }

        /// <summary>
        /// The layout dimensions match the settings, and spawn/exit are in bounds and distinct.
        /// </summary>
        [Test]
        public void Layout_DimensionsAndEndpoints_AreValid()
        {
            var settings = new MazeSettings(24, 18, seed: 42);
            MazeLayout layout = NewMaze().Generate(settings);

            Assert.AreEqual(24, layout.Width, "width matches settings");
            Assert.AreEqual(18, layout.Height, "height matches settings");
            Assert.IsTrue(layout.InBounds(layout.Spawn.x, layout.Spawn.y), "spawn in bounds");
            Assert.IsTrue(layout.InBounds(layout.Exit.x, layout.Exit.y), "exit in bounds");
            Assert.AreNotEqual(layout.Spawn, layout.Exit, "spawn and exit must differ");
        }

        /// <summary>
        /// A floor must be a network with loops, not a tree of one-way corridors. Dead ends are what
        /// make a maze tedious — every wrong turn forces you to retrace it — so only a small share of
        /// cells may be dead ends.
        /// </summary>
        [Test]
        public void Floors_HaveFewDeadEnds()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                MazeLayout layout = NewMaze().Generate(new MazeSettings(12, 12, seed));

                int deadEnds = 0;
                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        int open = 0;
                        foreach (Direction dir in Directions.All)
                        {
                            if (layout.CanMove(x, y, dir)) open++;
                        }

                        if (open == 1) deadEnds++;
                    }
                }

                float fraction = deadEnds / (float)(layout.Width * layout.Height);
                MooseRunnerFacade.Log($"seed {seed}: {deadEnds} dead ends ({fraction:P0})");
                Assert.Less(fraction, 0.12f, $"seed {seed}: too many dead ends for a braided floor");
            }
        }

        /// <summary>
        /// Loops exist: a braided floor has more passages than the cell count minus one, which is the
        /// exact number a tree would have. More passages than that means at least one cycle.
        /// </summary>
        [Test]
        public void Floors_ContainLoops()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                MazeLayout layout = NewMaze().Generate(new MazeSettings(12, 12, seed));

                int passages = 0;
                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        if (layout.CanMove(x, y, Direction.North)) passages++;
                        if (layout.CanMove(x, y, Direction.East)) passages++;
                    }
                }

                int treePassages = layout.Width * layout.Height - 1;
                Assert.Greater(passages, treePassages,
                    $"seed {seed}: a floor with no extra passages has no loops at all");
            }
        }

        /// <summary>
        /// Open rooms exist, so a floor is not wall-to-wall corridor. Verified by finding at least one
        /// 3×3 block of cells with no walls between any of them.
        /// </summary>
        [Test]
        public void Floors_ContainOpenRooms()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                MazeLayout layout = NewMaze().Generate(new MazeSettings(14, 14, seed));
                Assert.IsTrue(HasOpenBlock(layout, 3), $"seed {seed}: no 3x3 open room found");
            }
        }

        /// <summary>
        /// Whether the layout contains a square block of the given size with every internal wall open.
        /// </summary>
        /// <param name="layout">Layout to search.</param>
        /// <param name="size">Block side length in cells.</param>
        /// <returns><c>true</c> if such a block exists.</returns>
        private static bool HasOpenBlock(MazeLayout layout, int size)
        {
            for (int y = 0; y + size <= layout.Height; y++)
            {
                for (int x = 0; x + size <= layout.Width; x++)
                {
                    bool allOpen = true;
                    for (int dy = 0; dy < size && allOpen; dy++)
                    {
                        for (int dx = 0; dx < size && allOpen; dx++)
                        {
                            if (dx + 1 < size && !layout.CanMove(x + dx, y + dy, Direction.East))
                                allOpen = false;
                            if (dy + 1 < size && !layout.CanMove(x + dx, y + dy, Direction.North))
                                allOpen = false;
                        }
                    }

                    if (allOpen) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Undersized settings are clamped to a minimum 2×2 grid rather than producing an empty maze.
        /// </summary>
        [Test]
        public void Settings_ClampTinyDimensions()
        {
            MazeLayout layout = NewMaze().Generate(new MazeSettings(0, 1, seed: 5));
            Assert.GreaterOrEqual(layout.Width, 2, "width clamped to >= 2");
            Assert.GreaterOrEqual(layout.Height, 2, "height clamped to >= 2");
            HashSet<int> reachable = ReachableFromSpawn(layout);
            Assert.AreEqual(layout.Width * layout.Height, reachable.Count, "clamped maze still connected");
        }
    }
}
