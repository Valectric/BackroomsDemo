using System.Threading;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using Backrooms.UIManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.Gameplay.Tests
{
    /// <summary>
    /// White-box tests that the demo actually ends where it says it does: a stairwell on the last
    /// floor finishes the game, and a stairwell on any floor above it does not.
    /// </summary>
    /// <remarks>
    /// <see cref="DemoRunTests"/> proves the rule; this proves the rule is wired to the staircase.
    /// Those are different claims, and the second is the one that fails silently — a demo that never
    /// ends looks exactly like a demo whose ending nobody has reached yet.
    /// </remarks>
    public class DemoEndingTests
    {
        /// <summary>The scene under test, as shipped.</summary>
        private const string SceneName = "Backrooms";

        /// <summary>
        /// Descending from the last floor finishes the demo and shows the victory screen with a score
        /// that matches the run; every floor above it descends as usual.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask TheLastStaircase_EndsTheDemoRatherThanDescending(CancellationToken ct)
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync(
                SceneName, forceReload: true, cleanDontDestroyOnLoad: true);
            for (int i = 0; i < 10; i++) await UniTask.Yield(ct);

            var controller = Object.FindAnyObjectByType<GameplayController>();
            var player = Object.FindAnyObjectByType<PlayerFacade>();
            var maze = Object.FindAnyObjectByType<MazeFacade>();
            var hud = Object.FindAnyObjectByType<HudFacade>();

            Assert.IsNotNull(controller, "the shipped scene must contain the gameplay controller");

            PlayerManagerTestFacade input = player.GetTestFacade();
            input.SimulationEnabled = true;
            input.Tap();
            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            input.ClearInput();

            // Down to the last floor. Every one of these is a descent that must NOT end the demo,
            // which is half of what is being asserted.
            while (controller.CurrentFloor < DemoRun.FinalFloor)
            {
                int before = controller.CurrentFloor;
                controller.DescendToNextFloor();
                await UniTask.Yield(ct);

                Assert.AreEqual(before + 1, controller.CurrentFloor, "the descent should go down one");
                Assert.IsFalse(controller.HasWonDemo,
                    $"floor {before} is not the last one, so it must not end the demo");
                Assert.IsFalse(hud.VictoryShown, "and must not congratulate anybody");
            }

            Assert.AreEqual(DemoRun.FinalFloor, controller.CurrentFloor, "standing on the last floor");

            // Walk onto a way down. The arrival guard keeps the staircase the player emerged from
            // from firing underfoot, so this steps onto a different one.
            MazeLayout layout = maze.CurrentLayout;
            Vector2Int stairs = layout.NearestStairs(layout.WorldToCell(player.Position));
            player.SpawnAt(layout.CellCenterToWorld(stairs));

            for (int i = 0; i < 30 && !controller.HasWonDemo; i++) await UniTask.WaitForFixedUpdate(ct);

            Assert.IsTrue(controller.HasWonDemo,
                $"a way down on floor {DemoRun.FinalFloor} should finish the demo");
            Assert.AreEqual(DemoRun.FinalFloor, controller.CurrentFloor,
                "and must not have generated a seventh floor");
            Assert.IsTrue(hud.VictoryShown, "the victory screen should be up");
            Assert.IsFalse(hud.CaughtShown, "and it is not a death");

            Assert.AreEqual(DemoRun.Score(DemoRun.FinalFloor, controller.RelicsCollected),
                hud.Score, "the screen should show the score the run actually earned");

            MooseRunnerFacade.Log($"demo finished on floor {controller.CurrentFloor} with "
                + $"{controller.RelicsCollected} relics, score {hud.Score}");

            // And it holds, exactly as a death does, rather than vanishing on the next click.
            Assert.IsFalse(hud.RetryOffered, "the win screen must not be dismissable immediately");

            input.Tap();
            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            input.ClearInput();

            Assert.IsTrue(controller.HasWonDemo, "an early click must not start another run");
            Assert.AreEqual(DemoRun.FinalFloor, controller.CurrentFloor, "nor rebuild the level");
        }
    }
}
