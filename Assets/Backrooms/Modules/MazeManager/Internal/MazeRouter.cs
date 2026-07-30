using Backrooms.MazeManager.Internal.Generation;

namespace Backrooms.MazeManager.Internal
{
    /// <summary>
    /// Internal coordinator for the MazeManager module. Pure single-line wiring: it forwards
    /// generation requests to the <see cref="MazeGenerator"/> submodule and holds nothing else.
    /// </summary>
    internal sealed class MazeRouter
    {
        private readonly MazeGenerator _generator = new MazeGenerator();

        /// <summary>
        /// Generates a maze layout for the given settings by delegating to the generator submodule.
        /// </summary>
        /// <param name="settings">Grid size and seed.</param>
        /// <returns>The generated layout.</returns>
        public MazeLayout Generate(MazeSettings settings) => _generator.Generate(settings);
    }
}
