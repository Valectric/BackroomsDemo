using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// Internal coordinator for the UIManager module. Holds the HUD's display state and forwards
    /// drawing to the renderer submodule; pure wiring, no presentation logic of its own.
    /// </summary>
    internal sealed class UIRouter
    {
        private readonly HudRenderer _renderer = new HudRenderer();
        private readonly HudOverlays _overlays = new HudOverlays();

        /// <summary>Compass arrows to draw this frame.</summary>
        private IReadOnlyList<CompassMark> _compass = new CompassMark[0];

        /// <summary>Lines describing what the player is carrying.</summary>
        private IReadOnlyList<string> _carriedLines = new string[0];

        /// <summary>Colour for each carried line.</summary>
        private IReadOnlyList<Color> _carriedColours = new Color[0];

        /// <summary>How long the player has done nothing at all, in seconds.</summary>
        private float _idleSeconds;

        /// <summary>
        /// How long a player has to be doing nothing before the controls are explained to them.
        /// </summary>
        /// <remarks>
        /// Idle-triggered rather than shown on arrival. Someone who is already walking around knows
        /// how to walk around, and telling them anyway is clutter over the one part of the screen a
        /// horror game needs left alone. Someone who has stopped dead is either lost or has not
        /// worked out that the screen halves do different things — and that is exactly who the hint
        /// is for.
        /// </remarks>
        private const float IdleBeforeHintSeconds = 10f;

        /// <summary>How long the hints take to fade up once they are due, in seconds.</summary>
        private const float HintFadeSeconds = 0.8f;

        /// <summary>
        /// Reports whether the player is doing anything, which decides if the control hints appear.
        /// </summary>
        /// <param name="active">Whether the player gave any input this frame.</param>
        /// <param name="deltaTime">Seconds since the last update.</param>
        public void TickActivity(bool active, float deltaTime)
        {
            _idleSeconds = active ? 0f : _idleSeconds + deltaTime;
        }

        /// <summary>How visible the control hints should be right now, 0 to 1.</summary>
        public float HintStrength => Mathf.Clamp01(
            (_idleSeconds - IdleBeforeHintSeconds) / HintFadeSeconds);

        /// <summary>
        /// Sets the compass arrows for this frame.
        /// </summary>
        /// <param name="marks">One mark per compass relic with something to point at.</param>
        public void SetCompass(IReadOnlyList<CompassMark> marks)
            => _compass = marks ?? new CompassMark[0];

        /// <summary>The floor map to draw in the corner, or <c>null</c> for none.</summary>
        public Texture2D Map { get; private set; }

        /// <summary>Where the player is on the map, as a fraction of the floor in each axis.</summary>
        public Vector2 MapPlayer { get; private set; }

        /// <summary>How much of the floor the map window shows, as a fraction of each axis.</summary>
        public float MapWindow { get; private set; } = 1f;

        /// <summary>Whether the map is being shown.</summary>
        public bool MapShown { get; private set; }

        /// <summary>Uncollected relics, as fractions of the floor in each axis.</summary>
        public IReadOnlyList<Vector2> MapRelics { get; private set; } = new Vector2[0];

        /// <summary>
        /// Sets the corner map. Passing a null texture hides it.
        /// </summary>
        /// <param name="map">Baked floor map, or null for none.</param>
        /// <param name="player">Player position as a fraction of the floor in each axis.</param>
        /// <param name="window">Fraction of the floor to show around the player.</param>
        /// <param name="relics">Uncollected relics as fractions of the floor, or null for none.</param>
        public void SetMap(Texture2D map, Vector2 player, float window,
            IReadOnlyList<Vector2> relics = null)
        {
            Map = map;
            MapPlayer = player;
            MapWindow = Mathf.Clamp(window, 0.05f, 1f);
            MapShown = map != null;
            MapRelics = relics ?? new Vector2[0];
        }

        /// <summary>
        /// Sets the list of what the player is carrying.
        /// </summary>
        /// <param name="lines">One line per carried relic.</param>
        /// <param name="colours">Colour for each line, same order.</param>
        public void SetCarried(IReadOnlyList<string> lines, IReadOnlyList<Color> colours)
        {
            _carriedLines = lines ?? new string[0];
            _carriedColours = colours ?? new Color[0];
        }

        /// <summary>Whether the screen is taller than it is wide.</summary>
        public static bool IsPortrait => Screen.height > Screen.width;

        /// <summary>How long the floor-arrival banner stays on screen, in seconds.</summary>
        private const float BannerSeconds = 2.5f;

        /// <summary>Seconds shown on the run timer.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Whether the end-of-run banner is showing.</summary>
        public bool EscapedShown { get; private set; }

        /// <summary>Whether the caught-by-a-Dweller banner is showing.</summary>
        public bool CaughtShown { get; private set; }

        /// <summary>Whether the game is waiting on the title screen for the run to be started.</summary>
        public bool TitleShown { get; private set; } = true;

        /// <summary>
        /// Shows the title screen and waits there.
        /// </summary>
        /// <param name="bestFloors">Deepest floor reached in any run.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        public void ShowTitle(int bestFloors, int bestRelics)
        {
            BestFloors = bestFloors;
            BestRelics = bestRelics;
            TitleShown = true;
            CaughtShown = false;
        }

        /// <summary>
        /// Leaves the title screen for a run in progress.
        /// </summary>
        public void HideTitle() => TitleShown = false;

        /// <summary>Whether a Dweller is currently hunting the player.</summary>
        public bool HuntedShown { get; private set; }

        /// <summary>How close the nearest hunting Dweller is, 0 at the edge of its range and 1 on top of you.</summary>
        public float HuntedCloseness { get; private set; }

        /// <summary>
        /// Sets the pursuit warning. The player cannot see behind them and fog hides most of what is
        /// ahead, so being hunted has to be said on the HUD as well as shown in the world.
        /// </summary>
        /// <param name="hunted">Whether any Dweller is chasing.</param>
        /// <param name="closeness">How close the nearest one is, clamped to 0..1.</param>
        /// <param name="hunterName">What the nearest hunter is called, or <c>null</c> for none.</param>
        public void SetHunted(bool hunted, float closeness, string hunterName = null)
        {
            HuntedShown = hunted;
            HuntedCloseness = Mathf.Clamp01(closeness);
            HunterName = string.IsNullOrEmpty(hunterName) ? "DWELLER" : hunterName;
        }

        /// <summary>What the nearest hunting Dweller is called. Named so the player learns the
        /// roster: a WATCHER is a different problem from a SKITTER, and being told which is which is
        /// how they find that out.</summary>
        public string HunterName { get; private set; } = "DWELLER";

        /// <summary>
        /// Shows the banner for being caught by a Dweller and freezes the final time.
        /// </summary>
        /// <param name="floor">Floor the run ended on.</param>
        /// <param name="finalSeconds">How long the player lasted.</param>
        /// <param name="relics">How many relics the player was carrying.</param>
        /// <param name="bestFloors">Deepest floor reached in any run.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        public void ShowCaught(int floor, float finalSeconds, int relics, int bestFloors, int bestRelics)
        {
            Floor = floor;
            ElapsedSeconds = finalSeconds;
            Relics = relics;
            BestFloors = bestFloors;
            BestRelics = bestRelics;
            CaughtShown = true;
            BannerRemaining = 0f;
        }

        /// <summary>Relics the player is carrying.</summary>
        public int Relics { get; private set; }

        /// <summary>Deepest floor reached in any run.</summary>
        public int BestFloors { get; private set; }

        /// <summary>Most relics carried in any run.</summary>
        public int BestRelics { get; private set; }

        /// <summary>Seconds of relic-pickup flash left to show.</summary>
        public float RelicFlashRemaining { get; private set; }

        /// <summary>
        /// Announces that a relic was just picked up.
        /// </summary>
        /// <param name="total">How many the player now carries.</param>
        public void ShowRelic(int total)
        {
            Relics = total;
            RelicFlashRemaining = RelicFlashSeconds;
        }

        /// <summary>How long the relic-pickup flash stays on screen, in seconds.</summary>
        private const float RelicFlashSeconds = 1.8f;

        /// <summary>Floor number shown on the HUD.</summary>
        public int Floor { get; private set; } = 1;

        /// <summary>Name of the current floor.</summary>
        public string FloorName { get; private set; } = string.Empty;

        /// <summary>Seconds of arrival banner left to display.</summary>
        public float BannerRemaining { get; private set; }

        /// <summary>Whether the floor-arrival banner is currently visible.</summary>
        public bool BannerShown => BannerRemaining > 0f;

        /// <summary>
        /// Announces arrival on a floor and starts the banner timer.
        /// </summary>
        /// <param name="floor">Floor number the player just reached.</param>
        /// <param name="name">Display name of that floor.</param>
        /// <param name="relics">How many relics the player is carrying.</param>
        public void ShowFloor(int floor, string name, int relics)
        {
            Floor = floor;
            FloorName = name;
            Relics = relics;
            BannerRemaining = BannerSeconds;

            // Arriving on a floor is not idling, so do not immediately start counting towards a hint.
            _idleSeconds = 0f;
        }

        /// <summary>
        /// Counts the arrival banner down.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last update.</param>
        public void TickBanner(float deltaTime)
        {
            if (BannerRemaining > 0f) BannerRemaining = Mathf.Max(0f, BannerRemaining - deltaTime);
            if (RelicFlashRemaining > 0f)
            {
                RelicFlashRemaining = Mathf.Max(0f, RelicFlashRemaining - deltaTime);
            }
        }

        /// <summary>
        /// Updates the time shown on the timer.
        /// </summary>
        /// <param name="seconds">Seconds since the run started.</param>
        public void SetElapsed(float seconds) => ElapsedSeconds = seconds;

        /// <summary>
        /// Shows the end-of-run banner with a final time.
        /// </summary>
        /// <param name="finalSeconds">Final run time in seconds.</param>
        public void ShowEscaped(float finalSeconds)
        {
            ElapsedSeconds = finalSeconds;
            EscapedShown = true;
        }

        /// <summary>
        /// Clears the banner and resets the timer for a new run.
        /// </summary>
        public void Reset()
        {
            ElapsedSeconds = 0f;
            EscapedShown = false;
            CaughtShown = false;
            _idleSeconds = 0f;
            Relics = 0;
            RelicFlashRemaining = 0f;
            HuntedShown = false;
            HuntedCloseness = 0f;
            HunterName = "DWELLER";
            Floor = 1;
            FloorName = string.Empty;
            BannerRemaining = 0f;
        }

        /// <summary>
        /// Draws the HUD for this frame.
        /// </summary>
        public void Draw()
        {
            // Before anything else: a portrait phone cannot play this, so say so and draw nothing
            // underneath that would suggest otherwise.
            if (IsPortrait)
            {
                _overlays.DrawRotatePrompt();
                return;
            }

            if (TitleShown)
            {
                _renderer.DrawTitle(BestFloors, BestRelics);
                return;
            }

            if (CaughtShown)
            {
                _renderer.DrawCaught(Floor, ElapsedSeconds, Relics, BestFloors, BestRelics);
                return;
            }

            if (EscapedShown)
            {
                _renderer.DrawEscaped(ElapsedSeconds);
                return;
            }

            // The carried list is drawn first because everything else along the top has to know
            // where it ends.
            float carriedBottom = _overlays.DrawCarried(_carriedLines, _carriedColours);

            if (MapShown) _renderer.DrawMap(Map, MapPlayer, MapWindow, MapRelics, carriedBottom);

            // The pursuit warning is drawn under the arrival banner but over the status line, and
            // stays visible during the banner — arriving on a floor next to a Dweller is exactly when
            // the player most needs telling.
            if (HuntedShown) _renderer.DrawHunted(HunterName, HuntedCloseness, ElapsedSeconds);

            _overlays.DrawPadHints(HintStrength);

            _overlays.DrawCompass(_compass, carriedBottom);

            _renderer.DrawStatus(ElapsedSeconds, Floor, Relics);
            if (BannerShown) _renderer.DrawFloorBanner(Floor, FloorName);
            if (RelicFlashRemaining > 0f) _renderer.DrawRelicFlash(Relics);
        }

        /// <summary>
        /// Formats a duration the same way the HUD displays it.
        /// </summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>The formatted duration.</returns>
        public string FormatTime(float seconds) => HudRenderer.FormatTime(seconds);
    }
}
