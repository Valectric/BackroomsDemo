using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Backrooms.PlayerManager.Internal.Input
{
    /// <summary>
    /// Reads real hardware into a <see cref="PlayerInputState"/>. Two schemes are supported at once,
    /// so the same build works on desktop and on a phone browser:
    /// <list type="bullet">
    /// <item>Desktop — WASD/arrows to move, hold left mouse button and drag to look, shift to sprint.</item>
    /// <item>Touch — a touch starting on the left half of the screen acts as a virtual stick
    /// (drag from where you pressed); a touch starting on the right half looks around.</item>
    /// </list>
    /// The touch scheme needs no on-screen widgets, which keeps the mobile build simple and avoids
    /// UI hit-testing getting in the way of the camera.
    /// </summary>
    internal sealed class PlayerInputReader
    {
        /// <summary>Drag distance in pixels that corresponds to full stick deflection.</summary>
        private const float StickRadiusPixels = 140f;

        /// <summary>Touch drag beyond this distance from its start counts as sprinting.</summary>
        private const float SprintStickThreshold = 0.85f;

        /// <summary>
        /// Samples all connected devices and combines them into a single intent for this frame.
        /// </summary>
        /// <returns>The player's input for this frame.</returns>
        public PlayerInputState Read()
        {
            PlayerInputState state = PlayerInputState.None;
            ReadKeyboard(ref state);
            ReadMouse(ref state);
            ReadTouch(ref state);
            state.Move = Vector2.ClampMagnitude(state.Move, 1f);
            return state;
        }

        /// <summary>
        /// Adds WASD/arrow movement and shift-to-sprint from the keyboard, if one is present.
        /// </summary>
        /// <param name="state">Input state being accumulated.</param>
        private static void ReadKeyboard(ref PlayerInputState state)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            var move = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

            state.Move += move;
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) state.Sprint = true;
        }

        /// <summary>
        /// Adds look input from a held-and-dragged mouse. Drag-to-look is used rather than pointer
        /// lock because pointer lock is unreliable inside a WebGL canvas on some browsers.
        /// </summary>
        /// <param name="state">Input state being accumulated.</param>
        private static void ReadMouse(ref PlayerInputState state)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return;
            state.Look += mouse.delta.ReadValue();
        }

        /// <summary>
        /// Adds touch input: left-half touches drive a virtual stick relative to where the finger
        /// first landed, right-half touches drive the camera.
        /// </summary>
        /// <param name="state">Input state being accumulated.</param>
        private static void ReadTouch(ref PlayerInputState state)
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null) return;

            float halfWidth = Screen.width * 0.5f;

            foreach (TouchControl t in touch.touches)
            {
                if (!t.press.isPressed) continue;

                Vector2 start = t.startPosition.ReadValue();
                if (start.x < halfWidth)
                {
                    Vector2 drag = t.position.ReadValue() - start;
                    Vector2 stick = drag / StickRadiusPixels;
                    if (stick.magnitude > SprintStickThreshold) state.Sprint = true;
                    state.Move += Vector2.ClampMagnitude(stick, 1f);
                }
                else
                {
                    state.Look += t.delta.ReadValue();
                }
            }
        }
    }
}
