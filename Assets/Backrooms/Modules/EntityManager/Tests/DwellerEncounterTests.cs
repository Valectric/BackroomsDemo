using System.Collections.Generic;
using Backrooms.MazeManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.EntityManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests that measure whether a player crossing a real floor actually runs
    /// into a Dweller.
    /// </summary>
    /// <remarks>
    /// This exists because "the Dweller works" was true of every unit test while the game shipped a
    /// floor you could cross without ever seeing one. Pathing, state transitions and catching were
    /// all individually correct; what was wrong was the encounter *rate*, which no test that places a
    /// Dweller next to the player can observe. These simulate the whole crossing on the grid — no
    /// rendering, so a couple of hundred runs cost less than a second — and assert on how often the
    /// player is found.
    /// </remarks>
    public class DwellerEncounterTests
    {
        /// <summary>Floor size the game actually ships.</summary>
        private const int FloorCells = 24;

        /// <summary>
        /// The tuning the game ships, mirrored here so this measures what players get. Keep these
        /// in step with <c>GameplayController.cellsPerDweller</c> and the defaults on
        /// <c>DwellerFacade</c>; a sweep of the three showed sense range dominates and Dweller
        /// count barely matters, so tune that first.
        /// </summary>
        private const int ShippingDwellers = 3;

        /// <summary>Sense range the game ships, in cells.</summary>
        private const int ShippingSense = 12;

        /// <summary>Patrol span the game ships on a 24-cell floor, in cells.</summary>
        private const int ShippingPatrolSpan = 18;

        /// <summary>
        /// Base Dweller cells travelled per player cell: 2.2 m/s against a 3.2 m/s walk. The player is
        /// assumed to walk, never sprint, and never to stop — the shortest possible exposure. Each
        /// kind then scales this by its own speed.
        /// </summary>
        private const float DwellerPace = 2.2f / 3.2f;

        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// Creates a Dweller and returns its test seam.
        /// </summary>
        /// <param name="name">Object name, so a failure says which Dweller.</param>
        /// <returns>A test facade over a new Dweller.</returns>
        private static DwellerManagerTestFacade NewDweller(string name)
        {
            var go = new GameObject(name);
            return go.AddComponent<DwellerFacade>().GetTestFacade();
        }

        /// <summary>
        /// Generates a floor the size the game ships.
        /// </summary>
        /// <param name="seed">Generation seed.</param>
        /// <returns>The layout.</returns>
        private static MazeLayout NewFloor(int seed)
        {
            var go = new GameObject("MazeManager");
            return go.AddComponent<MazeFacade>()
                .GetTestFacade()
                .Generate(new MazeSettings(FloorCells, FloorCells, seed));
        }

        /// <summary>
        /// The start cells the gameplay layer hands out, for the given number of Dwellers.
        /// </summary>
        /// <param name="layout">The floor.</param>
        /// <param name="count">How many Dwellers.</param>
        /// <returns>One start cell each.</returns>
        private static List<Vector2Int> Starts(MazeLayout layout, int count)
        {
            int right = layout.Width - 1;
            int top = layout.Height - 1;
            var all = new List<Vector2Int>
            {
                new Vector2Int(right, top),
                new Vector2Int(0, top),
                new Vector2Int(right, 0),
                new Vector2Int(right / 2, top)
            };

            var starts = new List<Vector2Int>(count);
            foreach (Vector2Int cell in all)
            {
                if (starts.Count == count) break;
                if (cell == layout.Spawn || layout.IsStairs(cell)) continue;
                starts.Add(cell);
            }

            return starts;
        }

        /// <summary>
        /// Walks a player from spawn to the nearest stairwell while Dwellers roam, and reports
        /// whether any of them started hunting.
        /// </summary>
        /// <param name="layout">The floor to cross.</param>
        /// <param name="dwellers">The Dwellers roaming it, already placed.</param>
        /// <returns><c>true</c> if at least one Dweller entered a chase during the crossing.</returns>
        private static bool CrossingIsHunted(MazeLayout layout, List<DwellerManagerTestFacade> dwellers,
            List<float> paces)
        {
            Vector2Int player = layout.Spawn;
            List<Vector2Int> route = dwellers[0].PathBetween(
                layout, player, layout.NearestStairs(player));
            Assert.IsNotNull(route, "a connected floor must have a route to its stairs");

            var budget = new float[dwellers.Count];
            bool hunted = false;

            foreach (Vector2Int step in route)
            {
                player = step;
                for (int i = 0; i < dwellers.Count; i++)
                {
                    budget[i] += paces[i];
                    while (budget[i] >= 1f)
                    {
                        budget[i] -= 1f;
                        dwellers[i].StepOneCell(player);
                    }

                    dwellers[i].UpdateState(player);
                    if (dwellers[i].IsChasing) hunted = true;
                }
            }

            return hunted;
        }

        /// <summary>
        /// Runs the crossing across many seeds and reports how often the player was hunted.
        /// </summary>
        /// <param name="dwellerCount">How many Dwellers roam each floor.</param>
        /// <param name="senseRange">Sense range in cells.</param>
        /// <param name="patrolSpan">Patrol trip length in cells, or 0 to leave the default.</param>
        /// <returns>The share of runs in which a Dweller began hunting, 0..1.</returns>
        private static float HuntedRate(int dwellerCount, int senseRange, int patrolSpan)
        {
            const int trials = 25;
            int hunted = 0;

            for (int seed = 0; seed < trials; seed++)
            {
                DoNotDestroyOnTeardown.CleanSceneImmediate();
                MazeLayout layout = NewFloor(seed);

                var dwellers = new List<DwellerManagerTestFacade>();
                var paces = new List<float>();
                List<Vector2Int> starts = Starts(layout, dwellerCount);
                for (int i = 0; i < starts.Count; i++)
                {
                    // Deal out kinds exactly as the gameplay layer does, and apply each one's own
                    // multipliers — a roster of a far-sighted Watcher and a near-blind Skitter is a
                    // different measurement from three identical creatures.
                    DwellerArchetype archetype = DwellerArchetypes.For(DwellerArchetypes.AtIndex(i));

                    DwellerManagerTestFacade d = NewDweller($"Dweller_{i}_{archetype.Kind}");
                    d.SetSenseRange(Mathf.Max(1,
                        Mathf.RoundToInt(senseRange * archetype.SenseMultiplier)));
                    if (patrolSpan > 0)
                    {
                        d.SetPatrolSpan(Mathf.Max(3,
                            Mathf.RoundToInt(patrolSpan * archetype.PatrolMultiplier)));
                    }

                    d.Place(layout, starts[i], seed * 31 + i);
                    dwellers.Add(d);
                    paces.Add(DwellerPace * archetype.SpeedMultiplier);
                }

                if (CrossingIsHunted(layout, dwellers, paces)) hunted++;
            }

            return (float)hunted / trials;
        }

        /// <summary>
        /// The shipping configuration must find the player on most crossings. This is the test that
        /// would have caught "I don't see any dwellers": everything else about them was correct.
        /// </summary>
        [Test]
        public void ShippingFloor_HuntsThePlayerOnMostCrossings()
        {
            float rate = HuntedRate(ShippingDwellers, ShippingSense, ShippingPatrolSpan);
            MooseRunnerFacade.Log($"shipping setup hunted on {rate:P0} of crossings");
            Assert.Greater(rate, 0.7f,
                $"a player crossing a 24x24 floor was hunted only {rate:P0} of the time");
        }

        /// <summary>
        /// The configuration the game shipped with — one Dweller, sense range 5, wandering a step at
        /// a time — must be measurably worse, which is the evidence that the encounter rate was the
        /// real defect rather than the pathing or the state machine.
        /// </summary>
        [Test]
        public void OldConfiguration_ScarcelyEverFindsThePlayer()
        {
            float shipped = HuntedRate(ShippingDwellers, ShippingSense, ShippingPatrolSpan);
            float old = HuntedRate(dwellerCount: 1, senseRange: 5, patrolSpan: 1);
            MooseRunnerFacade.Log($"old setup hunted on {old:P0}, new setup on {shipped:P0}");
            Assert.Greater(shipped, old + 0.2f,
                $"the new setup ({shipped:P0}) should clearly beat the old one ({old:P0})");
        }

        /// <summary>
        /// A patrolling Dweller must actually cover ground. One that ends a long patrol near where it
        /// started is wandering with extra steps, and would never reach the player's side of a floor.
        /// </summary>
        [Test]
        public void APatrollingDweller_CoversGround()
        {
            MazeLayout layout = NewFloor(4);
            var start = new Vector2Int(layout.Width - 1, layout.Height - 1);

            DwellerManagerTestFacade dweller = NewDweller("Dweller");
            dweller.SetPatrolSpan(ShippingPatrolSpan);
            dweller.Place(layout, start, seed: 7);

            // Somewhere the Dweller can never sense the player, so it only ever patrols.
            var farAway = new Vector2Int(-50, -50);
            float furthest = 0f;
            for (int step = 0; step < 120; step++)
            {
                Vector2Int cell = dweller.StepOneCell(farAway);
                furthest = Mathf.Max(furthest, Vector2Int.Distance(cell, start));
            }

            MooseRunnerFacade.Log($"patrolling Dweller reached {furthest:F1} cells from its start");
            Assert.Greater(furthest, 10f,
                $"a patrol of 120 steps only ever got {furthest:F1} cells from where it started");
        }

        /// <summary>
        /// Once hunting, a Dweller must actually close the distance — the losing condition depends on
        /// it reaching the player rather than merely knowing where they are.
        /// </summary>
        [Test]
        public void AHuntingDweller_ClosesOnAStandingPlayer()
        {
            MazeLayout layout = NewFloor(11);
            Vector2Int player = layout.Spawn;

            DwellerManagerTestFacade dweller = NewDweller("Dweller");
            dweller.SetSenseRange(99);
            dweller.Place(layout, new Vector2Int(layout.Width - 1, layout.Height - 1), seed: 3);

            int startDistance = dweller.DistanceTo(player);
            for (int step = 0; step < layout.Width * layout.Height; step++)
            {
                if (dweller.StepOneCell(player) == player) break;
            }

            MooseRunnerFacade.Log($"closed from {startDistance} cells to {dweller.DistanceTo(player)}");
            Assert.AreEqual(player, dweller.Cell, "a hunting Dweller must reach a standing player");
        }
    }
}
