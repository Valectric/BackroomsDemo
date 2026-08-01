using UnityEngine;

namespace Backrooms.PlayerManager
{
    /// <summary>
    /// One frame of player intent, independent of where it came from. Real hardware (keyboard,
    /// mouse, touchscreen) and simulated test input both produce this same struct, so the movement
    /// code downstream is identical for a real player and for an automated test.
    /// </summary>
    public struct PlayerInputState
    {
        /// <summary>
        /// Movement intent in local space, range -1..1 per axis: X strafes right, Y walks forward.
        /// </summary>
        public Vector2 Move;

        /// <summary>
        /// Look intent for this frame in degrees-equivalent delta: X turns (yaw), Y pitches.
        /// </summary>
        public Vector2 Look;

        /// <summary>Whether the sprint action is held.</summary>
        public bool Sprint;

        /// <summary>
        /// Whether a confirm press started this frame — a click or a tap. Used for out-of-gameplay
        /// choices such as restarting after a Dweller catches you; movement ignores it.
        /// </summary>
        public bool Confirm;

        /// <summary>Input representing "no intent at all".</summary>
        public static PlayerInputState None => new PlayerInputState
        {
            Move = Vector2.zero,
            Look = Vector2.zero,
            Sprint = false,
            Confirm = false
        };
    }
}
