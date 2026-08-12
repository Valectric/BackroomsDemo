using System.Collections.Generic;
using Backrooms.EntityManager;
using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Decides how many Dwellers a floor carries, what kind each one is, where they start, and which
    /// of them is currently the nearest threat. Split out of <see cref="GameplayController"/>, which
    /// had grown past the size a single file should carry.
    /// </summary>
    /// <remarks>
    /// A plain C# class rather than a component: the gameplay layer owns cross-module composition,
    /// and this is composition — it holds module facades and puts them to work, but has no scene
    /// presence of its own.
    /// </remarks>
    public sealed class DwellerDirector
    {
        /// <summary>Every Dweller currently roaming the floor.</summary>
        private readonly List<DwellerFacade> _dwellers = new List<DwellerFacade>();

        /// <summary>Grid cells of floor per Dweller.</summary>
        private readonly int _cellsPerDweller;

        /// <summary>Never place more than this many Dwellers on one floor.</summary>
        private readonly int _maxDwellers;

        /// <summary>Transform new Dwellers are created alongside.</summary>
        private readonly Transform _host;

        /// <summary>
        /// How far from the player's arrival point a Dweller must start, in cells. Arriving on a
        /// floor face to face with one is not a threat, it is an ambush the player could not have
        /// avoided.
        /// </summary>
        private const float MinCellsFromArrival = 9f;

        /// <summary>
        /// Creates the director.
        /// </summary>
        /// <param name="host">Transform new Dwellers are created alongside.</param>
        /// <param name="seeded">A Dweller already present in the scene, or <c>null</c>.</param>
        /// <param name="cellsPerDweller">Grid cells of floor per Dweller.</param>
        /// <param name="maxDwellers">Ceiling on Dwellers per floor.</param>
        public DwellerDirector(Transform host, DwellerFacade seeded, int cellsPerDweller,
            int maxDwellers)
        {
            _host = host;
            _cellsPerDweller = Mathf.Max(1, cellsPerDweller);
            _maxDwellers = Mathf.Max(1, maxDwellers);
            if (seeded != null) _dwellers.Add(seeded);
        }

        /// <summary>How many Dwellers are roaming the current floor.</summary>
        public int Count => _dwellers.Count;

        /// <summary>Whether any Dweller is currently hunting the player.</summary>
        public bool AnyHunting
        {
            get
            {
                foreach (DwellerFacade d in _dwellers)
                {
                    if (d != null && d.IsChasing) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Whether any Dweller on the floor has reached the player.
        /// </summary>
        /// <returns><c>true</c> if the run is over.</returns>
        public bool AnyCaughtPlayer()
        {
            foreach (DwellerFacade d in _dwellers)
            {
                if (d != null && d.HasCaught) return true;
            }

            return false;
        }

        /// <summary>
        /// Populates a floor with Dwellers.
        /// </summary>
        /// <remarks>
        /// How many is decided by the floor's area, not by taste. One Dweller wandering a 24×24 grid
        /// is one Dweller you never meet. They start in the corners furthest from the spawn — never
        /// on a stairwell, which would have one camping a cell the player has to reach, and never
        /// next to the player, which is an unavoidable death. Each is a different kind, dealt in
        /// turn, because a floor with three identical creatures teaches the player nothing.
        /// </remarks>
        /// <param name="layout">The floor being populated.</param>
        /// <param name="floor">One-based floor number.</param>
        /// <param name="target">The player transform to hunt.</param>
        /// <param name="speed">Base Dweller speed for this floor, in metres per second.</param>
        /// <param name="seed">Seed for deterministic behaviour.</param>
        public void PopulateFloor(MazeLayout layout, int floor, Transform target, float speed,
            int seed)
        {
            List<Vector2Int> starts = Starts(layout, floor);
            Ensure(starts.Count);

            for (int i = 0; i < _dwellers.Count; i++)
            {
                if (i < starts.Count)
                {
                    _dwellers[i].SetKind(DwellerArchetypes.KindFor(i, floor));
                    _dwellers[i].Place(layout, starts[i], target, speed, seed + floor * 31 + i);
                }
                else
                {
                    _dwellers[i].Hide();
                }
            }
        }

        /// <summary>
        /// The position of the nearest Dweller still on the floor, hunting or not.
        /// </summary>
        /// <param name="from">World position to measure from.</param>
        /// <param name="position">Receives its position.</param>
        /// <returns><c>true</c> if any Dweller is on the floor.</returns>
        public bool TryGetNearestPosition(Vector3 from, out Vector3 position)
        {
            position = Vector3.zero;
            float best = float.PositiveInfinity;

            var flat = new Vector3(from.x, 0f, from.z);
            foreach (DwellerFacade d in _dwellers)
            {
                if (d == null || !d.IsActive) continue;

                Vector3 at = d.transform.position;
                float away = Vector3.Distance(flat, new Vector3(at.x, 0f, at.z));
                if (away >= best) continue;

                best = away;
                position = at;
            }

            return !float.IsPositiveInfinity(best);
        }

        /// <summary>
        /// Unmakes the nearest Dweller inside a cone in front of the player.
        /// </summary>
        /// <param name="origin">Where the shot comes from.</param>
        /// <param name="forward">Direction the player is facing, flattened and normalised.</param>
        /// <param name="range">How far the shot reaches, in metres.</param>
        /// <param name="halfAngle">Half-width of the cone, in degrees.</param>
        /// <returns><c>true</c> if something was hit.</returns>
        public bool TryBanishInFront(Vector3 origin, Vector3 forward, float range, float halfAngle)
        {
            DwellerFacade hit = null;
            float best = float.PositiveInfinity;

            var flat = new Vector3(origin.x, 0f, origin.z);
            foreach (DwellerFacade d in _dwellers)
            {
                if (d == null || !d.IsActive) continue;

                Vector3 at = d.transform.position;
                float away = (new Vector3(at.x, 0f, at.z) - flat).magnitude;
                if (away >= best) continue;
                if (!DwellerAim.IsInCone(origin, forward, at, range, halfAngle)) continue;

                best = away;
                hit = d;
            }

            if (hit == null) return false;
            hit.Banish();
            return true;
        }

        /// <summary>
        /// Unmakes whichever Dweller has reached the player, so a ward that saves them does not
        /// leave the creature standing on top of them to catch them again next frame.
        /// </summary>
        /// <returns><c>true</c> if one was removed.</returns>
        public bool BanishWhicheverCaughtThePlayer()
        {
            foreach (DwellerFacade d in _dwellers)
            {
                if (d == null || !d.HasCaught) continue;
                d.Banish();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the nearest Dweller that is hunting the player.
        /// </summary>
        /// <param name="playerPosition">World position of the player.</param>
        /// <param name="distance">Receives the distance to it, in metres.</param>
        /// <param name="name">Receives what it is called, or <c>null</c> if nothing is hunting.</param>
        /// <returns><c>true</c> if any Dweller is hunting.</returns>
        public bool TryGetNearestHunter(Vector3 playerPosition, out float distance, out string name)
        {
            distance = float.PositiveInfinity;
            name = null;

            var flat = new Vector3(playerPosition.x, 0f, playerPosition.z);
            foreach (DwellerFacade d in _dwellers)
            {
                if (d == null || !d.IsChasing) continue;

                Vector3 at = d.transform.position;
                float away = Vector3.Distance(flat, new Vector3(at.x, 0f, at.z));
                if (away >= distance) continue;

                distance = away;
                name = d.DisplayName;
            }

            return name != null;
        }

        /// <summary>
        /// How many Dwellers this floor gets, and where each starts. Candidates are the corners and
        /// edge midpoints, rotated by the floor number so consecutive floors do not open identically.
        /// </summary>
        /// <param name="layout">The floor being populated.</param>
        /// <param name="floor">One-based floor number.</param>
        /// <returns>One start cell per Dweller the floor should carry.</returns>
        private List<Vector2Int> Starts(MazeLayout layout, int floor)
        {
            int wanted = Mathf.Clamp(
                layout.Width * layout.Height / _cellsPerDweller, 1, _maxDwellers);

            int right = layout.Width - 1;
            int top = layout.Height - 1;
            var candidates = new List<Vector2Int>
            {
                new Vector2Int(right, top),
                new Vector2Int(0, top),
                new Vector2Int(right, 0),
                new Vector2Int(right / 2, top),
                new Vector2Int(right, top / 2),
                new Vector2Int(right / 2, top / 2)
            };

            // Sort the candidates by how far they are from where the player arrives, furthest
            // first, and refuse anything close. The player used to appear with a Dweller already on
            // top of them, because the corners were fixed while the arrival point moved to whichever
            // way up the floor chose.
            candidates.Sort((a, b) =>
                (b - layout.Spawn).sqrMagnitude.CompareTo((a - layout.Spawn).sqrMagnitude));

            var starts = new List<Vector2Int>(wanted);
            foreach (Vector2Int cell in candidates)
            {
                if (starts.Count == wanted) break;
                if (cell == layout.Spawn) continue;
                if (layout.IsStairs(cell) || layout.IsStairsUp(cell)) continue;
                if (starts.Contains(cell)) continue;

                float away = (cell - layout.Spawn).magnitude;
                if (away < MinCellsFromArrival) continue;
                starts.Add(cell);
            }

            if (starts.Count == 0) starts.Add(new Vector2Int(right, top));
            return starts;
        }

        /// <summary>
        /// Grows the pool of Dwellers to the requested size, reusing any authored in the scene and
        /// creating the rest. Dwellers persist between floors and are re-placed rather than rebuilt.
        /// </summary>
        /// <param name="count">How many Dwellers the floor needs.</param>
        private void Ensure(int count)
        {
            if (_dwellers.Count == 0)
            {
                foreach (DwellerFacade found in
                         Object.FindObjectsByType<DwellerFacade>(FindObjectsSortMode.None))
                {
                    if (!_dwellers.Contains(found)) _dwellers.Add(found);
                }
            }

            while (_dwellers.Count < count)
            {
                var go = new GameObject($"Dweller_{_dwellers.Count}");
                go.transform.SetParent(_host, worldPositionStays: true);
                _dwellers.Add(go.AddComponent<DwellerFacade>());
            }
        }
    }
}
