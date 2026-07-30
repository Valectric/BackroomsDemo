using Backrooms.MazeManager.Internal;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// Test seam for the MazeManager module. Its constructor takes an internal router, so only the
    /// production <see cref="MazeFacade"/> can create one (via <see cref="MazeFacade.GetTestFacade"/>).
    /// Not intended for production use — only for automated testing. Lets tests generate layouts by
    /// seed and inspect them without any scene geometry.
    /// </summary>
    public sealed class MazeManagerTestFacade
    {
        private readonly MazeRouter _router;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        internal MazeManagerTestFacade(MazeRouter router)
        {
            _router = router;
        }

        /// <summary>
        /// Generates a maze layout for the given settings — the same path production uses, exposed
        /// directly so tests can assert on the layout without a scene.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout Generate(MazeSettings settings) => _router.Generate(settings);
    }
}
