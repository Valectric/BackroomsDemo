using System.Collections.Generic;
using Backrooms.MazeManager;
using Backrooms.RelicManager.Internal.Placement;
using UnityEngine;

namespace Backrooms.RelicManager.Internal
{
    /// <summary>
    /// Internal coordinator for the RelicManager module. Owns which cells hold a relic and which
    /// have been collected, and forwards placement to the planner and geometry to the builder.
    /// </summary>
    internal sealed class RelicRouter
    {
        private readonly RelicPlanner _planner = new RelicPlanner();
        private readonly RelicBuilder _builder = new RelicBuilder();

        /// <summary>Cells still holding an uncollected relic, and the object standing there.</summary>
        private readonly Dictionary<Vector2Int, GameObject> _standing =
            new Dictionary<Vector2Int, GameObject>();

        /// <summary>Which kind stands in each uncollected cell.</summary>
        private readonly Dictionary<Vector2Int, RelicKind> _kinds =
            new Dictionary<Vector2Int, RelicKind>();

        /// <summary>Relics the player is carrying, and how many uses each has left.</summary>
        private readonly Dictionary<RelicKind, int> _held = new Dictionary<RelicKind, int>();

        /// <summary>The kind collected by the most recent successful collect.</summary>
        public RelicKind LastCollected { get; private set; }

        /// <summary>The floor the relics were placed on, or <c>null</c> before placement.</summary>
        public MazeLayout Layout { get; private set; }

        /// <summary>How many relics the player has collected this run.</summary>
        public int Collected { get; private set; }

        /// <summary>How many relics are still standing on the current floor.</summary>
        public int Remaining => _standing.Count;

        /// <summary>
        /// Places relics on a floor, clearing anything left from the previous one.
        /// </summary>
        /// <param name="layout">The floor to place on.</param>
        /// <param name="count">How many relics to place.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="firstKind">Index into the roster for this floor's first relic.</param>
        /// <param name="parent">Transform to parent the relics under.</param>
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> Place(MazeLayout layout, int count, int seed, int firstKind,
            Transform parent)
        {
            ClearStanding();
            Layout = layout;

            var placed = new List<Vector2Int>();
            if (layout == null) return placed;

            int offered = 0;
            foreach (Vector2Int cell in _planner.Plan(layout, count, new System.Random(seed)))
            {
                // Deal the roster out in turn as the player descends, skipping anything already
                // carried: being offered a second Wayfinder Stone is being offered nothing.
                RelicKind kind = NextUnheld(firstKind + offered);
                offered++;

                _kinds[cell] = kind;
                _standing[cell] = _builder.Build(layout, cell, RelicArchetypes.For(kind), parent);
                placed.Add(cell);
            }

            return placed;
        }

        /// <summary>
        /// The first kind at or after an index that the player is not already carrying.
        /// </summary>
        /// <param name="index">Where to start looking in the roster.</param>
        /// <returns>A kind worth offering.</returns>
        private RelicKind NextUnheld(int index)
        {
            for (int step = 0; step < RelicArchetypes.Count; step++)
            {
                RelicKind kind = RelicArchetypes.AtIndex(index + step);
                if (!Holds(kind)) return kind;
            }

            // Everything is already carried: offer the Banisher, the only one worth stacking.
            return RelicKind.Banisher;
        }

        /// <summary>
        /// Whether the player is carrying a kind of relic with uses left.
        /// </summary>
        /// <param name="kind">Kind to test.</param>
        /// <returns><c>true</c> if it is held and not spent.</returns>
        public bool Holds(RelicKind kind) => _held.TryGetValue(kind, out int left) && left != 0;

        /// <summary>
        /// How many uses of a kind remain.
        /// </summary>
        /// <param name="kind">Kind to query.</param>
        /// <returns>Uses left, 0 if not held or spent, -1 if unlimited.</returns>
        public int ChargesOf(RelicKind kind) => _held.TryGetValue(kind, out int left) ? left : 0;

        /// <summary>
        /// Spends one use of a relic.
        /// </summary>
        /// <param name="kind">Kind to spend.</param>
        /// <returns><c>true</c> if a use was available and has now been spent.</returns>
        public bool Spend(RelicKind kind)
        {
            if (!Holds(kind)) return false;
            int left = _held[kind];
            if (left > 0) _held[kind] = left - 1;
            return true;
        }

        /// <summary>Every kind the player is carrying with uses left.</summary>
        public IEnumerable<RelicKind> Carried
        {
            get
            {
                foreach (KeyValuePair<RelicKind, int> entry in _held)
                {
                    if (entry.Value != 0) yield return entry.Key;
                }
            }
        }

        /// <summary>
        /// World positions of every relic still standing, for the relic compass.
        /// </summary>
        /// <returns>The positions.</returns>
        public IEnumerable<Vector3> StandingPositions()
        {
            if (Layout == null) yield break;
            foreach (Vector2Int cell in _standing.Keys) yield return Layout.CellCenterToWorld(cell);
        }

        /// <summary>
        /// Collects any relic the player is standing close enough to.
        /// </summary>
        /// <param name="playerPosition">World position of the player.</param>
        /// <param name="radius">How close counts as collecting, in metres.</param>
        /// <returns><c>true</c> if a relic was collected on this call.</returns>
        public bool TryCollect(Vector3 playerPosition, float radius)
        {
            if (Layout == null || _standing.Count == 0) return false;

            var flat = new Vector3(playerPosition.x, 0f, playerPosition.z);

            // Find first, remove after: mutating the dictionary inside its own foreach is a trap that
            // only shows up once a floor happens to carry two relics close together.
            var reached = new Vector2Int(int.MinValue, int.MinValue);
            foreach (KeyValuePair<Vector2Int, GameObject> entry in _standing)
            {
                Vector3 at = Layout.CellCenterToWorld(entry.Key);
                if (Vector3.Distance(flat, new Vector3(at.x, 0f, at.z)) > radius) continue;
                reached = entry.Key;
                break;
            }

            if (!_standing.TryGetValue(reached, out GameObject collected)) return false;

            if (collected != null) Object.Destroy(collected);
            _standing.Remove(reached);

            RelicKind kind = _kinds.TryGetValue(reached, out RelicKind found) ? found : RelicKind.Ward;
            _kinds.Remove(reached);
            LastCollected = kind;
            Take(kind);

            Collected++;
            return true;
        }

        /// <summary>
        /// Adds a relic to what the player carries. Unlimited relics are stored as -1 so they never
        /// run down; charged ones stack, so a second Banisher is five more shots rather than a
        /// duplicate that does nothing.
        /// </summary>
        /// <param name="kind">Kind collected.</param>
        private void Take(RelicKind kind)
        {
            int gained = RelicArchetypes.For(kind).Charges;
            if (gained == 0)
            {
                _held[kind] = -1;
                return;
            }

            _held[kind] = _held.TryGetValue(kind, out int existing) && existing > 0
                ? existing + gained
                : gained;
        }

        /// <summary>
        /// Whether a cell still holds an uncollected relic.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns><c>true</c> if a relic stands there.</returns>
        public bool HasRelicAt(Vector2Int cell) => _standing.ContainsKey(cell);

        /// <summary>
        /// Resets the collected tally for a new run. Placement is per floor; the tally is per run.
        /// </summary>
        public void ResetRun()
        {
            Collected = 0;
            _held.Clear();
            ClearStanding();
            Layout = null;
        }

        /// <summary>
        /// Destroys any relics still standing and forgets them.
        /// </summary>
        private void ClearStanding()
        {
            foreach (GameObject relic in _standing.Values)
            {
                if (relic != null) Object.Destroy(relic);
            }

            _standing.Clear();
            _kinds.Clear();
        }
    }
}
