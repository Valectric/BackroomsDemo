using System.Collections.Generic;
using Backrooms.MazeManager;
using Backrooms.RelicManager.Internal;
using UnityEngine;

namespace Backrooms.RelicManager
{
    /// <summary>
    /// Test seam for the RelicManager module. Its constructor takes the internal router, so only the
    /// production <see cref="RelicFacade"/> can create one. Not intended for production use — only
    /// for automated testing. Lets tests assert where relics land, and collect them without a player.
    /// </summary>
    public sealed class RelicManagerTestFacade
    {
        private readonly RelicRouter _router;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        internal RelicManagerTestFacade(RelicRouter router)
        {
            _router = router;
        }

        /// <summary>How many relics the player has collected this run.</summary>
        public int Collected => _router.Collected;

        /// <summary>How many relics are still standing on the current floor.</summary>
        public int Remaining => _router.Remaining;

        /// <summary>
        /// Places relics on a floor with an explicit count, bypassing the inspector setting.
        /// </summary>
        /// <param name="layout">The floor to place on.</param>
        /// <param name="count">How many relics to place.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="parent">Transform to parent the relics under.</param>
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> Place(MazeLayout layout, int count, int seed, Transform parent)
            => _router.Place(layout, count, seed, parent);

        /// <summary>
        /// Whether a cell still holds an uncollected relic.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns><c>true</c> if a relic stands there.</returns>
        public bool HasRelicAt(Vector2Int cell) => _router.HasRelicAt(cell);

        /// <summary>
        /// Collects a relic if the given position has reached one.
        /// </summary>
        /// <param name="position">World position to collect from.</param>
        /// <param name="radius">How close counts as collecting, in metres.</param>
        /// <returns><c>true</c> if a relic was collected.</returns>
        public bool TryCollect(Vector3 position, float radius) => _router.TryCollect(position, radius);
    }
}
