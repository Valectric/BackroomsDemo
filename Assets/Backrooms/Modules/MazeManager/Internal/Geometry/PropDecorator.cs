using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Dresses a floor with furniture so it reads as a stolen piece of a real place rather than a
    /// bare corridor grid.
    /// </summary>
    /// <remarks>
    /// Placement matters more than the models: furniture dropped at random offsets in the middle of
    /// a room looks wrong however good the meshes are. Pieces that belong against a wall are pushed
    /// back until they nearly touch one and turned to face into the room; pieces that stand in the
    /// open sit on the cell centre. Placement is driven by a seeded generator so a floor is always
    /// furnished identically, and nothing carries a collider — furniture is scenery, not an obstacle
    /// course.
    /// </remarks>
    internal sealed class PropDecorator
    {
        /// <summary>
        /// Clearance from the wall plane for a wall-hugging piece, in metres. The skirting is a box
        /// straddling the wall plane, so it stands 0.11m proud into the room; anything less than that
        /// buries the bottom of every cabinet and bookcase inside the trim.
        /// </summary>
        private const float WallGap = 0.14f;

        /// <summary>Cells occupied by a structural column, which must stay clear of furniture.</summary>
        private HashSet<Vector2Int> _columnCells = new HashSet<Vector2Int>();

        /// <summary>World-space footprints of everything already placed on this floor.</summary>
        private readonly List<Bounds> _placed = new List<Bounds>();

        /// <summary>Chance an open floor cell gets an island piece of furniture.</summary>
        private const double OpenCellChance = 0.5;

        /// <summary>Chance a cell with a wall gets something backed onto it.</summary>
        private const double WallCellChance = 0.55;

        /// <summary>World size of one cell on the floor being decorated.</summary>
        private float _cellSize = 4f;

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
        public void Decorate(MazeLayout layout, FloorTheme theme, int seed, Transform parent,
            HashSet<Vector2Int> columnCells = null)
        {
            _cellSize = layout.CellSize;
            _catalog = PropCatalog.LoadOrNull();
            _columnCells = columnCells ?? new HashSet<Vector2Int>();
            _placed.Clear();

            var root = new GameObject("Props");
            root.transform.SetParent(parent, worldPositionStays: false);
            if (_catalog == null) return;

            var rng = new System.Random(seed);

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell == layout.Spawn || cell == layout.Exit) continue;

                    // A column occupies the middle of its cell; furniture there grows through it.
                    if (_columnCells.Contains(cell)) continue;

                    List<Direction> walls = ClosedSides(layout, x, y);
                    bool isOpenCell = walls.Count == 0;

                    double chance = isOpenCell ? OpenCellChance : WallCellChance;
                    if (rng.NextDouble() > chance) continue;

                    Vector3 centre = layout.CellCenterToWorld(cell);
                    Direction? wall = walls.Count > 0 ? walls[rng.Next(walls.Count)] : (Direction?)null;
                    PlaceModel(theme, root.transform, centre, wall, rng);
                }
            }
        }

        /// <summary>
        /// The sides of a cell that are walls rather than open passages.
        /// </summary>
        /// <param name="layout">The maze.</param>
        /// <param name="x">Cell X.</param>
        /// <param name="y">Cell Y.</param>
        /// <returns>Directions that are blocked.</returns>
        private static List<Direction> ClosedSides(MazeLayout layout, int x, int y)
        {
            var closed = new List<Direction>(4);
            foreach (Direction dir in Directions.All)
            {
                if (!layout.CanMove(x, y, dir)) closed.Add(dir);
            }

            return closed;
        }

        /// <summary>
        /// The direction pointing away from a wall, into the room.
        /// </summary>
        /// <param name="wall">Which side of the cell the wall is on.</param>
        /// <returns>A unit vector pointing into the room.</returns>
        private static Vector3 IntoRoom(Direction wall)
        {
            Vector2Int d = Directions.Delta(wall);
            return new Vector3(-d.x, 0f, -d.y);
        }

        /// <summary>
        /// Places one piece of furniture, seated against a wall when the cell has one.
        /// </summary>
        /// <param name="theme">Floor palette and style.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="centre">World centre of the cell.</param>
        /// <param name="wall">Wall to back onto, or <c>null</c> for an open cell.</param>
        /// <param name="rng">Seeded generator.</param>
        private void PlaceModel(FloorTheme theme, Transform parent, Vector3 centre,
            Direction? wall, System.Random rng)
        {
            GameObject[] choices = wall.HasValue
                ? _catalog.AgainstWallFor(theme.Props)
                : _catalog.FreestandingFor(theme.Props);
            if (choices == null || choices.Length == 0) return;

            GameObject model = choices[rng.Next(choices.Length)];
            if (model == null) return;

            // Kenney's furniture is modelled facing -Z, so aiming the transform's forward into the
            // room turns the piece's back on it. Aim forward at the wall instead and the front face
            // ends up looking into the room.
            Quaternion rotation = wall.HasValue
                ? Quaternion.LookRotation(-IntoRoom(wall.Value))
                : Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);

            GameObject instance = Object.Instantiate(model, centre, rotation, parent);
            instance.name = model.name;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            {
                Object.Destroy(collider);
            }

            if (wall.HasValue) SeatAgainstWall(instance, centre, wall.Value);
            SeatOnFloor(instance, centre.y);

            // Reject anything that landed on top of a neighbouring cell's furniture. Seating only
            // ever clamped the wall axis, so wide pieces in adjacent cells used to interpenetrate.
            if (!TryGetWorldBounds(instance, out Bounds footprint))
            {
                return;
            }

            footprint.Expand(0.12f);
            foreach (Bounds other in _placed)
            {
                if (!other.Intersects(footprint)) continue;
                Object.Destroy(instance);
                return;
            }

            _placed.Add(footprint);
        }

        /// <summary>
        /// Drops a piece so it rests exactly on the floor. Models in the pack are pivoted variously
        /// at their base, centre or top, so instantiating at floor level alone sinks some of them
        /// halfway into the ground and leaves others hovering.
        /// </summary>
        /// <param name="instance">The placed object.</param>
        /// <param name="floorY">World height of the floor.</param>
        private static void SeatOnFloor(GameObject instance, float floorY)
        {
            if (!TryGetWorldBounds(instance, out Bounds bounds)) return;
            instance.transform.position += Vector3.up * (floorY - bounds.min.y);
        }

        /// <summary>
        /// Slides a placed object back until its rear face nearly touches the wall, using its real
        /// bounds so a deep cabinet and a thin chair both end up flush rather than floating.
        /// </summary>
        /// <param name="instance">The placed object.</param>
        /// <param name="cellCentre">World centre of the cell.</param>
        /// <param name="wall">Wall to back onto.</param>
        private void SeatAgainstWall(GameObject instance, Vector3 cellCentre, Direction wall)
        {
            if (!TryGetWorldBounds(instance, out Bounds bounds)) return;

            Vector3 into = IntoRoom(wall);
            Vector3 back = -into;

            // Work in the axis pointing from the room towards the wall, and measure where the piece
            // actually ends rather than assuming its pivot sits at its centre. Several models in the
            // pack are pivoted at an edge or a corner, and assuming otherwise buries them in the wall.
            float wallFace = Vector3.Dot(cellCentre + back * (_cellSize * 0.5f), back);
            float pieceBack = Vector3.Dot(bounds.center, back)
                              + Vector3.Dot(bounds.extents, new Vector3(
                                  Mathf.Abs(back.x), Mathf.Abs(back.y), Mathf.Abs(back.z)));

            float overshoot = pieceBack - (wallFace - WallGap);
            instance.transform.position -= back * overshoot;

            // Never leave a piece hanging outside the cell on the opposite side.
            if (!TryGetWorldBounds(instance, out Bounds after)) return;
            float frontFace = Vector3.Dot(cellCentre + into * (_cellSize * 0.5f), into);
            float pieceFront = Vector3.Dot(after.center, into)
                               + Vector3.Dot(after.extents, new Vector3(
                                   Mathf.Abs(into.x), Mathf.Abs(into.y), Mathf.Abs(into.z)));
            if (pieceFront > frontFace) instance.transform.position -= into * (pieceFront - frontFace);
        }

        /// <summary>
        /// Combined world-space bounds of every renderer on an object.
        /// </summary>
        /// <param name="instance">Object to measure.</param>
        /// <param name="bounds">Receives the combined bounds.</param>
        /// <returns><c>true</c> if the object has any renderers.</returns>
        private static bool TryGetWorldBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
