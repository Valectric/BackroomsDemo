using Backrooms.UIManager;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.UIManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for the heads-up display's state and time formatting. They assert
    /// what the player would read on screen without rendering a frame, so the checks stay
    /// deterministic and resolution-independent.
    /// </summary>
    public class HudStateTests
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
        /// Creates a HUD module in the scene and returns its test facade.
        /// </summary>
        /// <returns>A test facade over a new HUD module.</returns>
        private static (HudFacade facade, UIManagerTestFacade test) NewHud()
        {
            var go = new GameObject("Hud");
            HudFacade facade = go.AddComponent<HudFacade>();
            return (facade, facade.GetTestFacade());
        }

        /// <summary>
        /// A fresh HUD shows a zeroed timer and no end-of-run banner.
        /// </summary>
        [Test]
        public void NewHud_StartsAtZero_WithNoBanner()
        {
            (HudFacade _, UIManagerTestFacade test) = NewHud();

            Assert.AreEqual(0f, test.ElapsedSeconds, 1e-4f, "timer starts at zero");
            Assert.IsFalse(test.EscapedShown, "banner hidden at the start of a run");
        }

        /// <summary>
        /// The timer reflects the elapsed time it is given.
        /// </summary>
        [Test]
        public void SetElapsed_UpdatesTimer()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.SetElapsed(42.5f);

            Assert.AreEqual(42.5f, test.ElapsedSeconds, 1e-4f, "timer shows the supplied time");
            Assert.IsFalse(test.EscapedShown, "updating the timer must not show the banner");
        }

        /// <summary>
        /// Reaching the exit shows the banner and freezes the final time.
        /// </summary>
        [Test]
        public void ShowEscaped_ShowsBanner_WithFinalTime()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.SetElapsed(10f);
            hud.ShowEscaped(67f);

            Assert.IsTrue(test.EscapedShown, "banner shows once the exit is reached");
            Assert.AreEqual(67f, test.ElapsedSeconds, 1e-4f, "banner shows the final time");
        }

        /// <summary>
        /// Starting a new run clears the banner and the timer, so a replay does not inherit the
        /// previous run's result.
        /// </summary>
        [Test]
        public void ResetHud_ClearsBannerAndTimer()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.ShowEscaped(99f);
            hud.ResetHud();

            Assert.IsFalse(test.EscapedShown, "banner cleared for the new run");
            Assert.AreEqual(0f, test.ElapsedSeconds, 1e-4f, "timer cleared for the new run");
        }

        /// <summary>
        /// Arriving on a floor records the floor and shows the arrival banner.
        /// </summary>
        [Test]
        public void ShowFloor_RecordsFloor_AndShowsBanner()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.ShowFloor(3, "JANKY LAUNDROMAT", 0);

            Assert.AreEqual(3, test.Floor, "floor number recorded");
            Assert.AreEqual("JANKY LAUNDROMAT", test.FloorName, "floor name recorded");
            Assert.IsTrue(test.BannerShown, "arrival banner visible on arrival");
        }

        /// <summary>
        /// The arrival banner clears itself after a few seconds so it does not block the view, while
        /// the floor number stays on the status line.
        /// </summary>
        [Test]
        public void FloorBanner_ExpiresButFloorPersists()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.ShowFloor(2, "ABANDONED MALL", 0);
            test.TickBanner(1f);
            Assert.IsTrue(test.BannerShown, "banner still up shortly after arrival");

            test.TickBanner(5f);
            Assert.IsFalse(test.BannerShown, "banner clears itself");
            Assert.AreEqual(2, test.Floor, "floor number stays on the status line");
        }

        /// <summary>
        /// The pursuit warning goes up when a Dweller starts hunting and comes down when it loses
        /// interest. Without this the player has no way to know they are being chased by something
        /// behind them, in fog.
        /// </summary>
        [Test]
        public void HuntedWarning_FollowsThePursuit()
        {
            (HudFacade hud, UIManagerTestFacade _) = NewHud();

            Assert.IsFalse(hud.HuntedShown, "nothing is hunting at the start of a run");

            hud.SetHunted(true, 0.4f);
            Assert.IsTrue(hud.HuntedShown, "the warning must appear the moment a Dweller gives chase");

            hud.SetHunted(false, 0f);
            Assert.IsFalse(hud.HuntedShown, "the warning must clear when the chase ends");
        }

        /// <summary>
        /// Resetting for a new run clears the pursuit warning. A warning left over from the run that
        /// just ended would tell the player they are being chased before anything has found them.
        /// </summary>
        [Test]
        public void ResetHud_ClearsTheHuntedWarning()
        {
            (HudFacade hud, UIManagerTestFacade _) = NewHud();

            hud.SetHunted(true, 1f);
            hud.ShowCaught(3, 42f, relics: 1, bestFloors: 3, bestRelics: 1);
            hud.ResetHud();

            Assert.IsFalse(hud.HuntedShown, "a new run starts unhunted");
            Assert.IsFalse(hud.CaughtShown, "a new run starts without the caught banner");
        }

        /// <summary>
        /// The end screen withholds the retry for its full ten seconds, so a run cannot be ended and
        /// restarted by the same reflex click. This is the whole behaviour: the screen exists to be
        /// read, and before this it was routinely dismissed before a single line had been.
        /// </summary>
        [Test]
        public void DeathScreen_WithholdsTheRetryUntilTheScreenHasBeenRead()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.ShowCaught(4, 96f, relics: 2, bestFloors: 6, bestRelics: 3);
            Assert.IsFalse(hud.RetryOffered, "the click that ended the run must not start the next");

            // A tenth of a second short of the ten, which is where an off-by-one would hide.
            for (int i = 0; i < 99; i++) test.TickBanner(0.1f);
            Assert.Less(test.CaughtSeconds, test.RetrySeconds, "still inside the ten seconds");
            Assert.IsFalse(hud.RetryOffered, "nine and a bit seconds is not ten");

            for (int i = 0; i < 3; i++) test.TickBanner(0.1f);
            Assert.IsTrue(hud.RetryOffered, "past ten seconds the player may go again");
        }

        /// <summary>
        /// The world fades out under the numbers over the first five seconds, and is completely gone
        /// by the end of them — leaving the second half of the wait as text on black.
        /// </summary>
        [Test]
        public void DeathScreen_FadesTheWorldOutOverTheFirstFiveSeconds()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            hud.ShowCaught(2, 31f, relics: 0, bestFloors: 4, bestRelics: 2);
            Assert.AreEqual(0f, hud.CaughtFade, 1e-3f, "the floor is still fully visible at the kill");

            for (int i = 0; i < 25; i++) test.TickBanner(0.1f);
            float half = hud.CaughtFade;
            Assert.Greater(half, 0f, "the fade should be under way at two and a half seconds");
            Assert.Less(half, 1f, "and not finished");

            for (int i = 0; i < 25; i++) test.TickBanner(0.1f);
            Assert.AreEqual(1f, hud.CaughtFade, 1e-3f, "black by five seconds");
            Assert.AreEqual(test.FadeSeconds, test.CaughtSeconds, 1e-3f, "five seconds have passed");
            Assert.IsFalse(hud.RetryOffered, "the fade finishing is only halfway through the wait");
        }

        /// <summary>
        /// The end screen's clock only runs while that screen is up: it starts at zero on each death,
        /// and a new run clears it. A clock left running would offer the next death's retry instantly.
        /// </summary>
        [Test]
        public void DeathScreen_ClockRunsOnlyWhileTheScreenIsUp()
        {
            (HudFacade hud, UIManagerTestFacade test) = NewHud();

            // Nothing has died yet, so ticking the HUD must not accumulate anything.
            for (int i = 0; i < 200; i++) test.TickBanner(0.1f);
            Assert.AreEqual(0f, test.CaughtSeconds, 1e-3f, "the clock does not run during a run");

            hud.ShowCaught(3, 42f, relics: 1, bestFloors: 3, bestRelics: 1);
            for (int i = 0; i < 200; i++) test.TickBanner(0.1f);
            Assert.IsTrue(hud.RetryOffered, "twenty seconds is well past the wait");

            hud.ResetHud();
            Assert.AreEqual(0f, test.CaughtSeconds, 1e-3f, "a new run clears the clock");
            Assert.IsFalse(hud.RetryOffered, "and with it the offer");

            // Dying a second time must wait all over again.
            hud.ShowCaught(1, 12f, relics: 0, bestFloors: 3, bestRelics: 1);
            Assert.AreEqual(0f, test.CaughtSeconds, 1e-3f, "the second death starts from zero");
            Assert.IsFalse(hud.RetryOffered, "and waits its own ten seconds");
        }

        /// <summary>
        /// The rotate prompt tracks the screen shape. A phone held upright cannot play this — the
        /// level is built around a wide field of view and a HUD anchored to the corners — so the game
        /// asks rather than letting someone judge it sideways.
        /// </summary>
        [Test]
        public void RotatePrompt_FollowsTheScreenShape()
        {
            (HudFacade hud, UIManagerTestFacade _) = NewHud();

            Assert.AreEqual(Screen.height > Screen.width, hud.ShowingRotatePrompt,
                "the prompt should show exactly when the screen is taller than it is wide");
        }

        /// <summary>
        /// Control hints stay hidden while the player is doing anything, and appear once they have
        /// stopped for a while. A hint shown to someone already walking is clutter over the middle of
        /// a horror game; a hint shown to someone who has stood still for ten seconds is help.
        /// </summary>
        [Test]
        public void ControlHints_AppearOnlyAfterThePlayerGoesIdle()
        {
            (HudFacade hud, UIManagerTestFacade _) = NewHud();
            UIManagerTestFacade seam = hud.GetTestFacade();

            // Nine seconds of standing still is not yet enough.
            for (int i = 0; i < 90; i++) seam.TickActivity(false, 0.1f);
            Assert.AreEqual(0f, hud.HintStrength, 1e-3f, "nine seconds idle shows nothing");

            // Past ten, and past the fade, they are fully up.
            for (int i = 0; i < 20; i++) seam.TickActivity(false, 0.1f);
            Assert.AreEqual(1f, hud.HintStrength, 1e-3f, "they should be up after eleven seconds");

            // Any input at all puts them away immediately.
            seam.TickActivity(true, 0.1f);
            Assert.AreEqual(0f, hud.HintStrength, 1e-3f, "moving again hides them at once");
        }

        /// <summary>
        /// The compass and carried list accept being handed nothing, which is the normal state for
        /// most of a run — the player starts every run carrying no relics at all.
        /// </summary>
        [Test]
        public void CompassAndCarried_AcceptBeingEmpty()
        {
            (HudFacade hud, UIManagerTestFacade _) = NewHud();

            Assert.DoesNotThrow(() => hud.SetCompass(null), "a null compass is an empty compass");
            Assert.DoesNotThrow(() => hud.SetCarried(null, null), "and nothing carried is normal");
            Assert.DoesNotThrow(() => hud.SetCompass(new CompassMark[0]), "as is an empty list");
        }

        /// <summary>
        /// Durations are displayed as zero-padded minutes and seconds, and negative input is treated
        /// as zero rather than rendering a nonsensical time.
        /// </summary>
        [Test]
        public void FormatTime_UsesZeroPaddedMinutesAndSeconds()
        {
            (HudFacade _, UIManagerTestFacade test) = NewHud();

            Assert.AreEqual("00:00", test.FormatTime(0f), "zero");
            Assert.AreEqual("00:07", test.FormatTime(7.9f), "seconds truncate, not round");
            Assert.AreEqual("01:00", test.FormatTime(60f), "exactly one minute");
            Assert.AreEqual("02:05", test.FormatTime(125f), "minutes and seconds");
            Assert.AreEqual("10:00", test.FormatTime(600f), "two-digit minutes");
            Assert.AreEqual("00:00", test.FormatTime(-5f), "negative clamps to zero");
        }
    }
}
