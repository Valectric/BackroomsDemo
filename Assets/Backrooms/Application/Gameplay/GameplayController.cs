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

        [Tooltip("The Dweller that hunts the player. Found in the scene if left empty.")]
        [SerializeField] private DwellerFacade dweller;

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

            PlaceDweller();

            if (hud != null) hud.ShowFloor(CurrentFloor, theme.Name);
            Debug.Log($"[Gameplay] Floor {CurrentFloor}: {theme.Name}");
        }

        /// <summary>
        /// Drops a Dweller onto the new floor, moving a little faster on every floor down because
        /// deeper floors are meant to be deadlier.
        /// </summary>
        /// <remarks>
        /// It starts in a far corner that is neither the player's spawn nor a stairwell. Spawning it
        /// on a way down would have it camping a cell the player has to reach, and spawning it near
        /// the player would be an instant, unavoidable death.
        /// </remarks>
        private void PlaceDweller()
        {
            if (dweller == null) return;

            MazeLayout layout = maze.CurrentLayout;
            float speed = dwellerBaseSpeed + (CurrentFloor - 1) * dwellerSpeedPerFloor;
            dweller.Place(layout, ChooseDwellerStart(layout), player.transform, speed,
                seed + CurrentFloor);
        }

        /// <summary>
        /// Picks the corner a Dweller starts in: the corners are tried in an order that rotates with
        /// the floor number, so consecutive floors do not all start it in the same place, and any
        /// corner holding the spawn or a stairwell is skipped.
        /// </summary>
        /// <param name="layout">The floor being populated.</param>
        /// <returns>The cell to start the Dweller in.</returns>
        private Vector2Int ChooseDwellerStart(MazeLayout layout)
        {
            Vector2Int[] corners =
            {
                new Vector2Int(0, layout.Height - 1),
                new Vector2Int(layout.Width - 1, 0),
                new Vector2Int(layout.Width - 1, layout.Height - 1)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2Int candidate = corners[(CurrentFloor + i) % corners.Length];
                if (candidate == layout.Spawn) continue;
                if (layout.IsStairs(candidate)) continue;
                return candidate;
            }

            return corners[0];
        }

        /// <summary>
        /// Tracks run time and detects the player reaching a stairwell down.
        /// </summary>
        private void Update()
        {
            if (IsCaught || HasEscaped || maze == null || player == null) return;

            if (dweller != null && dweller.HasCaught)
            {
                IsCaught = true;
                if (hud != null) hud.ShowCaught(CurrentFloor, ElapsedSeconds);
                Debug.Log($"[Gameplay] Caught on floor {CurrentFloor} after {ElapsedSeconds:F1}s");
                return;
            }

            ElapsedSeconds += Time.deltaTime;
            if (hud != null) hud.SetElapsed(ElapsedSeconds);

            if (DistanceToStairs <= stairsRadius)
            {
                HasEscaped = true;
                DescendToNextFloor();
            }
        }
    }
}
