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
        [Tooltip("Metres per second. Compare directly against the player's walk and sprint speeds.")]
        [SerializeField] private float metresPerSecond = 2.2f;

        [Tooltip("How many cells away the Dweller notices the player.")]
        [SerializeField] private int senseRangeCells = 12;

        [Tooltip("How close, in metres, counts as catching the player.")]
        [SerializeField] private float catchRadius = 1.1f;

        private readonly DwellerRouter _router = new DwellerRouter();
        private DwellerManagerTestFacade _testFacade;
        private Transform _target;
        private GameObject _body;
        private GameObject _eyes;
        private Light _chaseLight;
        private Material _bodyMaterial;

        /// <summary>Body colour while the Dweller has not noticed the player.</summary>
        private static readonly Color LurkingColour = new Color(0.06f, 0.05f, 0.07f);

        /// <summary>Body colour once it is hunting, so a chase reads at a glance.</summary>
        private static readonly Color HuntingColour = new Color(0.16f, 0.03f, 0.04f);

        /// <summary>Colour of the eyes and the light a hunting Dweller throws.</summary>
        private static readonly Color HuntingGlow = new Color(1f, 0.22f, 0.16f);

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
            _router.SenseRangeCells = senseRangeCells;
            // Patrol trips are scaled to the floor: on a big grid a Dweller that only ever walks a
            // few cells at a time stays in the corner it started in.
            _router.PatrolSpanCells = Mathf.Max(8, Mathf.Max(layout.Width, layout.Height) * 3 / 4);
            _router.Place(layout, startCell, seed);
            _target = target;
            metresPerSecond = speedMetresPerSecond;

            EnsureBody();
            transform.position = layout.CellCenterToWorld(startCell);
            _body.SetActive(true);
        }

        /// <summary>
        /// Hides the Dweller, used while no floor is active.
        /// </summary>
        public void Hide()
        {
            _router.Deactivate();
            _target = null;
            if (_body != null) _body.SetActive(false);
            ShowPursuit(false);
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

            ShowPursuit(_router.IsChasing);
        }

        /// <summary>
        /// Switches the Dweller between lurking and hunting appearance. A player has to be able to
        /// tell, at a glance down a foggy corridor, whether the shape ahead has noticed them — the
        /// difference between tense and merely confusing.
        /// </summary>
        /// <param name="hunting">Whether the Dweller is chasing the player.</param>
        private void ShowPursuit(bool hunting)
        {
            if (_eyes != null) _eyes.SetActive(hunting);
            if (_chaseLight != null) _chaseLight.enabled = hunting;
            if (_bodyMaterial == null) return;

            Color colour = hunting ? HuntingColour : LurkingColour;
            if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", colour);
            if (_bodyMaterial.HasProperty("_Color")) _bodyMaterial.SetColor("_Color", colour);
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

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _bodyMaterial = new Material(shader);
            if (_bodyMaterial.HasProperty("_Smoothness")) _bodyMaterial.SetFloat("_Smoothness", 0.1f);
            _body.GetComponent<MeshRenderer>().sharedMaterial = _bodyMaterial;

            BuildPursuitTell();
            ShowPursuit(false);
        }

        /// <summary>
        /// Builds the parts that only appear while the Dweller is hunting: a pair of glowing eyes and
        /// the red light they cast. Both are switched off while it lurks, so their appearance is the
        /// signal rather than something the player has to squint at and compare.
        /// </summary>
        private void BuildPursuitTell()
        {
            _eyes = new GameObject("DwellerEyes");
            _eyes.transform.SetParent(transform, worldPositionStays: false);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Standard");
            var glow = new Material(unlit);
            if (glow.HasProperty("_BaseColor")) glow.SetColor("_BaseColor", HuntingGlow);
            if (glow.HasProperty("_Color")) glow.SetColor("_Color", HuntingGlow);

            foreach (float side in new[] { -1f, 1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0f ? "EyeLeft" : "EyeRight";
                Object.Destroy(eye.GetComponent<Collider>());
                eye.transform.SetParent(_eyes.transform, worldPositionStays: false);
                // The body is rotated to face its heading, so the eyes go on its local +Z — and far
                // enough along it to clear the capsule. The body is 0.7 wide, so anything closer
                // than its 0.35 radius leaves the eyes buried inside it and invisible.
                eye.transform.localPosition = new Vector3(side * 0.15f, 1.80f, 0.34f);
                eye.transform.localScale = Vector3.one * 0.12f;
                eye.GetComponent<MeshRenderer>().sharedMaterial = glow;
            }

            var lightGo = new GameObject("DwellerChaseLight");
            lightGo.transform.SetParent(transform, worldPositionStays: false);
            lightGo.transform.localPosition = new Vector3(0f, 1.7f, 0.3f);

            _chaseLight = lightGo.AddComponent<Light>();
            _chaseLight.type = LightType.Point;
            _chaseLight.color = HuntingGlow;
            _chaseLight.intensity = 2.4f;
            _chaseLight.range = 6f;
            _chaseLight.shadows = LightShadows.None;
        }
    }
}
