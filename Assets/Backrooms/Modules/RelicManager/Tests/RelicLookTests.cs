using System.Collections.Generic;
using System.IO;
using System.Threading;
using Backrooms.MazeManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.RelicManager.Tests
{
    /// <summary>
    /// Photographs a relic in a built floor, into <c>Screenshots/</c>. Whether a relic reads as
    /// something worth crossing a floor for — rather than as a stray light or a bit of scenery — is
    /// only answerable from a frame.
    /// </summary>
    public class RelicLookTests
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
        /// Builds a floor, stands a relic in it, and photographs it from a few metres back and from
        /// across the room — the two distances that matter, since a relic has to be noticed from far
        /// away and recognised up close.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask ARelic_ReadsAsSomethingWorthCrossingAFloorFor(CancellationToken ct)
        {
            FloorTheme theme = FloorThemes.ForFloor(1);
            FloorAtmosphere.Apply(theme);

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(977, theme);
            MazeLayout layout = maze.CurrentLayout;

            var relicGo = new GameObject("Relics");
            RelicFacade relics = relicGo.AddComponent<RelicFacade>();

            List<Vector2Int> placed = relics.PlaceForFloor(layout, seed: 1);
            Assert.IsNotEmpty(placed, "a relic should have been placed");

            Vector3 at = layout.CellCenterToWorld(placed[0]);
            MooseRunnerFacade.Log($"relic at cell {placed[0]}");

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;

            // The relic goes wherever the floor's dead corner is, so the camera has to follow the
            // geometry rather than a fixed offset — a fixed one photographs the inside of a wall.
            Vector3 view = ViewingDirection(layout, placed[0], out int openCells);

            // Never back off further than the floor actually goes, or the camera ends up inside the
            // wall behind it and photographs wallpaper.
            float room = Mathf.Max(2f, openCells * layout.CellSize - 1.2f);

            foreach ((string tag, float back) in new[] { ("close", 3.4f), ("far", 11f) })
            {
                camGo.transform.position = at + view * Mathf.Min(back, room) + Vector3.up * 1.7f;
                camGo.transform.rotation = Quaternion.LookRotation(
                    at + Vector3.up * 1.1f - camGo.transform.position);

                for (int i = 0; i < 5; i++) await UniTask.Yield(ct);
                await Capture($"relic-{tag}", ct);
            }
        }

        /// <summary>
        /// Photographs the first floor from above with every relic on it, and the floor below it for
        /// comparison. Forty relics is a number that can only be judged as a picture: the question is
        /// whether the floor reads as generous or as littered, and no assertion can answer it.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask TheFirstFloor_ReadsAsGenerousRatherThanLittered(CancellationToken ct)
        {
            foreach (int floor in new[] { 1, 2 })
            {
                DoNotDestroyOnTeardown.CleanSceneImmediate();

                FloorTheme theme = FloorThemes.ForFloor(floor);
                FloorAtmosphere.Apply(theme);

                var mazeGo = new GameObject("MazeManager");
                MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
                maze.GenerateAndBuild(floor * 977, theme, hasFloorAbove: floor > 1);
                MazeLayout layout = maze.CurrentLayout;

                var relicGo = new GameObject("Relics");
                RelicFacade relics = relicGo.AddComponent<RelicFacade>();
                relics.ResetRun();
                List<Vector2Int> placed = relics.PlaceForFloor(layout, seed: 1, floor);

                MooseRunnerFacade.Log($"floor {floor}: {placed.Count} relics on the plan");

                // A perspective view through runtime fog photographs haze, not a layout — the same
                // reason FloorLookTests shoots its plans orthographic with the ceiling out of the way.
                GameObject ceiling = GameObject.Find("Ceiling");
                if (ceiling != null) ceiling.SetActive(false);
                bool fog = RenderSettings.fog;
                RenderSettings.fog = false;

                // Lit flat and bright for the duration of the shot. A plan is a map, and a map has to
                // be readable: the floor's own lighting is a handful of fixtures near the player, and
                // from forty metres up the culler switches even those off, so an unlit plan comes out
                // as a black square with three green stairwells in it.
                AmbientMode ambientWas = RenderSettings.ambientMode;
                Color ambientColourWas = RenderSettings.ambientLight;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.85f, 0.84f, 0.80f);

                float span = layout.Width * layout.CellSize;
                float depth = layout.Height * layout.CellSize;

                var camGo = new GameObject("PlanCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(span, depth) * 0.5f + 2f;
                cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camGo.transform.position = new Vector3(span * 0.5f, 40f, depth * 0.5f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                for (int i = 0; i < 5; i++) await UniTask.Yield(ct);
                await Capture($"relics-plan-floor{floor}", ct);

                RenderSettings.ambientMode = ambientWas;
                RenderSettings.ambientLight = ambientColourWas;
                RenderSettings.fog = fog;
                if (ceiling != null) ceiling.SetActive(true);
                Object.Destroy(camGo);

                // And the view that actually decides it: standing where the player starts, looking
                // down the longest thing they can see. A plan says how many there are; only this says
                // whether walking into the level feels like being handed something.
                var eyeGo = new GameObject("EyeCamera");
                var eye = eyeGo.AddComponent<Camera>();
                eye.fieldOfView = 70f;
                eye.farClipPlane = 45f;
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = RenderSettings.fogColor;

                Vector3 spawn = layout.CellCenterToWorld(layout.Spawn);
                eyeGo.transform.position = spawn + Vector3.up * 1.7f;
                eyeGo.transform.rotation = Quaternion.LookRotation(
                    LongestView(layout, layout.Spawn));

                for (int i = 0; i < 5; i++) await UniTask.Yield(ct);
                await Capture($"relics-eye-floor{floor}", ct);
            }
        }

        /// <summary>
        /// The direction with the most open floor ahead of it, so an eye-level shot looks down the
        /// longest thing there is to see rather than at the nearest wall.
        /// </summary>
        /// <param name="layout">The floor being photographed.</param>
        /// <param name="from">Cell the camera stands in.</param>
        /// <returns>A unit world direction to look along.</returns>
        private static Vector3 LongestView(MazeLayout layout, Vector2Int from)
        {
            Vector3 best = Vector3.forward;
            int bestRun = -1;

            foreach (Direction dir in Directions.All)
            {
                Vector2Int step = Directions.Delta(dir);
                Vector2Int at = from;
                int run = 0;
                while (layout.CanMove(at.x, at.y, dir) && run < 20)
                {
                    at += step;
                    run++;
                }

                if (run <= bestRun) continue;
                bestRun = run;
                best = new Vector3(step.x, 0f, step.y);
            }

            return best;
        }

        /// <summary>
        /// A world direction with open floor behind it, so a camera backed off along it is standing
        /// in the level rather than inside a wall.
        /// </summary>
        /// <param name="layout">The floor being photographed.</param>
        /// <param name="cell">Cell holding the relic.</param>
        /// <param name="openCells">Receives how many cells stay open along that direction.</param>
        /// <returns>A unit world direction to back the camera along.</returns>
        private static Vector3 ViewingDirection(MazeLayout layout, Vector2Int cell, out int openCells)
        {
            // Prefer a direction that stays open for several cells, so the "far" shot has room.
            // A run of zero means the direction is walled off immediately, and must never win.
            Vector3 best = Vector3.back;
            int bestRun = 0;

            foreach (Direction dir in Directions.All)
            {
                Vector2Int step = Directions.Delta(dir);
                Vector2Int at = cell;
                int run = 0;
                while (layout.CanMove(at.x, at.y, dir) && run < 6)
                {
                    at += step;
                    run++;
                }

                if (run <= bestRun) continue;
                bestRun = run;
                best = new Vector3(step.x, 0f, step.y);
            }

            openCells = bestRun;
            return best;
        }

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
