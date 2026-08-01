using System.Collections.Generic;
using System.Threading;
using Backrooms.EntityManager;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using Backrooms.UIManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.SessionRecorder;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.Gameplay.Tests
{
    /// <summary>
    /// Records a playthrough of the shipped game to video, for human and reviewer eyes rather than
    /// for assertions. It plays the real scene through simulated input exactly as
    /// <see cref="EscapeLevel0E2E"/> does — descending floors by walking to a stairwell — and leaves
    /// an mp4 plus a per-object motion log under <c>.mooserunner/Recordings/playthrough</c>.
    /// </summary>
    /// <remarks>
    /// Marked <see cref="ExplicitAttribute"/> on purpose. Unity Recorder logs benign
    /// "AudioRender ... called while system was not recording" errors when it stops, and a suite that
    /// leaves errors in the console is not a clean pass — so this is opted into when footage is
    /// wanted, rather than run on every sweep. Run it with
    /// <c>test --class Backrooms.Gameplay.Tests PlaythroughRecording</c>.
    /// </remarks>
    [Explicit("Records footage on demand; Unity Recorder logs benign errors when it stops. Remove this attribute to run it — NUnit skips Explicit tests even under --class selection.")]
    public class PlaythroughRecording
    {
        /// <summary>The scene under test, as shipped.</summary>
        private const string SceneName = "Backrooms";

        /// <summary>Where the session is written, relative to the project root.</summary>
        private const string OutputPath = ".mooserunner/Recordings/playthrough";

        /// <summary>How close to a waypoint counts as having reached it, in metres.</summary>
        private const float WaypointRadius = 0.6f;

        /// <summary>How many floors to play through before stopping the camera.</summary>
        private const int FloorsToPlay = 3;

        private static MazeFacade _maze;
        private static PlayerFacade _player;
        private static GameplayController _controller;
        private static HudFacade _hud;
        private static PlayerManagerTestFacade _input;
        private static SessionInfo _session;

        /// <summary>
        /// Loads the shipped scene and starts recording from the player's own camera.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test, Order(0)]
        public async UniTask Step0_StartRecording(CancellationToken ct)
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync(
                SceneName, forceReload: true, cleanDontDestroyOnLoad: true);

            for (int i = 0; i < 10; i++) await UniTask.Yield(ct);

            _maze = Object.FindAnyObjectByType<MazeFacade>();
            _player = Object.FindAnyObjectByType<PlayerFacade>();
            _controller = Object.FindAnyObjectByType<GameplayController>();
            _hud = Object.FindAnyObjectByType<HudFacade>();

            Assert.IsNotNull(_maze, "the shipped scene must contain the maze module");
            Assert.IsNotNull(_player, "the shipped scene must contain the player module");
            Assert.IsNotNull(_controller, "the shipped scene must contain the gameplay controller");

            _input = _player.GetTestFacade();
            _input.SimulationEnabled = true;

            // Past the title the way a player gets past it.
            _input.Tap();
            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            _input.ClearInput();

            // Tag the Dwellers so the recording's motion log says where each one was and when,
            // which is what turns "something chased me" into something reviewable after the fact.
            foreach (DwellerFacade dweller in
                     Object.FindObjectsByType<DwellerFacade>(FindObjectsSortMode.None))
            {
                if (dweller.GetComponent<RecordableObject>() == null)
                {
                    dweller.gameObject.AddComponent<RecordableObject>();
                }
            }

            var config = new SessionRecordingConfig(
                _player.HeadCamera, OutputPath, videoFrameRate: 30);
            _session = await SessionRecorderFacade.Instance.StartRecordingAsync(config, ct);

            MooseRunnerFacade.Log($"recording to {_session.SessionPath}");
            Assert.IsNotNull(_session, "the recorder should have started a session");
        }

        /// <summary>
        /// Plays the game: walks to the nearest stairwell on each floor in turn, descending until the
        /// floor budget is spent or a Dweller ends the run.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test, Order(1)]
        public async UniTask Step1_PlayThroughFloors(CancellationToken ct)
        {
            for (int played = 0; played < FloorsToPlay; played++)
            {
                if (_controller.IsCaught) break;

                int floor = _controller.CurrentFloor;
                MazeLayout layout = _maze.CurrentLayout;
                Vector2Int from = layout.WorldToCell(_player.Position);
                Vector2Int stairs = layout.NearestStairs(from);

                List<Vector2Int> route = FindRoute(layout, from, stairs);
                if (route == null)
                {
                    MooseRunnerFacade.Log($"floor {floor}: no route to stairs at {stairs}");
                    break;
                }

                MooseRunnerFacade.Log(
                    $"floor {floor}: walking {route.Count} cells to stairs at {stairs}");

                foreach (Vector2Int cell in route)
                {
                    if (_controller.IsCaught) break;
                    if (_controller.CurrentFloor != floor) break;
                    if (!await WalkTo(layout.CellCenterToWorld(cell), ct)) break;
                }

                if (_controller.IsCaught)
                {
                    MooseRunnerFacade.Log($"caught on floor {floor} — recording the end screen");
                    _input.ClearInput();

                    // Hold on the end screen so the footage shows it, then take the retry.
                    for (int i = 0; i < 120; i++) await UniTask.WaitForFixedUpdate(ct);
                    break;
                }

                if (_controller.CurrentFloor == floor)
                {
                    MooseRunnerFacade.Log($"floor {floor}: did not reach the stairs");
                    break;
                }

                // Stand still a moment on arrival so the floor banner is legible in the video.
                _input.ClearInput();
                for (int i = 0; i < 90; i++) await UniTask.WaitForFixedUpdate(ct);
            }

            _input.ClearInput();
            MooseRunnerFacade.Log(
                $"playthrough ended on floor {_controller.CurrentFloor} after "
                + $"{_controller.ElapsedSeconds:F1}s, caught={_controller.IsCaught}, "
                + $"hunted={_controller.IsHunted}, dwellers={_controller.DwellerCount}");
        }

        /// <summary>
        /// Stops the recorder and reports where the footage landed.
        /// </summary>
        [Test, Order(2)]
        public void Step2_StopRecording()
        {
            SessionRecorderFacade.Instance.StopRecording();
            MooseRunnerFacade.Log($"session written to {_session.SessionPath}");
            Assert.IsNotNull(_session.SessionPath, "the session should have a path on disk");
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
                if (_controller.CurrentFloor != floorOnEntry || _controller.IsCaught) return true;

                Vector3 pos = _player.Position;
                var toTarget = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                if (toTarget.magnitude <= WaypointRadius) return true;

                Vector3 local = Quaternion.Euler(0f, -_input.Yaw, 0f) * toTarget.normalized;
                float yawError = Mathf.DeltaAngle(
                    _input.Yaw, Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg);

                // Walking rather than sprinting: this is footage of the game being played, and a
                // sprint past every room shows none of them.
                _input.SetInput(
                    move: new Vector2(local.x, local.z),
                    look: new Vector2(Mathf.Clamp(yawError, -6f, 6f), 0f),
                    sprint: false);

                await UniTask.WaitForFixedUpdate(ct);
            }

            return false;
        }

        /// <summary>
        /// Breadth-first search for the shortest cell route between two cells, following only open
        /// passages.
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
