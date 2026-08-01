using System.IO;
using System.Threading;
using Backrooms.MazeManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.EntityManager.Tests
{
    /// <summary>
    /// Photographs a Dweller lurking and the same Dweller hunting, into <c>Screenshots/</c>. Whether
    /// a player can tell at a glance that the shape down the corridor has noticed them is not
    /// something any assertion can answer — the two frames side by side are the test.
    /// </summary>
    public class DwellerLookTests
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
        /// Builds a floor, stands a Dweller on it and photographs it from a few metres away, once
        /// while it is unaware and once while it is hunting.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask HuntingDweller_LooksDifferentFromALurkingOne(CancellationToken ct)
        {
            FloorTheme theme = FloorThemes.ForFloor(1);
            FloorAtmosphere.Apply(theme);

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(977, theme);
            MazeLayout layout = maze.CurrentLayout;

            // A cell open on every side, and a viewing direction that is genuinely open — placing the
            // camera a fixed offset away photographed the far side of a wall, with the Dweller's
            // light bleeding through it (point lights here cast no shadows) and the Dweller hidden.
            Vector2Int here = FindOpenCell(layout);
            Vector2Int step = ViewingStep(layout, here);

            var prey = new GameObject("Prey");
            prey.transform.position = layout.CellCenterToWorld(
                Clamp(layout, here + step * 5));

            var dwellerGo = new GameObject("Dweller");
            DwellerFacade dweller = dwellerGo.AddComponent<DwellerFacade>();

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;

            Vector3 stand = layout.CellCenterToWorld(here);
            camGo.transform.position = layout.CellCenterToWorld(here + step) + Vector3.up * 1.7f;
            camGo.transform.rotation = Quaternion.LookRotation(stand + Vector3.up * 1.2f
                                                               - camGo.transform.position);
            MooseRunnerFacade.Log($"dweller at {here}, camera at {here + step}, prey at {here + step * 5}");

            // Unaware: the same Dweller with its senses turned off, so the only difference between
            // the two frames is the state it is in.
            dweller.Place(layout, here, prey.transform, 2.2f, seed: 5);
            dweller.GetTestFacade().SetSenseRange(0);
            for (int i = 0; i < 6; i++) await UniTask.Yield(ct);
            Assert.IsFalse(dweller.IsChasing, "with no senses the Dweller must stay unaware");
            await Capture("dweller-lurking", ct);

            // Hunting: senses restored, prey well inside range.
            dweller.Place(layout, here, prey.transform, 2.2f, seed: 5);
            for (int i = 0; i < 6; i++) await UniTask.Yield(ct);
            MooseRunnerFacade.Log($"dweller state while hunting: {dweller.State}");
            Assert.IsTrue(dweller.IsChasing, "a Dweller four cells from its prey must be hunting");
            await Capture("dweller-hunting", ct);
        }

        /// <summary>
        /// Finds a cell with open passages on all four sides, so there is room to stand back and look.
        /// </summary>
        /// <param name="layout">The floor to search.</param>
        /// <returns>An open cell, or the grid centre as a fallback.</returns>
        private static Vector2Int FindOpenCell(MazeLayout layout)
        {
            for (int y = 2; y < layout.Height - 2; y++)
            {
                for (int x = 2; x < layout.Width - 2; x++)
                {
                    if (layout.IsStairs(new Vector2Int(x, y))) continue;
                    bool open = true;
                    foreach (Direction dir in Directions.All)
                    {
                        if (!layout.CanMove(x, y, dir)) open = false;
                    }

                    if (open) return new Vector2Int(x, y);
                }
            }

            return new Vector2Int(layout.Width / 2, layout.Height / 2);
        }

        /// <summary>
        /// A one-cell step from a cell in a direction that is actually walkable, so nothing stands
        /// between the camera and its subject.
        /// </summary>
        /// <param name="layout">The floor.</param>
        /// <param name="from">Cell to step away from.</param>
        /// <returns>The grid step to take.</returns>
        private static Vector2Int ViewingStep(MazeLayout layout, Vector2Int from)
        {
            foreach (Direction dir in Directions.All)
            {
                if (layout.CanMove(from.x, from.y, dir)) return Directions.Delta(dir);
            }

            return new Vector2Int(0, -1);
        }

        /// <summary>
        /// Clamps a cell into the grid.
        /// </summary>
        /// <param name="layout">The floor.</param>
        /// <param name="cell">Cell to clamp.</param>
        /// <returns>The clamped cell.</returns>
        private static Vector2Int Clamp(MazeLayout layout, Vector2Int cell)
            => new Vector2Int(
                Mathf.Clamp(cell.x, 0, layout.Width - 1),
                Mathf.Clamp(cell.y, 0, layout.Height - 1));

        /// <summary>
        /// Writes the current frame into <c>Screenshots/</c> under a given name.
        /// </summary>
        /// <param name="name">File name without extension.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask Capture(string name, CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(ct);
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");
                Directory.CreateDirectory(dir);
                string path = Path.GetFullPath(Path.Combine(dir, $"{name}.png"));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                MooseRunnerFacade.Log($"captured {name} -> {path}");
            }
            finally
            {
                Object.Destroy(shot);
            }
        }
    }
}
