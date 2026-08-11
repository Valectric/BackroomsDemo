using UnityEngine;

namespace Backrooms.EntityManager
{
    /// <summary>
    /// Decides whether something in the world is inside a cone in front of the player — the test the
    /// Banisher uses to pick what it unmakes.
    /// </summary>
    /// <remarks>
    /// A free function rather than a method on the director, because the director lives in the
    /// application layer whose only test assembly is the black-box E2E one. The geometry that decides
    /// whether a shot hits is exactly the part worth testing white-box, so it lives here where it can
    /// be. There was no test of it at all before, and the shot could have missed every target in the
    /// game without a single assertion noticing.
    /// </remarks>
    public static class DwellerAim
    {
        /// <summary>
        /// Whether a target is within a cone in front of an origin, ignoring height.
        /// </summary>
        /// <param name="origin">Where the shot comes from.</param>
        /// <param name="forward">Direction the shooter faces; need not be normalised or flat.</param>
        /// <param name="target">The thing being shot at.</param>
        /// <param name="range">How far the shot reaches, in metres.</param>
        /// <param name="halfAngle">Half the cone's width, in degrees.</param>
        /// <returns><c>true</c> if the target is inside the cone.</returns>
        public static bool IsInCone(Vector3 origin, Vector3 forward, Vector3 target, float range,
            float halfAngle)
        {
            Vector3 flatOrigin = Flat(origin);
            Vector3 toTarget = Flat(target) - flatOrigin;

            float away = toTarget.magnitude;
            if (away > range) return false;

            // Standing on top of the target counts as facing it. Otherwise the one moment the player
            // most wants the shot to work — something already on them — is the moment the direction
            // is undefined and it refuses.
            if (away < 0.05f) return true;

            Vector3 facing = Flat(forward);
            if (facing.sqrMagnitude < 1e-6f) return false;

            return Vector3.Angle(facing, toTarget) <= halfAngle;
        }

        /// <summary>
        /// Drops the height from a vector, so aiming is judged on the floor plan.
        /// </summary>
        /// <param name="v">Vector to flatten.</param>
        /// <returns>The same vector with no height.</returns>
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
