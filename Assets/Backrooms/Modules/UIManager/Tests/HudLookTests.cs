using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.UIManager.Tests
{
    /// <summary>
    /// Photographs the HUD states into <c>Screenshots/</c>. Whether a warning is legible, and whether
    /// a border reads as alarming rather than as a rendering fault, are judgements only a picture can
    /// settle — the state tests can only say the flag was set.
    /// </summary>
    public class HudLookTests
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
        /// Photographs the pursuit warning at its faintest and at its most urgent, and the
        /// end-of-run screen a Dweller leaves behind.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask HuntedWarning_AndCaughtScreen_AreLegible(CancellationToken ct)
        {
            var camGo = new GameObject("HudCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            // A mid grey stands in for the level, so the warning is judged against something rather
            // than against black, which flatters any bright overlay.
            cam.backgroundColor = new Color(0.34f, 0.32f, 0.26f);

            var hudGo = new GameObject("Hud");
            HudFacade hud = hudGo.AddComponent<HudFacade>();
            hud.SetElapsed(74f);
            hud.ShowFloor(3, "JANKY LAUNDROMAT");

            hud.SetHunted(true, 0.15f);
            await Capture("hud-hunted-far", ct);

            hud.SetHunted(true, 1f);
            await Capture("hud-hunted-close", ct);

            hud.SetHunted(false, 0f);
            hud.ShowCaught(3, 74f);
            await Capture("hud-caught", ct);

            Assert.IsTrue(hud.CaughtShown, "the caught screen should be the one photographed last");
        }

        /// <summary>
        /// Writes the current frame into <c>Screenshots/</c> under a given name.
        /// </summary>
        /// <param name="name">File name without extension.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        private static async UniTask Capture(string name, CancellationToken ct)
        {
            for (int i = 0; i < 3; i++) await UniTask.Yield(ct);
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
