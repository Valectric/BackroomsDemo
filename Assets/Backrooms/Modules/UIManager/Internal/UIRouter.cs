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

        /// <summary>How long the floor-arrival banner stays on screen, in seconds.</summary>
        private const float BannerSeconds = 2.5f;

        /// <summary>Seconds shown on the run timer.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Whether the end-of-run banner is showing.</summary>
        public bool EscapedShown { get; private set; }

        /// <summary>Whether the caught-by-a-Dweller banner is showing.</summary>
        public bool CaughtShown { get; private set; }

        /// <summary>
        /// Shows the banner for being caught by a Dweller and freezes the final time.
        /// </summary>
        /// <param name="floor">Floor the run ended on.</param>
        /// <param name="finalSeconds">How long the player lasted.</param>
        public void ShowCaught(int floor, float finalSeconds)
        {
            Floor = floor;
            ElapsedSeconds = finalSeconds;
            CaughtShown = true;
            BannerRemaining = 0f;
        }

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
        public void ShowFloor(int floor, string name)
        {
            Floor = floor;
            FloorName = name;
            BannerRemaining = BannerSeconds;
        }

        /// <summary>
        /// Counts the arrival banner down.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last update.</param>
        public void TickBanner(float deltaTime)
        {
            if (BannerRemaining > 0f) BannerRemaining = Mathf.Max(0f, BannerRemaining - deltaTime);
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
            Floor = 1;
            FloorName = string.Empty;
            BannerRemaining = 0f;
        }

        /// <summary>
        /// Draws the HUD for this frame.
        /// </summary>
        public void Draw()
        {
            if (CaughtShown)
            {
                _renderer.DrawCaught(Floor, ElapsedSeconds);
                return;
            }

            if (EscapedShown)
            {
                _renderer.DrawEscaped(ElapsedSeconds);
                return;
            }

            _renderer.DrawStatus(ElapsedSeconds, Floor);
            if (BannerShown) _renderer.DrawFloorBanner(Floor, FloorName);
        }

        /// <summary>
        /// Formats a duration the same way the HUD displays it.
        /// </summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>The formatted duration.</returns>
        public string FormatTime(float seconds) => HudRenderer.FormatTime(seconds);
    }
}
