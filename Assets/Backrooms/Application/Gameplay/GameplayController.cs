using System.Collections.Generic;
using Backrooms.EntityManager;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using System.Collections.Generic;
using Backrooms.RelicManager;
using Backrooms.UIManager;
using Backrooms.AudioManager;
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

        [Tooltip("The relics module. Found in the scene if left empty.")]
        [SerializeField] private RelicFacade relics;

        [Tooltip("The audio module. Found in the scene if left empty.")]
        [SerializeField] private AudioFacade audioModule;

        [Tooltip("Grid cells of floor per Dweller. A 24x24 floor at 190 gets three.")]
        [SerializeField] private int cellsPerDweller = 190;

        [Tooltip("Never place more than this many Dwellers on one floor.")]
        [SerializeField] private int maxDwellers = 4;

        /// <summary>Decides how many Dwellers a floor carries and where they go.</summary>
        private DwellerDirector _director;

        /// <summary>Turns carried relics into compass arrows and usable powers.</summary>
        private readonly PowerDirector _powers = new PowerDirector();

        /// <summary>Reused each frame so the HUD is not handed fresh lists sixty times a second.</summary>
        private readonly List<string> _carriedLines = new List<string>();

        /// <summary>Colour for each carried line.</summary>
        private readonly List<Color> _carriedColours = new List<Color>();

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

        /// <summary>How many Dwellers are roaming the current floor.</summary>
        public int DwellerCount => _director == null ? 0 : _director.Count;

        /// <summary>Whether any Dweller is currently hunting the player.</summary>
        public bool IsHunted => _director != null && _director.AnyHunting;

        /// <summary>How many relics the player has collected this run.</summary>
        public int RelicsCollected => relics == null ? 0 : relics.Collected;

        /// <summary>Deepest floor reached in any run on this device.</summary>
        public int BestFloors { get; private set; }

        /// <summary>Most relics carried in any run on this device.</summary>
        public int BestRelics { get; private set; }

        /// <summary>Where the best run is remembered between sessions.</summary>
        private const string BestFloorsKey = "Backrooms.BestFloors";

        /// <summary>Where the best relic haul is remembered between sessions.</summary>
        private const string BestRelicsKey = "Backrooms.BestRelics";

        /// <summary>
        /// Records the run that just ended if it beat the stored best. A relic the player never gets
        /// to compare against anything is just a noise and a number.
        /// </summary>
        private void RecordBest()
        {
            BestFloors = Mathf.Max(BestFloors, CurrentFloor);
            BestRelics = Mathf.Max(BestRelics, RelicsCollected);
            PlayerPrefs.SetInt(BestFloorsKey, BestFloors);
            PlayerPrefs.SetInt(BestRelicsKey, BestRelics);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Updates the HUD and the audio from whichever Dweller is hunting and closest.
        /// </summary>
        private void ReportPursuit()
        {
            bool hunted = _director.TryGetNearestHunter(player.Position, out float closest,
                out string hunter);
            float closeness = hunted ? 1f - Mathf.Clamp01(closest / HuntedWarningMetres) : 0f;

            if (hud != null) hud.SetHunted(hunted, closeness, hunter);
            if (audioModule != null) audioModule.SetHunted(hunted, closeness);
        }

        /// <summary>
        /// Distance at which the pursuit warning is at its faintest. Beyond a Dweller's sense range,
        /// so the warning is already up by the time one is visible through the fog.
        /// </summary>
        private const float HuntedWarningMetres = 34f;

        /// <summary>
        /// Acts on the player's double-tap gestures, spending a relic if one fires.
        /// </summary>
        private void UsePowers()
        {
            if (!_powers.TryUsePowers(relics, player, _director, out RelicKind used)) return;

            if (audioModule != null) audioModule.PlayRelic();
            if (hud != null) hud.ShowRelic(RelicsCollected);
            Debug.Log($"[Gameplay] Used {RelicArchetypes.For(used).DisplayName}");
        }

        /// <summary>
        /// Feeds the HUD the compass arrows and the list of what the player is carrying.
        /// </summary>
        private void ReportCarried()
        {
            if (hud == null || relics == null) return;

            hud.SetCompass(_powers.Compasses(relics, player, maze, _director));

            _carriedLines.Clear();
            _carriedColours.Clear();
            foreach (RelicKind kind in relics.Carried)
            {
                RelicArchetype archetype = RelicArchetypes.For(kind);
                int charges = relics.ChargesOf(kind);

                // Unlimited relics are stored as -1; showing "-1 left" would be nonsense.
                _carriedLines.Add(charges < 0
                    ? archetype.DisplayName
                    : $"{archetype.DisplayName}  x{charges}");
                _carriedColours.Add(archetype.Colour);
            }

            hud.SetCarried(_carriedLines, _carriedColours);
        }

        /// <summary>
        /// Collects a relic if the player has reached one, and says so.
        /// </summary>
        private void CollectRelics()
        {
            if (relics == null || !relics.TryCollect(player.Position)) return;
            if (audioModule != null) audioModule.PlayRelic();
            if (hud != null) hud.ShowRelic(RelicsCollected);

            RelicArchetype found = RelicArchetypes.For(relics.LastCollected);
            Debug.Log($"[Gameplay] Found {found.DisplayName}: {found.Effect}");
        }

        /// <summary>
        /// Tells the audio module whether the player is moving, so footsteps keep time with them.
        /// </summary>
        private void ReportMovement()
        {
            if (audioModule == null) return;
            audioModule.SetMovement(player.IsMoving, player.IsSprinting);
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
            if (relics == null) relics = FindAnyObjectByType<RelicFacade>();
            if (audioModule == null) audioModule = FindAnyObjectByType<AudioFacade>();

            BestFloors = PlayerPrefs.GetInt(BestFloorsKey, 0);
            BestRelics = PlayerPrefs.GetInt(BestRelicsKey, 0);
            _director = new DwellerDirector(transform.parent, dweller, cellsPerDweller, maxDwellers);
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
            if (relics != null) relics.ResetRun();
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

            _director.PopulateFloor(maze.CurrentLayout, CurrentFloor, player.transform,
                dwellerBaseSpeed + (CurrentFloor - 1) * dwellerSpeedPerFloor, seed);
            if (relics != null) relics.PlaceForFloor(maze.CurrentLayout, seed + CurrentFloor * 613,
                CurrentFloor - 1);

            if (audioModule != null)
            {
                audioModule.SetFloor(CurrentFloor);
                if (CurrentFloor > 1) audioModule.PlayDescend();
            }

            if (hud != null) hud.ShowFloor(CurrentFloor, theme.Name, RelicsCollected);
            Debug.Log($"[Gameplay] Floor {CurrentFloor}: {theme.Name}");
        }

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

            if (_director.AnyCaughtPlayer() && _powers.TrySpendWard(relics, _director))
            {
                // The ward took it. Say so where the player is already looking for bad news.
                if (hud != null) hud.ShowRelic(RelicsCollected);
                if (audioModule != null) audioModule.PlayRelic();
                Debug.Log($"[Gameplay] A ward absorbed a Dweller on floor {CurrentFloor}");
            }

            if (_director.AnyCaughtPlayer())
            {
                IsCaught = true;
                player.MovementEnabled = false;
                RecordBest();
                if (audioModule != null) audioModule.Silence();
                if (hud != null)
                {
                    hud.SetHunted(false, 0f, null);
                    hud.ShowCaught(CurrentFloor, ElapsedSeconds, RelicsCollected, BestFloors, BestRelics);
                }

                Debug.Log($"[Gameplay] Caught on floor {CurrentFloor} after {ElapsedSeconds:F1}s");
                return;
            }

            ElapsedSeconds += Time.deltaTime;
            if (hud != null) hud.SetElapsed(ElapsedSeconds);
            ReportPursuit();
            CollectRelics();
            ReportMovement();
            UsePowers();
            ReportCarried();
            if (hud != null) hud.SetPlayerActive(player.HasInput);

            if (DistanceToStairs <= stairsRadius)
            {
                HasEscaped = true;
                DescendToNextFloor();
            }
        }
    }
}
