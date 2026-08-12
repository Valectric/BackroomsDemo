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
        {
            List<Vector2Int> placed =
                _router.Place(layout, RelicsFor(layout, floor), seed, floor, transform);
            CullGlows();
            return placed;
        }

        /// <summary>
        /// Takes charge of the relic glows so only the ones near the player stay lit.
        /// </summary>
        /// <remarks>
        /// Forty relics on the first floor is forty realtime lights, which is more than the whole
        /// ceiling carries — the floor every player sees would have become the most expensive one in
        /// the game. This costs nothing visually: a relic's glow has a range of seven metres, so one
        /// twenty metres away is already lighting nothing, and switching it off is invisible by
        /// construction rather than by judgement.
        /// </remarks>
        private void CullGlows()
        {
            if (_culler == null)
            {
                _culler = gameObject.AddComponent<LightCuller>();

                // Comfortably past the glow's own seven-metre range, so a relic is lit before it can
                // possibly light anything, and never the other way round.
                _culler.RadiusMetres = GlowCullMetres;
                _culler.MaxActive = MaxLitRelics;
            }

            _culler.Collect();
        }

        /// <summary>Keeps the relic glows near the player lit and the rest off.</summary>
        private LightCuller _culler;

        /// <summary>How far a relic glow stays lit, in metres. Its own range is seven.</summary>
        private const float GlowCullMetres = 14f;

        /// <summary>Most relic glows lit at once.</summary>
        private const int MaxLitRelics = 6;

        /// <summary>How many relic glows are currently lit.</summary>
        public int LitGlows => _culler == null ? 0 : _culler.ActiveCount;

        /// <summary>
        /// How many relics a floor should carry: one per way down unless the scene pins a number,
        /// and four times that on the first floor.
        /// </summary>
        /// <remarks>
        /// Tying the count to the stairwells keeps the two in step by construction rather than as two
        /// magic numbers that drift apart. It also means a single relic per floor no longer rations
        /// the powers so hard that most of a run is spent carrying nothing: the roster is dealt out
        /// in turn and skips anything already held, so a floor with three relics offers three
        /// different ones.
        /// <para>
        /// The first floor is deliberately littered with them. A player who finds nothing in their
        /// first two minutes has no reason to believe there is anything to find, and floor 1 is the
        /// only floor every player sees. What it mostly hands out is Wards — that is what floor 1
        /// favours, and a Ward does not stack — so the floor reads as generous without being
        /// enriching, and the roster proper is somewhere below.
        /// </para>
        /// </remarks>
        /// <param name="layout">The floor being placed on.</param>
        /// <param name="floor">One-based floor number.</param>
        /// <returns>How many relics to place.</returns>
        private int RelicsFor(MazeLayout layout, int floor)
        {
            // An explicit count means exactly that count, on every floor. It is how a test or a
            // designer pins the number, and multiplying it behind their back would make the field lie.
            if (relicsPerFloor > 0) return relicsPerFloor;

            int waysDown = layout == null || layout.Stairs.Count == 0
                ? DefaultWaysDown
                : layout.Stairs.Count;
            int baseline = Mathf.Max(1, Mathf.RoundToInt(waysDown * RelicsPerWayDown));

            return floor <= 1 ? baseline * FirstFloorMultiplier : baseline;
        }

        /// <summary>
        /// How many times the usual number of relics the first floor carries.
        /// </summary>
        private const int FirstFloorMultiplier = 4;

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
        /// Whether the last relic collected added nothing, because one was already carried and that
        /// kind does not stack. The game says so rather than claiming to have handed something over.
        /// </summary>
        public bool LastWasSpare => _router.LastWasSpare;

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
