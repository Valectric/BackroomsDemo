using Backrooms.UIManager.Internal;

namespace Backrooms.UIManager
{
    /// <summary>
    /// Test seam for the UIManager module. Its constructor takes the internal router, so only the
    /// production <see cref="HudFacade"/> can create one. Not intended for production use — only for
    /// automated testing. Exposes the HUD's display state and its time formatting so tests can
    /// assert what the player would see without rendering a frame.
    /// </summary>
    public sealed class UIManagerTestFacade
    {
        private readonly UIRouter _router;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        internal UIManagerTestFacade(UIRouter router)
        {
            _router = router;
        }

        /// <summary>Seconds currently shown on the run timer.</summary>
        public float ElapsedSeconds => _router.ElapsedSeconds;

        /// <summary>Whether the end-of-run banner is showing.</summary>
        public bool EscapedShown => _router.EscapedShown;

        /// <summary>
        /// Formats a duration exactly as the HUD renders it.
        /// </summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>The formatted duration, for example <c>01:07</c>.</returns>
        public string FormatTime(float seconds) => _router.FormatTime(seconds);
    }
}
