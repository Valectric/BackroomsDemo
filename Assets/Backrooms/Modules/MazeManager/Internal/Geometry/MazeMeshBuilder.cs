using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Builds the maze's renderable meshes: one combined wall mesh, a floor quad and a ceiling quad.
    /// Combining every wall into a single mesh keeps the draw-call count low, which matters for the
    /// mobile WebGL target. Walls are double-sided (separate vertices per face) so normals come out
    /// correct from <see cref="Mesh.RecalculateNormals"/>.
    /// </summary>
    internal sealed class MazeMeshBuilder
    {
        /// <summary>World size in metres that one texture tile covers.</summary>
        private const float UvScale = 2f;

        /// <summary>
        /// Builds one mesh containing every wall panel.
        /// </summary>
        /// <param name="walls">Planned wall segments.</param>
        /// <param name="wallHeight">Wall height in metres.</param>
        /// <returns>A combined double-sided wall mesh.</returns>
        public Mesh BuildWalls(List<WallSegment> walls, float wallHeight)
        {
            var verts = new List<Vector3>(walls.Count * 8);
            var uvs = new List<Vector2>(walls.Count * 8);
            var tris = new List<int>(walls.Count * 12);

            foreach (WallSegment w in walls)
            {
                float half = w.Length * 0.5f;
                Vector3 a, b;
                if (w.AlongX)
                {
                    a = new Vector3(w.Center.x - half, 0f, w.Center.z);
                    b = new Vector3(w.Center.x + half, 0f, w.Center.z);
                }
                else
                {
                    a = new Vector3(w.Center.x, 0f, w.Center.z - half);
                    b = new Vector3(w.Center.x, 0f, w.Center.z + half);
                }

                AddQuad(verts, uvs, tris, a, b, wallHeight, w.Length);
            }

            var mesh = new Mesh { name = "MazeWalls" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds one mesh of low boxes running along every wall, used as skirting. A solid box
        /// stands proud of the wall on both sides, so it reads as trim instead of z-fighting with the
        /// wall plane, and it gives the flat surfaces a floor line to sit on.
        /// </summary>
        /// <param name="walls">Planned wall segments.</param>
        /// <param name="height">Trim height in metres.</param>
        /// <param name="thickness">Total trim thickness in metres.</param>
        /// <returns>A combined trim mesh.</returns>
        public Mesh BuildTrim(List<WallSegment> walls, float height, float thickness)
        {
            var verts = new List<Vector3>(walls.Count * 24);
            var tris = new List<int>(walls.Count * 36);

            foreach (WallSegment w in walls)
            {
                float halfLen = w.Length * 0.5f;
                float halfThick = thickness * 0.5f;

                // Let the X-aligned runs own every junction. Without this the two runs overlap in a
                // square at each corner with exactly coplanar top faces, which z-fights.
                if (!w.AlongX) halfLen -= halfThick;

                Vector3 size = w.AlongX
                    ? new Vector3(halfLen, height * 0.5f, halfThick)
                    : new Vector3(halfThick, height * 0.5f, halfLen);
                var centre = new Vector3(w.Center.x, height * 0.5f, w.Center.z);

                AddBox(verts, tris, centre, size);
            }

            var mesh = new Mesh { name = "MazeTrim" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Appends an axis-aligned box to a mesh under construction.
        /// </summary>
        /// <param name="verts">Vertex accumulator.</param>
        /// <param name="tris">Triangle-index accumulator.</param>
        /// <param name="centre">Box centre.</param>
        /// <param name="half">Half-extents on each axis.</param>
        private static void AddBox(List<Vector3> verts, List<int> tris, Vector3 centre, Vector3 half)
        {
            int b = verts.Count;

            verts.Add(centre + new Vector3(-half.x, -half.y, -half.z));
            verts.Add(centre + new Vector3(half.x, -half.y, -half.z));
            verts.Add(centre + new Vector3(half.x, -half.y, half.z));
            verts.Add(centre + new Vector3(-half.x, -half.y, half.z));
            verts.Add(centre + new Vector3(-half.x, half.y, -half.z));
            verts.Add(centre + new Vector3(half.x, half.y, -half.z));
            verts.Add(centre + new Vector3(half.x, half.y, half.z));
            verts.Add(centre + new Vector3(-half.x, half.y, half.z));

            int[] faces =
            {
                4, 6, 5, 4, 7, 6, // top
                0, 1, 2, 0, 2, 3, // bottom
                0, 5, 1, 0, 4, 5, // -z
                3, 2, 6, 3, 6, 7, // +z
                1, 6, 2, 1, 5, 6, // +x
                0, 3, 7, 0, 7, 4  // -x
            };

            foreach (int f in faces) tris.Add(b + f);
        }

        /// <summary>
        /// Builds a single horizontal quad covering the whole grid, facing up (floor) or down
        /// (ceiling).
        /// </summary>
        /// <param name="worldWidth">Grid width in metres.</param>
        /// <param name="worldDepth">Grid depth in metres.</param>
        /// <param name="y">Height of the plane in metres.</param>
        /// <param name="faceUp"><c>true</c> for a floor, <c>false</c> for a ceiling.</param>
        /// <param name="name">Mesh name.</param>
        /// <returns>The plane mesh.</returns>
        public Mesh BuildPlane(float worldWidth, float worldDepth, float y, bool faceUp, string name)
        {
            var verts = new List<Vector3>
            {
                new Vector3(0f, y, 0f),
                new Vector3(worldWidth, y, 0f),
                new Vector3(worldWidth, y, worldDepth),
                new Vector3(0f, y, worldDepth)
            };

            var uvs = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(worldWidth / UvScale, 0f),
                new Vector2(worldWidth / UvScale, worldDepth / UvScale),
                new Vector2(0f, worldDepth / UvScale)
            };

            var tris = faceUp
                ? new List<int> { 0, 3, 2, 0, 2, 1 }
                : new List<int> { 0, 1, 2, 0, 2, 3 };

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Appends a double-sided vertical quad standing on the segment from <paramref name="a"/> to
        /// <paramref name="b"/>. Front and back faces get their own vertices so recalculated normals
        /// point in opposite directions.
        /// </summary>
        /// <param name="verts">Vertex accumulator.</param>
        /// <param name="uvs">UV accumulator.</param>
        /// <param name="tris">Triangle-index accumulator.</param>
        /// <param name="a">Base start point.</param>
        /// <param name="b">Base end point.</param>
        /// <param name="height">Wall height in metres.</param>
        /// <param name="length">Panel length, used for UV tiling.</param>
        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 a, Vector3 b, float height, float length)
        {
            Vector3 up = new Vector3(0f, height, 0f);
            float u = length / UvScale;

            // One texture height per wall. The wall texture bakes a floor-to-ceiling grime gradient,
            // which is height-anchored content rather than tiling content: repeating it vertically
            // snapped brightness back by 14% partway up every wall in the game.
            const float v = 1f;

            int start = verts.Count;

            // Front face.
            verts.Add(a);
            verts.Add(b);
            verts.Add(b + up);
            verts.Add(a + up);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, v));
            uvs.Add(new Vector2(0f, v));
            tris.Add(start + 0); tris.Add(start + 2); tris.Add(start + 1);
            tris.Add(start + 0); tris.Add(start + 3); tris.Add(start + 2);

            // Back face (own vertices, reversed winding).
            int back = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(b + up);
            verts.Add(a + up);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, v));
            uvs.Add(new Vector2(0f, v));
            tris.Add(back + 0); tris.Add(back + 1); tris.Add(back + 2);
            tris.Add(back + 0); tris.Add(back + 2); tris.Add(back + 3);
        }
    }
}
