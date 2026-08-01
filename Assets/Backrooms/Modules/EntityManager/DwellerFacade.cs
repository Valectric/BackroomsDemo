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
        [SerializeField] private float catchRadius = 1.1f;

        private readonly DwellerRouter _router = new DwellerRouter();
        private readonly DwellerBody _shape = new DwellerBody();
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
            metresPerSecond = speedMetresPerSecond * archetype.SpeedMultiplier;

            if (!_shape.Exists || _shape.Archetype.Kind != _kind) _shape.Build(transform, archetype);
            transform.position = layout.CellCenterToWorld(startCell);
            _shape.SetVisible(true);
        }

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

            Vector3 targetPos = _router.Layout.CellCenterToWorld(_router.TargetCell);
            Vector3 here = transform.position;
            float step = metresPerSecond * Time.fixedDeltaTime;

            if (Vector3.Distance(new Vector3(here.x, 0f, here.z), targetPos) <= step)
            {
                transform.position = targetPos;
                _router.ArriveAtTarget();
                _router.ChooseNextCell(playerCell);
            }
            else
            {
                Vector3 dir = (targetPos - new Vector3(here.x, 0f, here.z)).normalized;
                transform.position = here + dir * step;
                if (dir.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(dir);
            }

            float toPlayer = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_target.position.x, 0f, _target.position.z));
            if (toPlayer <= catchRadius) _router.MarkCaught();

            _shape.ShowPursuit(_router.IsChasing);
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
