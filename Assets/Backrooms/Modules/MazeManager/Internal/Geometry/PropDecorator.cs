using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Dresses a floor with props so it reads as a stolen piece of a real place rather than a bare
    /// corridor grid. Every prop is built from primitives and tinted from the floor's palette, so
    /// there is no imported art to download — which matters for a browser build.
    /// </summary>
    /// <remarks>
    /// Placement is driven by a seeded generator, so a given floor is always furnished identically.
    /// Props are placed against walls or in open rooms and never block a passage, because the maze
    /// must stay traversable.
    /// </remarks>
    internal sealed class PropDecorator
    {
        /// <summary>Props are kept below this height so they never hide the ceiling lights.</summary>
        private const float MaxPropHeight = 2.2f;

        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        /// <summary>Floor-to-ceiling height of the current floor.</summary>
        private float _wallHeight = 3f;

        /// <summary>Furniture models, or <c>null</c> when the project has none.</summary>
        private PropCatalog _catalog;

        /// <summary>
        /// Furnishes a floor.
        /// </summary>
        /// <param name="layout">The maze being dressed.</param>
        /// <param name="theme">Palette and prop style for this floor.</param>
        /// <param name="seed">Seed for deterministic placement.</param>
        /// <param name="parent">Transform to parent the props under.</param>
        /// <param name="wallHeight">Floor-to-ceiling height, so columns can actually reach it.</param>
        public void Decorate(MazeLayout layout, FloorTheme theme, int seed, Transform parent,
            float wallHeight)
        {
            _wallHeight = wallHeight;
            _materials.Clear();
            _catalog = PropCatalog.LoadOrNull();

            var root = new GameObject("Props");
            root.transform.SetParent(parent, worldPositionStays: false);

            var rng = new System.Random(seed);
            float cell = layout.CellSize;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (IsReservedCell(layout, x, y)) continue;

                    int openSides = CountOpenSides(layout, x, y);

                    // Open cells are room interiors; dress those. Corridors get occasional clutter.
                    bool isRoomCell = openSides >= 3;
                    if (!isRoomCell && rng.NextDouble() > 0.12) continue;
                    if (isRoomCell && rng.NextDouble() > 0.45) continue;

                    Vector3 centre = layout.CellCenterToWorld(new Vector2Int(x, y));
                    if (!TryPlaceModel(theme, root.transform, centre, cell, isRoomCell, rng))
                    {
                        PlaceProp(theme, root.transform, centre, cell, isRoomCell, rng);
                    }
                }
            }
        }

        /// <summary>
        /// Whether a cell must stay clear: the spawn and the exit.
        /// </summary>
        /// <param name="layout">The maze.</param>
        /// <param name="x">Cell X.</param>
        /// <param name="y">Cell Y.</param>
        /// <returns><c>true</c> when nothing may be placed there.</returns>
        private static bool IsReservedCell(MazeLayout layout, int x, int y)
        {
            var cell = new Vector2Int(x, y);
            return cell == layout.Spawn || cell == layout.Exit;
        }

        /// <summary>
        /// How many of a cell's four sides are open passages.
        /// </summary>
        /// <param name="layout">The maze.</param>
        /// <param name="x">Cell X.</param>
        /// <param name="y">Cell Y.</param>
        /// <returns>Number of open sides, 0 to 4.</returns>
        private static int CountOpenSides(MazeLayout layout, int x, int y)
        {
            int open = 0;
            foreach (Direction dir in Directions.All)
            {
                if (layout.CanMove(x, y, dir)) open++;
            }

            return open;
        }

        /// <summary>
        /// Places a real furniture model if the catalogue has one for this floor style.
        /// </summary>
        /// <param name="theme">Floor palette and style.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="centre">World centre of the cell.</param>
        /// <param name="cell">Cell size in metres.</param>
        /// <param name="isRoomCell">Whether this is an open room rather than a corridor.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <returns><c>true</c> when a model was placed, <c>false</c> to fall back to primitives.</returns>
        private bool TryPlaceModel(FloorTheme theme, Transform parent, Vector3 centre, float cell,
            bool isRoomCell, System.Random rng)
        {
            if (_catalog == null) return false;

            GameObject[] choices = isRoomCell
                ? _catalog.FreestandingFor(theme.Props)
                : _catalog.AgainstWallFor(theme.Props);
            if (choices == null || choices.Length == 0) return false;

            GameObject model = choices[rng.Next(choices.Length)];
            if (model == null) return false;

            // Keep furniture off the centre line so it never plugs a corridor.
            float offset = cell * 0.28f;
            var pos = new Vector3(
                centre.x + (float)(rng.NextDouble() - 0.5) * offset * 2f,
                0f,
                centre.z + (float)(rng.NextDouble() - 0.5) * offset * 2f);

            GameObject instance = Object.Instantiate(model, pos,
                Quaternion.Euler(0f, rng.Next(4) * 90f, 0f), parent);
            instance.name = model.name;

            // Furniture is scenery: strip colliders so it decorates without snagging movement.
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            {
                Object.Destroy(collider);
            }

            return true;
        }

        /// <summary>
        /// Builds one prop appropriate to the floor's style.
        /// </summary>
        /// <param name="theme">Floor palette and style.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="centre">World centre of the cell.</param>
        /// <param name="cell">Cell size in metres.</param>
        /// <param name="isRoomCell">Whether this is an open room rather than a corridor.</param>
        /// <param name="rng">Seeded generator.</param>
        private void PlaceProp(FloorTheme theme, Transform parent, Vector3 centre, float cell,
            bool isRoomCell, System.Random rng)
        {
            // Keep props off the centre line so they never plug a corridor.
            float offset = cell * 0.32f;
            var pos = new Vector3(
                centre.x + (float)(rng.NextDouble() - 0.5) * offset * 2f,
                0f,
                centre.z + (float)(rng.NextDouble() - 0.5) * offset * 2f);

            switch (theme.Props)
            {
                case PropStyle.Mall:
                    if (isRoomCell) Counter(parent, pos, theme, rng, "Planter", 0.9f);
                    else Facade(parent, pos, theme, rng, "Shopfront");
                    break;

                case PropStyle.Laundromat:
                    Counter(parent, pos, theme, rng, "WashingMachines", 1.15f);
                    break;

                case PropStyle.Carnival:
                    if (isRoomCell) Facade(parent, pos, theme, rng, "Booth");
                    else Pillar(parent, pos, theme, rng, "PostAndBunting");
                    break;

                case PropStyle.Asylum:
                    if (isRoomCell) Counter(parent, pos, theme, rng, "Gurney", 0.8f);
                    else Pillar(parent, pos, theme, rng, "Doorframe");
                    break;

                default:
                    if (isRoomCell) Pillar(parent, pos, theme, rng, "Pillar");
                    else Counter(parent, pos, theme, rng, "StackedBoxes", 1.0f);
                    break;
            }
        }

        /// <summary>
        /// A tall narrow prop: a support pillar, post or doorframe upright.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="pos">World position at floor level.</param>
        /// <param name="theme">Floor palette.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <param name="name">Object name.</param>
        private void Pillar(Transform parent, Vector3 pos, FloorTheme theme, System.Random rng,
            string name)
        {
            // A support column that stops short of the ceiling reads as a random post stuck in the
            // floor, so columns always span the full height and only their girth varies.
            float width = 0.34f + (float)rng.NextDouble() * 0.12f;
            Box(parent, name, pos + Vector3.up * _wallHeight * 0.5f,
                new Vector3(width, _wallHeight, width), theme.Wall);

            // A capital and base in trim colour so the column has some articulation.
            Box(parent, name + "Base", pos + Vector3.up * 0.16f,
                new Vector3(width * 1.35f, 0.32f, width * 1.35f), theme.Trim);
            Box(parent, name + "Capital", pos + Vector3.up * (_wallHeight - 0.16f),
                new Vector3(width * 1.35f, 0.32f, width * 1.35f), theme.Trim);
        }

        /// <summary>
        /// A waist-high block: a counter, bench, machine bank or gurney, with an accent strip on top
        /// so it catches the light.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="pos">World position at floor level.</param>
        /// <param name="theme">Floor palette.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <param name="name">Object name.</param>
        /// <param name="height">Block height in metres.</param>
        private void Counter(Transform parent, Vector3 pos, FloorTheme theme, System.Random rng,
            string name, float height)
        {
            float width = 0.9f + (float)rng.NextDouble() * 0.7f;
            float depth = 0.6f + (float)rng.NextDouble() * 0.3f;

            Box(parent, name, pos + Vector3.up * height * 0.5f,
                new Vector3(width, height, depth), theme.Trim);
            Box(parent, name + "Top", pos + Vector3.up * (height + 0.04f),
                new Vector3(width * 0.98f, 0.08f, depth * 0.98f), theme.Accent);
        }

        /// <summary>
        /// A tall flat prop standing against the space: a shopfront or fairground booth, with a bright
        /// signage band across the top.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="pos">World position at floor level.</param>
        /// <param name="theme">Floor palette.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <param name="name">Object name.</param>
        private void Facade(Transform parent, Vector3 pos, FloorTheme theme, System.Random rng,
            string name)
        {
            float width = 1.4f + (float)rng.NextDouble() * 0.8f;
            const float height = MaxPropHeight;

            Box(parent, name, pos + Vector3.up * height * 0.5f,
                new Vector3(width, height, 0.35f), theme.Trim);
            Box(parent, name + "Sign", pos + Vector3.up * (height - 0.28f),
                new Vector3(width * 1.04f, 0.42f, 0.42f), theme.Accent);
        }

        /// <summary>
        /// Creates a coloured box prop with no collider, so props decorate without snagging movement.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">Object name.</param>
        /// <param name="centre">World centre of the box.</param>
        /// <param name="size">Full size on each axis.</param>
        /// <param name="colour">Base colour.</param>
        private void Box(Transform parent, string name, Vector3 centre, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(colour);
        }

        /// <summary>
        /// Returns a shared material for a colour, creating it once per floor so props batch instead
        /// of each carrying its own material instance.
        /// </summary>
        /// <param name="colour">Base colour.</param>
        /// <returns>A shared material.</returns>
        private Material MaterialFor(Color colour)
        {
            if (_materials.TryGetValue(colour, out Material existing)) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            _materials[colour] = mat;
            return mat;
        }
    }
}
