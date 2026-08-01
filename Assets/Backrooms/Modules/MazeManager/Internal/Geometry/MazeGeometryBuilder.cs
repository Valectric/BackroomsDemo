using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Turns a planned maze into actual scene geometry: chunked walls, skirting, floor and ceiling,
    /// the fluorescent lights, the stairwells down to the next floor, and the furniture. Everything
    /// is parented under a single root so a rebuild can destroy and recreate it cleanly.
    /// </summary>
    /// <remarks>
    /// This is a plain C# class; the owning <see cref="MazeFacade"/> MonoBehaviour passes in the
    /// parent transform, so no hidden Unity lookups happen here. Fixtures, stairwells and furniture
    /// each have their own builder — this class only decides the order they run in.
    /// </remarks>
    internal sealed class MazeGeometryBuilder
    {
        /// <summary>Palette for the floor currently being built.</summary>
        private FloorTheme _theme = FloorThemes.ForFloor(1);

        private readonly MazeWallPlanner _planner = new MazeWallPlanner();
        private readonly MazeMeshBuilder _meshBuilder = new MazeMeshBuilder();
        private readonly MazeFixtureBuilder _fixtures = new MazeFixtureBuilder();
        private readonly StairwellBuilder _stairwells = new StairwellBuilder();
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

            CreateFloor(layout, cellSize, worldWidth, worldDepth, root.transform);

            CreateTexturedMeshObject("Ceiling", root.transform,
                _meshBuilder.BuildPlane(worldWidth, worldDepth, wallHeight, faceUp: false, "MazeCeiling"),
                _theme.Ceiling, ProceduralTextures.CeilingTiles(_theme.Ceiling, _themeSeed),
                addCollider: false);

            _fixtures.CreateLights(layout, cellSize, wallHeight, lightSpacingCells, _theme,
                root.transform);
            CreateStairwells(layout, wallHeight, root.transform);

            HashSet<Vector2Int> columnCells =
                _fixtures.CreateColumns(layout, cellSize, wallHeight, _theme, root.transform);
            _decorator.Decorate(layout, _theme, layout.Width * 31 + layout.Height, root.transform,
                columnCells);

            return root;
        }

        /// <summary>
        /// Builds the floor: a rendered surface with a hole cut for every stairwell, plus a separate,
        /// unbroken collider. Keeping the collider whole is deliberate — the stairwells are set
        /// dressing for a floor change that fires on proximity, and a player who fell into one would
        /// be stuck in a decorative pit with no way out.
        /// </summary>
        /// <param name="layout">The maze being built.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="worldWidth">Grid width in metres.</param>
        /// <param name="worldDepth">Grid depth in metres.</param>
        /// <param name="parent">Parent transform.</param>
        private void CreateFloor(MazeLayout layout, float cellSize, float worldWidth, float worldDepth,
            Transform parent)
        {
            var holes = new HashSet<Vector2Int>(layout.Stairs);
            CreateTexturedMeshObject("Floor", parent,
                _meshBuilder.BuildFloorWithHoles(layout.Width, layout.Height, cellSize, holes),
                _theme.Floor, ProceduralTextures.Carpet(_theme.Floor, _themeSeed), addCollider: false);

            var collider = new GameObject("FloorCollider");
            collider.transform.SetParent(parent, worldPositionStays: false);
            collider.AddComponent<MeshCollider>().sharedMesh =
                _meshBuilder.BuildPlane(worldWidth, worldDepth, 0f, faceUp: true, "MazeFloorCollider");
        }

        /// <summary>
        /// Sinks a stairwell into every cell the layout marks as one.
        /// </summary>
        /// <param name="layout">The maze being built.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="parent">Parent transform.</param>
        private void CreateStairwells(MazeLayout layout, float wallHeight, Transform parent)
        {
            var root = new GameObject("Stairwells");
            root.transform.SetParent(parent, worldPositionStays: false);

            foreach (Vector2Int cell in layout.Stairs)
            {
                _stairwells.Build(layout, cell, wallHeight, _theme, root.transform);
            }
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

            Material shared = MazeMaterials.Textured(
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
            go.AddComponent<MeshRenderer>().sharedMaterial = MazeMaterials.Textured(colour, texture);
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
            go.AddComponent<MeshRenderer>().sharedMaterial = MazeMaterials.Lit(color);
            if (addCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
