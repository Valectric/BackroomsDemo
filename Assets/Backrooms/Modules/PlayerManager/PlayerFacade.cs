using Backrooms.PlayerManager.Internal;
using UnityEngine;

namespace Backrooms.PlayerManager
{
    /// <summary>
    /// This is a Module. The single public door into PlayerManager: a first-person player that walks
    /// and looks from touch or desktop input and collides with the level. Place one on a GameObject
    /// in the scene; it self-bootstraps its controller, head camera and internal router. Concrete by
    /// design — there is no interface (zero-interface rule).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFacade : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Eye height above the floor, in metres.")]
        [SerializeField] private float eyeHeight = 1.7f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintSpeed = 5.6f;
        [SerializeField] private float lookSensitivity = 0.12f;

        private CharacterController _controller;
        private PlayerRouter _router;
        private PlayerManagerTestFacade _testFacade;

        /// <summary>The player's head camera, created on Awake if the prefab has none.</summary>
        public Camera HeadCamera { get; private set; }

        /// <summary>Current world position of the player's feet.</summary>
        public Vector3 Position => transform.position;

        /// <summary>Current facing direction on the horizontal plane.</summary>
        public Vector3 Forward => transform.forward;

        /// <summary>
        /// Whether a confirm press — a click or a tap — started this frame. Exposed so the gameplay
        /// layer can offer choices like "try again" without reaching for input hardware itself; this
        /// module is the one that owns devices.
        /// </summary>
        public bool ConfirmPressed => _router != null && _router.ReadInput().Confirm;

        /// <summary>Whether a double tap landed on the movement side this frame.</summary>
        public bool DoubleTappedMoveSide
            => _router != null && _router.ReadInput().DoubleTapMoveSide;

        /// <summary>Whether a double tap landed on the look side this frame.</summary>
        public bool DoubleTappedLookSide
            => _router != null && _router.ReadInput().DoubleTapLookSide;

        /// <summary>
        /// Moves the player forward through the level, stopping short of anything solid.
        /// </summary>
        /// <param name="distance">How far to try to travel, in metres.</param>
        /// <returns>How far the player actually moved, in metres.</returns>
        public float Blink(float distance)
        {
            if (_router == null) return 0f;

            Vector3 from = transform.position + Vector3.up * 0.9f;
            Vector3 heading = transform.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-4f) return 0f;
            heading.Normalize();

            // Stop short of whatever is in the way rather than through it — a blink that can cross a
            // wall turns a 96m maze into an open field.
            float travel = distance;
            if (Physics.Raycast(from, heading, out RaycastHit hit, distance + _controller.radius))
            {
                travel = Mathf.Max(0f, hit.distance - _controller.radius - 0.1f);
            }

            if (travel <= 0.05f) return 0f;
            _router.Teleport(transform.position + heading * travel);
            return travel;
        }

        /// <summary>
        /// Whether the player is doing anything at all this frame — moving, looking, or tapping.
        /// Used to notice someone who has stopped, which is when a control hint is worth showing.
        /// </summary>
        public bool HasInput
        {
            get
            {
                if (_router == null) return false;
                PlayerInputState input = _router.ReadInput();
                return input.Move.sqrMagnitude > 0.02f
                       || input.Look.sqrMagnitude > 0.25f
                       || input.Confirm
                       || input.DoubleTapMoveSide
                       || input.DoubleTapLookSide;
            }
        }

        /// <summary>Whether the player is asking to move this frame.</summary>
        public bool IsMoving => _router != null && _router.ReadInput().Move.sqrMagnitude > 0.02f;

        /// <summary>Whether the player is asking to sprint while moving.</summary>
        public bool IsSprinting
        {
            get
            {
                if (_router == null) return false;
                PlayerInputState input = _router.ReadInput();
                return input.Sprint && input.Move.sqrMagnitude > 0.02f;
            }
        }

        /// <summary>
        /// Builds the module's own pieces: the character controller sizing, the head camera, and the
        /// internal router that wires input to movement.
        /// </summary>
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.height = 1.8f;
            _controller.radius = 0.3f;
            _controller.center = new Vector3(0f, 0.9f, 0f);
            _controller.slopeLimit = 50f;
            _controller.stepOffset = 0.3f;

            EnsureHeadCamera();
            _router = new PlayerRouter(_controller, transform, HeadCamera.transform);
            ApplyTuning();
        }

        /// <summary>
        /// Drives the player's movement on the physics clock, as gameplay logic should be.
        /// </summary>
        private void FixedUpdate()
        {
            if (!MovementEnabled) return;
            _router?.Tick(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Whether the player responds to movement and look input. Turned off while a run is over so
        /// the end-of-run screen is a stop rather than a wander with a banner over it.
        /// </summary>
        public bool MovementEnabled { get; set; } = true;

        /// <summary>
        /// Places the player at a world position, keeping their feet on the given floor level.
        /// </summary>
        /// <param name="footPosition">World position for the player's feet.</param>
        public void SpawnAt(Vector3 footPosition)
        {
            _router?.Teleport(footPosition + Vector3.up * 0.1f);
        }

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="PlayerManagerTestFacade"/>.</returns>
        public PlayerManagerTestFacade GetTestFacade()
            => _testFacade ??= new PlayerManagerTestFacade(_router, this);

        /// <summary>
        /// Finds an existing child camera or creates a head camera at eye height, so a bare
        /// GameObject with this facade is a complete, usable player.
        /// </summary>
        private void EnsureHeadCamera()
        {
            HeadCamera = GetComponentInChildren<Camera>();
            if (HeadCamera != null) return;

            var head = new GameObject("Head");
            head.transform.SetParent(transform, worldPositionStays: false);
            head.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            HeadCamera = head.AddComponent<Camera>();
            HeadCamera.nearClipPlane = 0.05f;

            // Fog is effectively opaque well before this, so anything beyond is shaded and thrown
            // away. The default 1000m clip submitted the entire level every frame, and clearing with
            // the skybox drew a full-screen pass behind a sealed interior. Clearing to a flat colour
            // instead also means the horizon matches the fog rather than showing sky through a gap.
            HeadCamera.farClipPlane = 45f;
            HeadCamera.clearFlags = CameraClearFlags.SolidColor;
            HeadCamera.backgroundColor = RenderSettings.fogColor;
            head.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Pushes the inspector-configured speeds and sensitivity into the motor.
        /// </summary>
        private void ApplyTuning()
        {
            _router.SetTuning(walkSpeed, sprintSpeed, lookSensitivity);
        }
    }
}
