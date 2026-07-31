using Backrooms.EntityManager.Internal;
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
        [Tooltip("Cells travelled per second. The gameplay layer raises this on deeper floors.")]
        [SerializeField] private float cellsPerSecond = 1.4f;

        [Tooltip("How many cells away the Dweller notices the player.")]
        [SerializeField] private int senseRangeCells = 5;

        [Tooltip("How close, in metres, counts as catching the player.")]
        [SerializeField] private float catchRadius = 1.1f;

        private readonly DwellerRouter _router = new DwellerRouter();
        private DwellerManagerTestFacade _testFacade;
        private Transform _target;
        private GameObject _body;

        /// <summary>What the Dweller is currently doing.</summary>
        public DwellerState State => _router.State;

        /// <summary>Whether the Dweller has caught the player.</summary>
        public bool HasCaught => _router.State == DwellerState.Caught;

        /// <summary>The cell the Dweller currently occupies.</summary>
        public Vector2Int Cell => _router.Cell;

        /// <summary>
        /// Places the Dweller on a floor and gives it something to hunt.
        /// </summary>
        /// <param name="layout">The maze it roams.</param>
        /// <param name="startCell">Cell to start in.</param>
        /// <param name="target">The player transform to hunt.</param>
        /// <param name="speedCellsPerSecond">Movement speed for this floor.</param>
        /// <param name="seed">Seed for deterministic wandering.</param>
        public void Place(MazeLayout layout, Vector2Int startCell, Transform target,
            float speedCellsPerSecond, int seed)
        {
            _router.SenseRangeCells = senseRangeCells;
            _router.Place(layout, startCell, seed);
            _target = target;
            cellsPerSecond = speedCellsPerSecond;

            EnsureBody();
            transform.position = layout.CellCenterToWorld(startCell);
            _body.SetActive(true);
        }

        /// <summary>
        /// Hides the Dweller, used while no floor is active.
        /// </summary>
        public void Hide()
        {
            if (_body != null) _body.SetActive(false);
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
            float step = cellsPerSecond * _router.Layout.CellSize * Time.fixedDeltaTime;

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
        }

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="DwellerManagerTestFacade"/>.</returns>
        public DwellerManagerTestFacade GetTestFacade()
            => _testFacade ??= new DwellerManagerTestFacade(_router);

        /// <summary>
        /// Builds the Dweller's visible body once: a dark, unsettlingly tall shape that reads at a
        /// distance down a corridor without needing any imported art.
        /// </summary>
        private void EnsureBody()
        {
            if (_body != null) return;

            _body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _body.name = "DwellerBody";
            Object.Destroy(_body.GetComponent<Collider>());
            _body.transform.SetParent(transform, worldPositionStays: false);
            _body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            _body.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);

            var colour = new Color(0.06f, 0.05f, 0.07f);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            _body.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
