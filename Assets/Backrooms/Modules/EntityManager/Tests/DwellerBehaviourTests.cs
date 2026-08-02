using System.Collections.Generic;
using Backrooms.EntityManager;
using Backrooms.MazeManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.EntityManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for Dweller behaviour: when it notices the player, how it paths
    /// through the corridors, and that it can never move through a wall. The brain is pure grid
    /// logic, so these run without any scene geometry and are fully deterministic.
    /// </summary>
    public class DwellerBehaviourTests
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
        /// Creates a maze layout and a Dweller placed in it, without scene geometry.
        /// </summary>
        /// <param name="seed">Maze seed.</param>
        /// <param name="start">Cell the Dweller starts in.</param>
        /// <returns>The layout and the Dweller's test facade.</returns>
        private static (MazeLayout layout, DwellerManagerTestFacade dweller) NewDweller(
            int seed, Vector2Int start)
        {
            var mazeGo = new GameObject("MazeManager");
            MazeLayout layout = mazeGo.AddComponent<MazeFacade>()
                .Generate(new MazeSettings(10, 10, seed));

            var dwellerGo = new GameObject("Dweller");
            DwellerManagerTestFacade dweller = dwellerGo.AddComponent<DwellerFacade>().GetTestFacade();
            dweller.Place(layout, start, seed);
            return (layout, dweller);
        }

        /// <summary>
        /// The Watcher must stand perfectly still while the player looks at it, and cover ground
        /// faster than a sprint the moment they look away.
        /// </summary>
        /// <remarks>
        /// Both halves matter. Standing still is what makes it a rule the player can use; being
        /// faster than a sprint is what stops the rule being optional. If it could be outrun, looking
        /// at it would never be worth the cost of not looking where you are going.
        /// </remarks>
        [Test]
        public void TheWatcher_FreezesWhenWatched_AndOutrunsASprintWhenNotSeen()
        {
            (MazeLayout _, DwellerManagerTestFacade dweller) = NewDweller(3, Vector2Int.zero);

            var self = new Vector3(10f, 0f, 0f);
            var player = new Vector3(0f, 0f, 0f);
            const float sprint = 5.6f;

            float watched = dweller.StepGait(DwellerKind.Watcher, self, player,
                Vector3.right, chasing: true, patrolSpeed: 1.6f, chaseSpeed: 3.4f, deltaTime: 0.02f);
            Assert.AreEqual(0f, watched, 1e-4f, "looked straight at, it must not move at all");

            float unseen = dweller.StepGait(DwellerKind.Watcher, self, player,
                Vector3.left, chasing: true, patrolSpeed: 1.6f, chaseSpeed: 3.4f, deltaTime: 0.02f);
            MooseRunnerFacade.Log($"watcher: watched {watched:F2} m/s, unseen {unseen:F2} m/s");
            Assert.Greater(unseen, sprint,
                $"unwatched it moves {unseen:F2} m/s, which a {sprint} m/s sprint escapes");
        }

        /// <summary>
        /// The Skitter must creep, then stop and flash a warning three times, then run at far more
        /// than a sprint.
        /// </summary>
        /// <remarks>
        /// The flashes are the fairness. It is allowed to be unoutrunnable during the charge only
        /// because it stands still first and says so.
        /// </remarks>
        [Test]
        public void TheSkitter_Creeps_ThenFlashesThreeTimes_ThenChargesFasterThanASprint()
        {
            (MazeLayout _, DwellerManagerTestFacade dweller) = NewDweller(5, Vector2Int.zero);

            var self = new Vector3(8f, 0f, 0f);
            var player = Vector3.zero;
            const float dt = 0.02f;
            const float sprint = 5.6f;

            float creep = dweller.StepGait(DwellerKind.Skitter, self, player, Vector3.right,
                chasing: false, patrolSpeed: 1.6f, chaseSpeed: 4.2f, deltaTime: dt);
            Assert.Less(creep, 3.2f, "it should be creeping before it sees anything worth charging");

            // Drive the wind-up and count the flashes, then the charge itself.
            int flashes = 0;
            bool lit = false;
            float fastest = 0f;
            bool charged = false;

            for (int step = 0; step < 300; step++)
            {
                float speed = dweller.StepGait(DwellerKind.Skitter, self, player, Vector3.right,
                    chasing: true, patrolSpeed: 1.6f, chaseSpeed: 4.2f, deltaTime: dt);

                if (dweller.GaitAlarmed && !lit) flashes++;
                lit = dweller.GaitAlarmed;

                if (dweller.GaitCharging)
                {
                    charged = true;
                    fastest = Mathf.Max(fastest, speed);
                }

                if (charged && !dweller.GaitCharging) break;
            }

            MooseRunnerFacade.Log($"skitter: {flashes} flashes, charge peaked at {fastest:F2} m/s");
            Assert.IsTrue(charged, "it should eventually commit to a charge");
            Assert.GreaterOrEqual(flashes, 3, "it must flash a warning at least three times first");
            Assert.Greater(fastest, sprint * 1.5f,
                $"a charge at {fastest:F2} m/s is not a rush against a {sprint} m/s sprint");
        }

        /// <summary>
        /// A Dweller starts unaware of the player.
        /// </summary>
        [Test]
        public void NewDweller_StartsPatrolling()
        {
            (MazeLayout _, DwellerManagerTestFacade dweller) = NewDweller(1, Vector2Int.zero);
            Assert.AreEqual(DwellerState.Patrol, dweller.State, "starts unaware");
        }

        /// <summary>
        /// The Dweller switches to chasing once the player is within its sense range, measured along
        /// open corridors rather than straight through walls.
        /// </summary>
        [Test]
        public void EntersChase_WhenPlayerIsWithinSenseRange()
        {
            (MazeLayout layout, DwellerManagerTestFacade dweller) = NewDweller(7, Vector2Int.zero);
            dweller.SetSenseRange(3);

            // A cell three corridor-steps away must be noticed.
            List<Vector2Int> path = dweller.PathBetween(layout, Vector2Int.zero, layout.Stairs[0]);
            Assert.IsNotNull(path, "exit is reachable");
            Vector2Int near = path[2];

            dweller.UpdateState(near);
            MooseRunnerFacade.Log($"distance to {near} is {dweller.DistanceTo(near)} cells");
            Assert.AreEqual(DwellerState.Chase, dweller.State, "notices a nearby player");
        }

        /// <summary>
        /// A player far away down the corridors goes unnoticed, so the Dweller keeps wandering.
        /// </summary>
        [Test]
        public void StaysPatrolling_WhenPlayerIsFarAway()
        {
            (MazeLayout layout, DwellerManagerTestFacade dweller) = NewDweller(7, Vector2Int.zero);
            dweller.SetSenseRange(2);

            int distance = dweller.DistanceTo(layout.Stairs[0]);
            Assert.Greater(distance, 2, "the exit is further than the sense range for this seed");

            dweller.UpdateState(layout.Stairs[0]);
            Assert.AreEqual(DwellerState.Patrol, dweller.State, "does not notice a distant player");
        }

        /// <summary>
        /// While chasing, every step the Dweller takes reduces its distance to the player — it closes
        /// in rather than wandering.
        /// </summary>
        [Test]
        public void Chasing_ClosesDistanceEveryStep()
        {
            (MazeLayout layout, DwellerManagerTestFacade dweller) = NewDweller(3, Vector2Int.zero);
            dweller.SetSenseRange(999);

            Vector2Int player = layout.Stairs[0];
            int previous = dweller.DistanceTo(player);

            for (int step = 0; step < 8 && previous > 0; step++)
            {
                dweller.StepOneCell(player);
                int now = dweller.DistanceTo(player);
                Assert.Less(now, previous, $"step {step} should close the gap");
                previous = now;
            }

            MooseRunnerFacade.Log($"closed to {previous} cells from the player");
        }

        /// <summary>
        /// Every move a Dweller makes, chasing or wandering, must cross an open passage. A Dweller
        /// that clipped through walls would be both unfair and off-model.
        /// </summary>
        [Test]
        public void NeverMovesThroughAWall()
        {
            (MazeLayout layout, DwellerManagerTestFacade dweller) = NewDweller(11, Vector2Int.zero);
            dweller.SetSenseRange(2);

            Vector2Int player = layout.Stairs[0];
            for (int step = 0; step < 40; step++)
            {
                Vector2Int before = dweller.Cell;
                Vector2Int after = dweller.StepOneCell(player);

                if (after == before) continue;

                Vector2Int delta = after - before;
                Assert.AreEqual(1, Mathf.Abs(delta.x) + Mathf.Abs(delta.y),
                    "a move is exactly one orthogonal cell");

                Direction dir = DirectionFor(delta);
                Assert.IsTrue(layout.CanMove(before.x, before.y, dir),
                    $"step {step}: moved from {before} to {after} through a wall");
            }
        }

        /// <summary>
        /// Once a Dweller has caught the player the run is over; nothing sends it back to patrolling.
        /// </summary>
        [Test]
        public void CaughtState_IsTerminal()
        {
            (MazeLayout layout, DwellerManagerTestFacade dweller) = NewDweller(5, Vector2Int.zero);
            var go = new GameObject("Probe");
            DwellerFacade facade = go.AddComponent<DwellerFacade>();
            DwellerManagerTestFacade probe = facade.GetTestFacade();
            probe.Place(layout, Vector2Int.zero, 5);

            // Drive the shared router into the caught state through the production path.
            probe.SetSenseRange(0);
            probe.UpdateState(layout.Stairs[0]);
            Assert.AreEqual(DwellerState.Patrol, probe.State, "far player leaves it patrolling");

            dweller.SetSenseRange(999);
            dweller.UpdateState(layout.Stairs[0]);
            Assert.AreEqual(DwellerState.Chase, dweller.State, "close player triggers a chase");
        }

        /// <summary>
        /// Maps a one-cell delta to the direction it represents.
        /// </summary>
        /// <param name="delta">Cell delta of a single step.</param>
        /// <returns>The matching direction.</returns>
        private static Direction DirectionFor(Vector2Int delta)
        {
            if (delta == new Vector2Int(0, 1)) return Direction.North;
            if (delta == new Vector2Int(1, 0)) return Direction.East;
            if (delta == new Vector2Int(0, -1)) return Direction.South;
            return Direction.West;
        }
    }
}
