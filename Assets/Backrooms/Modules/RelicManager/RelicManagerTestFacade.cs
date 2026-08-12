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
/// <summary>
        /// Places a single relic of a named kind, for tests about what a relic does rather than
        /// which one a floor offers.
        /// </summary>
        /// <param name="layout">The floor to place on.</param>
        /// <param name="kind">The kind to place.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="parent">Transform to parent the relic under.</param>
        /// <returns>The cell that received the relic.</returns>
        public List<Vector2Int> PlaceKind(MazeLayout layout, RelicKind kind, int seed,
            Transform parent)
            => _router.PlaceOne(layout, kind, seed, parent);

        /// <summary>
        /// Places relics on a floor with an explicit count, bypassing the inspector setting.
        /// </summary>
        /// <param name="layout">The floor to place on.</param>
        /// <param name="count">How many relics to place.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="parent">Transform to parent the relics under.</param>
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> Place(MazeLayout layout, int count, int seed, Transform parent)
            => _router.Place(layout, count, seed, 1, parent);

        /// <summary>
        /// Places relics choosing which kind the floor offers first.
        /// </summary>
        /// <param name="layout">The floor to place on.</param>
        /// <param name="count">How many relics to place.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="floor">One-based floor number, which decides what the floor tends to offer.</param>
        /// <param name="parent">Transform to parent the relics under.</param>
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> Place(MazeLayout layout, int count, int seed, int floor,
            Transform parent)
            => _router.Place(layout, count, seed, floor, parent);

        /// <summary>The kind picked up by the most recent successful collect.</summary>
        public RelicKind LastCollected => _router.LastCollected;

        /// <summary>
        /// Whether the last relic collected added nothing, because one was already carried and that
        /// kind does not stack.
        /// </summary>
        public bool LastWasSpare => _router.LastWasSpare;

        /// <summary>
        /// Whether the player is carrying a kind of relic with uses left.
        /// </summary>
        /// <param name="kind">Kind to test.</param>
        /// <returns><c>true</c> if held and not spent.</returns>
        public bool Holds(RelicKind kind) => _router.Holds(kind);

        /// <summary>
        /// How many uses of a kind remain.
        /// </summary>
        /// <param name="kind">Kind to query.</param>
        /// <returns>Uses left, 0 if not held, -1 if unlimited.</returns>
        public int ChargesOf(RelicKind kind) => _router.ChargesOf(kind);

        /// <summary>
        /// Spends one use of a relic.
        /// </summary>
        /// <param name="kind">Kind to spend.</param>
        /// <returns><c>true</c> if a use was spent.</returns>
        public bool Spend(RelicKind kind) => _router.Spend(kind);

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
