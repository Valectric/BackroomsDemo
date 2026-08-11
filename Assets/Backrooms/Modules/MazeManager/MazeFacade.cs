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
        [Tooltip("Grid width in cells. 24x24 at 4m per cell is a 96m floor.")]
        [SerializeField] private int width = 24;

        [Tooltip("Grid height in cells.")]
        [SerializeField] private int height = 24;
        [SerializeField] private int seed = 1;
        [SerializeField] private float cellSize = 4f;

        [Header("Geometry")]
        [Tooltip("Fallback ceiling height. Each floor theme overrides this with its own.")]
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
        public float WallHeight => CeilingHeight;

        /// <summary>The ceiling height this floor builds at: the theme's, or the inspector fallback.</summary>
        private float CeilingHeight => _theme != null && _theme.CeilingHeight > 0.5f
            ? _theme.CeilingHeight
            : wallHeight;

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
        /// <param name="hasFloorAbove">Whether to carry ways up; false on the first floor.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout GenerateAndBuild(int newSeed, FloorTheme theme = null,
            bool hasFloorAbove = true)
        {
            EnsureRouter();
            seed = newSeed;
            if (theme != null) _theme = theme;
            CurrentLayout = _router.Generate(new MazeSettings(width, height, newSeed, cellSize)
            {
                HasFloorAbove = hasFloorAbove,
                RoomCount = _theme.RoomCount,
                RoomMinSize = _theme.RoomMinSize,
                RoomMaxSize = _theme.RoomMaxSize
            });
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

            // The theme owns the height, so a floor's proportions travel with its palette rather
            // than being one number shared by every floor in the game.
            _geometryRoot = _router.BuildGeometry(
                CurrentLayout, CeilingHeight, lightSpacingCells, transform, _theme);
        }

        /// <summary>
        /// World-space position where the player should start, at floor level in the spawn cell.
        /// </summary>
        /// <returns>The spawn position, or <see cref="Vector3.zero"/> if no layout exists.</returns>
        public Vector3 GetSpawnPosition()
            => CurrentLayout == null ? Vector3.zero : CurrentLayout.CellCenterToWorld(CurrentLayout.Spawn);

        /// <summary>
        /// World-space position of the stairwell nearest a point, at floor level in its cell. A floor
        /// carries several ways down, so "the exit" is whichever one the player is closest to.
        /// </summary>
        /// <param name="from">World position to measure from.</param>
        /// <returns>The nearest stairwell position, or <see cref="Vector3.zero"/> if no layout exists.</returns>
        public Vector3 GetNearestStairsPosition(Vector3 from)
        {
            if (CurrentLayout == null) return Vector3.zero;
            Vector2Int nearest = CurrentLayout.NearestStairs(CurrentLayout.WorldToCell(from));
            return CurrentLayout.CellCenterToWorld(nearest);
        }

        /// <summary>
        /// World-space position of the way up nearest a point, at floor level in its cell.
        /// </summary>
        /// <param name="from">World position to measure from.</param>
        /// <returns>The nearest way up, or <see cref="Vector3.zero"/> if no layout exists.</returns>
        public Vector3 GetNearestStairsUpPosition(Vector3 from)
        {
            if (CurrentLayout == null) return Vector3.zero;
            Vector2Int nearest = CurrentLayout.NearestStairsUp(CurrentLayout.WorldToCell(from));
            return CurrentLayout.CellCenterToWorld(nearest);
        }

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
