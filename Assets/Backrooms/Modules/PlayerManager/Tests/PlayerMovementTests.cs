using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.PlayerManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for the first-person player. Input is injected through the module's
    /// inbound simulation seam, so the tests exercise exactly the movement and look code a real
    /// player's keyboard or touch input reaches — no Input System device events are synthesised.
    /// </summary>
    public class PlayerMovementTests
    {
        /// <summary>Physics steps each test simulates; at 50 Hz this is one second.</summary>
        private const int Steps = 50;

        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// Creates a floor plane so the character controller has something to stand on, then spawns a
        /// player above it in simulation mode.
        /// </summary>
        /// <returns>The player's test facade.</returns>
        private static PlayerManagerTestFacade SpawnPlayerOnFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);

            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 0.1f, 0f);
            var facade = go.AddComponent<PlayerFacade>();
            PlayerManagerTestFacade test = facade.GetTestFacade();
            test.SimulationEnabled = true;
            return test;
        }

        /// <summary>
        /// Advances the physics simulation by a number of fixed steps.
        /// </summary>
        /// <param name="steps">Number of fixed updates to wait for.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask StepPhysics(int steps, CancellationToken ct)
        {
            for (int i = 0; i < steps; i++) await UniTask.WaitForFixedUpdate(ct);
        }

        /// <summary>
        /// Holding forward moves the player forward roughly at the walk speed and leaves them on the
        /// floor.
        /// </summary>
        [Test]
        public async UniTask MoveForward_AdvancesAlongFacing(CancellationToken ct)
        {
            PlayerManagerTestFacade player = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);
            Vector3 start = player.Position;

            player.SetInput(move: new Vector2(0f, 1f));
            await StepPhysics(Steps, ct);

            Vector3 delta = player.Position - start;
            MooseRunnerFacade.Log($"walked {delta.magnitude:F2}m in {Steps} steps");

            Assert.Greater(delta.z, 1.5f, "should advance along +Z (initial facing)");
            Assert.Less(Mathf.Abs(delta.x), 0.2f, "should not drift sideways");
        }

        /// <summary>
        /// Sprinting covers more ground than walking over the same number of steps.
        /// </summary>
        [Test]
        public async UniTask Sprint_CoversMoreGroundThanWalk(CancellationToken ct)
        {
            PlayerManagerTestFacade walker = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);
            Vector3 walkStart = walker.Position;
            walker.SetInput(move: new Vector2(0f, 1f));
            await StepPhysics(Steps, ct);
            float walked = (walker.Position - walkStart).magnitude;

            DoNotDestroyOnTeardown.CleanSceneImmediate();

            PlayerManagerTestFacade sprinter = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);
            Vector3 sprintStart = sprinter.Position;
            sprinter.SetInput(move: new Vector2(0f, 1f), sprint: true);
            await StepPhysics(Steps, ct);
            float sprinted = (sprinter.Position - sprintStart).magnitude;

            MooseRunnerFacade.Log($"walked {walked:F2}m, sprinted {sprinted:F2}m");
            Assert.Greater(sprinted, walked * 1.2f, "sprinting should be clearly faster");
        }

        /// <summary>
        /// With no input the player stays put horizontally.
        /// </summary>
        [Test]
        public async UniTask NoInput_PlayerStaysStill(CancellationToken ct)
        {
            PlayerManagerTestFacade player = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);
            Vector3 start = player.Position;

            player.ClearInput();
            await StepPhysics(Steps, ct);

            Vector3 delta = player.Position - start;
            Assert.Less(new Vector2(delta.x, delta.z).magnitude, 0.05f, "should not drift");
        }

        /// <summary>
        /// Look input to the right turns the body clockwise (increasing yaw).
        /// </summary>
        [Test]
        public async UniTask LookRight_TurnsBody(CancellationToken ct)
        {
            PlayerManagerTestFacade player = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);

            player.SetInput(move: Vector2.zero, look: new Vector2(20f, 0f));
            await StepPhysics(10, ct);

            float yaw = player.Yaw;
            MooseRunnerFacade.Log($"yaw after look-right: {yaw:F1}");
            Assert.Greater(yaw, 5f, "yaw should increase when looking right");
            Assert.Less(yaw, 180f, "yaw should not wrap past half a turn in this test");
        }

        /// <summary>
        /// Sustained upward look input is clamped so the view never flips over.
        /// </summary>
        [Test]
        public async UniTask LookUp_PitchIsClamped(CancellationToken ct)
        {
            PlayerManagerTestFacade player = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);

            player.SetInput(move: Vector2.zero, look: new Vector2(0f, 100f));
            await StepPhysics(Steps, ct);

            MooseRunnerFacade.Log($"pitch after sustained look-up: {player.Pitch:F1}");
            Assert.GreaterOrEqual(player.Pitch, -85.01f, "pitch must not exceed the upward clamp");
            Assert.Less(player.Pitch, -80f, "pitch should have reached the upward clamp");
        }

        /// <summary>
        /// A wall directly ahead stops the player instead of letting them pass through it.
        /// </summary>
        [Test]
        public async UniTask WallAhead_BlocksMovement(CancellationToken ct)
        {
            PlayerManagerTestFacade player = SpawnPlayerOnFloor();

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1.5f, 2f);
            wall.transform.localScale = new Vector3(10f, 3f, 0.4f);

            await StepPhysics(5, ct);

            player.SetInput(move: new Vector2(0f, 1f));
            await StepPhysics(Steps * 2, ct);

            MooseRunnerFacade.Log($"stopped at z={player.Position.z:F2} (wall at z=2)");
            Assert.Less(player.Position.z, 2f, "player must not pass through the wall");
            Assert.Greater(player.Position.z, 0.5f, "player should have walked up to the wall");
        }

        /// <summary>
        /// Simulation mode is opt-in: with it disabled the module ignores injected intent, so a test
        /// cannot accidentally drive a player that is meant to be reading real hardware.
        /// </summary>
        [Test]
        public async UniTask SimulationDisabled_IgnoresInjectedInput(CancellationToken ct)
        {
            PlayerManagerTestFacade player = SpawnPlayerOnFloor();
            await StepPhysics(5, ct);
            Vector3 start = player.Position;

            player.SimulationEnabled = false;
            player.SetInput(move: new Vector2(0f, 1f));
            await StepPhysics(Steps, ct);

            Vector3 delta = player.Position - start;
            Assert.Less(new Vector2(delta.x, delta.z).magnitude, 0.05f,
                "injected input must be ignored when simulation mode is off");
        }
    }
}
