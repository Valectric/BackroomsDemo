using UnityEngine;

namespace Backrooms.PlayerManager.Internal.Movement
{
    /// <summary>
    /// Applies a <see cref="PlayerInputState"/> to the player's body and camera: yaw turns the body,
    /// pitch tilts the camera (clamped so the view cannot flip), and movement is driven through the
    /// <see cref="CharacterController"/> so walls block the player.
    /// </summary>
    /// <remarks>
    /// Plain C#: the owning facade passes the controller and camera transform in at construction, so
    /// this class never looks anything up in the scene.
    /// </remarks>
    internal sealed class PlayerMotor
    {
        /// <summary>Steepest up/down view angle in degrees.</summary>
        private const float MaxPitch = 85f;

        /// <summary>Downward acceleration in metres per second squared.</summary>
        private const float Gravity = -18f;

        /// <summary>Downward speed kept while grounded so the controller stays snapped to the floor.</summary>
        private const float GroundedStick = -2f;

        private readonly CharacterController _controller;
        private readonly Transform _body;
        private readonly Transform _camera;

        private float _pitch;
        private float _verticalSpeed;

        /// <summary>Metres per second while walking.</summary>
        public float WalkSpeed { get; set; } = 3.2f;

        /// <summary>Metres per second while sprinting.</summary>
        public float SprintSpeed { get; set; } = 5.6f;

        /// <summary>Degrees of rotation per unit of look input.</summary>
        public float LookSensitivity { get; set; } = 0.12f;

        /// <summary>Current camera pitch in degrees, negative looking up.</summary>
        public float Pitch => _pitch;

        /// <summary>
        /// Creates the motor over the player's controller, body and camera.
        /// </summary>
        /// <param name="controller">The character controller that performs collision-aware moves.</param>
        /// <param name="body">The transform that yaws (the player root).</param>
        /// <param name="camera">The transform that pitches (the head/camera).</param>
        internal PlayerMotor(CharacterController controller, Transform body, Transform camera)
        {
            _controller = controller;
            _body = body;
            _camera = camera;
        }

        /// <summary>
        /// Advances the player by one step of simulation.
        /// </summary>
        /// <param name="input">The intent to apply.</param>
        /// <param name="deltaTime">Time step in seconds.</param>
        public void Tick(PlayerInputState input, float deltaTime)
        {
            ApplyLook(input.Look);
            ApplyMove(input, deltaTime);
        }

        /// <summary>
        /// Turns the body by the yaw delta and tilts the camera by the pitch delta, clamping pitch to
        /// <see cref="MaxPitch"/> in both directions.
        /// </summary>
        /// <param name="look">Look delta for this step.</param>
        private void ApplyLook(Vector2 look)
        {
            if (look.sqrMagnitude > 0f)
            {
                _body.Rotate(Vector3.up, look.x * LookSensitivity, Space.World);
                _pitch = Mathf.Clamp(_pitch - look.y * LookSensitivity, -MaxPitch, MaxPitch);
            }

            if (_camera != null) _camera.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>
        /// Moves the controller horizontally from the movement intent and applies gravity.
        /// </summary>
        /// <param name="input">The intent to apply.</param>
        /// <param name="deltaTime">Time step in seconds.</param>
        private void ApplyMove(PlayerInputState input, float deltaTime)
        {
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 planar = (_body.right * move.x + _body.forward * move.y)
                             * (input.Sprint ? SprintSpeed : WalkSpeed);

            if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = GroundedStick;
            else _verticalSpeed += Gravity * deltaTime;

            Vector3 velocity = planar + Vector3.up * _verticalSpeed;
            _controller.Move(velocity * deltaTime);
        }

        /// <summary>
        /// Teleports the player to a position, resetting fall speed. Used for spawning.
        /// </summary>
        /// <param name="position">World position to place the player at.</param>
        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            _body.position = position;
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }
    }
}
