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

        [Header("Run")]
        [Tooltip("Seed for the maze. Change for a different layout.")]
        [SerializeField] private int seed = 1;

        [Tooltip("How close to the exit centre counts as escaping, in metres.")]
        [SerializeField] private float exitRadius = 1.5f;

        /// <summary>Whether the player has reached the exit this run.</summary>
        public bool HasEscaped { get; private set; }

        /// <summary>Seconds of gameplay elapsed since the run started.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Straight-line distance from the player to the exit, in metres.</summary>
        public float DistanceToExit =>
            maze == null || player == null
                ? float.PositiveInfinity
                : Vector3.Distance(
                    new Vector3(player.Position.x, 0f, player.Position.z),
                    new Vector3(maze.GetExitPosition().x, 0f, maze.GetExitPosition().z));

        /// <summary>
        /// Finds the module facades in the scene when they were not assigned in the inspector, so the
        /// scene only needs the objects present rather than hand-wired references.
        /// </summary>
        private void Awake()
        {
            if (maze == null) maze = FindAnyObjectByType<MazeFacade>();
            if (player == null) player = FindAnyObjectByType<PlayerFacade>();
            if (hud == null) hud = FindAnyObjectByType<HudFacade>();
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
            HasEscaped = false;
            ElapsedSeconds = 0f;

            maze.GenerateAndBuild(runSeed);
            player.SpawnAt(maze.GetSpawnPosition());
            if (hud != null) hud.ResetHud();
        }

        /// <summary>
        /// Tracks run time and detects the player reaching the exit.
        /// </summary>
        private void Update()
        {
            if (HasEscaped || maze == null || player == null) return;

            ElapsedSeconds += Time.deltaTime;
            if (hud != null) hud.SetElapsed(ElapsedSeconds);

            if (DistanceToExit <= exitRadius)
            {
                HasEscaped = true;
                if (hud != null) hud.ShowEscaped(ElapsedSeconds);
                Debug.Log($"[Gameplay] Escaped in {ElapsedSeconds:F1}s");
            }
        }
    }
}
