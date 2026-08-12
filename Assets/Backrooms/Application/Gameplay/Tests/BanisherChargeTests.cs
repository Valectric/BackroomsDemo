using System.Collections.Generic;
using System.Threading;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using Backrooms.RelicManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.Gameplay.Tests
{
    /// <summary>
    /// White-box tests for what a Banisher shot costs. The relic carries five uses, and the question
    /// this settles is whether a shot that hits nothing is one of them.
    /// </summary>
    /// <remarks>
    /// Written because it was not: <see cref="PowerDirector"/> had no tests at all, so the whole
    /// question of what a power costs to use was answerable only by playing the game and counting.
    /// A miss used to be free, which meant the best way to play was to hold the key and sweep the
    /// corridor — the relic had five uses in the carried list and effectively unlimited ones in the
    /// hand.
    /// </remarks>
    public class BanisherChargeTests
    {
        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>
        /// A shot that hits nothing still costs one of the five.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask AMissedShot_StillSpendsACharge(CancellationToken ct)
        {
            (PowerDirector powers, RelicFacade relics, PlayerFacade player, DwellerDirector dwellers,
                MazeFacade maze) = NewStand();

            GiveABanisher(relics, maze);
            Assert.AreEqual(5, relics.ChargesOf(RelicKind.Banisher), "it arrives with five shots");

            // Nothing has been placed on this floor, so the shot cannot hit anything.
            PlayerManagerTestFacade input = player.GetTestFacade();
            input.SimulationEnabled = true;

            for (int shot = 1; shot <= 5; shot++)
            {
                input.SetInput(Vector2.zero, banish: true);
                await UniTask.Yield(ct);

                bool used = powers.TryUsePowers(relics, player, dwellers, maze, out RelicKind _);

                Assert.IsFalse(used, $"shot {shot} hit nothing, so nothing was banished");
                Assert.IsTrue(powers.BanisherMissed, $"shot {shot} should report as a miss");
                Assert.AreEqual(5 - shot, relics.ChargesOf(RelicKind.Banisher),
                    $"after {shot} missed shots the relic should have {5 - shot} left");

                input.ClearInput();
                await UniTask.Yield(ct);
            }

            Assert.IsFalse(relics.Holds(RelicKind.Banisher),
                "five misses empty it exactly as five kills would");

            // And an empty relic fires nothing at all, rather than clicking forever.
            input.SetInput(Vector2.zero, banish: true);
            await UniTask.Yield(ct);
            Assert.IsFalse(powers.TryUsePowers(relics, player, dwellers, maze, out RelicKind _),
                "a spent Banisher must not fire");
            Assert.IsFalse(powers.BanisherMissed,
                "and must not report a miss either — there was no shot to miss with");
        }

        /// <summary>
        /// Nothing is spent when the key is not pressed. A relic that drained while carried would be
        /// a far worse bug than a free miss, and the charge is now spent on a path that no longer
        /// waits for a kill to confirm it.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask CarryingIt_SpendsNothing(CancellationToken ct)
        {
            (PowerDirector powers, RelicFacade relics, PlayerFacade player, DwellerDirector dwellers,
                MazeFacade maze) = NewStand();

            GiveABanisher(relics, maze);
            player.GetTestFacade().SimulationEnabled = true;
            player.GetTestFacade().ClearInput();

            for (int frame = 0; frame < 30; frame++)
            {
                powers.TryUsePowers(relics, player, dwellers, maze, out RelicKind _);
                await UniTask.Yield(ct);
            }

            Assert.AreEqual(5, relics.ChargesOf(RelicKind.Banisher),
                "thirty frames of carrying it must cost nothing");
        }

        /// <summary>
        /// Builds a floor and the pieces a power needs to fire.
        /// </summary>
        /// <returns>The director under test and everything it acts on.</returns>
        private static (PowerDirector, RelicFacade, PlayerFacade, DwellerDirector, MazeFacade)
            NewStand()
        {
            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(977, FloorThemes.ForFloor(1), hasFloorAbove: false);

            var playerGo = new GameObject("Player");
            PlayerFacade player = playerGo.AddComponent<PlayerFacade>();
            player.SpawnAt(maze.GetSpawnPosition());

            var relicGo = new GameObject("Relics");
            RelicFacade relics = relicGo.AddComponent<RelicFacade>();
            relics.ResetRun();

            // No Dweller seeded and no floor populated, so every shot is a clean miss.
            var directorGo = new GameObject("Dwellers");
            var dwellers = new DwellerDirector(directorGo.transform, null, 190, 4);

            return (new PowerDirector(), relics, player, dwellers, maze);
        }

        /// <summary>
        /// Puts a Banisher in the player's hands by walking onto one, the way the game does.
        /// </summary>
        /// <param name="relics">The relic module.</param>
        /// <param name="maze">The floor the relic stands on.</param>
        private static void GiveABanisher(RelicFacade relics, MazeFacade maze)
        {
            RelicManagerTestFacade seam = relics.GetTestFacade();
            List<Vector2Int> placed = seam.PlaceKind(
                maze.CurrentLayout, RelicKind.Banisher, 5, relics.transform);

            Assert.IsNotEmpty(placed, "a Banisher should have been placed");
            Assert.IsTrue(
                relics.TryCollect(maze.CurrentLayout.CellCenterToWorld(placed[0])),
                "and should have been collected");
        }
    }
}
