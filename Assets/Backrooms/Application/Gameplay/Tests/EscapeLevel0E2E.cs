using System.Collections.Generic;
using System.IO;
using System.Threading;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.Gameplay.Tests
{
    /// <summary>
    /// Black-box end-to-end run of Level 0 in the real, shipped gameplay scene. The steps are ordered
    /// and share state: the scene is loaded once, the player is inspected where the game put them,
    /// and the maze is then solved by <b>actually walking it</b> with simulated player input until
    /// the game itself reports an escape. Nothing is teleported and no production method is called to
    /// cause an effect — the test only reads state and supplies input.
    /// </summary>
    public class EscapeLevel0E2E
    {
        /// <summary>The scene under test, as shipped.</summary>
        private const string SceneName = "Backrooms";

        /// <summary>How close to a waypoint counts as having reached it, in metres.</summary>
        private const float WaypointRadius = 0.6f;

        private static MazeFacade _maze;
        private static PlayerFacade _player;
        private static GameplayController _controller;
        private static PlayerManagerTestFacade _input;

        /// <summary>
        /// Loads the real gameplay scene and waits for the level to build itself.
        /// </summary>
        [Test, Order(0)]
        public async UniTask Step0_LoadRealScene(CancellationToken ct)
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync(
                SceneName, forceReload: true, cleanDontDestroyOnLoad: true);

            // Let Awake/Start run so the scene builds its own level.
            for (int i = 0; i < 10; i++) await UniTask.Yield(ct);

            _maze = Object.FindAnyObjectByType<MazeFacade>();
            _player = Object.FindAnyObjectByType<PlayerFacade>();
            _controller = Object.FindAnyObjectByType<GameplayController>();

            Assert.IsNotNull(_maze, "the shipped scene must contain the maze module");
            Assert.IsNotNull(_player, "the shipped scene must contain the player module");
            Assert.IsNotNull(_controller, "the shipped scene must contain the gameplay controller");
            Assert.IsNotNull(_maze.CurrentLayout, "the scene should have generated a maze on start");
        }

        /// <summary>
        /// The level built itself: geometry exists in the scene and the player was placed in the
        /// maze's spawn cell rather than left at the origin.
        /// </summary>
        [Test, Order(1)]
        public async UniTask Step1_LevelBuiltAndPlayerSpawned(CancellationToken ct)
        {
            MazeLayout layout = _maze.CurrentLayout;

            Assert.IsNotNull(GameObject.Find("MazeGeometry"), "maze geometry should be in the scene");

            Vector2Int playerCell = layout.WorldToCell(_player.Position);
            MooseRunnerFacade.Log($"player spawned in cell {playerCell}, maze spawn is {layout.Spawn}");
            Assert.AreEqual(layout.Spawn, playerCell, "player should start in the maze's spawn cell");
            Assert.AreEqual(1, _controller.CurrentFloor, "a run starts on the first floor");

            await Capture("01-spawn", ct);
        }

        /// <summary>
        /// Supplying forward input moves the player, proving the shipped scene's input chain reaches
        /// the character controller.
        /// </summary>
        [Test, Order(2)]
        public async UniTask Step2_PlayerRespondsToInput(CancellationToken ct)
        {
            _input = _player.GetTestFacade();
            _input.SimulationEnabled = true;

            Vector3 start = _player.Position;
            _input.SetInput(move: new Vector2(0f, 1f));
            for (int i = 0; i < 30; i++) await UniTask.WaitForFixedUpdate(ct);
            _input.ClearInput();

            float moved = Vector3.Distance(start, _player.Position);
            MooseRunnerFacade.Log($"player moved {moved:F2}m from forward input");
            Assert.Greater(moved, 0.2f, "player should respond to movement input");
        }

        /// <summary>
        /// Walks the maze from the player's current cell to the exit, following a route the test
        /// works out for itself from the public layout, and asserts the game drops them a floor.
        /// </summary>
        [Test, Order(3)]
        public async UniTask Step3_WalkToExit_GameReportsEscape(CancellationToken ct)
        {
            MazeLayout layout = _maze.CurrentLayout;
            List<Vector2Int> route = FindRoute(layout, layout.WorldToCell(_player.Position), layout.Exit);
            Assert.IsNotNull(route, "a route to the exit must exist in a perfect maze");
            MooseRunnerFacade.Log($"route to exit is {route.Count} cells");

            int startFloor = _controller.CurrentFloor;
            int index = 0;
            foreach (Vector2Int cell in route)
            {
                bool reached = await WalkTo(layout.CellCenterToWorld(cell), ct);
                if (!reached)
                {
                    MooseRunnerFacade.Log($"stuck heading to cell {cell}");
                    break;
                }

                if (++index == 6)
                {
                    _input.ClearInput();
                    await Capture("02-corridor", ct);
                }

                // Reaching the exit drops the player to the next floor, which rebuilds the level.
                if (_controller.CurrentFloor > startFloor) break;
            }

            _input.ClearInput();
            await Capture("03-nextfloor", ct);
            MooseRunnerFacade.Log(
                $"reached floor {_controller.CurrentFloor} after {_controller.ElapsedSeconds:F1}s");
            Assert.AreEqual(startFloor + 1, _controller.CurrentFloor,
                "reaching the exit should descend one floor");
        }

        /// <summary>
        /// Captures what the player's camera currently sees to a PNG under <c>Screenshots/</c>, so
        /// the rendered result can be inspected after the run. State assertions cannot catch a black
        /// screen, a missing material or fog that hides everything — a frame can.
        /// </summary>
        /// <param name="label">File name stem for the capture.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask Capture(string label, CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string dir = Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");
                Directory.CreateDirectory(dir);
                string path = Path.GetFullPath(Path.Combine(dir, $"{label}.png"));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                MooseRunnerFacade.Log($"captured {label} -> {path} ({shot.width}x{shot.height})");
            }
            finally
            {
                Object.Destroy(shot);
            }
        }

        /// <summary>
        /// Steers the player towards a world position with simulated input until they arrive or the
        /// attempt times out.
        /// </summary>
        /// <param name="target">World position to walk to.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        /// <returns><c>true</c> if the player reached the target.</returns>
        private static async UniTask<bool> WalkTo(Vector3 target, CancellationToken ct)
        {
            const int maxSteps = 400;
            int floorOnEntry = _controller.CurrentFloor;

            for (int step = 0; step < maxSteps; step++)
            {
                // Descending rebuilds the level and moves the player, so this waypoint is moot.
                if (_controller.CurrentFloor != floorOnEntry) return true;

                Vector3 pos = _player.Position;
                Vector3 toTarget = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                if (toTarget.magnitude <= WaypointRadius) return true;

                // Convert the world-space heading into body-local movement intent.
                Vector3 local = Quaternion.Euler(0f, -_input.Yaw, 0f) * toTarget.normalized;

                // Turn the head towards travel so the run also looks right on camera.
                float yawError = Mathf.DeltaAngle(
                    _input.Yaw, Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg);

                _input.SetInput(
                    move: new Vector2(local.x, local.z),
                    look: new Vector2(Mathf.Clamp(yawError, -8f, 8f), 0f),
                    sprint: true);

                await UniTask.WaitForFixedUpdate(ct);
            }

            return false;
        }

        /// <summary>
        /// Breadth-first search for the shortest cell route between two cells, following only open
        /// passages. This is the test working out its own way through the maze from public state.
        /// </summary>
        /// <param name="layout">The maze layout to search.</param>
        /// <param name="from">Starting cell.</param>
        /// <param name="to">Destination cell.</param>
        /// <returns>The route excluding the start cell, or <c>null</c> if unreachable.</returns>
        private static List<Vector2Int> FindRoute(MazeLayout layout, Vector2Int from, Vector2Int to)
        {
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                if (cur == to) break;

                foreach (Direction dir in Directions.All)
                {
                    if (!layout.CanMove(cur.x, cur.y, dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    var next = new Vector2Int(cur.x + d.x, cur.y + d.y);
                    if (!seen.Add(next)) continue;
                    cameFrom[next] = cur;
                    queue.Enqueue(next);
                }
            }

            if (!seen.Contains(to)) return null;

            var route = new List<Vector2Int>();
            for (Vector2Int c = to; c != from; c = cameFrom[c]) route.Add(c);
            route.Reverse();
            return route;
        }
    }
}
