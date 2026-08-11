using System.Collections.Generic;
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
    /// Captures the shots used on the store page, from the real built floors rather than from a
    /// staged set. A screenshot that flatters the game into something it is not is a promise the
    /// game then breaks in the first ten seconds.
    /// </summary>
    /// <remarks>
    /// Deterministic: the same seeds, cells and angles every run, so a shot can be reproduced after
    /// the lighting or the layout changes rather than being a lucky frame nobody can find again.
    /// </remarks>
    public class PressShots
    {
        /// <summary>Where the shots are written, relative to the project root.</summary>
        private const string Folder = "Screenshots/press";

        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>
        /// Takes five shots: three of the first floor, two from deeper down, one of them with a
        /// Dweller hunting.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask Capture(CancellationToken ct)
        {
            // Floor 1 three times, from three different rooms and facings, so the page shows a place
            // rather than one corner of it.
            await Shoot(1, "01-yellow-rooms", new Vector2Int(4, 4), 14f, ct);
            await Shoot(1, "02-yellow-corridor", new Vector2Int(12, 9), -18f, ct);
            await Shoot(1, "03-yellow-deep", new Vector2Int(18, 16), 22f, ct);

            // Then the floors that look least like it, to show the dungeon is not one room repeated.
            await Shoot(2, "04-abandoned-mall", new Vector2Int(10, 10), 12f, ct);
            await Shoot(4, "05-twisted-carnival", new Vector2Int(11, 11), 8f, ct, withDweller: true);

            MooseRunnerFacade.Log($"press shots written to {Folder}");
            Assert.Pass();
        }

        /// <summary>
        /// Builds a floor, points a camera at a cell and writes a PNG.
        /// </summary>
        /// <param name="floor">Floor number to build.</param>
        /// <param name="name">File name, without extension.</param>
        /// <param name="cell">Cell to stand in.</param>
        /// <param name="yaw">Degrees to turn off the longest sightline, for composition.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        /// <param name="withDweller">Whether to put a Dweller in shot.</param>
        private static async UniTask Shoot(int floor, string name, Vector2Int cell, float yaw,
            CancellationToken ct, bool withDweller = false)
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            FloorTheme theme = FloorThemes.ForFloor(floor);
            maze.GenerateAndBuild(floor * 977, theme, hasFloorAbove: floor > 1);
            FloorAtmosphere.Apply(theme);

            MazeLayout layout = maze.CurrentLayout;
            Vector2Int stand = Walkable(layout, cell);
            Vector3 eye = layout.CellCenterToWorld(stand) + Vector3.up * 1.7f;

            // Face down the longest open run from where we are standing. Choosing the angle by hand
            // put one camera nose-first into a corner: a picked cell can be open on two sides and
            // still show nothing but wall if you happen to look the wrong way.
            (Direction view, int run) = LongestView(layout, stand);
            float facing = view switch
            {
                Direction.North => 0f,
                Direction.East => 90f,
                Direction.South => 180f,
                _ => 270f
            };

            // Off the axis a little, so corridors read as space rather than as a flat elevation.
            facing += yaw;

            var camGo = new GameObject("PressCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.farClipPlane = 45f;
            cam.fieldOfView = 66f;
            camGo.transform.position = eye;
            camGo.transform.rotation = Quaternion.Euler(4f, facing, 0f);

            if (withDweller)
            {
                // Placed ahead and hunting, which is the only state worth a screenshot: a lurking
                // one is a dark shape doing nothing.
                // Well down the sightline, not in your face. At seven metres it filled the frame and
                // read as a jump scare — the end of a run rather than the middle of one. Far enough
                // back that the fog is still eating it, it is a shape with eyes that has noticed you,
                // which is the thing actually worth putting on a store page.
                float reach = Mathf.Min(17f, Mathf.Max(9f, (run - 0.5f) * layout.CellSize));
                Vector3 ahead = eye + Quaternion.Euler(0f, facing, 0f) * Vector3.forward * reach;
                Vector2Int at = layout.WorldToCell(ahead);

                var dwellerGo = new GameObject("Dweller");
                DwellerFacade dweller = dwellerGo.AddComponent<DwellerFacade>();
                dweller.SetKind(DwellerKind.Lurker);
                dweller.Place(layout, Walkable(layout, at), camGo.transform, 2.2f, floor * 13);

                // It notices the camera by itself within a few frames; a hunting Dweller is the only
                // one worth photographing, since a lurking one is a dark shape doing nothing.
                for (int i = 0; i < 8; i++) await UniTask.Yield(ct);
                MooseRunnerFacade.Log($"press dweller hunting={dweller.IsChasing}");
            }

            for (int i = 0; i < 6; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..", Folder));
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, name + ".png");
                File.WriteAllBytes(path, shot.EncodeToPNG());
                MooseRunnerFacade.Log($"press shot {name} at cell {stand} -> {path}");
            }
            finally
            {
                Object.Destroy(shot);
            }
        }

        /// <summary>
        /// The direction with the most open cells ahead of it, and how many.
        /// </summary>
        /// <param name="layout">The floor.</param>
        /// <param name="from">Cell being stood in.</param>
        /// <returns>The direction to face, and the run length in cells.</returns>
        private static (Direction, int) LongestView(MazeLayout layout, Vector2Int from)
        {
            Direction best = Direction.North;
            int bestRun = -1;

            foreach (Direction d in Directions.All)
            {
                Vector2Int step = Directions.Delta(d);
                Vector2Int at = from;
                int run = 0;

                while (run < 12 && layout.InBounds(at.x, at.y) && layout.CanMove(at.x, at.y, d))
                {
                    at += step;
                    run++;
                }

                if (run <= bestRun) continue;
                bestRun = run;
                best = d;
            }

            return (best, Mathf.Max(bestRun, 1));
        }

        /// <summary>
        /// The nearest cell to a wanted one that is open on at least two sides, so the camera is not
        /// jammed into a dead end.
        /// </summary>
        /// <param name="layout">The floor.</param>
        /// <param name="wanted">Preferred cell.</param>
        /// <returns>A cell worth standing in.</returns>
        private static Vector2Int Walkable(MazeLayout layout, Vector2Int wanted)
        {
            var best = new Vector2Int(
                Mathf.Clamp(wanted.x, 1, layout.Width - 2),
                Mathf.Clamp(wanted.y, 1, layout.Height - 2));

            for (int radius = 0; radius < 6; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = Mathf.Clamp(best.x + dx, 1, layout.Width - 2);
                        int y = Mathf.Clamp(best.y + dy, 1, layout.Height - 2);

                        int open = 0;
                        foreach (Direction d in Directions.All)
                        {
                            if (layout.CanMove(x, y, d)) open++;
                        }

                        if (open >= 2) return new Vector2Int(x, y);
                    }
                }
            }

            return best;
        }
    }
}
