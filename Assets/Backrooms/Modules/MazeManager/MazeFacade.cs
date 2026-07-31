using Backrooms.MazeManager.Internal;
using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// This is a Module. The single public door into MazeManager: generates the deterministic
    /// Level-0 maze layout and builds its scene geometry (walls, floor, ceiling, fluorescent
    /// lights). Place one on a GameObject in the scene; it self-bootstraps its internal router.
    /// Concrete by design — there is no interface (zero-interface rule).
    /// </summary>
    public sealed class MazeFacade : MonoBehaviour
    {
        [Header("Maze")]
        [SerializeField] private int width = 16;
        [SerializeField] private int height = 16;
        [SerializeField] private int seed = 1;
        [SerializeField] private float cellSize = 4f;

        [Header("Geometry")]
        [SerializeField] private float wallHeight = 3f;
        [SerializeField] private int lightSpacingCells = 3;

        [Header("Lifecycle")]
        [Tooltip("Generate and build the maze automatically on Start.")]
        [SerializeField] private bool buildOnStart = true;

        private MazeRouter _router;
        private MazeManagerTestFacade _testFacade;
        private GameObject _geometryRoot;
        private FloorTheme _theme = FloorThemes.ForFloor(1);

        /// <summary>The most recently generated layout, or <c>null</c> if none yet.</summary>
        public MazeLayout CurrentLayout { get; private set; }

        /// <summary>The seed used for the next or most recent generation.</summary>
        public int Seed => seed;

        /// <summary>The palette the maze is currently built with.</summary>
        public FloorTheme CurrentTheme => _theme;

        /// <summary>Wall height in metres, used for camera and light placement.</summary>
        public float WallHeight => wallHeight;

        /// <summary>
        /// Initialises the module's internal router before any other component's Start runs.
        /// </summary>
        private void Awake()
        {
            EnsureRouter();
        }

        /// <summary>
        /// Builds the maze on Start when <c>buildOnStart</c> is enabled, so a scene containing only
        /// this facade produces a playable level with no extra wiring.
        /// </summary>
        private void Start()
        {
            if (buildOnStart && CurrentLayout == null) GenerateAndBuild(seed);
        }

        /// <summary>
        /// Generates a maze for the given settings, stores it as <see cref="CurrentLayout"/>, and
        /// returns it. Does not build geometry.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout Generate(MazeSettings settings)
        {
            EnsureRouter();
            CurrentLayout = _router.Generate(settings);
            return CurrentLayout;
        }

        /// <summary>
        /// Generates a maze with the given seed using the inspector-configured size, then rebuilds
        /// the scene geometry for it. Any previously built geometry is destroyed first.
        /// </summary>
        /// <param name="newSeed">Seed to generate with.</param>
        /// <param name="theme">Palette to build with; keeps the current one when null.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout GenerateAndBuild(int newSeed, FloorTheme theme = null)
        {
            EnsureRouter();
            seed = newSeed;
            if (theme != null) _theme = theme;
            CurrentLayout = _router.Generate(new MazeSettings(width, height, newSeed, cellSize));
            RebuildGeometry();
            return CurrentLayout;
        }

        /// <summary>
        /// Destroys any existing geometry and rebuilds it from <see cref="CurrentLayout"/>. Does
        /// nothing when no layout has been generated yet.
        /// </summary>
        public void RebuildGeometry()
        {
            if (CurrentLayout == null) return;
            EnsureRouter();

            if (_geometryRoot != null)
            {
                if (Application.isPlaying) Destroy(_geometryRoot);
                else DestroyImmediate(_geometryRoot);
            }

            _geometryRoot = _router.BuildGeometry(
                CurrentLayout, wallHeight, lightSpacingCells, transform, _theme);
        }

        /// <summary>
        /// World-space position where the player should start, at floor level in the spawn cell.
        /// </summary>
        /// <returns>The spawn position, or <see cref="Vector3.zero"/> if no layout exists.</returns>
        public Vector3 GetSpawnPosition()
            => CurrentLayout == null ? Vector3.zero : CurrentLayout.CellCenterToWorld(CurrentLayout.Spawn);

        /// <summary>
        /// World-space position of the exit, at floor level in the exit cell.
        /// </summary>
        /// <returns>The exit position, or <see cref="Vector3.zero"/> if no layout exists.</returns>
        public Vector3 GetExitPosition()
            => CurrentLayout == null ? Vector3.zero : CurrentLayout.CellCenterToWorld(CurrentLayout.Exit);

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="MazeManagerTestFacade"/>.</returns>
        public MazeManagerTestFacade GetTestFacade()
        {
            EnsureRouter();
            return _testFacade ??= new MazeManagerTestFacade(_router);
        }

        /// <summary>
        /// Creates the router once if it does not yet exist, so generation works whether called from
        /// Awake or directly by a test immediately after AddComponent.
        /// </summary>
        private void EnsureRouter()
        {
            _router ??= new MazeRouter();
        }
    }
}
