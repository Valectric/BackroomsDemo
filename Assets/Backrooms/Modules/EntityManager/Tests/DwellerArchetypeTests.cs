using System.Collections.Generic;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.EntityManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for the Dweller roster. The point of having three kinds is that
    /// meeting one is a different problem from meeting another; these assert the differences are real
    /// rather than three recolours of the same creature, which is what the roster would quietly decay
    /// into under later tuning.
    /// </summary>
    public class DwellerArchetypeTests
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
        /// Every kind must have an archetype, and no two may share a display name.
        /// </summary>
        [Test]
        public void EveryKind_HasADistinctlyNamedArchetype()
        {
            var names = new HashSet<string>();
            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype archetype = DwellerArchetypes.For(kind);
                Assert.AreEqual(kind, archetype.Kind, $"{kind} must map to its own archetype");
                Assert.IsNotEmpty(archetype.DisplayName, $"{kind} needs a name for the HUD warning");
                Assert.IsTrue(names.Add(archetype.DisplayName),
                    $"two kinds share the name {archetype.DisplayName}");
            }

            Assert.AreEqual(names.Count, DwellerArchetypes.Count, "one archetype per kind");
        }

        /// <summary>
        /// The kinds must trade off against each other rather than one being strictly better: the
        /// fastest must not also see furthest. A dominant kind makes the other two pointless.
        /// </summary>
        [Test]
        public void NoKind_IsBothFastestAndFarthestSighted()
        {
            DwellerArchetype fastest = null;
            DwellerArchetype sharpest = null;

            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype a = DwellerArchetypes.For(kind);
                if (fastest == null || a.SpeedMultiplier > fastest.SpeedMultiplier) fastest = a;
                if (sharpest == null || a.SenseMultiplier > sharpest.SenseMultiplier) sharpest = a;
            }

            Assert.AreNotEqual(fastest.Kind, sharpest.Kind,
                $"{fastest.Kind} is both the fastest and the furthest-sighted, so the others are strictly worse");
        }

        /// <summary>
        /// Silhouettes must differ enough to tell apart at fog distance. Height is the cue that
        /// survives being far away and badly lit, so no two kinds may be nearly the same height.
        /// </summary>
        [Test]
        public void Silhouettes_AreDistinguishableByHeight()
        {
            var heights = new List<float>();
            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                heights.Add(DwellerArchetypes.For(kind).BodyHeight);
            }

            for (int i = 0; i < heights.Count; i++)
            {
                for (int j = i + 1; j < heights.Count; j++)
                {
                    Assert.Greater(Mathf.Abs(heights[i] - heights[j]), 0.4f,
                        $"heights {heights[i]:F2}m and {heights[j]:F2}m are too close to tell apart");
                }
            }
        }

        /// <summary>
        /// No Dweller may be tall enough to clip through the ceiling, which is 3 m.
        /// </summary>
        [Test]
        public void NoKind_IsTallerThanTheCeiling()
        {
            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype a = DwellerArchetypes.For(kind);
                Assert.Less(a.BodyHeight, 3f, $"{kind} at {a.BodyHeight}m would grow through the ceiling");
                Assert.Greater(a.BodyHeight, 0.5f, $"{kind} at {a.BodyHeight}m is too short to notice");
            }
        }

        /// <summary>
        /// A hunting Dweller must be able to catch a player who only walks, and must not be able to
        /// catch one who sprints.
        /// </summary>
        /// <remarks>
        /// This test replaces one that asserted only the upper bound, and that omission cost a
        /// working fail state. Every kind was slower than the player's walk, so no chase could ever
        /// end in a catch; the suite was green, the encounter rate measured 88%, and the game killed
        /// nobody. An upper bound alone says a chase is survivable. It takes both bounds to say a
        /// chase is a chase.
        /// </remarks>
        [Test]
        public void EveryKind_HasAnAnswer_ButNotTheSameOne()
        {
            const float baseSpeed = 2.2f;
            const float walk = 3.2f;
            const float sprint = 5.6f;

            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype a = DwellerArchetypes.For(kind);
                float chase = baseSpeed * a.ChaseMultiplier;

                Assert.Greater(chase, walk,
                    $"{kind} hunts at {chase:F2} m/s against a {walk} m/s walk — it can never close");

                switch (a.Movement)
                {
                    case DwellerMovement.Steady:
                        // Nothing else to exploit, so the legs have to be the answer.
                        Assert.Less(chase, sprint,
                            $"{kind} has no rule to exploit, so it must be outrunnable");
                        break;

                    case DwellerMovement.Freezes:
                        // The answer is your eyes, so the legs must NOT be — a freezer that can be
                        // outrun makes looking at it optional and the rule decoration.
                        Assert.Greater(a.UnobservedSpeed, sprint,
                            $"{kind} must outrun a sprint, or watching it is pointless");
                        break;

                    case DwellerMovement.Charges:
                        // The answer is the warning, so the charge is allowed to be unsurvivable —
                        // but only because it announces itself first.
                        Assert.Greater(a.ChargeSpeed, sprint,
                            $"{kind} should be unoutrunnable mid-charge, or the charge is a jog");
                        Assert.Less(a.StalkSpeed, walk,
                            $"{kind} must creep before it commits, or there is no reprieve");
                        Assert.Greater(a.WindUpSeconds, 0.5f,
                            $"{kind} must telegraph long enough to be reacted to");
                        Assert.GreaterOrEqual(a.WindUpBlinks, 3,
                            $"{kind} must flash a clear warning before it commits");
                        break;
                }
            }
        }

        /// <summary>
        /// The roster must open up with depth: the Lurker alone on floor 1, the Skitter from floor 2,
        /// and the Watcher from floor 4.
        /// </summary>
        /// <remarks>
        /// Each kind plays by a rule the player has to work out, and three unfamiliar rules at once
        /// is noise rather than difficulty — everything that kills you feels arbitrary because you
        /// never saw any of them behave twice.
        /// </remarks>
        [Test]
        public void TheRoster_OpensUpWithDepth()
        {
            const int slots = 8;

            for (int i = 0; i < slots; i++)
            {
                Assert.AreEqual(DwellerKind.Lurker, DwellerArchetypes.KindFor(i, 1),
                    "the first floor should only teach the Lurker");
            }

            for (int floor = 2; floor <= 3; floor++)
            {
                var seen = new HashSet<DwellerKind>();
                for (int i = 0; i < slots; i++) seen.Add(DwellerArchetypes.KindFor(i, floor));

                Assert.IsFalse(seen.Contains(DwellerKind.Watcher),
                    $"floor {floor} is too early for the Watcher");
                Assert.IsTrue(seen.Contains(DwellerKind.Skitter),
                    $"floor {floor} should be where the Skitter shows up");
                Assert.IsTrue(seen.Contains(DwellerKind.Lurker),
                    $"floor {floor} should still carry the Lurker to contrast against");
            }

            for (int floor = 4; floor <= 7; floor++)
            {
                var seen = new HashSet<DwellerKind>();
                for (int i = 0; i < slots; i++) seen.Add(DwellerArchetypes.KindFor(i, floor));

                Assert.AreEqual(3, seen.Count, $"floor {floor} should mix all three");
            }
        }

        /// <summary>
        /// Every kind must be slower while unaware than while hunting, so noticing the player is a
        /// visible change in behaviour and not just a change of colour.
        /// </summary>
        [Test]
        public void EveryKind_SpeedsUpWhenItStartsHunting()
        {
            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype a = DwellerArchetypes.For(kind);
                Assert.Greater(a.ChaseMultiplier, a.SpeedMultiplier,
                    $"{kind} moves no faster hunting ({a.ChaseMultiplier}) than patrolling ({a.SpeedMultiplier})");
            }
        }

        /// <summary>
        /// A patrolling Dweller must be slow enough that the player can cross a floor without being
        /// run down by something that has not even noticed them.
        /// </summary>
        [Test]
        public void PatrollingSpeed_StaysBelowAWalk()
        {
            const float baseSpeed = 2.2f;
            const float walk = 3.2f;

            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype a = DwellerArchetypes.For(kind);
                float patrol = baseSpeed * a.SpeedMultiplier;
                Assert.Less(patrol, walk,
                    $"{kind} patrols at {patrol:F2} m/s, which outruns a walking player unprovoked");
            }
        }

        /// <summary>
        /// Dealing kinds out by index must cycle through the whole roster, so a floor carrying three
        /// Dwellers gets one of each rather than three of the same.
        /// </summary>
        [Test]
        public void DealingByIndex_CyclesTheWholeRoster()
        {
            var seen = new HashSet<DwellerKind>();
            for (int i = 0; i < DwellerArchetypes.Count; i++) seen.Add(DwellerArchetypes.AtIndex(i));

            Assert.AreEqual(DwellerArchetypes.Count, seen.Count,
                "three consecutive slots should be three different kinds");
            Assert.AreEqual(DwellerArchetypes.AtIndex(0),
                DwellerArchetypes.AtIndex(DwellerArchetypes.Count), "dealing wraps around");
        }

        /// <summary>
        /// A Dweller told to be a kind reports that kind and its name, and can be told to be a
        /// different one — the same object is reused across floors rather than respawned.
        /// </summary>
        [Test]
        public void ADweller_TakesOnTheKindItIsGiven()
        {
            var go = new GameObject("Dweller");
            DwellerFacade dweller = go.AddComponent<DwellerFacade>();

            dweller.SetKind(DwellerKind.Watcher);
            Assert.AreEqual(DwellerKind.Watcher, dweller.Kind, "the Dweller should become a Watcher");
            Assert.AreEqual(DwellerArchetypes.For(DwellerKind.Watcher).DisplayName, dweller.DisplayName,
                "its display name should follow its kind");

            dweller.SetKind(DwellerKind.Skitter);
            Assert.AreEqual(DwellerKind.Skitter, dweller.Kind, "kind must be changeable between floors");
        }
    }
}
