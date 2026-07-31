namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// Internal coordinator for the UIManager module. Holds the HUD's display state and forwards
    /// drawing to the renderer submodule; pure wiring, no presentation logic of its own.
    /// </summary>
    internal sealed class UIRouter
    {
        private readonly HudRenderer _renderer = new HudRenderer();

        /// <summary>Seconds shown on the run timer.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Whether the end-of-run banner is showing.</summary>
        public bool EscapedShown { get; private set; }

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
        }

        /// <summary>
        /// Draws the HUD for this frame.
        /// </summary>
        public void Draw()
        {
            if (EscapedShown) _renderer.DrawEscaped(ElapsedSeconds);
            else _renderer.DrawTimer(ElapsedSeconds);
        }

        /// <summary>
        /// Formats a duration the same way the HUD displays it.
        /// </summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>The formatted duration.</returns>
        public string FormatTime(float seconds) => HudRenderer.FormatTime(seconds);
    }
}
