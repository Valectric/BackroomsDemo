using Backrooms.MazeManager.Internal;
using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// This is a Module. The single public door into MazeManager: generates the deterministic
    /// Level-0 maze layout that the rest of the game (geometry, player spawn, exit) is built from.
    /// Place one on a GameObject in the scene; it self-bootstraps its internal router. Concrete by
    /// design — there is no interface (zero-interface rule).
    /// </summary>
    public sealed class MazeFacade : MonoBehaviour
    {
        private MazeRouter _router;
        private MazeManagerTestFacade _testFacade;

        /// <summary>The most recently generated layout, or <c>null</c> if none yet.</summary>
        public MazeLayout CurrentLayout { get; private set; }

        /// <summary>
        /// Initialises the module's internal router. Runs before any other component's Start.
        /// </summary>
        private void Awake()
        {
            EnsureRouter();
        }

        /// <summary>
        /// Generates a maze for the given settings, stores it as <see cref="CurrentLayout"/>, and
        /// returns it.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout Generate(MazeSettings settings)
        {
            EnsureRouter();
            CurrentLayout = _router.Generate(settings);
            return CurrentLayout;
        }

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="MazeManagerTestFacade"/>.</returns>
        public MazeManagerTestFacade GetTestFacade()
        {
            EnsureRouter();
            return _testFacade ??= new MazeManagerTestFacade(_router);
        }

        /// <summary>
        /// Creates the router once if it does not yet exist, so generation works whether called from
        /// Awake or directly by a test immediately after AddComponent.
        /// </summary>
        private void EnsureRouter()
        {
            _router ??= new MazeRouter();
        }
    }
}
