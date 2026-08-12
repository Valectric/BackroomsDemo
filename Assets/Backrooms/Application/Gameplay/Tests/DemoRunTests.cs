using Backrooms.RelicManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;

namespace Backrooms.Gameplay.Tests
{
    /// <summary>
    /// White-box tests for how long the demo is and what a run is worth. The scoring is pure
    /// arithmetic, so it is asserted directly rather than played through.
    /// </summary>
    public class DemoRunTests
    {
        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>
        /// The score is floors and relics, and floors have to dominate.
        /// </summary>
        /// <remarks>
        /// This is the assertion that keeps the score pointing the player downwards. The first floor
        /// alone carries about forty relics, so at any generous per-relic rate the highest-scoring
        /// play would be to sweep floor 1 and never descend — a score that rewards not playing the
        /// game. Sweeping the whole of floor 1 must be worth less than simply walking down one
        /// staircase.
        /// </remarks>
        [Test]
        public void TheScore_RewardsGoingDeeperMoreThanHoarding()
        {
            Assert.AreEqual(0, DemoRun.Score(0, 0), "an unplayed run scores nothing");

            int sweptFirstFloor = DemoRun.Score(1, 40);
            int reachedSecondFloorEmptyHanded = DemoRun.Score(2, 0);

            MooseRunnerFacade.Log(
                $"floor 1 swept ({sweptFirstFloor}) vs floor 2 empty-handed "
                + $"({reachedSecondFloorEmptyHanded})");

            Assert.Less(sweptFirstFloor, reachedSecondFloorEmptyHanded,
                "clearing forty relics on floor 1 must not beat simply reaching floor 2");

            // Relics still have to be worth the detour, or the whole relic system is decoration.
            Assert.Greater(DemoRun.Score(3, 20), DemoRun.Score(3, 0),
                "at the same depth, more relics must score more");

            // Never negative, whatever it is handed.
            Assert.AreEqual(0, DemoRun.Score(-5, -5), "nonsense input scores zero, not a negative");
        }

        /// <summary>
        /// A full clear of the demo is the highest score the demo can produce.
        /// </summary>
        [Test]
        public void AFullClear_OutscoresAnyShorterRun()
        {
            int fullClear = DemoRun.Score(DemoRun.FinalFloor, 90);

            for (int floor = 1; floor < DemoRun.FinalFloor; floor++)
            {
                // 90 relics is more than any single floor carries, so this is a generous upper bound
                // on what a shorter run could possibly have collected.
                Assert.Less(DemoRun.Score(floor, 90), fullClear,
                    $"a run ending on floor {floor} must score less than finishing the demo");
            }
        }

        /// <summary>
        /// Only the last floor ends the demo.
        /// </summary>
        [Test]
        public void OnlyTheLastFloor_EndsTheDemo()
        {
            for (int floor = 1; floor < DemoRun.FinalFloor; floor++)
            {
                Assert.IsFalse(DemoRun.IsFinalFloor(floor),
                    $"floor {floor} has floors below it");
            }

            Assert.IsTrue(DemoRun.IsFinalFloor(DemoRun.FinalFloor), "the last floor ends it");
            Assert.IsTrue(DemoRun.IsFinalFloor(DemoRun.FinalFloor + 1),
                "and anything past it would too, if it could be reached");
        }

        /// <summary>
        /// Every relic in the roster must peak on a floor the player can actually reach, or the demo
        /// ships a relic most people will never be offered.
        /// </summary>
        /// <remarks>
        /// This is the coupling that had already broken once and that nothing caught: the relic odds
        /// cycled over seven floors while the demo is six long, which put the Banisher — the
        /// strongest thing in the game — on a floor that does not exist. Every individual weight was
        /// correct; the roster simply did not fit in the building. It lives here rather than with the
        /// relic tests because it is a fact about the application, not about the module: neither side
        /// can see the mismatch on its own.
        /// </remarks>
        [Test]
        public void EveryRelic_PeaksOnAFloorInsideTheDemo()
        {
            for (int i = 0; i < RelicArchetypes.Count; i++)
            {
                RelicKind kind = RelicArchetypes.AtIndex(i);
                int peak = RelicOdds.PeakFloorOf(kind);

                Assert.Greater(peak, 0, $"{kind} should peak on some floor");
                Assert.LessOrEqual(peak, DemoRun.FinalFloor,
                    $"{kind} peaks on floor {peak}, past the end of a "
                    + $"{DemoRun.FinalFloor}-floor demo — no player would ever be offered it");
            }
        }
    }
}
