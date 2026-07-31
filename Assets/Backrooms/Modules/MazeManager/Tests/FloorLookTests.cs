using System.IO;
using System.Threading;
using Backrooms.MazeManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// Visual inspection of the built floors. These tests assert only that geometry and props were
    /// produced, but their real value is the screenshots they leave in <c>Screenshots/</c>: an
    /// elevated view of each floor, which is the only way to judge whether a floor actually looks
    /// furnished. State assertions cannot tell you a level looks like a bare 1980s grid.
    /// </summary>
    public class FloorLookTests
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
        /// Builds one floor with its palette and photographs it from above the corner, then confirms
        /// props and trim were generated.
        /// </summary>
        /// <param name="floor">Floor number to build and photograph.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask BuildAndPhotograph(int floor, CancellationToken ct)
        {
            const string tag = "";
            FloorTheme theme = FloorThemes.ForFloor(floor);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.53f, 0.45f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = theme.Fog;
            RenderSettings.fogDensity = 0.012f;

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(floor * 977, theme);

            MazeLayout layout = maze.CurrentLayout;
            float span = layout.Width * layout.CellSize;

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(-span * 0.10f, 9.5f, -span * 0.10f);
            camGo.transform.rotation = Quaternion.Euler(28f, 42f, 0f);
            cam.fieldOfView = 70f;
            cam.farClipPlane = 200f;

            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");
                Directory.CreateDirectory(dir);
                string path = Path.GetFullPath(
                    Path.Combine(dir, $"floor-{floor}{tag}-{theme.Name.Replace(' ', '-')}.png"));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                MooseRunnerFacade.Log($"photographed floor {floor} ({theme.Name}) -> {path}");
            }
            finally
            {
                Object.Destroy(shot);
            }

            // The overhead view proves props exist; this is the view a player actually gets.
            Vector2Int room = FindOpenRoomCell(layout);
            Vector3 eye = layout.CellCenterToWorld(room) + Vector3.up * 1.7f;
            camGo.transform.position = eye;
            camGo.transform.rotation = Quaternion.Euler(4f, 35f, 0f);
            cam.fieldOfView = 66f;

            for (int i = 0; i < 3; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D eyeShot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");
                string path = Path.GetFullPath(
                    Path.Combine(dir, $"eye-{floor}{tag}-{theme.Name.Replace(' ', '-')}.png"));
                File.WriteAllBytes(path, eyeShot.EncodeToPNG());
                MooseRunnerFacade.Log($"eye-level floor {floor} at cell {room} -> {path}");
            }
            finally
            {
                Object.Destroy(eyeShot);
            }

            Assert.IsNotNull(GameObject.Find("Props"), "the floor should be furnished with props");
            Assert.IsNotNull(GameObject.Find("Trim"), "the floor should have skirting trim");
        }

        /// <summary>
        /// Finds a cell in the middle of an open room, so the eye-level shot looks across a space
        /// rather than into the nearest wall.
        /// </summary>
        /// <param name="layout">The floor to search.</param>
        /// <returns>A cell with open passages all around, or the grid centre as a fallback.</returns>
        private static Vector2Int FindOpenRoomCell(MazeLayout layout)
        {
            for (int y = 1; y < layout.Height - 1; y++)
            {
                for (int x = 1; x < layout.Width - 1; x++)
                {
                    if (layout.CanMove(x, y, Direction.North) && layout.CanMove(x, y, Direction.East)
                        && layout.CanMove(x, y, Direction.South) && layout.CanMove(x, y, Direction.West))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }

            return new Vector2Int(layout.Width / 2, layout.Height / 2);
        }

        /// <summary>
        /// Photographs a bookcase head-on. Whether a piece of furniture faces into the room or has
        /// its back turned cannot be asserted from a transform alone — the model's own facing
        /// convention decides it — so this leaves a picture to check.
        /// </summary>
        [Test]
        public async UniTask Furniture_FacesIntoTheRoom(CancellationToken ct)
        {
            FloorTheme theme = FloorThemes.ForFloor(1);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.68f, 0.6f);
            RenderSettings.fog = false;

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(977, theme);

            // Walk up to the instance root — the child directly under "Props" — rather than the
            // renderer's immediate parent, which for these models is the Props root at the origin.
            GameObject bookcase = null;
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Transform t = r.transform;
                while (t.parent != null && t.parent.name != "Props") t = t.parent;
                if (t.parent == null || !t.name.StartsWith("bookcase")) continue;
                bookcase = t.gameObject;
                break;
            }

            Assert.IsNotNull(bookcase, "the office floor should place bookcases against walls");

            // Stand in front of the piece, looking back at it from inside the room.
            // The piece's forward now aims at the wall, so step back the opposite way to stand in
            // the room looking at its front.
            Vector3 front = bookcase.transform.forward;
            Vector3 eye = bookcase.transform.position - front * 2.4f + Vector3.up * 1.5f;
            MooseRunnerFacade.Log($"bookcase at {bookcase.transform.position}, camera at {eye}");

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = eye;
            camGo.transform.rotation = Quaternion.LookRotation(
                bookcase.transform.position + Vector3.up * 0.9f - eye);
            cam.fieldOfView = 55f;

            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");
                Directory.CreateDirectory(dir);
                string path = Path.GetFullPath(Path.Combine(dir, "facing-bookcase.png"));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                MooseRunnerFacade.Log($"bookcase {bookcase.name} photographed -> {path}");
            }
            finally
            {
                Object.Destroy(shot);
            }
        }

        /// <summary>
        /// Photographs the yellow entry floor.
        /// </summary>
        [Test]
        public async UniTask Floor1_YellowRooms_LooksFurnished(CancellationToken ct)
            => await BuildAndPhotograph(1, ct);

        /// <summary>
        /// Photographs the mall floor, which should show shopfronts.
        /// </summary>
        [Test]
        public async UniTask Floor2_Mall_LooksFurnished(CancellationToken ct)
            => await BuildAndPhotograph(2, ct);

        /// <summary>
        /// Photographs the carnival floor, the most colourful of the set.
        /// </summary>
        [Test]
        public async UniTask Floor4_Carnival_LooksFurnished(CancellationToken ct)
            => await BuildAndPhotograph(4, ct);
    }
}
