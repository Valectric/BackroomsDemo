using System.Collections.Generic;
using Backrooms.MazeManager;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for maze wall planning and the world-space coordinate helpers. Wall
    /// planning is verified against an exact combinatorial invariant rather than a golden file, so
    /// the tests stay meaningful for any seed or grid size.
    /// </summary>
    public class MazeGeometryTests
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
        /// Creates a fresh MazeManager module in the scene and returns its test facade.
        /// </summary>
        /// <returns>A test facade over a new MazeManager module.</returns>
        private static MazeManagerTestFacade NewMaze()
        {
            var go = new GameObject("MazeManager");
            var facade = go.AddComponent<MazeFacade>();
            return facade.GetTestFacade();
        }

        /// <summary>
        /// A perfect maze of W×H cells would have exactly W*H + W + H + 1 wall panels. Rooms and
        /// braiding only ever remove walls, so the real count must sit at or below that ceiling while
        /// staying above the bare outer boundary — proving the extra passages were carved and that
        /// the planner never invents walls.
        /// </summary>
        [Test]
        public void WallCount_StaysBetweenBoundaryAndPerfectMaze()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 10; seed++)
            {
                var settings = new MazeSettings(12, 9, seed);
                MazeLayout layout = facade.Generate(settings);
                List<WallSegment> walls = facade.PlanWalls(layout);

                int perfectMaze = layout.Width * layout.Height + layout.Width + layout.Height + 1;
                int boundaryOnly = 2 * layout.Width + 2 * layout.Height;

                Assert.LessOrEqual(walls.Count, perfectMaze,
                    $"seed {seed}: carving may only remove walls, never add them");
                Assert.Greater(walls.Count, boundaryOnly,
                    $"seed {seed}: the floor should still have interior walls");
                Assert.Less(walls.Count, perfectMaze,
                    $"seed {seed}: rooms and loops should have opened some walls");
            }
        }

        /// <summary>
        /// Every planned wall must sit on a cell boundary — its centre lies on a multiple of the cell
        /// size along the axis it blocks — and must be within the grid bounds.
        /// </summary>
        [Test]
        public void Walls_AlignToCellBoundaries_AndStayInBounds()
        {
            var facade = NewMaze();
            var settings = new MazeSettings(8, 8, seed: 99, cellSize: 4f);
            MazeLayout layout = facade.Generate(settings);
            List<WallSegment> walls = facade.PlanWalls(layout);

            float maxX = layout.Width * layout.CellSize;
            float maxZ = layout.Height * layout.CellSize;

            foreach (WallSegment w in walls)
            {
                // The coordinate the wall blocks must land exactly on a cell boundary.
                float blocking = w.AlongX ? w.Center.z : w.Center.x;
                float onGrid = blocking / layout.CellSize;
                Assert.AreEqual(Mathf.Round(onGrid), onGrid, 1e-4f,
                    "wall must sit on a cell boundary");

                Assert.AreEqual(layout.CellSize, w.Length, 1e-4f, "wall spans exactly one cell");
                Assert.GreaterOrEqual(w.Center.x, -1e-4f, "wall inside grid (x min)");
                Assert.LessOrEqual(w.Center.x, maxX + 1e-4f, "wall inside grid (x max)");
                Assert.GreaterOrEqual(w.Center.z, -1e-4f, "wall inside grid (z min)");
                Assert.LessOrEqual(w.Center.z, maxZ + 1e-4f, "wall inside grid (z max)");
            }
        }

        /// <summary>
        /// No two planned walls may occupy the same position and orientation — a duplicate would mean
        /// a shared edge was emitted twice, producing z-fighting geometry.
        /// </summary>
        [Test]
        public void Walls_ContainNoDuplicates()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(10, 10, seed: 321));
            List<WallSegment> walls = facade.PlanWalls(layout);

            var seen = new HashSet<string>();
            foreach (WallSegment w in walls)
            {
                string key = $"{w.Center.x:F3}|{w.Center.z:F3}|{w.AlongX}";
                Assert.IsTrue(seen.Add(key), $"duplicate wall at {key}");
            }
        }

        /// <summary>
        /// Where a passage is open between two cells there must be no wall on that shared edge, and
        /// where it is closed there must be exactly one. This ties geometry directly to walkability.
        /// </summary>
        [Test]
        public void OpenPassages_HaveNoWall_ClosedPassages_Do()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(8, 8, seed: 2024, cellSize: 4f));
            List<WallSegment> walls = facade.PlanWalls(layout);

            var wallKeys = new HashSet<string>();
            foreach (WallSegment w in walls)
            {
                wallKeys.Add($"{w.Center.x:F3}|{w.Center.z:F3}|{w.AlongX}");
            }

            float half = layout.CellSize * 0.5f;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    float cx = x * layout.CellSize + half;
                    float cz = y * layout.CellSize + half;
                    MazeCell cell = layout.CellAt(x, y);

                    string northKey = $"{cx:F3}|{cz + half:F3}|True";
                    Assert.AreEqual(!cell.NorthOpen, wallKeys.Contains(northKey),
                        $"north wall mismatch at ({x},{y})");

                    string eastKey = $"{cx + half:F3}|{cz:F3}|False";
                    Assert.AreEqual(!cell.EastOpen, wallKeys.Contains(eastKey),
                        $"east wall mismatch at ({x},{y})");
                }
            }
        }

        /// <summary>
        /// Converting a cell to a world position and back must return the original cell, for every
        /// cell in the grid.
        /// </summary>
        [Test]
        public void CellToWorld_RoundTrips_ForEveryCell()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(11, 7, seed: 8, cellSize: 4f));

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Vector3 world = layout.CellCenterToWorld(cell);
                    Assert.AreEqual(cell, layout.WorldToCell(world), $"round-trip failed at ({x},{y})");
                }
            }
        }

        /// <summary>
        /// The spawn and exit world positions must sit inside the grid footprint and be distinct.
        /// </summary>
        [Test]
        public void SpawnAndExit_WorldPositions_AreDistinctAndInsideGrid()
        {
            var go = new GameObject("MazeManager");
            var facade = go.AddComponent<MazeFacade>();
            MazeLayout layout = facade.Generate(new MazeSettings(9, 9, seed: 3, cellSize: 4f));

            Vector3 spawn = facade.GetSpawnPosition();
            Vector3 exit = facade.GetExitPosition();

            Assert.AreNotEqual(spawn, exit, "spawn and exit must differ");
            float maxX = layout.Width * layout.CellSize;
            float maxZ = layout.Height * layout.CellSize;
            foreach (Vector3 p in new[] { spawn, exit })
            {
                Assert.Greater(p.x, 0f, "inside grid (x)");
                Assert.Less(p.x, maxX, "inside grid (x)");
                Assert.Greater(p.z, 0f, "inside grid (z)");
                Assert.Less(p.z, maxZ, "inside grid (z)");
            }
        }
    }
}
