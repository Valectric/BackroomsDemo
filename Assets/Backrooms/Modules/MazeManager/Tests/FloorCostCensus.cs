using Backrooms.MazeManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// Holds a floor to a drawing budget. Every ceiling fixture is a realtime point light, and at
    /// one per three cells a floor carried 73 of them — which is what a WebGL build on a phone
    /// actually cannot afford. Triangles were never the problem: a floor is about 20,000, which is
    /// nothing. Lights and draw calls are the whole cost, so those are what is bounded here.
    /// </summary>
    public class FloorCostCensus
    {
        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>Most realtime lights that may be switched ON at once.</summary>
        /// <remarks>
        /// The budget that matters is what the renderer sees, not what exists. A floor may carry any
        /// number of fixtures; LightCuller keeps the handful near the player on and the rest off,
        /// because the fog makes the others invisible anyway.
        /// </remarks>
        private const int ActiveLightBudget = 22;

        /// <summary>Most renderers a floor may carry, each one a potential draw call.</summary>
        private const int RendererBudget = 320;

        /// <summary>
        /// Every floor must stay inside the drawing budget.
        /// </summary>
        [Test]
        public void EveryFloor_StaysWithinItsDrawingBudget()
        {
            for (int floor = 1; floor <= 5; floor++)
            {
                DoNotDestroyOnTeardown.CleanSceneImmediate();

                var go = new GameObject("MazeManager");
                MazeFacade maze = go.AddComponent<MazeFacade>();
                FloorTheme theme = FloorThemes.ForFloor(floor);
                maze.GenerateAndBuild(floor * 977, theme, hasFloorAbove: floor > 1);

                Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                int lights = all.Length;

                // Stand where the player starts and let the culler decide what is worth lighting.
                Vector3 viewer = maze.CurrentLayout.CellCenterToWorld(maze.CurrentLayout.Spawn);
                foreach (LightCuller culler in
                         Object.FindObjectsByType<LightCuller>(
                             FindObjectsSortMode.None))
                {
                    culler.Apply(viewer);
                }

                int active = 0;
                foreach (Light l in all)
                {
                    if (l != null && l.enabled && l.gameObject.activeInHierarchy) active++;
                }
                MeshRenderer[] renderers =
                    Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

                long tris = 0;
                foreach (MeshFilter f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;
                }

                MooseRunnerFacade.Log(
                    $"floor {floor} {theme.Name}: {active} of {lights} lights on, "
                    + $"{renderers.Length} renderers, {tris:N0} triangles");

                Assert.LessOrEqual(active, ActiveLightBudget,
                    $"{theme.Name} has {active} lights switched on; the budget is "
                    + $"{ActiveLightBudget}. Distant fixtures must be culled, not merely present.");
                Assert.LessOrEqual(renderers.Length, RendererBudget,
                    $"{theme.Name} carries {renderers.Length} renderers; the budget is {RendererBudget}");
            }
        }
    }
}
