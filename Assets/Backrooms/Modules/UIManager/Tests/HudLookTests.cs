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

            // The HUD starts on the title screen, and the title draws instead of everything else.
            // Without this every capture below photographs the title — which is exactly what they
            // silently did from the moment the title screen was added, so the pursuit warning and
            // the end screen went unverified while the test still passed.
            hud.HideTitle();
            hud.SetElapsed(74f);
            hud.ShowFloor(3, "JANKY LAUNDROMAT", 1);

            // The compass arrows and carried list, which only appear once relics are held.
            hud.SetCompass(new[]
            {
                new CompassMark(-38f, 21f, new Color(1f, 0.35f, 0.75f)),
                new CompassMark(4f, 63f, new Color(0.45f, 1f, 0.8f)),
                new CompassMark(72f, 44f, new Color(0.72f, 0.42f, 1f))
            });
            hud.SetCarried(
                new[]
                {
                    "DEFENSE WARD  x1",
                    "BANISHER  x3  (G / double-tap left)",
                    "BLINK SHARD  (F / double-tap right)"
                },
                new[]
                {
                    new Color(1f, 0.92f, 0.55f),
                    new Color(1f, 0.62f, 0.25f),
                    new Color(0.55f, 0.78f, 1f)
                });
            hud.SetMap(StandInMap(), new Vector2(0.42f, 0.55f), 20f / 24f, new[]
            {
                new Vector2(0.30f, 0.44f),
                new Vector2(0.55f, 0.62f),
                new Vector2(0.47f, 0.30f),
                new Vector2(0.62f, 0.50f)
            });
            await Capture("hud-powers", ct);

            hud.SetHunted(true, 0.15f);
            await Capture("hud-hunted-far", ct);

            hud.SetHunted(true, 1f);
            await Capture("hud-hunted-close", ct);

            // The end screen is now a ten-second sequence rather than one picture, so it is
            // photographed at the three moments that define it: mid-fade with the floor still
            // showing, fully black, and the moment the way out appears. A single capture at the
            // instant of death would show an almost empty frame and prove nothing.
            hud.SetHunted(false, 0f);
            UIManagerTestFacade seam = hud.GetTestFacade();

            hud.ShowCaught(3, 74f, relics: 2, bestFloors: 5, bestRelics: 4);
            Advance(seam, 2.5f);
            await Capture("hud-caught-fading", ct);

            Advance(seam, 5f);
            await Capture("hud-caught-black", ct);

            Advance(seam, 10.4f);
            await Capture("hud-caught-retry", ct);

            // The other half of the end screen: a run that set the record must not restate its own
            // numbers under a "best" heading, which is what made it read as doubled.
            hud.ShowCaught(6, 132f, relics: 5, bestFloors: 6, bestRelics: 5);
            Advance(seam, 10.4f);
            await Capture("hud-caught-record", ct);

            // And the other ending, which most players will only ever see once.
            hud.ShowVictory(floors: 6, finalSeconds: 412f, relics: 47, score: 13175,
                floorPoints: 2000, relicPoints: 25);
            Advance(seam, 4.4f);
            await Capture("hud-victory-scoring", ct);

            Advance(seam, 10.9f);
            await Capture("hud-victory", ct);

            Assert.IsTrue(hud.VictoryShown, "the victory screen should be the one photographed last");
            Assert.IsTrue(hud.RetryOffered, "and should be showing its way out by now");
        }

        /// <summary>
        /// Winds the end screen's clock forward to a given age, so each moment of the death sequence
        /// can be photographed without waiting for it in real time.
        /// </summary>
        /// <param name="seam">Test seam over the HUD being photographed.</param>
        /// <param name="seconds">Age of the end screen to advance to, in seconds since the death.</param>
        private static void Advance(UIManagerTestFacade seam, float seconds)
        {
            // In small steps rather than one jump: the same path the running game takes, so a fade
            // that only behaves for a single large delta would not pass here either.
            while (seam.EndScreenSeconds < seconds) seam.TickBanner(0.05f);
        }

        /// <summary>
        /// Builds a stand-in floor map: a grid of rooms, so the corner map can be judged for size,
        /// placement and whether the player marker lands where it should.
        /// </summary>
        /// <returns>A map-shaped texture.</returns>
        private static Texture2D StandInMap()
        {
            const int cells = 24;
            const int px = 8;
            var map = new Texture2D(cells * px, cells * px, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[map.width * map.height];
            for (int y = 0; y < map.height; y++)
            {
                for (int x = 0; x < map.width; x++)
                {
                    bool wall = x % px == 0 || y % px == 0;
                    pixels[y * map.width + x] = wall
                        ? new Color32(18, 17, 14, 235)
                        : new Color32(196, 190, 168, 190);
                }
            }

            map.SetPixels32(pixels);
            map.Apply();
            return map;
        }

        /// <summary>
        /// The corner map must appear only when something has supplied one, so a player without the
        /// relic gets no map rather than an empty frame in the corner.
        /// </summary>
        [Test]
        public void CornerMap_ShowsOnlyWhenAMapIsSupplied()
        {
            var hudGo = new GameObject("Hud");
            HudFacade hud = hudGo.AddComponent<HudFacade>();

            Assert.IsFalse(hud.MapShown, "nothing has supplied a map yet");

            hud.SetMap(StandInMap(), new Vector2(0.5f, 0.5f), 0.8f);
            Assert.IsTrue(hud.MapShown, "a supplied map should be drawn");

            hud.SetMap(null, Vector2.zero, 1f);
            Assert.IsFalse(hud.MapShown, "dropping the relic should take the map away");
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
