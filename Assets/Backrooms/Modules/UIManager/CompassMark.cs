using UnityEngine;

namespace Backrooms.UIManager
{
    /// <summary>
    /// One arrow the HUD should draw: which way to turn to face something, how far away it is, and
    /// what colour says which relic is pointing.
    /// </summary>
    /// <remarks>
    /// The bearing is relative to where the player is looking rather than to a compass direction. A
    /// player in a featureless maze has no idea which way north is, so "12 degrees to your left" is
    /// actionable where "north-east" is not.
    /// </remarks>
    public struct CompassMark
    {
        /// <summary>Signed angle from the player's facing to the target, in degrees.</summary>
        public float Bearing;

        /// <summary>Distance to the target in metres, shown alongside the arrow.</summary>
        public float Distance;

        /// <summary>Colour of the relic doing the pointing.</summary>
        public Color Colour;

        /// <summary>
        /// Creates a compass mark.
        /// </summary>
        /// <param name="bearing">Signed angle from the player's facing, in degrees.</param>
        /// <param name="distance">Distance to the target in metres.</param>
        /// <param name="colour">Colour of the relic doing the pointing.</param>
        public CompassMark(float bearing, float distance, Color colour)
        {
            Bearing = bearing;
            Distance = distance;
            Colour = colour;
        }
    }
}
