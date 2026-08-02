using Backrooms.EntityManager.Internal;
using Backrooms.EntityManager.Internal.Behaviour;
using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.EntityManager
{
    /// <summary>
    /// This is a Module. The single public door into EntityManager: a Dweller, one of the nightmare
    /// creatures that roam the floors of the Backrooms. It wanders its floor until the player comes
    /// within range, then paths after them through the corridors and catches them. Concrete by
    /// design — there is no interface.
    /// </summary>
    public sealed class DwellerFacade : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Metres per second. Compare directly against the player's walk and sprint speeds.")]
        [SerializeField] private float metresPerSecond = 2.2f;

        [Tooltip("How many cells away the Dweller notices the player.")]
        [SerializeField] private int senseRangeCells = 12;

        [Tooltip("How close, in metres, counts as catching the player.")]
        [SerializeField] private float catchRadius = 1.35f;

        [Tooltip("Hard ceiling on hunting speed. Must stay under the player's sprint (5.6) at any depth.")]
        [SerializeField] private float maxChaseMetresPerSecond = 5.1f;

        /// <summary>Speed while unaware, in metres per second.</summary>
        private float _patrolSpeed = 2.2f;

        /// <summary>Speed while hunting, in metres per second.</summary>
        private float _chaseSpeed = 2.2f;

        /// <summary>
        /// Distance at which a hunting Dweller stops following the grid and steers straight at the
        /// player. Wider than a cell half-width, so hugging a wall cannot put anyone out of reach.
        /// </summary>
        private const float CloseInMetres = 3f;

        private readonly DwellerRouter _router = new DwellerRouter();
        private readonly DwellerBody _shape = new DwellerBody();
        private readonly DwellerGait _gait = new DwellerGait();
        private DwellerManagerTestFacade _testFacade;
        private Transform _target;
        private DwellerKind _kind = DwellerKind.Lurker;

        /// <summary>Which kind of Dweller this is.</summary>
        public DwellerKind Kind => _kind;

        /// <summary>What this Dweller is called when it gives chase.</summary>
        public string DisplayName => DwellerArchetypes.For(_kind).DisplayName;

        /// <summary>
        /// Chooses which kind of Dweller this is. Call before <see cref="Place"/>; the shape is
        /// rebuilt on the next placement so one object can be a different creature each floor.
        /// </summary>
        /// <param name="kind">The kind to become.</param>
        public void SetKind(DwellerKind kind) => _kind = kind;

        /// <summary>What the Dweller is currently doing.</summary>
        public DwellerState State => _router.State;

        /// <summary>Whether the Dweller has caught the player.</summary>
        public bool HasCaught => _router.State == DwellerState.Caught;

        /// <summary>Whether the Dweller is actively hunting the player right now.</summary>
        public bool IsChasing => _router.IsChasing;

        /// <summary>The cell the Dweller currently occupies.</summary>
        public Vector2Int Cell => _router.Cell;

        /// <summary>
        /// Places the Dweller on a floor and gives it something to hunt.
        /// </summary>
        /// <param name="layout">The maze it roams.</param>
        /// <param name="startCell">Cell to start in.</param>
        /// <param name="target">The player transform to hunt.</param>
        /// <param name="speedMetresPerSecond">Movement speed for this floor, in metres per second.</param>
        /// <param name="seed">Seed for deterministic wandering.</param>
        public void Place(MazeLayout layout, Vector2Int startCell, Transform target,
            float speedMetresPerSecond, int seed)
        {
            DwellerArchetype archetype = DwellerArchetypes.For(_kind);

            _router.SenseRangeCells = Mathf.Max(1,
                Mathf.RoundToInt(senseRangeCells * archetype.SenseMultiplier));

            // Patrol trips are scaled to the floor: on a big grid a Dweller that only ever walks a
            // few cells at a time stays in the corner it started in.
            int baseSpan = Mathf.Max(8, Mathf.Max(layout.Width, layout.Height) * 3 / 4);
            _router.PatrolSpanCells = Mathf.Max(3,
                Mathf.RoundToInt(baseSpan * archetype.PatrolMultiplier));

            _router.Place(layout, startCell, seed);
            _target = target;

            _patrolSpeed = speedMetresPerSecond * archetype.SpeedMultiplier;

            // Hunting speed is its own number, not the patrol speed. With one speed for both, every
            // kind ambled below the player's 3.2 m/s walk and a chase could never end in a catch —
            // the game reported being hunted on 88% of crossings and killed the player on none of
            // them. The ceiling keeps a sprint an escape however deep the floor.
            _chaseSpeed = Mathf.Min(
                speedMetresPerSecond * archetype.ChaseMultiplier, maxChaseMetresPerSecond);

            metresPerSecond = _patrolSpeed;

            if (!_shape.Exists || _shape.Archetype.Kind != _kind) _shape.Build(transform, archetype);
            transform.position = layout.CellCenterToWorld(startCell);
            _shape.SetVisible(true);
        }

        /// <summary>
        /// Whether this Dweller is on a floor and able to act.
        /// </summary>
        public bool IsActive => _router.Layout != null;

        /// <summary>
        /// Unmakes the Dweller: it leaves the floor and does not come back until the next one.
        /// </summary>
        public void Banish() => Hide();

        /// <summary>
        /// Hides the Dweller, used while no floor is active.
        /// </summary>
        public void Hide()
        {
            _router.Deactivate();
            _target = null;
            _shape.SetVisible(false);
            _shape.ShowPursuit(false);
        }

        /// <summary>
        /// Moves the Dweller on the physics clock: it advances toward its target cell, picking a new
        /// one each time it arrives, and catches the player on contact.
        /// </summary>
        private void FixedUpdate()
        {
            if (_router.Layout == null || _target == null || HasCaught) return;

            Vector2Int playerCell = _router.Layout.WorldToCell(_target.position);
            _router.UpdateState(playerCell);

            // How fast, and whether it ignores the grid this frame, is the kind's own rule.
            metresPerSecond = _gait.Step(DwellerArchetypes.For(_kind), transform.position,
                _target.position, _target.forward, _router.IsChasing, _patrolSpeed, _chaseSpeed,
                Time.fixedDeltaTime);

            Vector3 here = transform.position;

            // A charge runs at a point it committed to, straight through the pathing. It is allowed
            // to leave the grid because it only ever starts with a clear line to the player, so the
            // line it takes is one the player could have seen coming.
            if (_gait.StraightTarget.HasValue)
            {
                StepStraight(_gait.StraightTarget.Value);
                Finish();
                return;
            }

            // Once it is in the player's own cell, steer at the player rather than at the cell
            // centre. Pathing is a grid, but the player is not on it: standing against a wall puts
            // them 2m off centre on a 4m cell, and a Dweller that only ever walks centre-to-centre
            // passes by at arm's length and never lands a catch. The last stride has to be
            // continuous or the whole chase can fail on geometry.
            float toPlayerFlat = Vector3.Distance(
                new Vector3(here.x, 0f, here.z),
                new Vector3(_target.position.x, 0f, _target.position.z));

            // Cell equality alone is not enough: a player standing exactly on a boundary belongs to
            // one cell while being physically closer to the Dweller in the next one. Home on
            // proximity as well, so the final stride never depends on which cell a coordinate
            // rounds into.
            bool closing = _router.IsChasing
                           && (_router.Cell == playerCell || toPlayerFlat <= CloseInMetres);
            Vector3 targetPos = closing
                ? new Vector3(_target.position.x, 0f, _target.position.z)
                : _router.Layout.CellCenterToWorld(_router.TargetCell);
            float step = metresPerSecond * Time.fixedDeltaTime;

            if (Vector3.Distance(new Vector3(here.x, 0f, here.z), targetPos) <= step)
            {
                transform.position = targetPos;

                // Only a grid target counts as arriving somewhere; homing on the player is a stride
                // within the cell it already occupies.
                if (!closing)
                {
                    _router.ArriveAtTarget();
                    _router.ChooseNextCell(playerCell);
                }
            }
            else
            {
                Vector3 dir = (targetPos - new Vector3(here.x, 0f, here.z)).normalized;
                transform.position = here + dir * step;
                if (dir.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(dir);
            }

            Finish();
        }

        /// <summary>
        /// Runs directly at a committed point, ignoring the grid.
        /// </summary>
        /// <param name="target">The point being charged, at this Dweller's own height.</param>
        private void StepStraight(Vector3 target)
        {
            Vector3 here = transform.position;
            var flatTarget = new Vector3(target.x, here.y, target.z);
            float step = metresPerSecond * Time.fixedDeltaTime;

            Vector3 toTarget = flatTarget - here;
            transform.position = toTarget.magnitude <= step ? flatTarget : here + toTarget.normalized * step;

            Vector3 facing = new Vector3(toTarget.x, 0f, toTarget.z);
            if (facing.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(facing);

            // The grid has to be told where the charge left it, or the next path step walks back to
            // wherever it thought it still was.
            _router.SnapTo(_router.Layout.WorldToCell(transform.position));
        }

        /// <summary>
        /// Applies the catch test and the body's colours for this frame.
        /// </summary>
        private void Finish()
        {
            float toPlayer = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_target.position.x, 0f, _target.position.z));
            if (toPlayer <= catchRadius) _router.MarkCaught();

            _shape.ShowPursuit(_router.IsChasing);
            if (_gait.Alarmed || _router.IsChasing) _shape.ShowAlarm(_gait.Alarmed);
        }

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="DwellerManagerTestFacade"/>.</returns>
        public DwellerManagerTestFacade GetTestFacade()
            => _testFacade ??= new DwellerManagerTestFacade(_router);
    }
}
