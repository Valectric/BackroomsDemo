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
        /// Shows the banner for being caught by a Dweller.
        /// </summary>
        /// <param name="floor">Floor the run ended on.</param>
        /// <param name="finalSeconds">How long the player lasted.</param>
        /// <param name="relics">How many relics they were carrying.</param>
        /// <param name="bestFloors">Deepest floor reached in any run.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        public void ShowCaught(int floor, float finalSeconds, int relics, int bestFloors, int bestRelics)
            => Router.ShowCaught(floor, finalSeconds, relics, bestFloors, bestRelics);

        /// <summary>
        /// Announces that a relic was just picked up.
        /// </summary>
        /// <param name="total">How many the player now carries.</param>
        public void ShowRelic(int total) => Router.ShowRelic(total);

        /// <summary>How many relics the HUD is showing.</summary>
        public int Relics => Router.Relics;

        /// <summary>Whether the caught banner is showing.</summary>
        public bool CaughtShown => Router.CaughtShown;

        /// <summary>
        /// Sets the pursuit warning shown while a Dweller is hunting the player.
        /// </summary>
        /// <param name="hunted">Whether any Dweller is chasing.</param>
        /// <param name="closeness">How close the nearest one is, 0 at the edge of its range to 1 on top of you.</param>
        /// <param name="hunterName">What the nearest hunter is called, or <c>null</c> for a generic warning.</param>
        public void SetHunted(bool hunted, float closeness, string hunterName = null)
            => Router.SetHunted(hunted, closeness, hunterName);

        /// <summary>What the nearest hunting Dweller is called.</summary>
        public string HunterName => Router.HunterName;

        /// <summary>Whether the pursuit warning is showing.</summary>
        public bool HuntedShown => Router.HuntedShown;

        /// <summary>Floor number currently shown on the HUD.</summary>
        public int Floor => Router.Floor;

        /// <summary>Whether the floor-arrival banner is on screen.</summary>
        public bool BannerShown => Router.BannerShown;

        /// <summary>
        /// Announces arrival on a floor, showing the banner for a few seconds.
        /// </summary>
        /// <param name="floor">Floor number the player reached.</param>
        /// <param name="name">Display name of that floor.</param>
        /// <param name="relics">How many relics the player is carrying.</param>
        public void ShowFloor(int floor, string name, int relics)
            => Router.ShowFloor(floor, name, relics);

        /// <summary>
        /// Counts the arrival banner down on the frame clock.
        /// </summary>
        private void Update() => Router.TickBanner(Time.deltaTime);

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
