using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Dresses a floor with furniture so it reads as a stolen piece of a real place rather than a
    /// bare corridor grid.
    /// </summary>
    /// <remarks>
    /// Placement matters more than the models. Furniture used to be placed one piece per cell, which
    /// put everything on the same 4m lattice as the walls and made the level read as a grid however
    /// good the meshes were. Wall furniture is now laid along continuous <see cref="WallRun"/>s at
    /// free offsets, so a piece lands where the wall has room for it rather than where the grid says.
    /// Islands still sit near a cell centre but are jittered off it. Everything is driven by a seeded
    /// generator so a floor is always furnished identically, and nothing carries a collider —
    /// furniture is scenery, not an obstacle course.
    /// </remarks>
    internal sealed class PropDecorator
    {
        // Furniture is deliberately sparse: a handful of pieces per floor rather than a dressed set.
        // A Backrooms floor reads as abandoned because it is nearly empty, and a lone bench at the
        // end of a corridor says more than a wall lined with them. These skip chances were an order
        // of magnitude lower and the floors looked furnished rather than deserted.

        /// <summary>Chance a single-cell stub of wall is left bare.</summary>
        private const double StubSkipChance = 0.99;

        /// <summary>Chance a two-cell run of wall is left bare.</summary>
        private const double ShortRunSkipChance = 0.95;

        /// <summary>Chance a run of three cells or more is left bare.</summary>
        private const double LongRunSkipChance = 0.88;

        /// <summary>Chance a cell with no walls at all gets an island piece of furniture.</summary>
        private const double OpenCellChance = 0.1;

        /// <summary>How far an island piece is offset from its cell centre, in metres.</summary>
        private const float IslandJitter = 1f;

        /// <summary>Slack added to a footprint before testing it against what is already placed.</summary>
        private const float RejectMargin = 0.12f;

        private readonly WallRunPlanner _runPlanner = new WallRunPlanner();
        private readonly WallRunDresser _dresser = new WallRunDresser();

        /// <summary>World-space footprints of everything already placed on this floor.</summary>
        private readonly List<Bounds> _placed = new List<Bounds>();

        /// <summary>Cells occupied by a structural column, which must stay clear of furniture.</summary>
        private HashSet<Vector2Int> _columnCells = new HashSet<Vector2Int>();

        /// <summary>Furniture models for the floor being decorated.</summary>
        private PropCatalog _catalog;

        /// <summary>
        /// Furnishes a floor. Does nothing when the project has no furniture catalogue, so a build
        /// without the model pack still produces a playable, if empty, level.
        /// </summary>
        /// <param name="layout">The maze being dressed.</param>
        /// <param name="theme">Palette and prop style for this floor.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="parent">Transform to parent the furniture under.</param>
        /// <param name="columnCells">Cells already taken by structural columns.</param>
        /// <returns>How many pieces of furniture were placed.</returns>
        public int Decorate(MazeLayout layout, FloorTheme theme, int seed, Transform parent,
            HashSet<Vector2Int> columnCells = null)
        {
            _catalog = PropCatalog.LoadOrNull();
            _columnCells = columnCells ?? new HashSet<Vector2Int>();
            _placed.Clear();

            var root = new GameObject("Props");
            root.transform.SetParent(parent, worldPositionStays: false);
            if (_catalog == null) return 0;

            var rng = new System.Random(seed);
            HashSet<Vector2Int> reserved = ReservedCells(layout);

            List<WallRun> runs = _runPlanner.Plan(layout, reserved);
            Shuffle(runs, rng);

            // Runs are dressed in shuffled order on purpose. Placement rejects a piece that lands on
            // one already there, so whatever is dressed first wins; scanning row-major meant the
            // south and west of every floor always won and the north-east was quietly thinned.
            GameObject[] wallModels = _catalog.AgainstWallFor(theme.Props);
            int placed = 0;
            foreach (WallRun run in runs)
            {
                if (rng.NextDouble() < SkipChanceFor(run)) continue;
                placed += _dresser.Dress(run, wallModels, root.transform, rng, _placed);
            }

            return placed + PlaceIslands(layout, theme, reserved, root.transform, rng);
        }

        /// <summary>
        /// How likely a run is to be left bare, weighted by its length. Dressing every 4m stub
        /// between two doorways puts a lone chair on each of them and rebuilds the lattice out of
        /// furniture; concentrating the dressing on long walls gives the floor stretches that are
        /// properly furnished and stretches that are empty, which is how a real building looks.
        /// </summary>
        /// <param name="run">The run being considered.</param>
        /// <returns>Probability of leaving the run bare.</returns>
        private static double SkipChanceFor(WallRun run)
        {
            if (run.Cells <= 1) return StubSkipChance;
            return run.Cells == 2 ? ShortRunSkipChance : LongRunSkipChance;
        }

        /// <summary>
        /// Cells that must stay clear of furniture because the player has to read them at a glance:
        /// where they arrive, and every way down.
        /// </summary>
        /// <param name="layout">The maze being dressed.</param>
        /// <returns>The reserved cells.</returns>
        private static HashSet<Vector2Int> ReservedCells(MazeLayout layout)
        {
            var reserved = new HashSet<Vector2Int> { layout.Spawn };
            foreach (Vector2Int stairs in layout.Stairs) reserved.Add(stairs);
            foreach (Vector2Int stairs in layout.StairsUp) reserved.Add(stairs);
            return reserved;
        }

        /// <summary>
        /// Places freestanding pieces in cells with no walls at all — the middles of carved rooms,
        /// where a piece against a wall would be too far away to break up the space. Each is jittered
        /// off the cell centre so islands do not line up on the grid either.
        /// </summary>
        /// <param name="layout">The maze being dressed.</param>
        /// <param name="theme">Palette and prop style for this floor.</param>
        /// <param name="reserved">Cells that must stay clear.</param>
        /// <param name="parent">Transform to parent the furniture under.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <returns>How many pieces were placed.</returns>
        private int PlaceIslands(MazeLayout layout, FloorTheme theme, HashSet<Vector2Int> reserved,
            Transform parent, System.Random rng)
        {
            GameObject[] choices = _catalog.FreestandingFor(theme.Props);
            if (choices.Length == 0) return 0;

            int placed = 0;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (reserved.Contains(cell)) continue;

                    // A column occupies the middle of its cell; furniture there grows through it.
                    if (_columnCells.Contains(cell)) continue;
                    if (!IsOpenOnAllSides(layout, x, y)) continue;
                    if (rng.NextDouble() > OpenCellChance) continue;

                    if (PlaceIsland(choices, layout.CellCenterToWorld(cell), parent, rng)) placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// Places one island piece near a cell centre, rejecting it if it lands on something already
        /// placed.
        /// </summary>
        /// <param name="choices">Models that stand on the floor in the open.</param>
        /// <param name="centre">World centre of the cell.</param>
        /// <param name="parent">Transform to parent the furniture under.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <returns><c>true</c> if the piece was kept.</returns>
        private bool PlaceIsland(GameObject[] choices, Vector3 centre, Transform parent,
            System.Random rng)
        {
            GameObject model = choices[rng.Next(choices.Length)];
            if (model == null) return false;

            var offset = new Vector3(
                ((float)rng.NextDouble() * 2f - 1f) * IslandJitter, 0f,
                ((float)rng.NextDouble() * 2f - 1f) * IslandJitter);

            GameObject instance = Object.Instantiate(model, centre + offset,
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), parent);
            instance.name = model.name;
            PropPlacement.StripColliders(instance);
            PropPlacement.SeatOnFloor(instance, centre.y);

            if (!PropPlacement.TryGetWorldBounds(instance, out Bounds footprint))
            {
                Object.Destroy(instance);
                return false;
            }

            footprint.Expand(RejectMargin);
            foreach (Bounds other in _placed)
            {
                if (!other.Intersects(footprint)) continue;
                Object.Destroy(instance);
                return false;
            }

            _placed.Add(footprint);
            return true;
        }

        /// <summary>
        /// Whether a cell has an open passage on all four sides, marking it as room interior rather
        /// than corridor.
        /// </summary>
        /// <param name="layout">The maze.</param>
        /// <param name="x">Cell X.</param>
        /// <param name="y">Cell Y.</param>
        /// <returns><c>true</c> if nothing walls the cell in.</returns>
        private static bool IsOpenOnAllSides(MazeLayout layout, int x, int y)
        {
            foreach (Direction dir in Directions.All)
            {
                if (!layout.CanMove(x, y, dir)) return false;
            }

            return true;
        }

        /// <summary>
        /// Shuffles a list in place with a seeded generator, so the order is random but reproducible.
        /// </summary>
        /// <param name="items">List to shuffle.</param>
        /// <param name="rng">Seeded generator.</param>
        private static void Shuffle(List<WallRun> items, System.Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
