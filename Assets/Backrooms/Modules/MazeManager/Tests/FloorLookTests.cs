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

            // Use the production atmosphere path, not a copy of it: photographing settings the game
            // never applies makes these screenshots lie about what ships.
            FloorAtmosphere.Apply(theme);

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            // Floor 1 ships with no ways up, so it must be photographed with none either —
            // a capture generated differently from the game is a picture of a game nobody plays.
            maze.GenerateAndBuild(floor * 977, theme, hasFloorAbove: floor > 1);

            MazeLayout layout = maze.CurrentLayout;
            float span = layout.Width * layout.CellSize;

            // A perspective view through runtime fog photographed haze, not a layout. Plan view with
            // fog off and the ceiling hidden is the only way to actually audit walls, rooms and where
            // the furniture ended up.
            float depth = layout.Height * layout.CellSize;
            GameObject ceiling = GameObject.Find("Ceiling");
            if (ceiling != null) ceiling.SetActive(false);
            bool fogWas = RenderSettings.fog;
            RenderSettings.fog = false;

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(span, depth) * 0.5f + 2f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.transform.position = new Vector3(span * 0.5f, 40f, depth * 0.5f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

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

            if (ceiling != null) ceiling.SetActive(true);
            RenderSettings.fog = fogWas;
            cam.orthographic = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.farClipPlane = 45f;

            // The plan view proves the layout; this is the view a player actually gets.
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

            GameObject props = GameObject.Find("Props");
            Assert.IsNotNull(props, "the floor should be furnished with props");
            Assert.IsNotNull(GameObject.Find("Trim"), "the floor should have skirting trim");

            // Furniture count is the one number that decides both how dressed a floor looks and how
            // much a phone has to draw, and neither screenshot shows it. Log it so tuning coverage is
            // done against a measurement rather than an impression.
            MooseRunnerFacade.Log(
                $"floor {floor} ({theme.Name}): {props.transform.childCount} props over "
                + $"{layout.Width}x{layout.Height} cells");
            Assert.Greater(props.transform.childCount, 0, "the floor should have furniture on it");
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
            FloorAtmosphere.Apply(theme);
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

            // Read the name now, not after the awaits below. Props rejected for overlapping a
            // neighbour are destroyed with Object.Destroy, which Unity defers to the end of the
            // frame — so a prop can still be found, and still be gone a frame later. Holding a
            // reference across a yield and then touching it is what made this test flaky.
            string bookcaseName = bookcase.name;
            Vector3 bookcaseAt = bookcase.transform.position;

            // Stand in front of the piece, looking back at it from inside the room.
            // The piece's forward now aims at the wall, so step back the opposite way to stand in
            // the room looking at its front.
            Vector3 front = bookcase.transform.forward;
            Vector3 eye = bookcaseAt - front * 2.4f + Vector3.up * 1.5f;
            MooseRunnerFacade.Log($"bookcase at {bookcaseAt}, camera at {eye}");

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = eye;
            camGo.transform.rotation = Quaternion.LookRotation(
                bookcaseAt + Vector3.up * 0.9f - eye);
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
                MooseRunnerFacade.Log($"bookcase {bookcaseName} photographed -> {path}");
            }
            finally
            {
                Object.Destroy(shot);
            }
        }

        /// <summary>
        /// Photographs a stairwell from the approach and from above. A hole cut in the floor mesh,
        /// a shaft lining and a flight of treads are three separate pieces of geometry that have to
        /// line up in world space; nothing asserts that they do, and a gap between them shows as a
        /// view straight out of the level.
        /// </summary>
        [Test]
        public async UniTask Stairwell_ReadsAsAWayDown(CancellationToken ct)
        {
            FloorTheme theme = FloorThemes.ForFloor(1);
            FloorAtmosphere.Apply(theme);

            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            maze.GenerateAndBuild(977, theme);

            MazeLayout layout = maze.CurrentLayout;
            Assert.IsNotEmpty(layout.Stairs, "the floor should carry stairwells");

            Vector2Int cell = layout.Stairs[0];
            Vector3 centre = layout.CellCenterToWorld(cell);
            MooseRunnerFacade.Log($"stairwell at cell {cell}, {layout.Stairs.Count} on this floor");

            var camGo = new GameObject("InspectionCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 66f;
            cam.farClipPlane = 60f;

            // Stand in the neighbouring cell so the approach is not photographed through a wall.
            Vector3 approach = ApproachOffset(layout, cell) * layout.CellSize * 0.95f;
            camGo.transform.position = centre + approach + Vector3.up * 1.7f;
            camGo.transform.rotation = Quaternion.LookRotation(centre + Vector3.up * 0.2f
                                                               - camGo.transform.position);

            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);
            await Capture("stairs-approach", ct);

            // Fog hides the bottom of the shaft from the approach; look straight in without it.
            bool fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
            camGo.transform.position = centre + approach * 0.45f + Vector3.up * 3.6f;
            camGo.transform.rotation = Quaternion.LookRotation(centre + Vector3.down * 1.5f
                                                               - camGo.transform.position);

            for (int i = 0; i < 3; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);
            await Capture("stairs-shaft", ct);

            // And the way up the player arrives out of, which is the spawn cell.
            Vector2Int upCell = layout.Spawn;
            Assert.IsTrue(layout.IsStairsUp(upCell), "the player should arrive out of a way up");

            Vector3 upAt = layout.CellCenterToWorld(upCell);
            Vector3 upApproach = ApproachOffset(layout, upCell) * layout.CellSize * 1.1f;
            camGo.transform.position = upAt + upApproach + Vector3.up * 1.7f;
            camGo.transform.rotation = Quaternion.LookRotation(upAt + Vector3.up * 2.2f
                                                               - camGo.transform.position);

            for (int i = 0; i < 3; i++) await UniTask.Yield(ct);
            await UniTask.WaitForEndOfFrame(ct);
            await Capture("stairs-up", ct);

            RenderSettings.fog = fogWas;
        }

        /// <summary>
        /// A unit offset towards a cell the player could approach a stairwell from.
        /// </summary>
        /// <param name="layout">The floor being photographed.</param>
        /// <param name="cell">The stairwell's cell.</param>
        /// <returns>A unit world offset, defaulting to south if the cell is walled in.</returns>
        private static Vector3 ApproachOffset(MazeLayout layout, Vector2Int cell)
        {
            foreach (Direction dir in Directions.All)
            {
                if (!layout.CanMove(cell.x, cell.y, dir)) continue;
                Vector2Int d = Directions.Delta(dir);
                return new Vector3(d.x, 0f, d.y);
            }

            return Vector3.back;
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
        /// Photographs the laundromat floor, which should show washers and dryers.
        /// </summary>
        [Test]
        public async UniTask Floor3_Laundromat_LooksFurnished(CancellationToken ct)
            => await BuildAndPhotograph(3, ct);

        /// <summary>
        /// Photographs the asylum floor, the darkest of the set.
        /// </summary>
        [Test]
        public async UniTask Floor5_Asylum_LooksFurnished(CancellationToken ct)
            => await BuildAndPhotograph(5, ct);

        /// <summary>
        /// Photographs the carnival floor, the most colourful of the set.
        /// </summary>
        [Test]
        public async UniTask Floor4_Carnival_LooksFurnished(CancellationToken ct)
            => await BuildAndPhotograph(4, ct);
    }
}
