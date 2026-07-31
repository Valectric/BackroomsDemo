using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Turns a planned maze into actual scene geometry: a combined wall object, a floor, a ceiling
    /// and the fluorescent ceiling lights that give Level 0 its look. Everything is parented under a
    /// single root so a rebuild can destroy and recreate it cleanly.
    /// </summary>
    /// <remarks>
    /// This is a plain C# class; the owning <see cref="MazeFacade"/> MonoBehaviour passes in the
    /// parent transform, so no hidden Unity lookups happen here.
    /// </remarks>
    internal sealed class MazeGeometryBuilder
    {
        /// <summary>Palette for the floor currently being built.</summary>
        private FloorTheme _theme = FloorThemes.ForFloor(1);

        private readonly MazeWallPlanner _planner = new MazeWallPlanner();
        private readonly MazeMeshBuilder _meshBuilder = new MazeMeshBuilder();
        private readonly PropDecorator _decorator = new PropDecorator();

        /// <summary>Seed used for this floor's generated textures.</summary>
        private int _themeSeed;

        /// <summary>
        /// Builds the geometry for a layout under a new root GameObject.
        /// </summary>
        /// <param name="layout">The maze to realise.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="lightSpacingCells">Place a ceiling light every N cells on both axes.</param>
        /// <param name="parent">Transform to parent the generated root under, may be <c>null</c>.</param>
        /// <param name="theme">Palette for the floor being built.</param>
        /// <returns>The root GameObject containing all generated geometry.</returns>
        public GameObject Build(MazeLayout layout, float cellSize, float wallHeight,
            int lightSpacingCells, Transform parent, FloorTheme theme)
        {
            _theme = theme ?? FloorThemes.ForFloor(1);
            _themeSeed = Mathf.Abs(_theme.Name.GetHashCode()) % 9973;
            var root = new GameObject("MazeGeometry");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            float worldWidth = layout.Width * cellSize;
            float worldDepth = layout.Height * cellSize;

            List<WallSegment> walls = _planner.Plan(layout, cellSize);
            CreateChunkedWalls(walls, cellSize, wallHeight, root.transform);

            CreateMeshObject("Trim", root.transform,
                _meshBuilder.BuildTrim(walls, 0.28f, 0.22f), _theme.Trim, addCollider: false);

            CreateTexturedMeshObject("Floor", root.transform,
                _meshBuilder.BuildPlane(worldWidth, worldDepth, 0f, faceUp: true, "MazeFloor"),
                _theme.Floor, ProceduralTextures.Carpet(_theme.Floor, _themeSeed), addCollider: true);

            CreateTexturedMeshObject("Ceiling", root.transform,
                _meshBuilder.BuildPlane(worldWidth, worldDepth, wallHeight, faceUp: false, "MazeCeiling"),
                _theme.Ceiling, ProceduralTextures.CeilingTiles(_theme.Ceiling, _themeSeed),
                addCollider: false);

            CreateLights(layout, cellSize, wallHeight, lightSpacingCells, root.transform);
            CreateExitMarker(layout, wallHeight, root.transform);
            HashSet<Vector2Int> columnCells = CreateColumns(layout, cellSize, wallHeight, root.transform);
            _decorator.Decorate(layout, _theme, layout.Width * 31 + layout.Height, root.transform,
                columnCells);

            return root;
        }

        /// <summary>
        /// Places a glowing green marker in the exit cell so the player has something to find. The
        /// marker is a trigger-free visual only; reaching the exit is decided by distance in the
        /// gameplay layer.
        /// </summary>
        /// <param name="layout">The maze, used to locate the exit cell.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="parent">Parent transform for the marker.</param>
        private static void CreateExitMarker(MazeLayout layout, float wallHeight, Transform parent)
        {
            Vector3 pos = layout.CellCenterToWorld(layout.Exit);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ExitMarker";
            Object.Destroy(marker.GetComponent<BoxCollider>());
            marker.transform.SetParent(parent, worldPositionStays: false);
            marker.transform.position = new Vector3(pos.x, wallHeight * 0.5f, pos.z);
            marker.transform.localScale = new Vector3(0.6f, wallHeight * 0.9f, 0.6f);

            var exitColor = new Color(0.35f, 1f, 0.45f);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", exitColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", exitColor);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", exitColor * 2.5f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var glow = new GameObject("ExitGlow");
            glow.transform.SetParent(marker.transform, worldPositionStays: false);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = exitColor;
            light.intensity = 3f;
            light.range = 12f;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Raises structural columns through the larger open spaces. These are architecture rather
        /// than furniture: a hall several cells wide with an unbroken ceiling reads as a void, and a
        /// regular grid of columns gives it scale and something to navigate by.
        /// </summary>
        /// <param name="layout">The floor being built.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="parent">Parent transform for the columns.</param>
        /// <returns>The cells a column now occupies, so nothing else is placed in them.</returns>
        private HashSet<Vector2Int> CreateColumns(MazeLayout layout, float cellSize, float wallHeight,
            Transform parent)
        {
            var occupied = new HashSet<Vector2Int>();
            var root = new GameObject("Columns");
            root.transform.SetParent(parent, worldPositionStays: false);

            Material shaft = CreateMaterial(_theme.Wall);
            Material trim = CreateMaterial(_theme.Trim);
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
                    if (new Vector2Int(x, y) == layout.Spawn) continue;
                    if (new Vector2Int(x, y) == layout.Exit) continue;

                    bool openAllRound = true;
                    foreach (Direction dir in Directions.All)
                    {
                        if (!layout.CanMove(x, y, dir)) openAllRound = false;
                    }

                    if (!openAllRound) continue;

                    var cell = new Vector2Int(x, y);
                    Vector3 pos = layout.CellCenterToWorld(cell);
                    Column(root.transform, pos, width, wallHeight, shaft, trim);
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
            Cube(parent, "Column", pos + Vector3.up * height * 0.5f,
                new Vector3(width, height, width), shaft);
            Cube(parent, "ColumnBase", pos + Vector3.up * 0.16f,
                new Vector3(width * 1.4f, 0.32f, width * 1.4f), trim);
            Cube(parent, "ColumnCapital", pos + Vector3.up * (height - 0.16f),
                new Vector3(width * 1.4f, 0.32f, width * 1.4f), trim);
        }

        /// <summary>
        /// Creates a collider-free cube with the given material.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">Object name.</param>
        /// <param name="centre">World centre.</param>
        /// <param name="size">Full size on each axis.</param>
        /// <param name="material">Material to render with.</param>
        private static void Cube(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Cells per side of one wall chunk. Walls are split into chunks rather than combined into
        /// a single mesh because URP applies its additional-light limit <b>per renderer</b>: one
        /// giant wall mesh can only ever be lit by a handful of the ceiling lights, which makes the
        /// whole level look flat. Chunking gives each part of the maze its own light budget, at the
        /// cost of a few more draw calls.
        /// </summary>
        private const int WallChunkCells = 3;

        /// <summary>
        /// Builds the maze walls as a grid of chunk meshes sharing one material.
        /// </summary>
        /// <param name="walls">All planned wall segments.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="parent">Parent transform for the chunk objects.</param>
        private void CreateChunkedWalls(List<WallSegment> walls, float cellSize, float wallHeight,
            Transform parent)
        {
            float chunkWorldSize = cellSize * WallChunkCells;
            var chunks = new Dictionary<Vector2Int, List<WallSegment>>();

            foreach (WallSegment w in walls)
            {
                var key = new Vector2Int(
                    Mathf.FloorToInt(w.Center.x / chunkWorldSize),
                    Mathf.FloorToInt(w.Center.z / chunkWorldSize));
                if (!chunks.TryGetValue(key, out List<WallSegment> list))
                {
                    list = new List<WallSegment>();
                    chunks[key] = list;
                }

                list.Add(w);
            }

            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(parent, worldPositionStays: false);

            Material shared = CreateTexturedMaterial(
                _theme.Wall, ProceduralTextures.Wall(_theme.Wall, _themeSeed));
            foreach (KeyValuePair<Vector2Int, List<WallSegment>> chunk in chunks)
            {
                var go = new GameObject($"WallChunk_{chunk.Key.x}_{chunk.Key.y}");
                go.transform.SetParent(wallsRoot.transform, worldPositionStays: false);

                Mesh mesh = _meshBuilder.BuildWalls(chunk.Value, wallHeight);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = shared;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Creates a child GameObject rendering a mesh with a generated texture.
        /// </summary>
        /// <param name="name">Child object name.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="mesh">Mesh to render.</param>
        /// <param name="colour">Base colour.</param>
        /// <param name="texture">Generated surface texture.</param>
        /// <param name="addCollider">Whether to add a mesh collider.</param>
        private static void CreateTexturedMeshObject(string name, Transform parent, Mesh mesh,
            Color colour, Texture2D texture, bool addCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = CreateTexturedMaterial(colour, texture);
            if (addCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>
        /// Creates a child GameObject that renders the given mesh with a URP-lit material of the
        /// given colour, optionally with a matching mesh collider.
        /// </summary>
        /// <param name="name">Child object name.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="mesh">Mesh to render.</param>
        /// <param name="color">Base colour for the material.</param>
        /// <param name="addCollider">Whether to add a <see cref="MeshCollider"/>.</param>
        private static void CreateMeshObject(string name, Transform parent, Mesh mesh, Color color,
            bool addCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = CreateMaterial(color);
            if (addCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>
        /// Creates a URP-lit material carrying a generated texture, so surfaces have grain and
        /// pattern instead of reading as flat blocks of colour.
        /// </summary>
        /// <param name="colour">Base colour.</param>
        /// <param name="texture">Generated surface texture.</param>
        /// <returns>A new textured material.</returns>
        private static Material CreateTexturedMaterial(Color colour, Texture2D texture)
        {
            Material mat = CreateMaterial(colour);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            mat.mainTexture = texture;
            return mat;
        }

        /// <summary>
        /// Creates an opaque URP-lit material. Falls back to the legacy standard shader only if the
        /// URP shader is unavailable, so the geometry never renders as magenta.
        /// </summary>
        /// <param name="color">Base colour.</param>
        /// <returns>A new material instance.</returns>
        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                // In a player build a shader reached only via Shader.Find is stripped unless it is
                // in Graphics Settings' always-included list, and Unity silently swaps in the
                // magenta error shader. Say so loudly instead of shipping a pink level.
                Debug.LogError("[Maze] URP Lit shader missing from the build — geometry will render "
                               + "magenta. Run Backrooms/Ensure Always-Included Shaders.");
                return new Material(Shader.Find("Sprites/Default"));
            }

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            return mat;
        }

        /// <summary>
        /// Scatters point lights below the ceiling on a regular grid so the corridors are lit by
        /// distinct pools of light rather than uniform ambience.
        /// </summary>
        /// <param name="layout">The maze, used for grid extents.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="spacingCells">Light spacing in cells on both axes.</param>
        /// <param name="parent">Parent transform for the light objects.</param>
        private void CreateLights(MazeLayout layout, float cellSize, float wallHeight,
            int spacingCells, Transform parent)
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
                    go.transform.position = new Vector3(
                        x * cellSize + cellSize * 0.5f,
                        wallHeight - 0.15f,
                        y * cellSize + cellSize * 0.5f);

                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = _theme.Light;
                    light.intensity = 4.2f;

                    // Range was twice the fixture spacing, so every light reached six cells through
                    // solid walls and nowhere was ever dark. Keep it just past the neighbouring cell.
                    light.range = cellSize * 1.9f;
                    light.shadows = LightShadows.None;

                    CreateLightPanel(go.transform, cellSize);
                }
            }
        }

        /// <summary>
        /// Adds the visible fluorescent fixture under a ceiling light: a flat emissive panel, so the
        /// player sees where the buzzing light is coming from rather than an unexplained glow.
        /// </summary>
        /// <param name="lightTransform">Transform of the point light to attach the panel to.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        private void CreateLightPanel(Transform lightTransform, float cellSize)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "LightPanel";
            Object.Destroy(panel.GetComponent<MeshCollider>());
            panel.transform.SetParent(lightTransform, worldPositionStays: false);
            // A Unity quad's normal is -Z. Euler(90) aimed it at the ceiling, so the fixture was
            // backface-culled for anyone standing under it and only visible from outside the level.
            panel.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            panel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // A real fluorescent troffer is 1.2m x 0.6m; the panels were nearly twice that.
            panel.transform.localScale = new Vector3(1.2f, 0.6f, 1f);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _theme.Light);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", _theme.Light);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", _theme.Light * 2f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
