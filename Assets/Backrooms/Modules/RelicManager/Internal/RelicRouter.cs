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
        /// <param name="parent">Transform to parent the relics under.</param>
        /// <returns>The cells that received a relic.</returns>
        public List<Vector2Int> Place(MazeLayout layout, int count, int seed, Transform parent)
        {
            ClearStanding();
            Layout = layout;

            var placed = new List<Vector2Int>();
            if (layout == null) return placed;

            foreach (Vector2Int cell in _planner.Plan(layout, count, new System.Random(seed)))
            {
                _standing[cell] = _builder.Build(layout, cell, parent);
                placed.Add(cell);
            }

            return placed;
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
            Collected++;
            return true;
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
        }
    }
}
