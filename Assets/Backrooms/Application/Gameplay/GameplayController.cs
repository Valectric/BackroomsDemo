using System.Collections.Generic;
using Backrooms.EntityManager;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
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

        /// <summary>Keeps the staircase the player arrived by from firing under their feet.</summary>
        private readonly ArrivalGuard _arrival = new ArrivalGuard();

        /// <summary>Reused each frame so the HUD is not handed fresh lists sixty times a second.</summary>
        private readonly List<string> _carriedLines = new List<string>();

        /// <summary>Colour for each carried line.</summary>
        private readonly List<Color> _carriedColours = new List<Color>();

        [Header("Run")]
        [Tooltip("Seed for the maze. Left at 0 a fresh one is drawn for every run.")]
        [SerializeField] private int seed;

        [Tooltip("Draw a new seed for each run, so no two runs are the same building.")]
        [SerializeField] private bool randomiseSeed = true;

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

        /// <summary>
        /// Whether the game is sitting on the title screen waiting to be started.
        /// </summary>
        /// <remarks>
        /// The level is built and standing behind the title, so the first thing a player sees is the
        /// game rather than a loading colour — but the clock is stopped, the player cannot move, and
        /// nothing hunts. It also gives the browser the user gesture it insists on before it will
        /// play a sound, which is why the game used to be silent until the first death.
        /// </remarks>
        public bool IsAwaitingStart { get; private set; }

        /// <summary>How many Dwellers are roaming the current floor.</summary>
        public int DwellerCount => _director == null ? 0 : _director.Count;

        /// <summary>
        /// Distance to the nearest Dweller on the floor, in metres, or infinity if there are none.
        /// Exposed so a test can assert the player never arrives face to face with one.
        /// </summary>
        public float NearestDwellerMetres
        {
            get
            {
                if (_director == null || player == null) return float.PositiveInfinity;
                if (!_director.TryGetNearestPosition(player.Position, out Vector3 at))
                {
                    return float.PositiveInfinity;
                }

                return Vector3.Distance(
                    new Vector3(player.Position.x, 0f, player.Position.z),
                    new Vector3(at.x, 0f, at.z));
            }
        }

        /// <summary>Whether any Dweller is currently hunting the player.</summary>
        public bool IsHunted => _director != null && _director.AnyHunting;

        /// <summary>How many relics the player has collected this run.</summary>
        public int RelicsCollected => relics == null ? 0 : relics.Collected;

        /// <summary>Deepest floor reached in any run on this device.</summary>
        public int BestFloors => _record?.BestFloors ?? 0;

        /// <summary>Most relics carried in any run on this device.</summary>
        public int BestRelics => _record?.BestRelics ?? 0;

        /// <summary>
        /// Remembers the best run this device has managed. Created in Awake rather than as a field
        /// initializer: those run inside the MonoBehaviour constructor, and Unity forbids reading
        /// PlayerPrefs there — it threw on every start, and the stored best never loaded.
        /// </summary>
        private RunRecord _record;

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
            if (!_powers.TryUsePowers(relics, player, _director, maze, out RelicKind used))
            {
                // A miss still gets a sound. Silence was indistinguishable from a broken key.
                if (_powers.BanisherMissed && audioModule != null) audioModule.PlayBanishMiss();
                return;
            }

            // Each power gets its own sound. Every one of them used to play the pickup chime, so
            // using the shard sounded exactly like finding a relic that was not there. Chosen here
            // rather than inside AudioManager, which has no business knowing what a relic is.
            if (audioModule != null)
            {
                switch (used)
                {
                    case RelicKind.BlinkShard:
                        audioModule.PlayBlink();
                        break;
                    case RelicKind.Banisher:
                        audioModule.PlayBanish();
                        break;
                    default:
                        audioModule.PlayRelic();
                        break;
                }
            }
            // Naming what was spent, not the pickup line: using a relic announced "RELIC
            // RECOVERED" and the carried total, so every blink read as having just found something.
            if (hud != null)
            {
                RelicArchetype spent = RelicArchetypes.For(used);
                hud.ShowFlash(spent.DisplayName, spent.Colour);
            }

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
                string line = charges < 0
                    ? archetype.DisplayName
                    : $"{archetype.DisplayName}  x{charges}";

                // Show the control this device actually has. A phone has no F key and a desktop
                // has no screen halves worth double-tapping now that the pointer locks.
                string how = Application.isMobilePlatform || !string.IsNullOrEmpty(archetype.KeyHint)
                    ? (Application.isMobilePlatform ? archetype.Gesture : archetype.KeyHint)
                    : archetype.Gesture;

                if (!string.IsNullOrEmpty(how)) line += "  " + how;
                _carriedLines.Add(line);
                _carriedColours.Add(archetype.Colour);
            }

            hud.SetCarried(_carriedLines, _carriedColours);
            ReportMap();
        }

        /// <summary>
        /// Feeds the HUD the corner map, when the player is carrying the relic that draws one.
        /// </summary>
        private void ReportMap()
        {
            if (!relics.Holds(RelicKind.SurveyorsLens) || maze.CurrentLayout == null)
            {
                hud.SetMap(null, Vector2.zero, 1f);
                return;
            }

            // Refreshed only when one is taken, rather than rebuilt every frame for a corner of the
            // screen that changes ten times a floor.
            if (_mapRelicCount != relics.RemainingCells.Count) RefreshMapRelics();

            MazeLayout layout = maze.CurrentLayout;
            Vector2Int cell = layout.WorldToCell(player.Position);
            var where = new Vector2(
                (cell.x + 0.5f) / layout.Width,
                (cell.y + 0.5f) / layout.Height);

            // A window of roughly 20 cells across, whatever size the floor happens to be.
            float window = Mathf.Clamp01(MapWindowCells / (float)Mathf.Max(layout.Width, 1));
            hud.SetMap(_map, where, window, _mapRelics);
        }

        /// <summary>
        /// Rebuilds the list of uncollected relic positions the map draws.
        /// </summary>
        private void RefreshMapRelics()
        {
            MazeLayout layout = maze.CurrentLayout;
            _mapRelics.Clear();

            foreach (Vector2Int cell in relics.RemainingCells)
            {
                _mapRelics.Add(new Vector2(
                    (cell.x + 0.5f) / layout.Width,
                    (cell.y + 0.5f) / layout.Height));
            }

            _mapRelicCount = _mapRelics.Count;
        }

        /// <summary>Uncollected relic positions the map draws, as fractions of the floor.</summary>
        private readonly List<Vector2> _mapRelics = new List<Vector2>();

        /// <summary>How many relics the map list was last built for.</summary>
        private int _mapRelicCount = -1;

        /// <summary>How many cells across the corner map shows.</summary>
        private const float MapWindowCells = 20f;

        /// <summary>The current floor's baked map, rebuilt whenever the floor is.</summary>
        private Texture2D _map;

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

            _record = new RunRecord();
            _director = new DwellerDirector(transform.parent, dweller, cellsPerDweller, maxDwellers);
        }

        /// <summary>
        /// Starts a run: builds the maze, then drops the player into its spawn cell.
        /// </summary>
        private void Start()
        {
            StartRun(NextSeed());
            WaitOnTitle();
        }

        /// <summary>
        /// Freezes the built run behind the title screen until the player starts it.
        /// </summary>
        private void WaitOnTitle()
        {
            IsAwaitingStart = true;
            ElapsedSeconds = 0f;
            if (player != null) player.MovementEnabled = false;
            if (hud != null) hud.ShowTitle(BestFloors, BestRelics);
        }

        /// <summary>
        /// Begins play from the title screen.
        /// </summary>
        public void BeginRun()
        {
            IsAwaitingStart = false;
            if (player != null) player.MovementEnabled = true;
            if (hud != null)
            {
                hud.HideTitle();
                hud.ShowFloor(CurrentFloor, maze.CurrentTheme.Name, RelicsCollected);
            }

            // The tap that got here is the gesture the browser was waiting for.
            if (audioModule != null)
            {
                audioModule.NoteInteraction(true);
                audioModule.SetFloor(CurrentFloor);
            }
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

            FloorTheme theme = BuildFloor();
            player.SpawnAt(maze.GetSpawnPosition());
            _arrival.Arrived(maze.CurrentLayout.Spawn);

            if (audioModule != null && CurrentFloor > 1) audioModule.PlayDescend();
            Announce(theme);
        }

        /// <summary>
        /// Climbs back to the floor above, arriving out of one of its ways down.
        /// </summary>
        /// <remarks>
        /// The mirror of descending, and the reason the stairs go both ways: you came down a
        /// staircase, so climbing one puts you back where that staircase came from. Every floor is
        /// rebuilt from a seed derived from its number, so the floor above is the same building the
        /// player left rather than a new one.
        /// </remarks>
        public void AscendToPreviousFloor()
        {
            if (CurrentFloor <= 1) return;

            CurrentFloor--;
            HasEscaped = false;

            FloorTheme theme = BuildFloor();

            // Emerging from a way down: the staircase the player originally descended.
            MazeLayout layout = maze.CurrentLayout;
            Vector2Int arrival = layout.Stairs.Count > 0 ? layout.Stairs[0] : layout.Spawn;
            player.SpawnAt(layout.CellCenterToWorld(arrival));
            _arrival.Arrived(arrival);

            Announce(theme);
        }

        /// <summary>
        /// Generates and populates the current floor, without deciding where the player stands.
        /// </summary>
        /// <returns>The palette the floor was built with.</returns>
        private FloorTheme BuildFloor()
        {
            FloorTheme theme = FloorThemes.ForFloor(CurrentFloor);
            // Floor 1 is where the player noclipped in, so it has nothing above it to climb to and
            // shows no ways up. Every floor below emerges from one.
            maze.GenerateAndBuild(seed + CurrentFloor * 977, theme, hasFloorAbove: CurrentFloor > 1);
            FloorAtmosphere.Apply(theme);

            _director.PopulateFloor(maze.CurrentLayout, CurrentFloor, player.transform,
                dwellerBaseSpeed + (CurrentFloor - 1) * dwellerSpeedPerFloor, seed);
            if (relics != null)
            {
                relics.PlaceForFloor(maze.CurrentLayout, seed + CurrentFloor * 613, CurrentFloor);
            }

            if (audioModule != null)
            {
                audioModule.SetFloor(CurrentFloor);

                // The theme already names the kind of place this is, so the ambience follows from it
                // rather than from a second table that could drift out of step.
                audioModule.SetAmbience(theme.Props.ToString().ToLowerInvariant(),
                    seed + CurrentFloor * 131);
            }

            // Baked once per floor rather than drawn per frame — see FloorMap.
            if (_map != null) Destroy(_map);
            _map = FloorMap.Build(maze.CurrentLayout);
            _mapRelicCount = -1;

            return theme;
        }

        /// <summary>
        /// Shows the arrival banner and logs the floor.
        /// </summary>
        /// <param name="theme">The palette the floor was built with.</param>
        private void Announce(FloorTheme theme)
        {
            if (hud != null) hud.ShowFloor(CurrentFloor, theme.Name, RelicsCollected);
            Debug.Log($"[Gameplay] Floor {CurrentFloor}: {theme.Name}");
        }

        /// <summary>
        /// The seed the next run should use: a fresh one unless the scene pins it.
        /// </summary>
        /// <remarks>
        /// Generation stays deterministic — the same seed always builds the same building — but a run
        /// that always starts from seed 1 is the same building every time, which is the opposite of
        /// what a 999-floor dungeon is for. The drawn seed is logged so any run can be replayed by
        /// pinning it in the inspector.
        /// </remarks>
        /// <returns>The seed to build with.</returns>
        private int NextSeed()
        {
            if (!randomiseSeed && seed != 0) return seed;

            int drawn = UnityEngine.Random.Range(1, int.MaxValue);
            Debug.Log($"[Gameplay] Run seed {drawn}");
            return drawn;
        }

        /// <summary>
        /// Tracks run time and detects the player reaching a stairwell down.
        /// </summary>
        private void Update()
        {
            if (maze == null || player == null) return;

            // Before anything else: the browser will not let a sound out until the player has
            // touched the screen once, so every frame gets a chance to notice that gesture —
            // including the frames where the run is over or has not begun.
            if (audioModule != null)
            {
                audioModule.NoteInteraction(player.HasInput || player.ConfirmPressed);
            }

            if (IsAwaitingStart)
            {
                if (player.ConfirmPressed) BeginRun();
                return;
            }

            if (IsCaught)
            {
                // A death screen you cannot leave is not a losing condition, it is a dead end.
                if (player.ConfirmPressed)
                {
                    StartRun(NextSeed());
                    BeginRun();
                }

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
                _record?.Submit(CurrentFloor, RelicsCollected);
                if (audioModule != null) audioModule.Silence();
                if (hud != null)
                {
                    hud.SetHunted(false, 0f, null);
                    hud.ShowCaught(CurrentFloor, ElapsedSeconds, RelicsCollected, BestFloors,
                        BestRelics, seed);
                }

                Debug.Log($"[Gameplay] Caught on floor {CurrentFloor} after {ElapsedSeconds:F1}s");
                return;
            }

            ElapsedSeconds += Time.deltaTime;
            // A sensitivity change is invisible until you move the mouse, so say the number.
            if (player.SensitivityChanged && hud != null)
            {
                hud.ShowFlash($"LOOK  {player.LookSensitivity:0.00}", new Color(0.75f, 0.85f, 1f));
            }

            if (audioModule != null) audioModule.TickAmbience(Time.deltaTime);
            if (hud != null) hud.SetElapsed(ElapsedSeconds);
            ReportPursuit();
            CollectRelics();
            ReportMovement();
            UsePowers();
            ReportCarried();
            if (hud != null) hud.SetPlayerActive(player.HasInput);

            _arrival.Update(maze.CurrentLayout.WorldToCell(player.Position));
            if (_arrival.StillOnArrivalStairs) return;

            if (DistanceToStairs <= stairsRadius)
            {
                HasEscaped = true;
                DescendToNextFloor();
                return;
            }

            // Climb only where the floor actually carries a way up. Asking by floor number would
            // work too, but NearestStairsUp answers "the cell you are standing in" when there are
            // none — which reads as zero distance and would ascend on the spot. Guarding on the data
            // that decides whether a riser was built keeps the two from ever disagreeing.
            if (CurrentFloor <= 1 || maze.CurrentLayout.StairsUp.Count == 0) return;

            Vector3 up = maze.GetNearestStairsUpPosition(player.Position);
            float toUp = Vector3.Distance(
                new Vector3(player.Position.x, 0f, player.Position.z),
                new Vector3(up.x, 0f, up.z));
            if (toUp <= stairsRadius) AscendToPreviousFloor();
        }
    }
}
