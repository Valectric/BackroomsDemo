using System.Collections.Generic;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// Measures how far a player can end up from the nearest way down. Stairwell placement enforces
    /// a minimum separation between stairwells, which is not the same thing as covering the floor —
    /// three well-spaced stairwells can still leave a whole quadrant stranded.
    /// </summary>
    /// <remarks>
    /// Separation and coverage are different properties, and only one of them was being enforced. The
    /// walk a player actually faces is the <i>worst</i> distance to the <i>nearest</i> stairwell, over
    /// every cell they might be standing in, measured through the maze rather than across it.
    /// </remarks>
    public class StairsCoverageTests
    {
        /// <summary>Floor size the game ships.</summary>
        private const int FloorCells = 24;

        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// Creates a fresh MazeManager module and returns its test facade.
        /// </summary>
        /// <returns>A test facade over a new MazeManager module.</returns>
        private static MazeManagerTestFacade NewMaze()
        {
            var go = new GameObject("MazeManager");
            return go.AddComponent<MazeFacade>().GetTestFacade();
        }

        /// <summary>
        /// Floors must differ in proportion, not only in palette: their own ceiling height and their
        /// own room sizes.
        /// </summary>
        /// <remarks>
        /// Both reviewers landed on this independently — every floor being the same 3m box with a
        /// different colour made the palette read as a filter over one room rather than as a
        /// different place. Height is the strongest signal available, so it is the one asserted.
        /// </remarks>
        [Test]
        public void EveryFloorTheme_HasItsOwnProportions()
        {
            var heights = new HashSet<float>();

            for (int floor = 1; floor <= 5; floor++)
            {
                FloorTheme theme = FloorThemes.ForFloor(floor);
                heights.Add(theme.CeilingHeight);

                Assert.Greater(theme.CeilingHeight, 2f,
                    $"{theme.Name}: below 2m the player cannot stand up in it");
                Assert.Less(theme.CeilingHeight, 9f,
                    $"{theme.Name}: above 9m the ceiling lights stop reaching the floor");
                Assert.GreaterOrEqual(theme.RoomMaxSize, theme.RoomMinSize,
                    $"{theme.Name}: room size range is inverted");
                Assert.Greater(theme.RoomCount, 0, $"{theme.Name}: a floor of pure corridor");
            }

            Assert.GreaterOrEqual(heights.Count, 4,
                "five floors sharing one or two ceiling heights is a palette swap, not five places");

            // The two that are meant to feel big must actually be bigger than the two meant to feel
            // tight, or the whole point of the change is lost while the test still passes.
            float mall = FloorThemes.ForFloor(2).CeilingHeight;
            float carnival = FloorThemes.ForFloor(4).CeilingHeight;
            float office = FloorThemes.ForFloor(1).CeilingHeight;
            float laundromat = FloorThemes.ForFloor(3).CeilingHeight;

            MooseRunnerFacade.Log(
                $"ceilings — office {office}m, mall {mall}m, laundromat {laundromat}m, "
                + $"carnival {carnival}m, asylum {FloorThemes.ForFloor(5).CeilingHeight}m");

            Assert.Greater(mall, office * 1.5f, "the mall should read as an atrium");
            Assert.Greater(carnival, laundromat * 2f, "the carnival should dwarf the laundromat");
        }

        /// <summary>
        /// The first floor must carry no ways up, because there is nothing above it to climb to,
        /// while every floor below it emerges from one.
        /// </summary>
        /// <remarks>
        /// A staircase that climbs to nowhere is worse than no staircase: the player walks all the
        /// way to it and it does nothing. Suppressing it at the layout is what makes the riser, the
        /// hole in the ceiling, the reserved cell and the nearest-way-up search all agree — so this
        /// asserts the layout, which is the single thing they all read.
        /// </remarks>
        [Test]
        public void TheFirstFloor_CarriesNoWaysUp()
        {
            MazeManagerTestFacade maze = NewMaze();

            for (int seed = 0; seed < 10; seed++)
            {
                MazeLayout ground = maze.Generate(new MazeSettings(FloorCells, FloorCells, seed)
                {
                    HasFloorAbove = false
                });
                MazeLayout below = maze.Generate(new MazeSettings(FloorCells, FloorCells, seed));

                Assert.AreEqual(0, ground.StairsUp.Count,
                    $"seed {seed}: the first floor must show no way up");
                Assert.Greater(ground.Stairs.Count, 0,
                    $"seed {seed}: it must still carry ways down");
                Assert.Greater(below.StairsUp.Count, 0,
                    $"seed {seed}: every floor below emerges from a way up");
                Assert.AreEqual(below.Spawn, ground.Spawn,
                    $"seed {seed}: removing the risers must not move where the player arrives");
            }
        }

        /// <summary>
        /// Walking distance in cells from every cell to the nearest stairwell, by breadth-first
        /// search outward from all stairwells at once.
        /// </summary>
        /// <param name="layout">The floor to measure.</param>
        /// <returns>Distance per cell, indexed y * width + x; -1 where unreachable.</returns>
        private static int[] DistanceToNearestStairs(MazeLayout layout)
        {
            var distance = new int[layout.Width * layout.Height];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;

            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int stairs in layout.Stairs)
            {
                distance[stairs.y * layout.Width + stairs.x] = 0;
                queue.Enqueue(stairs);
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
                    if (distance[index] != -1) continue;

                    distance[index] = here + 1;
                    queue.Enqueue(next);
                }
            }

            return distance;
        }

        /// <summary>
        /// The worst walk to a way down, over every cell of a floor.
        /// </summary>
        /// <param name="layout">The floor to measure.</param>
        /// <returns>The largest distance-to-nearest-stairwell, in cells.</returns>
        private static int WorstWalk(MazeLayout layout)
        {
            int worst = 0;
            foreach (int d in DistanceToNearestStairs(layout)) worst = Mathf.Max(worst, d);
            return worst;
        }

        /// <summary>
        /// No cell on a floor may be an unreasonable walk from the nearest way down. Three stairwells
        /// exist so that one is usually close; a floor where a quarter of the cells are 30-plus cells
        /// of walking from any of them delivers the cost of a big level with none of the benefit.
        /// </summary>
        [Test]
        public void NoCell_IsAnUnreasonableWalkFromAWayDown()
        {
            var facade = NewMaze();
            int worstSeen = 0;
            int worstSeed = 0;
            float total = 0f;

            const int seeds = 30;
            for (int seed = 0; seed < seeds; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));
                int worst = WorstWalk(layout);
                total += worst;
                if (worst <= worstSeen) continue;
                worstSeen = worst;
                worstSeed = seed;
            }

            MooseRunnerFacade.Log(
                $"worst walk to a way down: {worstSeen} cells (seed {worstSeed}); "
                + $"mean across {seeds} seeds {total / seeds:F1} cells");

            // Measured after the placement fix: worst 31 cells, mean 24.4 across these 30 seeds,
            // against worst 47 and mean 33.4 for the separation rule this replaced. The bar sits just
            // above the measured worst so it catches a regression towards the old behaviour — every
            // strategy that measured worse during that work landed at 33 or above.
            Assert.LessOrEqual(worstSeen, 33,
                $"seed {worstSeed} strands a cell {worstSeen} cells ({worstSeen * 4}m) from any stairwell");
        }

        /// <summary>
        /// The nearest way down must never sit on the player's doorstep. Optimising purely for
        /// coverage will happily put one two cells from where the player arrives, which ends the
        /// floor before it starts and removes the Dwellers from the game — the mirror image of the
        /// long-walk bug, and the reason the encounter rate collapsed when coverage was first fixed.
        /// </summary>
        [Test]
        public void TheNearestWayDown_IsNeverOnTheDoorstep()
        {
            var facade = NewMaze();
            int closestSeen = int.MaxValue;
            int closestSeed = 0;
            float total = 0f;

            const int seeds = 30;
            for (int seed = 0; seed < seeds; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));

                // Distance from spawn to whichever stairwell is nearest, through the maze.
                int[] fromStairs = DistanceToNearestStairs(layout);
                int atSpawn = fromStairs[layout.Spawn.y * layout.Width + layout.Spawn.x];
                total += atSpawn;

                if (atSpawn >= closestSeen) continue;
                closestSeen = atSpawn;
                closestSeed = seed;
            }

            MooseRunnerFacade.Log(
                $"shortest spawn-to-stairs walk: {closestSeen} cells (seed {closestSeed}); "
                + $"mean {total / seeds:F1} cells");

            Assert.GreaterOrEqual(closestSeen, 10,
                $"seed {closestSeed} puts a way down {closestSeen} cells from the spawn");
        }

        /// <summary>
        /// A floor must carry as many ways up as ways down, and the player must arrive out of one of
        /// the ways up. That is what makes descending read as moving through a building rather than
        /// as a teleport: you came down a staircase, so there is a staircase behind you.
        /// </summary>
        [Test]
        public void EveryFloor_HasEqualStairsAndSpawnsAtAWayUp()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 20; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));

                Assert.AreEqual(layout.Stairs.Count, layout.StairsUp.Count,
                    $"seed {seed}: as many ways up as down");
                Assert.IsTrue(layout.IsStairsUp(layout.Spawn),
                    $"seed {seed}: the player must arrive out of a way up");
            }
        }

        /// <summary>
        /// A cell may not be both a way up and a way down. One hole in the floor and one in the
        /// ceiling of the same cell would leave the player standing in mid-air over a pit.
        /// </summary>
        [Test]
        public void NoCell_IsBothAWayUpAndAWayDown()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 20; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));

                foreach (Vector2Int up in layout.StairsUp)
                {
                    Assert.IsFalse(layout.IsStairs(up),
                        $"seed {seed}: cell {up} is both a way up and a way down");
                }
            }
        }

        /// <summary>
        /// The ways up must be spread over the floor rather than clustered, since the player emerges
        /// from a different one each run and every one of them should open onto a different part of
        /// the level.
        /// </summary>
        [Test]
        public void TheWaysUp_AreSpreadAcrossTheFloor()
        {
            var facade = NewMaze();
            int closestSeen = int.MaxValue;

            for (int seed = 0; seed < 20; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));

                for (int i = 0; i < layout.StairsUp.Count; i++)
                {
                    for (int j = i + 1; j < layout.StairsUp.Count; j++)
                    {
                        int apart = Mathf.Abs(layout.StairsUp[i].x - layout.StairsUp[j].x)
                                    + Mathf.Abs(layout.StairsUp[i].y - layout.StairsUp[j].y);
                        closestSeen = Mathf.Min(closestSeen, apart);
                    }
                }
            }

            MooseRunnerFacade.Log($"closest pair of ways up across 20 seeds: {closestSeen} cells");
            Assert.Greater(closestSeen, 4, "two ways up landed almost on top of each other");
        }

        /// <summary>
        /// Every stairwell must be reachable, and the floor must carry the number it was asked for.
        /// </summary>
        [Test]
        public void EveryStairwell_IsReachableFromEverywhere()
        {
            var facade = NewMaze();
            for (int seed = 0; seed < 10; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));
                Assert.AreEqual(3, layout.Stairs.Count, $"seed {seed}: three ways down");

                foreach (int d in DistanceToNearestStairs(layout))
                {
                    Assert.GreaterOrEqual(d, 0, $"seed {seed}: a cell cannot reach any stairwell");
                }
            }
        }

        /// <summary>
        /// The three stairwells must not all cluster into one part of the grid. Measured as the
        /// spread of their positions: if all three sit in the same half on both axes, one corner of
        /// the floor is necessarily stranded however far apart they are from each other.
        /// </summary>
        [Test]
        public void Stairwells_DoNotAllSitInTheSameHalfOfTheFloor()
        {
            var facade = NewMaze();
            int lopsided = 0;

            const int seeds = 30;
            for (int seed = 0; seed < seeds; seed++)
            {
                MazeLayout layout = facade.Generate(new MazeSettings(FloorCells, FloorCells, seed));
                int half = layout.Width / 2;

                bool allLeft = true, allRight = true, allLow = true, allHigh = true;
                foreach (Vector2Int s in layout.Stairs)
                {
                    if (s.x >= half) allLeft = false;
                    if (s.x < half) allRight = false;
                    if (s.y >= half) allLow = false;
                    if (s.y < half) allHigh = false;
                }

                if (allLeft || allRight || allLow || allHigh) lopsided++;
            }

            float rate = (float)lopsided / seeds;
            MooseRunnerFacade.Log($"floors with all three stairwells in one half: {rate:P0}");
            Assert.Less(rate, 0.25f,
                $"{rate:P0} of floors put every way down in the same half of the grid");
        }
    }
}
