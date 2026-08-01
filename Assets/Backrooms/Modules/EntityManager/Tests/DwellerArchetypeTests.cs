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
        /// Even the fastest kind must stay below the player's sprint, or a chase becomes an
        /// unavoidable death rather than something to escape.
        /// </summary>
        [Test]
        public void EvenTheFastestKind_CanBeOutrunBySprinting()
        {
            const float baseSpeed = 2.2f;
            const float sprint = 5.6f;

            foreach (DwellerKind kind in System.Enum.GetValues(typeof(DwellerKind)))
            {
                DwellerArchetype a = DwellerArchetypes.For(kind);
                float speed = baseSpeed * a.SpeedMultiplier;
                Assert.Less(speed, sprint,
                    $"{kind} moves at {speed:F2} m/s against a {sprint} m/s sprint — inescapable on floor 1");
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
