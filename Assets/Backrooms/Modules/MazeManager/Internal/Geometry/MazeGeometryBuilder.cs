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
        /// <summary>Backrooms wallpaper yellow.</summary>
        private static readonly Color WallColor = new Color(0.84f, 0.78f, 0.48f);

        /// <summary>Damp mustard carpet.</summary>
        private static readonly Color FloorColor = new Color(0.42f, 0.36f, 0.21f);

        /// <summary>Pale ceiling tile.</summary>
        private static readonly Color CeilingColor = new Color(0.80f, 0.78f, 0.67f);

        /// <summary>Warm fluorescent tint.</summary>
        private static readonly Color LightColor = new Color(1f, 0.96f, 0.78f);

        private readonly MazeWallPlanner _planner = new MazeWallPlanner();
        private readonly MazeMeshBuilder _meshBuilder = new MazeMeshBuilder();

        /// <summary>
        /// Builds the geometry for a layout under a new root GameObject.
        /// </summary>
        /// <param name="layout">The maze to realise.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <param name="lightSpacingCells">Place a ceiling light every N cells on both axes.</param>
        /// <param name="parent">Transform to parent the generated root under, may be <c>null</c>.</param>
        /// <returns>The root GameObject containing all generated geometry.</returns>
        public GameObject Build(MazeLayout layout, float cellSize, float wallHeight,
            int lightSpacingCells, Transform parent)
        {
            var root = new GameObject("MazeGeometry");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            float worldWidth = layout.Width * cellSize;
            float worldDepth = layout.Height * cellSize;

            List<WallSegment> walls = _planner.Plan(layout, cellSize);

            CreateMeshObject("Walls", root.transform,
                _meshBuilder.BuildWalls(walls, wallHeight), WallColor, addCollider: true);

            CreateMeshObject("Floor", root.transform,
                _meshBuilder.BuildPlane(worldWidth, worldDepth, 0f, faceUp: true, "MazeFloor"),
                FloorColor, addCollider: true);

            CreateMeshObject("Ceiling", root.transform,
                _meshBuilder.BuildPlane(worldWidth, worldDepth, wallHeight, faceUp: false, "MazeCeiling"),
                CeilingColor, addCollider: false);

            CreateLights(layout, cellSize, wallHeight, lightSpacingCells, root.transform);

            return root;
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
        /// Creates an opaque URP-lit material. Falls back to the legacy standard shader only if the
        /// URP shader is unavailable, so the geometry never renders as magenta.
        /// </summary>
        /// <param name="color">Base colour.</param>
        /// <returns>A new material instance.</returns>
        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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
        private static void CreateLights(MazeLayout layout, float cellSize, float wallHeight,
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
                    light.color = LightColor;
                    light.intensity = 1.6f;
                    light.range = spacing * cellSize * 1.35f;
                    light.shadows = LightShadows.None;
                }
            }
        }
    }
}
