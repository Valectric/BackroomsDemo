using System.Collections.Generic;
using Backrooms.MazeManager;
using Backrooms.RelicManager.Internal;
using UnityEngine;

namespace Backrooms.RelicManager
{
    /// <summary>
    /// This is a Module. The single public door into RelicManager: the relics scattered through the
    /// Backrooms that a survivor might risk something to reach. Place one on a GameObject in the
    /// scene; it self-bootstraps its internal router. Concrete by design — there is no interface.
    /// </summary>
    /// <remarks>
    /// A relic exists to make descending a decision. Without one, every floor asks the same question
    /// — where are the stairs — and the answer is always "go there". A relic sits at the far end of
    /// the floor from every way down, so each floor asks instead: leave now, or go and get it.
    /// </remarks>
    public sealed class RelicFacade : MonoBehaviour
    {
        [Header("Relics")]
        [Tooltip("Relics per floor. Left at 0, a floor carries one per way down.")]
        [SerializeField] private int relicsPerFloor;

        [Tooltip("How close to a relic counts as collecting it, in metres.")]
        [SerializeField] private float collectRadius = 1.6f;

        private readonly RelicRouter _router = new RelicRouter();
        private RelicManagerTestFacade _testFacade;

        /// <summary>How many relics the player has collected this run.</summary>
        public int Collected => _router.Collected;

        /// <summary>How many relics are still standing on the current floor.</summary>
        public int Remaining => _router.Remaining;

        /// <summary>
        /// Places this floor's relics, clearing any left from the previous floor.
        /// </summary>
        /// <param name="layout">The floor to place on.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="floor">One-based floor number, which decides what the floor tends to offer.</param>
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> PlaceForFloor(MazeLayout layout, int seed, int floor = 1)
            => _router.Place(layout, RelicsFor(layout), seed, floor, transform);

        /// <summary>
        /// How many relics a floor should carry: one per way down unless the scene pins a number.
        /// </summary>
        /// <remarks>
        /// Tying the count to the stairwells keeps the two in step by construction rather than as two
        /// magic numbers that drift apart. It also means a single relic per floor no longer rations
        /// the powers so hard that most of a run is spent carrying nothing: the roster is dealt out
        /// in turn and skips anything already held, so a floor with three relics offers three
        /// different ones.
        /// </remarks>
        /// <param name="layout">The floor being placed on.</param>
        /// <returns>How many relics to place.</returns>
        private int RelicsFor(MazeLayout layout)
        {
            if (relicsPerFloor > 0) return relicsPerFloor;

            int waysDown = layout == null || layout.Stairs.Count == 0
                ? DefaultWaysDown
                : layout.Stairs.Count;
            return Mathf.Max(1, Mathf.RoundToInt(waysDown * RelicsPerWayDown));
        }

        /// <summary>Ways down assumed when a floor somehow reports none.</summary>
        private const int DefaultWaysDown = 3;

        /// <summary>
        /// Relics per way down, calibrated so a normal three-exit floor carries about ten. Kept as a
        /// ratio rather than a flat ten so a floor with more exits — a bigger floor — carries more.
        /// </summary>
        private const float RelicsPerWayDown = 10f / 3f;

        /// <summary>
        /// Collects a relic if the player has reached one.
        /// </summary>
        /// <param name="playerPosition">World position of the player.</param>
        /// <returns><c>true</c> if a relic was collected on this call.</returns>
        public bool TryCollect(Vector3 playerPosition)
            => _router.TryCollect(playerPosition, collectRadius);

        /// <summary>
        /// Clears the collected tally, everything carried, and the floor, for a fresh run.
        /// </summary>
        public void ResetRun() => _router.ResetRun();

        /// <summary>Cells on this floor that still hold an uncollected relic.</summary>
        public IReadOnlyList<Vector2Int> RemainingCells => _router.RemainingCells;

        /// <summary>The relic picked up by the most recent successful collect.</summary>
        public RelicKind LastCollected => _router.LastCollected;

        /// <summary>
        /// Whether the player is carrying a kind of relic with uses left.
        /// </summary>
        /// <param name="kind">Kind to test.</param>
        /// <returns><c>true</c> if it is held and not spent.</returns>
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
        /// <returns><c>true</c> if a use was available and has now been spent.</returns>
        public bool Spend(RelicKind kind) => _router.Spend(kind);

        /// <summary>Every kind the player is carrying with uses left.</summary>
        public IEnumerable<RelicKind> Carried => _router.Carried;

        /// <summary>
        /// World positions of every relic still standing, for the relic compass.
        /// </summary>
        /// <returns>The positions.</returns>
        public IEnumerable<Vector3> StandingPositions() => _router.StandingPositions();

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="RelicManagerTestFacade"/>.</returns>
        public RelicManagerTestFacade GetTestFacade()
            => _testFacade ??= new RelicManagerTestFacade(_router);
    }
}
