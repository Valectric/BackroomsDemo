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
    internal sealed class DwellerDirector
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
                    _dwellers[i].SetKind(DwellerArchetypes.AtIndex(i + floor));
                    _dwellers[i].Place(layout, starts[i], target, speed, seed + floor * 31 + i);
                }
                else
                {
                    _dwellers[i].Hide();
                }
            }
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

            var starts = new List<Vector2Int>(wanted);
            for (int i = 0; i < candidates.Count && starts.Count < wanted; i++)
            {
                Vector2Int cell = candidates[(i + floor) % candidates.Count];
                if (cell == layout.Spawn) continue;
                if (layout.IsStairs(cell)) continue;
                if (starts.Contains(cell)) continue;
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
