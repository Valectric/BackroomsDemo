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

        /// <summary>Recognises a double tap on the movement half of the screen.</summary>
        private readonly DoubleTapDetector _moveSideTaps = new DoubleTapDetector();

        /// <summary>Recognises a double tap on the look half of the screen.</summary>
        private readonly DoubleTapDetector _lookSideTaps = new DoubleTapDetector();

        /// <summary>Touch drag beyond this distance from its start counts as sprinting.</summary>
        private const float SprintStickThreshold = 0.85f;

        /// <summary>
        /// Samples all connected devices and combines them into a single intent for this frame,
        /// sampling at most once per frame however many times it is asked.
        /// </summary>
        /// <remarks>
        /// The caching is not an optimisation, it is the correctness fix. Sampling has a side effect
        /// — it feeds the double-tap detectors — and <c>wasPressedThisFrame</c> stays true for the
        /// whole frame, so a single physical tap read twice in one frame registered as two presses
        /// nanoseconds apart and came straight back as a double tap. The facade reads input from six
        /// separate properties, so one tap teleported the player instantly. Every one of those reads
        /// now sees the same sample.
        /// </remarks>
        /// <returns>The player's input for this frame.</returns>
        public PlayerInputState Read()
        {
            if (_sampledFrame == Time.frameCount) return _sample;

            _sampledFrame = Time.frameCount;
            FreshReads++;

            // The browser can drop the lock without telling us — Escape, a tab switch, losing
            // focus — and the cursor has to come back when it does, or the player is left with an
            // invisible pointer over a game that no longer turns.
            if (Cursor.lockState != CursorLockMode.Locked && !Cursor.visible) Cursor.visible = true;

            PlayerInputState state = PlayerInputState.None;
            ReadKeyboard(ref state);
            ReadMouse(ref state);
            ReadTouch(ref state);
            state.Move = Vector2.ClampMagnitude(state.Move, 1f);

            _sample = state;
            return _sample;
        }

        /// <summary>The frame the current sample was taken on.</summary>
        private int _sampledFrame = -1;

        /// <summary>The input sampled this frame, handed to every reader after the first.</summary>
        private PlayerInputState _sample = PlayerInputState.None;

        /// <summary>
        /// How many times hardware has actually been sampled. Not intended for production use — only
        /// for automated testing, where it is the evidence that one frame produces one sample.
        /// </summary>
        public int FreshReads { get; private set; }

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

            // Relic powers on the two keys immediately right of WASD, so the hand that is already on
            // the movement keys can reach them without moving. On touch these are double taps,
            // because the touch scheme has no on-screen widgets to press; a keyboard has keys, and
            // asking a desktop player to double-click the correct half of the screen mid-chase is
            // asking them to do something a keyboard already does better.
            if (kb.fKey.wasPressedThisFrame) state.BlinkKey = true;
            if (kb.gKey.wasPressedThisFrame) state.BanishKey = true;

            // Escape gives the cursor back. The browser also releases pointer lock on Escape by
            // itself and that cannot be prevented, so this exists to keep the editor and any
            // standalone build behaving the same way the web build already does.
            if (kb.escapeKey.wasPressedThisFrame) ReleasePointer();
        }

        /// <summary>
        /// Hands the cursor back to the player.
        /// </summary>
        private static void ReleasePointer()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Adds look input from the mouse: free look while the pointer is locked, and
        /// hold-and-drag when it is not.
        /// </summary>
        /// <remarks>
        /// A click locks the pointer, and Escape releases it — the browser enforces that exit itself
        /// and it cannot be suppressed, which is exactly why it is the right key to document.
        /// <para>
        /// Drag-to-look is kept as the fallback rather than removed. Pointer lock needs a user
        /// gesture and can simply be refused — an iframe without <c>allow="pointer-lock"</c>, or a
        /// browser that has had the permission denied for the site — and a game whose camera stops
        /// working because a permission was declined is worse than one that quietly asks you to hold
        /// the button.
        /// </para>
        /// </remarks>
        /// <param name="state">Input state being accumulated.</param>
        private void ReadMouse(ref PlayerInputState state)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                state.Confirm = true;

                if (!locked && !Application.isMobilePlatform)
                {
                    // The click that starts the run is also the gesture that earns the lock.
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                // A mouse click deliberately does not fire the screen-half gestures any more. They
                // exist because a touchscreen has nowhere to put a button; a desktop player has F
                // and G, and having a stray click also spend a relic is a way to lose one by
                // accident rather than a second way to use it.
            }

            if (!locked && !mouse.leftButton.isPressed) return;
            state.Look += mouse.delta.ReadValue();
        }

        /// <summary>
        /// Adds touch input: left-half touches drive a virtual stick relative to where the finger
        /// first landed, right-half touches drive the camera.
        /// </summary>
        /// <param name="state">Input state being accumulated.</param>
        private void ReadTouch(ref PlayerInputState state)
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null) return;

            float halfWidth = Screen.width * 0.5f;

            foreach (TouchControl t in touch.touches)
            {
                if (t.press.wasPressedThisFrame)
                {
                    state.Confirm = true;

                    // Judge the gesture by where the finger landed, not where it has dragged to.
                    bool onMoveSide = t.startPosition.ReadValue().x < halfWidth;
                    if (onMoveSide)
                    {
                        if (_moveSideTaps.Press(Time.unscaledTime)) state.DoubleTapMoveSide = true;
                    }
                    else if (_lookSideTaps.Press(Time.unscaledTime))
                    {
                        state.DoubleTapLookSide = true;
                    }
                }

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
