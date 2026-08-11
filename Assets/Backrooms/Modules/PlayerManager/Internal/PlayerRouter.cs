using Backrooms.PlayerManager.Internal.Input;
using Backrooms.PlayerManager.Internal.Movement;
using UnityEngine;

namespace Backrooms.PlayerManager.Internal
{
    /// <summary>
    /// Internal coordinator for the PlayerManager module. Pure single-line wiring between the input
    /// source submodule and the motor submodule; the two never reference each other directly.
    /// </summary>
    internal sealed class PlayerRouter
    {
        private readonly PlayerInputSource _input;
        private readonly PlayerMotor _motor;

        /// <summary>
        /// Creates the router over the module's submodules.
        /// </summary>
        /// <param name="controller">The player's character controller.</param>
        /// <param name="body">The transform that yaws.</param>
        /// <param name="camera">The transform that pitches.</param>
        internal PlayerRouter(CharacterController controller, Transform body, Transform camera)
        {
            _input = new PlayerInputSource();
            _motor = new PlayerMotor(controller, body, camera);
        }

        /// <summary>Whether the module is acting on simulated input instead of real hardware.</summary>
        public bool SimulationEnabled
        {
            get => _input.SimulationEnabled;
            set => _input.SimulationEnabled = value;
        }

        /// <summary>Current camera pitch in degrees.</summary>
        public float Pitch => _motor.Pitch;

        /// <summary>
        /// Advances the player by one simulation step using the currently selected input stream.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        public void Tick(float deltaTime) => _motor.Tick(_input.Read(), deltaTime);

        /// <summary>
        /// Turns the camera for this frame. Separate from <see cref="Tick"/> because a mouse delta
        /// is a per-frame quantity and must not be consumed once per physics step.
        /// </summary>
        public void TickLook() => _motor.TickLook(_input.Read());

        /// <summary>
        /// Sets the intent used while simulation mode is enabled.
        /// </summary>
        /// <param name="input">The simulated intent.</param>
        public void SetSimulatedInput(PlayerInputState input) => _input.SetSimulated(input);

        /// <summary>
        /// Reads the intent the module would act on this frame.
        /// </summary>
        /// <returns>The selected input state.</returns>
        public PlayerInputState ReadInput() => _input.Read();

        /// <summary>How many times hardware has actually been sampled, for tests.</summary>
        public int FreshInputReads => _input.FreshReads;

        /// <summary>
        /// Teleports the player to a world position.
        /// </summary>
        /// <param name="position">Target world position.</param>
        public void Teleport(Vector3 position) => _motor.Teleport(position);

        /// <summary>
        /// Applies movement tuning to the motor.
        /// </summary>
        /// <param name="walkSpeed">Metres per second while walking.</param>
        /// <param name="sprintSpeed">Metres per second while sprinting.</param>
        /// <param name="lookSensitivity">Degrees of rotation per unit of look input.</param>
        public void SetTuning(float walkSpeed, float sprintSpeed, float lookSensitivity)
        {
            _motor.WalkSpeed = walkSpeed;
            _motor.SprintSpeed = sprintSpeed;
            _motor.LookSensitivity = lookSensitivity;
        }
    }
}
