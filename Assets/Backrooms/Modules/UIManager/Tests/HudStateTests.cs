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
