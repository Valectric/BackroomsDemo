using System.Collections.Generic;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for wall-run planning — the model furniture placement is laid along.
    /// A run that crosses a wall, spans an opening, or stops short of the wall's real extent puts
    /// furniture inside geometry or leaves a wall bare, and neither is visible in a screenshot taken
    /// from above. These assert the run structure directly instead.
    /// </summary>
    public class WallRunTests
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
        /// Every cell a run claims must genuinely have a wall on that side. A run that overruns the
        /// wall would hang a bookcase across an open doorway.
        /// </summary>
        [Test]
        public void EveryCellInARun_HasAWallOnThatSide()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 6; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed));
                foreach (WallRun run in facade.PlanWallRuns(layout))
                {
                    for (int i = 0; i < run.Cells; i++)
                    {
                        Vector2Int cell = run.CellAt(i);
                        Assert.IsFalse(layout.CanMove(cell.x, cell.y, run.Side),
                            $"seed {seed}: run claims {cell} on its {run.Side} side but that side is open");
                    }
                }
            }
        }

        /// <summary>
        /// Consecutive cells of a run must be mutually reachable. A run that spans a closed edge
        /// crosses a perpendicular wall, and a piece laid across the join grows through it into the
        /// room next door.
        /// </summary>
        [Test]
        public void ARun_NeverCrossesAPerpendicularWall()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 6; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed));
                foreach (WallRun run in facade.PlanWallRuns(layout))
                {
                    Direction along = run.AlongX ? Direction.East : Direction.North;
                    for (int i = 0; i < run.Cells - 1; i++)
                    {
                        Vector2Int cell = run.CellAt(i);
                        Assert.IsTrue(layout.CanMove(cell.x, cell.y, along),
                            $"seed {seed}: run spans {cell} to the next cell through a wall");
                    }
                }
            }
        }

        /// <summary>
        /// Runs must be maximal: the cell just past either end must fail to qualify. A run that stops
        /// early would split one wall into two, and each half would then be inset for a doorway that
        /// is not there.
        /// </summary>
        [Test]
        public void Runs_AreMaximal()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed: 7));

            foreach (WallRun run in facade.PlanWallRuns(layout))
            {
                Direction along = run.AlongX ? Direction.East : Direction.North;
                AssertCannotExtend(layout, run, run.CellAt(run.Cells - 1), along);
                AssertCannotExtend(layout, run, run.CellAt(0), Directions.Opposite(along));
            }
        }

        /// <summary>
        /// Asserts a run cannot be extended one cell further in a direction.
        /// </summary>
        /// <param name="layout">The maze the run came from.</param>
        /// <param name="run">The run under test.</param>
        /// <param name="end">The end cell of the run.</param>
        /// <param name="outward">Direction pointing off that end.</param>
        private static void AssertCannotExtend(MazeLayout layout, WallRun run, Vector2Int end,
            Direction outward)
        {
            if (!layout.CanMove(end.x, end.y, outward)) return;
            Vector2Int next = end + Directions.Delta(outward);
            Assert.IsTrue(layout.CanMove(next.x, next.y, run.Side),
                $"run on the {run.Side} side ending at {end} could have included {next}");
        }

        /// <summary>
        /// A run's world extent must lie on its wall plane, start on a cell boundary and measure one
        /// cell size per cell claimed. Furniture is positioned from these numbers, so an error here
        /// puts every piece on the run inside the wall.
        /// </summary>
        [Test]
        public void RunGeometry_MatchesTheCellsItClaims()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(10, 14, seed: 41, cellSize: 4f));
            float cellSize = layout.CellSize;

            foreach (WallRun run in facade.PlanWallRuns(layout))
            {
                Assert.AreEqual(run.Cells * cellSize, run.Length, 1e-4f, "one cell size per cell");
                Assert.AreEqual(run.AlongX, run.Side == Direction.North || run.Side == Direction.South,
                    "north and south walls run along X, east and west along Z");

                // The wall plane sits on the far edge of the start cell for north/east sides and on
                // its near edge for south/west.
                Vector2Int d = Directions.Delta(run.Side);
                float expectedPlane = run.AlongX
                    ? (run.StartCell.y + Mathf.Max(0, d.y)) * cellSize
                    : (run.StartCell.x + Mathf.Max(0, d.x)) * cellSize;
                float actualPlane = run.AlongX ? run.Start.z : run.Start.x;
                Assert.AreEqual(expectedPlane, actualPlane, 1e-4f, "run sits on its wall plane");

                float expectedStart = (run.AlongX ? run.StartCell.x : run.StartCell.y) * cellSize;
                float actualStart = run.AlongX ? run.Start.x : run.Start.z;
                Assert.AreEqual(expectedStart, actualStart, 1e-4f, "run starts on a cell boundary");
                Assert.AreEqual(0f, run.Start.y, 1e-4f, "runs are measured at floor level");
            }
        }

        /// <summary>
        /// A run's into-room normal must point away from its wall, so furniture turned to face along
        /// it ends up looking into the room rather than into the wall.
        /// </summary>
        [Test]
        public void IntoRoom_PointsAwayFromTheWall()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(9, 9, seed: 5));

            foreach (WallRun run in facade.PlanWallRuns(layout))
            {
                Vector3 centre = layout.CellCenterToWorld(run.CellAt(0));
                Vector3 wallPoint = run.PointAt(run.Length * 0.5f);
                Assert.Greater(Vector3.Dot(centre - wallPoint, run.IntoRoom), 0f,
                    $"the {run.Side} run at {run.StartCell} points its normal into the wall");
                Assert.AreEqual(0f, Vector3.Dot(run.IntoRoom, run.Along), 1e-4f,
                    "the normal must be perpendicular to the run");
            }
        }

        /// <summary>
        /// A doorway flag must mean the wall line really is broken by an opening the player can walk
        /// through from this side; a run ending at a corner or the grid edge must not claim one.
        /// Furniture is inset 1.2m wherever the flag is set, so a false positive strips a metre of
        /// dressing off a wall that had no doorway.
        /// </summary>
        [Test]
        public void DoorwayFlags_MarkOnlyRealOpenings()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 4; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed));
                foreach (WallRun run in facade.PlanWallRuns(layout))
                {
                    Direction along = run.AlongX ? Direction.East : Direction.North;
                    AssertDoorwayFlag(layout, run, run.CellAt(run.Cells - 1), along, run.DoorwayAtEnd);
                    AssertDoorwayFlag(layout, run, run.CellAt(0), Directions.Opposite(along),
                        run.DoorwayAtStart);
                }
            }
        }

        /// <summary>
        /// Asserts one end's doorway flag against the layout it was derived from.
        /// </summary>
        /// <param name="layout">The maze the run came from.</param>
        /// <param name="run">The run under test.</param>
        /// <param name="end">The end cell of the run.</param>
        /// <param name="outward">Direction pointing off that end.</param>
        /// <param name="flag">The flag the planner set.</param>
        private static void AssertDoorwayFlag(MazeLayout layout, WallRun run, Vector2Int end,
            Direction outward, bool flag)
        {
            bool reachable = layout.CanMove(end.x, end.y, outward);
            Vector2Int next = end + Directions.Delta(outward);
            bool expected = reachable && layout.CanMove(next.x, next.y, run.Side);
            Assert.AreEqual(expected, flag,
                $"doorway flag wrong off the {outward} end of the {run.Side} run at {end}");
        }

        /// <summary>
        /// Excluded cells — spawn and the stairs — must never appear in any run, so nothing is placed
        /// against the walls the player has to read at a glance.
        /// </summary>
        [Test]
        public void ExcludedCells_NeverAppearInARun()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed: 12));
            var excluded = new HashSet<Vector2Int> { layout.Spawn };
            foreach (Vector2Int stairs in layout.Stairs) excluded.Add(stairs);

            List<WallRun> runs = facade.PlanWallRuns(layout, excluded);
            Assert.IsNotEmpty(runs, "a braided maze should still have plenty of wall to dress");

            foreach (WallRun run in runs)
            {
                for (int i = 0; i < run.Cells; i++)
                {
                    Assert.IsFalse(excluded.Contains(run.CellAt(i)),
                        $"run includes reserved cell {run.CellAt(i)}");
                }
            }
        }

        /// <summary>
        /// Runs must be long enough on average to be worth laying furniture along. If nearly every
        /// run were a single cell the placement would collapse back onto the 4m lattice this replaced,
        /// which no assertion downstream would notice.
        /// </summary>
        [Test]
        public void Runs_AreLongEnoughToBeWorthDressing()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed: 3));
            List<WallRun> runs = facade.PlanWallRuns(layout);

            int cells = 0;
            int longRuns = 0;
            foreach (WallRun run in runs)
            {
                cells += run.Cells;
                if (run.Cells >= 3) longRuns++;
            }

            float mean = (float)cells / runs.Count;
            Assert.Greater(mean, 1.5f, $"mean run length {mean:F2} cells is barely better than per-cell");
            Assert.Greater(longRuns, runs.Count / 10,
                "a floor should have some walls long enough to carry several pieces");
        }

        /// <summary>
        /// Planning the same layout twice must produce identical runs — furniture placement is
        /// seeded off them, and a floor has to look the same every time it is built.
        /// </summary>
        [Test]
        public void Planning_IsDeterministic()
        {
            var facade = NewMaze();
            MazeLayout layout = facade.Generate(new MazeSettings(12, 12, seed: 77));

            List<WallRun> first = facade.PlanWallRuns(layout);
            List<WallRun> second = facade.PlanWallRuns(layout);

            Assert.AreEqual(first.Count, second.Count, "run count must be stable");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].StartCell, second[i].StartCell, $"run {i} start cell");
                Assert.AreEqual(first[i].Side, second[i].Side, $"run {i} side");
                Assert.AreEqual(first[i].Cells, second[i].Cells, $"run {i} length");
            }
        }
    }
}
