using System.Collections.Generic;
using System.Threading;
using Backrooms.MazeManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.RelicManager.Tests
{
    /// <summary>
    /// Holds the relics to a drawing budget. The first floor carries four times as many as any other,
    /// and each one is a realtime light — so the floor that decides whether a player stays is also
    /// the one most able to make the game stutter on the phone they are deciding on.
    /// </summary>
    /// <remarks>
    /// This exists because the count was raised without measuring first, and measuring showed floor 1
    /// going from 70 lights to 110: more lights on relics than the entire ceiling carries. Nothing in
    /// the relic suite noticed, because every test there is about where relics are and what they do.
    /// </remarks>
    public class RelicCostTests
    {
        /// <summary>Most relic glows that may be lit at once.</summary>
        private const int LitGlowBudget = 6;

        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>
        /// However many relics a floor carries, only a handful of their glows are ever switched on.
        /// </summary>
        /// <remarks>
        /// A relic's light reaches seven metres, so one across the floor is lighting nothing while
        /// still being a light the renderer has to consider. Culling them is invisible by
        /// construction rather than by judgement — which is why the budget can be this tight.
        /// </remarks>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask OnlyTheNearestRelicGlows_StayLit(CancellationToken ct)
        {
            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            FloorTheme theme = FloorThemes.ForFloor(1);
            maze.GenerateAndBuild(977, theme, hasFloorAbove: false);

            var relicGo = new GameObject("Relics");
            RelicFacade relics = relicGo.AddComponent<RelicFacade>();
            relics.ResetRun();
            List<Vector2Int> placed = relics.PlaceForFloor(maze.CurrentLayout, 7, floor: 1);

            Assert.GreaterOrEqual(placed.Count, 36, "the first floor should be littered with relics");

            // Stand where the player starts and let the culler run through its own Update, so this
            // measures the path the game takes rather than a method the game never calls.
            var camGo = new GameObject("Viewer");
            camGo.AddComponent<Camera>();
            camGo.transform.position = maze.CurrentLayout.CellCenterToWorld(maze.CurrentLayout.Spawn);

            for (int i = 0; i < 5; i++) await UniTask.Yield(ct);

            int lit = 0;
            foreach (Light light in relicGo.GetComponentsInChildren<Light>(includeInactive: true))
            {
                if (light.enabled && light.gameObject.activeInHierarchy) lit++;
            }

            MooseRunnerFacade.Log($"{placed.Count} relics, {lit} glows lit at the spawn");

            Assert.LessOrEqual(lit, LitGlowBudget,
                $"{placed.Count} relics left {lit} glows lit; the budget is {LitGlowBudget}. "
                + "Distant glows must be switched off, not merely out of sight.");
            Assert.AreEqual(lit, relics.LitGlows, "the facade should report what is actually lit");
        }

        /// <summary>
        /// A relic close enough to light something is lit. The cull must not be so aggressive that
        /// the player walks up to a relic standing in the dark.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask AGlow_IsLitWhenThePlayerIsNearIt(CancellationToken ct)
        {
            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(977, FloorThemes.ForFloor(1), hasFloorAbove: false);

            var relicGo = new GameObject("Relics");
            RelicFacade relics = relicGo.AddComponent<RelicFacade>();
            relics.ResetRun();
            List<Vector2Int> placed = relics.PlaceForFloor(maze.CurrentLayout, 7, floor: 1);

            var camGo = new GameObject("Viewer");
            camGo.AddComponent<Camera>();
            camGo.transform.position = maze.CurrentLayout.CellCenterToWorld(placed[0]);

            for (int i = 0; i < 5; i++) await UniTask.Yield(ct);

            Assert.GreaterOrEqual(relics.LitGlows, 1,
                "standing on a relic, its own glow must be on");
        }
    }
}
