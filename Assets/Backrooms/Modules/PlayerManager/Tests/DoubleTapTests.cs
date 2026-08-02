using Backrooms.PlayerManager;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.PlayerManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for double-tap recognition. Relic powers are bound to this gesture,
    /// so a window that is too generous fires a power when the player meant to walk, and one that is
    /// too tight makes a relic feel broken.
    /// </summary>
    public class DoubleTapTests
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
        /// Two presses inside the window are a double tap; the first press alone is not.
        /// </summary>
        /// <summary>
        /// However many times the game asks for input in a frame, hardware must be sampled once.
        /// </summary>
        /// <remarks>
        /// This is the bug the detector tests could never catch, because the detector was correct.
        /// Sampling has a side effect — it feeds the double-tap detectors — and Unity keeps
        /// <c>wasPressedThisFrame</c> true for the whole frame. The facade exposes input through six
        /// separate properties, so one physical tap was registered as several presses microseconds
        /// apart and came straight back as a double tap: a single tap teleported the player. Every
        /// test of the detector passed throughout, because the fault was in how often it was asked.
        /// </remarks>
        [Test]
        public void ManyReadsInOneFrame_SampleHardwareOnce()
        {
            var go = new GameObject("Player");
            PlayerFacade player = go.AddComponent<PlayerFacade>();
            PlayerManagerTestFacade seam = player.GetTestFacade();

            int before = seam.FreshInputReads;

            // Exactly what the game does each frame: several independent questions about input.
            bool _ = player.ConfirmPressed;
            _ = player.DoubleTappedLookSide;
            _ = player.DoubleTappedMoveSide;
            _ = player.HasInput;
            _ = player.IsMoving;

            Assert.AreEqual(1, seam.FreshInputReads - before,
                "five reads in one frame must sample the hardware exactly once");
        }

        [Test]
        public void TwoQuickPresses_AreADoubleTap()
        {
            PlayerManagerTestFacade seam = NewSeam();

            Assert.IsFalse(seam.PressForDoubleTap(0f), "one press is not a gesture");
            Assert.IsTrue(seam.PressForDoubleTap(0.15f), "a second press soon after is");
        }

        /// <summary>
        /// Two presses far apart are two separate taps, not a gesture. Walking around tapping to
        /// steer must not keep firing relics.
        /// </summary>
        [Test]
        public void TwoSlowPresses_AreNotADoubleTap()
        {
            PlayerManagerTestFacade seam = NewSeam();

            Assert.IsFalse(seam.PressForDoubleTap(0f), "first press");
            Assert.IsFalse(seam.PressForDoubleTap(1.4f), "a press over a second later stands alone");
        }

        /// <summary>
        /// A recognised pair is consumed, so three quick presses fire once rather than twice. Without
        /// this a player drumming their finger would empty a relic's charges in a second.
        /// </summary>
        [Test]
        public void ThreeQuickPresses_FireOnlyOnce()
        {
            PlayerManagerTestFacade seam = NewSeam();

            Assert.IsFalse(seam.PressForDoubleTap(0f), "first");
            Assert.IsTrue(seam.PressForDoubleTap(0.1f), "second completes a pair");
            Assert.IsFalse(seam.PressForDoubleTap(0.2f), "third starts a new pair rather than firing");
        }

        /// <summary>
        /// A fourth press completes the second pair, so a deliberate double-double still works.
        /// </summary>
        [Test]
        public void FourQuickPresses_FireTwice()
        {
            PlayerManagerTestFacade seam = NewSeam();

            seam.PressForDoubleTap(0f);
            Assert.IsTrue(seam.PressForDoubleTap(0.1f), "first pair");
            seam.PressForDoubleTap(0.2f);
            Assert.IsTrue(seam.PressForDoubleTap(0.3f), "second pair");
        }

        /// <summary>
        /// Creates a player module and returns its test seam.
        /// </summary>
        /// <returns>A test facade over a new player module.</returns>
        private static PlayerManagerTestFacade NewSeam()
        {
            var go = new UnityEngine.GameObject("Player");
            go.AddComponent<UnityEngine.CharacterController>();
            return go.AddComponent<PlayerFacade>().GetTestFacade();
        }
    }
}
