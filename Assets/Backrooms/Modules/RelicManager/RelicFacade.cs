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
        [Tooltip("How many relics each floor carries.")]
        [SerializeField] private int relicsPerFloor = 1;

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
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> PlaceForFloor(MazeLayout layout, int seed)
            => _router.Place(layout, relicsPerFloor, seed, transform);

        /// <summary>
        /// Collects a relic if the player has reached one.
        /// </summary>
        /// <param name="playerPosition">World position of the player.</param>
        /// <returns><c>true</c> if a relic was collected on this call.</returns>
        public bool TryCollect(Vector3 playerPosition)
            => _router.TryCollect(playerPosition, collectRadius);

        /// <summary>
        /// Clears the collected tally and the floor for a fresh run.
        /// </summary>
        public void ResetRun() => _router.ResetRun();

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="RelicManagerTestFacade"/>.</returns>
        public RelicManagerTestFacade GetTestFacade()
            => _testFacade ??= new RelicManagerTestFacade(_router);
    }
}
