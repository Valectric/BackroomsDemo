using Backrooms.PlayerManager.Internal;
using UnityEngine;

namespace Backrooms.PlayerManager
{
    /// <summary>
    /// Test seam for the PlayerManager module. Its constructor takes the internal router, so only the
    /// production <see cref="PlayerFacade"/> can create one (via <see cref="PlayerFacade.GetTestFacade"/>).
    /// Not intended for production use — only for automated testing. Enables inbound simulation mode
    /// so a test can drive the player by intent ("walk forward", "look right") through exactly the
    /// same movement code a real player's input reaches.
    /// </summary>
    public sealed class PlayerManagerTestFacade
    {
        private readonly PlayerRouter _router;
        private readonly PlayerFacade _facade;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        /// <param name="facade">The owning production facade.</param>
        internal PlayerManagerTestFacade(PlayerRouter router, PlayerFacade facade)
        {
            _router = router;
            _facade = facade;
        }

        /// <summary>Whether the module is acting on simulated input instead of real hardware.</summary>
        public bool SimulationEnabled
        {
            get => _router.SimulationEnabled;
            set => _router.SimulationEnabled = value;
        }

        /// <summary>Current camera pitch in degrees, negative looking up.</summary>
        public float Pitch => _router.Pitch;

        /// <summary>Current world position of the player's feet.</summary>
        public Vector3 Position => _facade.Position;

        /// <summary>Current yaw of the player body in degrees.</summary>
        public float Yaw => _facade.transform.eulerAngles.y;

        /// <summary>
        /// Sets the simulated movement and look intent applied on subsequent physics steps.
        /// </summary>
        /// <param name="move">Movement intent, X strafes and Y walks forward, range -1..1.</param>
        /// <param name="look">Look delta for each step, X yaws and Y pitches.</param>
        /// <param name="sprint">Whether sprint is held.</param>
        public void SetInput(Vector2 move, Vector2 look = default, bool sprint = false)
        {
            _router.SetSimulatedInput(new PlayerInputState
            {
                Move = move,
                Look = look,
                Sprint = sprint
            });
        }

        /// <summary>
        /// Clears simulated intent so the player stands still.
        /// </summary>
        /// <summary>
        /// Drives a double-tap detector directly with a timestamp, so gesture recognition can be
        /// tested through any timing without a device or a wall clock.
        /// </summary>
        /// <param name="time">Time of the press, in seconds.</param>
        /// <returns><c>true</c> if this press completed a double tap.</returns>
        public bool PressForDoubleTap(float time) => _taps.Press(time);

        /// <summary>Detector the gesture tests drive.</summary>
        private readonly Internal.Input.DoubleTapDetector _taps = new Internal.Input.DoubleTapDetector();

        /// <summary>
        /// Simulates a tap or click, the same press a player uses to start a run or to try again.
        /// </summary>
        /// <remarks>
        /// Exposed so an end-to-end test can get past the title screen the way a player does, rather
        /// than by calling a production method to cause the effect.
        /// </remarks>
        public void Tap()
        {
            _router.SetSimulatedInput(new PlayerInputState { Confirm = true });
        }

        public void ClearInput() => _router.SetSimulatedInput(PlayerInputState.None);
    }
}
