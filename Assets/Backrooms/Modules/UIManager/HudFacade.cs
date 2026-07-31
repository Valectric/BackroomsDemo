using Backrooms.UIManager.Internal;
using UnityEngine;

namespace Backrooms.UIManager
{
    /// <summary>
    /// This is a Module. The single public door into UIManager: the in-game heads-up display —
    /// a run timer while you are lost, and a banner when you reach the exit. It renders whatever
    /// state it is told to show and never reads gameplay state itself, so the module has no
    /// dependency on the game it is displaying. Concrete by design — there is no interface.
    /// </summary>
    public sealed class HudFacade : MonoBehaviour
    {
        private UIRouter _router;
        private UIManagerTestFacade _testFacade;

        /// <summary>Seconds currently shown on the run timer.</summary>
        public float ElapsedSeconds => Router.ElapsedSeconds;

        /// <summary>Whether the end-of-run banner is currently showing.</summary>
        public bool EscapedShown => Router.EscapedShown;

        /// <summary>
        /// The module's router, created on first use so the HUD works whether it is driven from
        /// Awake, from Start, or straight after AddComponent in a test.
        /// </summary>
        private UIRouter Router => _router ??= new UIRouter();

        /// <summary>
        /// Updates the time shown on the run timer.
        /// </summary>
        /// <param name="seconds">Seconds since the run started.</param>
        public void SetElapsed(float seconds) => Router.SetElapsed(seconds);

        /// <summary>
        /// Shows the end-of-run banner with the final time.
        /// </summary>
        /// <param name="finalSeconds">Final run time in seconds.</param>
        public void ShowEscaped(float finalSeconds) => Router.ShowEscaped(finalSeconds);

        /// <summary>
        /// Clears the banner and resets the timer for a new run.
        /// </summary>
        public void ResetHud() => Router.Reset();

        /// <summary>
        /// Renders the HUD each IMGUI pass.
        /// </summary>
        private void OnGUI() => Router.Draw();

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="UIManagerTestFacade"/>.</returns>
        public UIManagerTestFacade GetTestFacade() => _testFacade ??= new UIManagerTestFacade(Router);
    }
}
