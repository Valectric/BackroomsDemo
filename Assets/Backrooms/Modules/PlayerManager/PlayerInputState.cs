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

        /// <summary>
        /// Whether a double tap landed on the movement side of the screen this frame. Relic powers
        /// are bound to gestures rather than buttons because the touch scheme deliberately has no
        /// on-screen widgets to hit.
        /// </summary>
        public bool DoubleTapMoveSide;

        /// <summary>Whether a double tap landed on the look side of the screen this frame.</summary>
        public bool DoubleTapLookSide;

        /// <summary>
        /// Whether the blink key went down this frame.
        /// </summary>
        /// <remarks>
        /// Kept separate from the gesture rather than folded into it. A double tap and a key press
        /// are different events that happen to drive the same power, and a keyboard player pressing
        /// F has not "double tapped the look side of the screen" — collapsing the two would make the
        /// gesture fields lie about what happened.
        /// </remarks>
        public bool BlinkKey;

        /// <summary>Whether the banish key went down this frame.</summary>
        public bool BanishKey;

        /// <summary>
        /// Steps of look-sensitivity adjustment asked for this frame: positive faster, negative
        /// slower. One step per wheel notch or per press of the plus/minus keys.
        /// </summary>
        public int SensitivitySteps;

        /// <summary>Input representing "no intent at all".</summary>
        public static PlayerInputState None => new PlayerInputState
        {
            Move = Vector2.zero,
            Look = Vector2.zero,
            Sprint = false,
            Confirm = false,
            DoubleTapMoveSide = false,
            DoubleTapLookSide = false
        };
    }
}
