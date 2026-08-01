using System.Collections.Generic;
using Backrooms.EntityManager;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using Backrooms.UIManager;
using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Application-layer composition for a run of Level 0. It owns the order of operations that no
    /// single module can own: generate and build the maze, then place the player in its spawn cell,
    /// then watch for the player reaching the exit. Cross-module calls go only through each module's
    /// public facade.
    /// </summary>
    public sealed class GameplayController : MonoBehaviour
    {
        [Header("Modules")]
        [Tooltip("The maze module. Found in the scene if left empty.")]
        [SerializeField] private MazeFacade maze;

        [Tooltip("The player module. Found in the scene if left empty.")]
        [SerializeField] private PlayerFacade player;

        [Tooltip("The heads-up display. Found in the scene if left empty.")]
        [SerializeField] private HudFacade hud;

        [Tooltip("A Dweller that hunts the player. Any others needed for the floor size are spawned.")]
        [SerializeField] private DwellerFacade dweller;

        [Tooltip("Grid cells of floor per Dweller. A 24x24 floor at 190 gets three.")]
        [SerializeField] private int cellsPerDweller = 190;

        [Tooltip("Never place more than this many Dwellers on one floor.")]
        [SerializeField] private int maxDwellers = 4;

        /// <summary>Every Dweller currently roaming the floor.</summary>
        private readonly List<DwellerFacade> _dwellers = new List<DwellerFacade>();

        [Header("Run")]
        [Tooltip("Seed for the maze. Change for a different layout.")]
        [SerializeField] private int seed = 1;

        [Tooltip("How close to a stairwell's centre counts as descending, in metres.")]
        [SerializeField] private float stairsRadius = 2f;

        [Tooltip("Dweller speed in METRES per second on floor 1. Player walks at 3.2, sprints at 5.6.")]
        [SerializeField] private float dwellerBaseSpeed = 2.2f;

        [Tooltip("Extra Dweller metres per second per floor. Should stay under sprint for many floors.")]
        [SerializeField] private float dwellerSpeedPerFloor = 0.12f;

        /// <summary>Whether the player has reached the exit on the current floor.</summary>
        public bool HasEscaped { get; private set; }

        /// <summary>The floor the player is currently on, counting from 1 downwards.</summary>
        public int CurrentFloor { get; private set; } = 1;

        /// <summary>Whether a Dweller has caught the player and ended the run.</summary>
        public bool IsCaught { get; private set; }

        /// <summary>How many Dweller are roaming the current floor.</summary>
        public int DwellerCount => _dwellers.Count;

        /// <summary>Whether any Dweller is currently hunting the player.</summary>
        public bool IsHunted
        {
            get
            {
                foreach (DwellerFacade d in _dwellers)
                {
                    if (d != null && d.IsChasing) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Whether any Dweller on the floor has reached the player.
        /// </summary>
        /// <returns><c>true</c> if the run is over.</returns>
        private bool AnyDwellerCaughtPlayer()
        {
            foreach (DwellerFacade d in _dwellers)
            {
                if (d != null && d.HasCaught) return true;
            }

            return false;
        }

        /// <summary>Seconds of gameplay elapsed since the run started.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>
        /// Straight-line distance from the player to the nearest stairwell down, in metres. A floor
        /// has several, so the one that matters is whichever is closest.
        /// </summary>
        public float DistanceToStairs
        {
            get
            {
                if (maze == null || player == null) return float.PositiveInfinity;
                Vector3 stairs = maze.GetNearestStairsPosition(player.Position);
                return Vector3.Distance(
                    new Vector3(player.Position.x, 0f, player.Position.z),
                    new Vector3(stairs.x, 0f, stairs.z));
            }
        }

        /// <summary>
        /// Finds the module facades in the scene when they were not assigned in the inspector, so the
        /// scene only needs the objects present rather than hand-wired references.
        /// </summary>
        private void Awake()
        {
            if (maze == null) maze = FindAnyObjectByType<MazeFacade>();
            if (player == null) player = FindAnyObjectByType<PlayerFacade>();
            if (hud == null) hud = FindAnyObjectByType<HudFacade>();
            if (dweller == null) dweller = FindAnyObjectByType<DwellerFacade>();
        }

        /// <summary>
        /// Starts a run: builds the maze, then drops the player into its spawn cell.
        /// </summary>
        private void Start()
        {
            StartRun(seed);
        }

        /// <summary>
        /// Builds the level for a seed and places the player at the spawn point.
        /// </summary>
        /// <param name="runSeed">Seed to generate the maze with.</param>
        public void StartRun(int runSeed)
        {
            if (maze == null || player == null)
            {
                Debug.LogError("[Gameplay] Missing MazeFacade or PlayerFacade in the scene.");
                return;
            }

            seed = runSeed;
            ElapsedSeconds = 0f;
            CurrentFloor = 0;
            IsCaught = false;
            player.MovementEnabled = true;
            if (hud != null) hud.ResetHud();

            DescendToNextFloor();
        }

        /// <summary>
        /// Moves the player one floor deeper: a fresh layout with that floor's palette, the player
        /// back at its entrance, and the arrival banner shown. The seed is derived from the run seed
        /// and the floor number, so a given run always produces the same sequence of floors.
        /// </summary>
        public void DescendToNextFloor()
        {
            CurrentFloor++;
            HasEscaped = false;

            FloorTheme theme = FloorThemes.ForFloor(CurrentFloor);
            maze.GenerateAndBuild(seed + CurrentFloor * 977, theme);
            player.SpawnAt(maze.GetSpawnPosition());
            FloorAtmosphere.Apply(theme);

            PlaceDwellers();

            if (hud != null) hud.ShowFloor(CurrentFloor, theme.Name);
            Debug.Log($"[Gameplay] Floor {CurrentFloor}: {theme.Name}");
        }

        /// <summary>
        /// Drops a Dweller onto the new floor, moving a little faster on every floor down because
        /// deeper floors are meant to be deadlier.
        /// </summary>
        /// <remarks>
        /// How many is decided by the floor's area, not by taste. One Dweller wandering a 24×24 grid
        /// is one Dweller you never meet: it has 576 cells to cover and the player is heading for the
        /// nearest way down. Dwellers start in the corners furthest from the spawn — never on a
        /// stairwell, which would have one camping a cell the player has to reach, and never next to
        /// the player, which is an unavoidable death.
        /// </remarks>
        private void PlaceDwellers()
        {
            MazeLayout layout = maze.CurrentLayout;
            List<Vector2Int> starts = DwellerStarts(layout);
            EnsureDwellers(starts.Count);

            float speed = dwellerBaseSpeed + (CurrentFloor - 1) * dwellerSpeedPerFloor;
            for (int i = 0; i < _dwellers.Count; i++)
            {
                if (i < starts.Count)
                {
                    _dwellers[i].Place(layout, starts[i], player.transform, speed,
                        seed + CurrentFloor * 31 + i);
                }
                else
                {
                    _dwellers[i].Hide();
                }
            }
        }

        /// <summary>
        /// How many Dwellers this floor gets, and where each starts. Candidates are the four corners
        /// plus the edge midpoints, ordered by distance from the spawn so the first Dwellers placed
        /// are the furthest away.
        /// </summary>
        /// <param name="layout">The floor being populated.</param>
        /// <returns>One start cell per Dweller the floor should carry.</returns>
        private List<Vector2Int> DwellerStarts(MazeLayout layout)
        {
            int wanted = Mathf.Clamp(
                layout.Width * layout.Height / Mathf.Max(1, cellsPerDweller), 1, Mathf.Max(1, maxDwellers));

            int right = layout.Width - 1;
            int top = layout.Height - 1;
            var candidates = new List<Vector2Int>
            {
                new Vector2Int(right, top),
                new Vector2Int(0, top),
                new Vector2Int(right, 0),
                new Vector2Int(right / 2, top),
                new Vector2Int(right, top / 2),
                new Vector2Int(right / 2, top / 2)
            };

            // Rotating the order by floor number stops every floor opening with the Dwellers in
            // identical places, without making their positions unpredictable within a run.
            var starts = new List<Vector2Int>(wanted);
            for (int i = 0; i < candidates.Count && starts.Count < wanted; i++)
            {
                Vector2Int cell = candidates[(i + CurrentFloor) % candidates.Count];
                if (cell == layout.Spawn) continue;
                if (layout.IsStairs(cell)) continue;
                if (starts.Contains(cell)) continue;
                starts.Add(cell);
            }

            if (starts.Count == 0) starts.Add(new Vector2Int(right, top));
            return starts;
        }

        /// <summary>
        /// Grows the pool of Dwellers to the requested size, reusing any authored in the scene and
        /// creating the rest. Dwellers persist between floors and are re-placed rather than rebuilt.
        /// </summary>
        /// <param name="count">How many Dwellers the floor needs.</param>
        private void EnsureDwellers(int count)
        {
            if (_dwellers.Count == 0)
            {
                if (dweller != null) _dwellers.Add(dweller);
                foreach (DwellerFacade found in FindObjectsByType<DwellerFacade>(FindObjectsSortMode.None))
                {
                    if (!_dwellers.Contains(found)) _dwellers.Add(found);
                }
            }

            while (_dwellers.Count < count)
            {
                var go = new GameObject($"Dweller_{_dwellers.Count}");
                go.transform.SetParent(transform.parent, worldPositionStays: true);
                _dwellers.Add(go.AddComponent<DwellerFacade>());
            }
        }

        /// <summary>
        /// Updates the HUD's pursuit warning from whichever Dweller is hunting and closest.
        /// </summary>
        private void ReportPursuit()
        {
            if (hud == null) return;

            float closest = float.PositiveInfinity;
            foreach (DwellerFacade d in _dwellers)
            {
                if (d == null || !d.IsChasing) continue;
                closest = Mathf.Min(closest, Vector3.Distance(
                    new Vector3(d.transform.position.x, 0f, d.transform.position.z),
                    new Vector3(player.Position.x, 0f, player.Position.z)));
            }

            bool hunted = !float.IsPositiveInfinity(closest);
            hud.SetHunted(hunted, hunted ? 1f - Mathf.Clamp01(closest / HuntedWarningMetres) : 0f);
        }

        /// <summary>
        /// Distance at which the pursuit warning is at its faintest. Beyond a Dweller's sense range,
        /// so the warning is already up by the time one is visible through the fog.
        /// </summary>
        private const float HuntedWarningMetres = 34f;

        /// <summary>
        /// Tracks run time and detects the player reaching a stairwell down.
        /// </summary>
        private void Update()
        {
            if (maze == null || player == null) return;

            if (IsCaught)
            {
                // A death screen you cannot leave is not a losing condition, it is a dead end.
                if (player.ConfirmPressed) StartRun(seed);
                return;
            }

            if (HasEscaped) return;

            if (AnyDwellerCaughtPlayer())
            {
                IsCaught = true;
                player.MovementEnabled = false;
                if (hud != null)
                {
                    hud.SetHunted(false, 0f);
                    hud.ShowCaught(CurrentFloor, ElapsedSeconds);
                }

                Debug.Log($"[Gameplay] Caught on floor {CurrentFloor} after {ElapsedSeconds:F1}s");
                return;
            }

            ElapsedSeconds += Time.deltaTime;
            if (hud != null) hud.SetElapsed(ElapsedSeconds);
            ReportPursuit();

            if (DistanceToStairs <= stairsRadius)
            {
                HasEscaped = true;
                DescendToNextFloor();
            }
        }
    }
}
