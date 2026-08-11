using Backrooms.EntityManager;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.EntityManager.Tests
{
    /// <summary>
    /// White-box tests for the cone the Banisher shoots down. This is the geometry that decides
    /// whether a shot kills anything, and until now nothing tested it at all — the relic could have
    /// missed every Dweller in the game and every suite would still have been green, because the
    /// tests covered the input arriving and the charges draining and nothing in between.
    /// </summary>
    public class DwellerAimTests
    {
        /// <summary>Range the Banisher reaches, matching the value the game fires with.</summary>
        private const float Range = 22f;

        /// <summary>Half-width of the cone in degrees, matching the game.</summary>
        private const float HalfAngle = 32f;

        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>
        /// A Dweller straight ahead and within range must be hit.
        /// </summary>
        [Test]
        public void SomethingStraightAhead_IsHit()
        {
            Assert.IsTrue(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f),
                    Range, HalfAngle),
                "a Dweller ten metres straight ahead is exactly what this is for");
        }

        /// <summary>
        /// Height must not matter: a shot is aimed on the floor plan, so a tall Watcher and a squat
        /// Skitter are hit the same way.
        /// </summary>
        [Test]
        public void HeightIsIgnored()
        {
            Assert.IsTrue(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 2.8f, 10f),
                    Range, HalfAngle),
                "aiming up at a Watcher's head must not be required");
        }

        /// <summary>
        /// Anything behind, beyond the range, or outside the cone must be missed — the shot has to be
        /// aimed or it is not a weapon, it is a button.
        /// </summary>
        [Test]
        public void BehindOrTooFarOrTooWide_IsMissed()
        {
            Assert.IsFalse(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -10f),
                    Range, HalfAngle),
                "something directly behind must not be hit");

            Assert.IsFalse(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, Range + 3f),
                    Range, HalfAngle),
                "beyond the range must not be hit");

            // 45 degrees off, comfortably outside a 32 degree half-angle.
            Assert.IsFalse(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(10f, 0f, 10f),
                    Range, HalfAngle),
                "45 degrees off centre is outside a 32 degree cone");
        }

        /// <summary>
        /// The cone's edge must sit where it claims to: just inside is a hit, just outside a miss.
        /// </summary>
        [Test]
        public void TheConeEdge_IsWhereItSaysItIs()
        {
            const float distance = 10f;

            float inside = Mathf.Tan((HalfAngle - 3f) * Mathf.Deg2Rad) * distance;
            float outside = Mathf.Tan((HalfAngle + 3f) * Mathf.Deg2Rad) * distance;

            Assert.IsTrue(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(inside, 0f, distance),
                    Range, HalfAngle),
                "three degrees inside the edge must hit");
            Assert.IsFalse(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(outside, 0f, distance),
                    Range, HalfAngle),
                "three degrees outside the edge must miss");
        }

        /// <summary>
        /// Something standing on top of the player must be hit whichever way they face.
        /// </summary>
        /// <remarks>
        /// The direction to a target at zero distance is undefined, so a naive angle test refuses
        /// exactly when the player most needs the shot to work.
        /// </remarks>
        [Test]
        public void SomethingOnTopOfYou_IsAlwaysHit()
        {
            Assert.IsTrue(
                DwellerAim.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0.01f, 0f, -0.01f),
                    Range, HalfAngle),
                "a Dweller already on you must be shootable, whichever way you are turned");
        }
    }
}
