using System.Collections.Generic;
using System.IO;
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
    /// Records a real death in the shipped game and photographs the ten seconds that follow it, so
    /// the end screen's pacing can be judged by eye rather than inferred from a fade value. It hunts
    /// down a Dweller, lets it catch the player, and then walks the whole sequence: the floor fading
    /// out under the numbers, the black screen, the retry being withheld, and the retry appearing.
    /// </summary>
    /// <remarks>
    /// Two things here cannot be checked any other way. Whether a five-second fade reads as a
    /// deliberate ending or as the game having hung is a judgement about a moving picture, and
    /// whether the retry is genuinely unclickable is a claim about the shipped scene rather than
    /// about a HUD in isolation — so this presses the button early and asserts nothing happened.
    /// <para>
    /// Marked <see cref="ExplicitAttribute"/> for the same reason as
    /// <see cref="PlaythroughRecording"/>: Unity Recorder logs benign "AudioRender ... called while
    /// system was not recording" errors on stop, and a suite that leaves errors in the console is not
    /// a clean pass. Run it deliberately with
    /// <c>test --class Backrooms.Gameplay.Tests DeathScreenRecording</c>.
    /// </para>
    /// </remarks>
    [Explicit("Records footage on demand; Unity Recorder logs benign errors when it stops. Remove this attribute to run it — NUnit skips Explicit tests even under --class selection.")]
    public class DeathScreenRecording
    {
        /// <summary>The scene under test, as shipped.</summary>
        private const string SceneName = "Backrooms";

        /// <summary>Where the session is written, relative to the project root.</summary>
        private const string OutputPath = ".mooserunner/Recordings/death-screen";

        /// <summary>Where the extracted stills are copied for review.</summary>
        private const string StillsFolder = "Screenshots/death";

        /// <summary>How long to spend trying to get killed before giving up, in seconds.</summary>
        private const float HuntBudgetSeconds = 240f;

        private static PlayerFacade _player;
        private static GameplayController _controller;
        private static HudFacade _hud;
        private static MazeFacade _maze;
        private static PlayerManagerTestFacade _input;
        private static SessionInfo _session;

        /// <summary>Which still of the sequence is being written next.</summary>
        private static int _still;

        /// <summary>
        /// Loads the shipped scene, starts the run the way a player does, and starts recording.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test, Order(0)]
        public async UniTask Step0_StartRecording(CancellationToken ct)
        {
            // Static, so it survives a second run in the same domain and would otherwise keep
            // counting from where the last one left off.
            _still = 0;

            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync(
                SceneName, forceReload: true, cleanDontDestroyOnLoad: true);

            for (int i = 0; i < 10; i++) await UniTask.Yield(ct);

            _player = Object.FindAnyObjectByType<PlayerFacade>();
            _controller = Object.FindAnyObjectByType<GameplayController>();
            _hud = Object.FindAnyObjectByType<HudFacade>();
            _maze = Object.FindAnyObjectByType<MazeFacade>();

            Assert.IsNotNull(_player, "the shipped scene must contain the player module");
            Assert.IsNotNull(_controller, "the shipped scene must contain the gameplay controller");
            Assert.IsNotNull(_hud, "the shipped scene must contain the HUD module");

            _input = _player.GetTestFacade();
            _input.SimulationEnabled = true;

            _input.Tap();
            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            _input.ClearInput();

            Assert.IsFalse(_controller.IsAwaitingStart, "the tap should have started the run");

            var config = new SessionRecordingConfig(_player.HeadCamera, OutputPath,
                videoFrameRate: 30);
            _session = await SessionRecorderFacade.Instance.StartRecordingAsync(config, ct);

            MooseRunnerFacade.Log($"recording to {_session.SessionPath}");
        }

        /// <summary>
        /// Walks into the nearest Dweller until it kills the player, then watches the whole ten
        /// seconds that follow: the fade filling in, an impatient click going nowhere, and the retry
        /// finally being offered.
        /// </summary>
        /// <remarks>
        /// The death and the wait are one test rather than two because the wait is timed from the
        /// death. A step boundary between them would put an unmeasured pause of the runner's choosing
        /// in the middle of the sequence, and the first assertion is about what the screen looks like
        /// two and a half seconds in.
        /// </remarks>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test, Order(1)]
        public async UniTask Step1_DieAndWatchTheScreenHold(CancellationToken ct)
        {
            float deadline = Time.time + HuntBudgetSeconds;

            while (!_controller.IsCaught && Time.time < deadline)
            {
                if (!TryFindNearestDweller(out Vector3 target))
                {
                    MooseRunnerFacade.Log("no active Dweller on this floor");
                    break;
                }

                // Once it has seen the player there is nothing left to do but stand still: it is
                // faster than walking towards it and it removes any chance of walking past.
                if (_controller.IsHunted)
                {
                    _input.ClearInput();
                    await UniTask.WaitForFixedUpdate(ct);
                    continue;
                }

                await StepTowards(target, ct);
            }

            _input.ClearInput();

            Assert.IsTrue(_controller.IsCaught,
                $"the test needs a death to photograph; none happened in {HuntBudgetSeconds:F0}s");
            Assert.IsTrue(_hud.CaughtShown, "the end screen should be up");
            MooseRunnerFacade.Log($"caught on floor {_controller.CurrentFloor} after "
                + $"{_controller.ElapsedSeconds:F1}s");

            await Photograph("caught", ct);

            await WaitOnDeathScreen(2.5f, ct);
            Assert.Greater(_hud.CaughtFade, 0f, "the floor should be fading out by 2.5s");
            Assert.Less(_hud.CaughtFade, 1f, "and should not have finished");
            await Photograph("fading", ct);

            // The impatient click, which is the whole reason for the wait: press it, release it, and
            // check the game ignored it. A player mashing at the screen must not skip the ending.
            _input.Tap();
            for (int i = 0; i < 4; i++) await UniTask.Yield(ct);
            _input.ClearInput();
            Assert.IsTrue(_controller.IsCaught, "a click at 2.5s must not start another run");

            await WaitOnDeathScreen(5.5f, ct);
            Assert.AreEqual(1f, _hud.CaughtFade, 1e-3f, "the floor should be gone by 5.5s");
            Assert.IsFalse(_hud.RetryOffered, "and the retry still withheld halfway through");
            await Photograph("black", ct);

            await WaitOnDeathScreen(9.5f, ct);
            Assert.IsFalse(_hud.RetryOffered, "nine and a half seconds is not ten");
            Assert.IsTrue(_controller.IsCaught, "still no way out of the end screen");
            await Photograph("waiting", ct);

            await WaitOnDeathScreen(10.5f, ct);
            Assert.IsTrue(_hud.RetryOffered, "past ten seconds the way out should appear");
            Assert.IsTrue(_controller.IsCaught, "showing the offer must not take it for the player");
            await Photograph("retry", ct);

            // And now the click that was refused before is accepted.
            _input.Tap();
            for (int i = 0; i < 6; i++) await UniTask.Yield(ct);
            _input.ClearInput();

            Assert.IsFalse(_controller.IsCaught, "a click after ten seconds should start a new run");
            Assert.AreEqual(1, _controller.CurrentFloor, "the new run starts at the top");

            // A moment of the fresh run on the end of the footage, so the recording shows the
            // sequence completing rather than cutting at the last frame of black.
            for (int i = 0; i < 60; i++) await UniTask.WaitForFixedUpdate(ct);
            await Photograph("restarted", ct);
        }

        /// <summary>
        /// Stops the recorder and says where the footage landed.
        /// </summary>
        [Test, Order(2)]
        public void Step2_StopRecording()
        {
            SessionRecorderFacade.Instance.StopRecording();

            MooseRunnerFacade.Log($"video at {Path.Combine(_session.SessionPath, "video.mp4")}");
            Assert.AreEqual(6, _still, "every moment of the sequence should have been photographed");
        }

        /// <summary>
        /// Writes the current frame into the stills folder, numbered in the order it was taken.
        /// </summary>
        /// <remarks>
        /// Captured live rather than cut out of the finished video afterwards. The first attempt did
        /// pull the stills from the mp4, and every one of them was about a second and a half late —
        /// the recorder's video does not start on the timestamp its session reports, so a frame asked
        /// for at 9.5s showed the screen as it was at eleven. That is exactly the kind of quiet
        /// offset that would have had a reviewer believing the retry appeared early. Shooting the
        /// frame at the instant the assertion runs cannot drift from what the assertion saw.
        /// </remarks>
        /// <param name="name">What this moment is called.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask Photograph(string name, CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string folder = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath, "..", StillsFolder));
                Directory.CreateDirectory(folder);

                _still++;
                string path = Path.Combine(folder, $"death-{_still:00}-{name}.png");
                File.WriteAllBytes(path, shot.EncodeToPNG());
                MooseRunnerFacade.Log(
                    $"{name} at {_hud.CaughtSeconds:F2}s, fade {_hud.CaughtFade:F2}, "
                    + $"retry {_hud.RetryOffered} -> {path}");
            }
            finally
            {
                Object.Destroy(shot);
            }
        }

        /// <summary>
        /// Waits until the end screen has been up for a given time, driving no input at all.
        /// </summary>
        /// <param name="age">Age of the end screen to wait for, in seconds since the death.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask WaitOnDeathScreen(float age, CancellationToken ct)
        {
            // Read from the HUD's own clock rather than a second one kept here: waiting on a
            // stopwatch this test owns would pass even if the HUD's clock had stopped.
            while (_hud.CaughtSeconds < age && _controller.IsCaught)
            {
                await UniTask.WaitForFixedUpdate(ct);
            }
        }

        /// <summary>
        /// Finds the closest Dweller still roaming the floor.
        /// </summary>
        /// <param name="position">Where that Dweller is standing.</param>
        /// <returns><c>true</c> if the floor has one.</returns>
        private static bool TryFindNearestDweller(out Vector3 position)
        {
            position = Vector3.zero;
            float best = float.PositiveInfinity;

            foreach (DwellerFacade dweller in
                     Object.FindObjectsByType<DwellerFacade>(FindObjectsSortMode.None))
            {
                if (!dweller.IsActive || !dweller.gameObject.activeInHierarchy) continue;

                float distance = Vector3.Distance(_player.Position, dweller.transform.position);
                if (distance >= best) continue;

                best = distance;
                position = dweller.transform.position;
            }

            return !float.IsPositiveInfinity(best);
        }

        /// <summary>
        /// Takes one step of a route towards a world position, following open passages.
        /// </summary>
        /// <param name="target">World position to head for.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask StepTowards(Vector3 target, CancellationToken ct)
        {
            MazeLayout layout = _maze.CurrentLayout;
            Vector2Int from = layout.WorldToCell(_player.Position);
            Vector2Int to = layout.WorldToCell(target);

            List<Vector2Int> route = FindRoute(layout, from, to);
            if (route == null || route.Count == 0)
            {
                await UniTask.WaitForFixedUpdate(ct);
                return;
            }

            // One cell at a time, re-planned on the next pass. The quarry moves, so a route walked
            // to the end is a route to where it used to be.
            Vector3 step = layout.CellCenterToWorld(route[0]);

            for (int i = 0; i < 90 && !_controller.IsCaught; i++)
            {
                Vector3 position = _player.Position;
                var toStep = new Vector3(step.x - position.x, 0f, step.z - position.z);
                if (toStep.magnitude <= 0.6f) break;

                Vector3 local = Quaternion.Euler(0f, -_input.Yaw, 0f) * toStep.normalized;
                float yawError = Mathf.DeltaAngle(
                    _input.Yaw, Mathf.Atan2(toStep.x, toStep.z) * Mathf.Rad2Deg);

                _input.SetInput(
                    move: new Vector2(local.x, local.z),
                    look: new Vector2(Mathf.Clamp(yawError, -6f, 6f), 0f),
                    sprint: true);

                await UniTask.WaitForFixedUpdate(ct);
            }
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
                Vector2Int current = queue.Dequeue();
                if (current == to) break;

                foreach (Direction dir in Directions.All)
                {
                    if (!layout.CanMove(current.x, current.y, dir)) continue;
                    Vector2Int delta = Directions.Delta(dir);
                    var next = new Vector2Int(current.x + delta.x, current.y + delta.y);
                    if (!seen.Add(next)) continue;
                    cameFrom[next] = current;
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
