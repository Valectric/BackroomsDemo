using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Builds the architectural fixtures of a floor that are neither surface nor furniture: the
    /// ceiling lights with their visible troffers, and the structural columns that give a wide hall
    /// scale. Split out of <see cref="MazeGeometryBuilder"/>, which was only getting longer.
    /// </summary>
    internal sealed class MazeFixtureBuilder
    {
        /// <summary>
        /// Scatters point lights below the ceiling on a regular grid so the corridors are lit by
        /// distinct pools of light rather than uniform ambience.
        /// </summary>
        /// <param name="layout">The maze, used for grid extents.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="spacingCells">Light spacing in cells on both axes.</param>
        /// <param name="theme">Palette, which supplies the light colour.</param>
        /// <param name="parent">Parent transform for the light objects.</param>
        public void CreateLights(MazeLayout layout, float cellSize, float wallHeight,
            int spacingCells, FloorTheme theme, Transform parent)
        {
            int spacing = spacingCells < 1 ? 1 : spacingCells;
            var lightsRoot = new GameObject("Lights");
            lightsRoot.transform.SetParent(parent, worldPositionStays: false);

            for (int y = spacing / 2; y < layout.Height; y += spacing)
            {
                for (int x = spacing / 2; x < layout.Width; x += spacing)
                {
                    var go = new GameObject($"CeilingLight_{x}_{y}");
                    go.transform.SetParent(lightsRoot.transform, worldPositionStays: false);
                    // Hung 15cm below the ceiling, N.L collapsed to 0.15 a metre out and the
                    // ceiling — the signature Backrooms surface — was the darkest thing in frame.
                    go.transform.position = new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        wallHeight - 0.45f,
                        y * cellSize + cellSize * 0.5f);

                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = theme.Light;
                    light.intensity = 7.5f;

                    // Range must cover at least three quarters of the fixture pitch or pools cannot
                    // meet: at 7.6m range on a 12m pitch, the point between four fixtures received no
                    // direct light at all. Overshooting the other way lights through walls.
                    light.range = cellSize * spacing * 0.92f;
                    light.shadows = LightShadows.None;

                    CreateLightPanel(go.transform, theme);
                }
            }
        }

        /// <summary>
        /// Adds the visible fluorescent fixture under a ceiling light: a flat emissive panel, so the
        /// player sees where the buzzing light is coming from rather than an unexplained glow.
        /// </summary>
        /// <param name="lightTransform">Transform of the point light to attach the panel to.</param>
        /// <param name="theme">Palette, which supplies the fixture colour.</param>
        private static void CreateLightPanel(Transform lightTransform, FloorTheme theme)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "LightPanel";
            Object.Destroy(panel.GetComponent<MeshCollider>());
            panel.transform.SetParent(lightTransform, worldPositionStays: false);
            // A Unity quad's normal is -Z. Euler(90) aimed it at the ceiling, so the fixture was
            // backface-culled for anyone standing under it and only visible from outside the level.
            panel.transform.localPosition = new Vector3(0f, 0.40f, 0f);
            panel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // A real fluorescent troffer is 1.2m x 0.6m; the panels were nearly twice that.
            panel.transform.localScale = new Vector3(1.2f, 0.6f, 1f);

            // Unlit has no emission, so the panel's brightness has to come from its base colour.
            panel.GetComponent<MeshRenderer>().sharedMaterial = MazeMaterials.Glowing(theme.Light, 3.5f);
        }

        /// <summary>
        /// Raises structural columns through the larger open spaces. These are architecture rather
        /// than furniture: a hall several cells wide with an unbroken ceiling reads as a void, and a
        /// regular grid of columns gives it scale and something to navigate by.
        /// </summary>
        /// <param name="layout">The floor being built.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="theme">Palette for the shaft and trim.</param>
        /// <param name="parent">Parent transform for the columns.</param>
        /// <returns>The cells a column now occupies, so nothing else is placed in them.</returns>
        public HashSet<Vector2Int> CreateColumns(MazeLayout layout, float cellSize, float wallHeight,
            FloorTheme theme, Transform parent)
        {
            var occupied = new HashSet<Vector2Int>();
            var root = new GameObject("Columns");
            root.transform.SetParent(parent, worldPositionStays: false);

            Material shaft = MazeMaterials.Lit(theme.Wall);
            Material trim = MazeMaterials.Lit(theme.Trim);
            const float width = 0.42f;

            // Every third cell, and only where the space is open on all sides, so columns land in
            // halls rather than blocking a corridor.
            // Offset from the ceiling-light lattice (which starts at spacing/2 = 1 and also steps
            // by 3). Sharing it put a point light inside every column capital, which then rendered
            // black because its faces were near-coplanar with the light origin.
            for (int y = 2; y < layout.Height - 1; y += 3)
            {
                for (int x = 2; x < layout.Width - 1; x += 3)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell == layout.Spawn) continue;
                    if (layout.IsStairs(cell) || layout.IsStairsUp(cell)) continue;

                    bool openAllRound = true;
                    foreach (Direction dir in Directions.All)
                    {
                        if (!layout.CanMove(x, y, dir)) openAllRound = false;
                    }

                    if (!openAllRound) continue;

                    Column(root.transform, layout.CellCenterToWorld(cell), width, wallHeight,
                        shaft, trim);
                    occupied.Add(cell);
                }
            }

            return occupied;
        }

        /// <summary>
        /// Builds one column: a full-height shaft with a base and capital.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="pos">Floor-level position.</param>
        /// <param name="width">Shaft width in metres.</param>
        /// <param name="height">Floor-to-ceiling height in metres.</param>
        /// <param name="shaft">Material for the shaft.</param>
        /// <param name="trim">Material for base and capital.</param>
        private static void Column(Transform parent, Vector3 pos, float width, float height,
            Material shaft, Material trim)
        {
            MazeMaterials.Cube(parent, "Column", pos + Vector3.up * height * 0.5f,
                new Vector3(width, height, width), shaft);
            MazeMaterials.Cube(parent, "ColumnBase", pos + Vector3.up * 0.16f,
                new Vector3(width * 1.4f, 0.32f, width * 1.4f), trim);
            MazeMaterials.Cube(parent, "ColumnCapital", pos + Vector3.up * (height - 0.16f),
                new Vector3(width * 1.4f, 0.32f, width * 1.4f), trim);
        }
    }
}
