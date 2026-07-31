using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Accumulates primitive shapes into a single welded mesh. Composing furniture from boxes and
    /// cylinders and then welding the result means a whole desk or locker bank is one mesh and one
    /// draw call, which is what makes generated props affordable on a mobile browser build.
    /// </summary>
    internal sealed class MeshParts
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _tris = new List<int>();
        private readonly string _name;

        /// <summary>
        /// Starts a new mesh under construction.
        /// </summary>
        /// <param name="name">Name given to the finished mesh.</param>
        public MeshParts(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Adds an axis-aligned box.
        /// </summary>
        /// <param name="centre">Centre of the box.</param>
        /// <param name="size">Full size on each axis.</param>
        public void Box(Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            int b = _verts.Count;

            _verts.Add(centre + new Vector3(-h.x, -h.y, -h.z));
            _verts.Add(centre + new Vector3(h.x, -h.y, -h.z));
            _verts.Add(centre + new Vector3(h.x, -h.y, h.z));
            _verts.Add(centre + new Vector3(-h.x, -h.y, h.z));
            _verts.Add(centre + new Vector3(-h.x, h.y, -h.z));
            _verts.Add(centre + new Vector3(h.x, h.y, -h.z));
            _verts.Add(centre + new Vector3(h.x, h.y, h.z));
            _verts.Add(centre + new Vector3(-h.x, h.y, h.z));

            for (int i = 0; i < 8; i++) _uvs.Add(new Vector2(0.5f, 0.5f));

            int[] faces =
            {
                4, 6, 5, 4, 7, 6,
                0, 1, 2, 0, 2, 3,
                0, 5, 1, 0, 4, 5,
                3, 2, 6, 3, 6, 7,
                1, 6, 2, 1, 5, 6,
                0, 3, 7, 0, 7, 4
            };

            foreach (int f in faces) _tris.Add(b + f);
        }

        /// <summary>
        /// Adds a cylinder lying along the Z axis, used for machine doors and castors.
        /// </summary>
        /// <param name="centre">Centre of the cylinder.</param>
        /// <param name="radius">Radius in metres.</param>
        /// <param name="length">Length along Z in metres.</param>
        /// <param name="sides">Number of radial segments.</param>
        public void Cylinder(Vector3 centre, float radius, float length, int sides)
        {
            int n = Mathf.Max(6, sides);
            float half = length * 0.5f;
            int baseIndex = _verts.Count;

            for (int i = 0; i < n; i++)
            {
                float angle = i / (float)n * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                _verts.Add(centre + new Vector3(x, y, -half));
                _verts.Add(centre + new Vector3(x, y, half));
                _uvs.Add(new Vector2(0.5f, 0.5f));
                _uvs.Add(new Vector2(0.5f, 0.5f));
            }

            // Side wall.
            for (int i = 0; i < n; i++)
            {
                int a = baseIndex + i * 2;
                int bb = baseIndex + ((i + 1) % n) * 2;
                _tris.Add(a); _tris.Add(bb); _tris.Add(a + 1);
                _tris.Add(bb); _tris.Add(bb + 1); _tris.Add(a + 1);
            }

            // End caps.
            int frontCentre = _verts.Count;
            _verts.Add(centre + new Vector3(0f, 0f, half));
            _uvs.Add(new Vector2(0.5f, 0.5f));
            int backCentre = _verts.Count;
            _verts.Add(centre + new Vector3(0f, 0f, -half));
            _uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < n; i++)
            {
                int a = baseIndex + i * 2;
                int bb = baseIndex + ((i + 1) % n) * 2;
                _tris.Add(frontCentre); _tris.Add(a + 1); _tris.Add(bb + 1);
                _tris.Add(backCentre); _tris.Add(bb); _tris.Add(a);
            }
        }

        /// <summary>
        /// Welds everything added so far into a finished mesh.
        /// </summary>
        /// <returns>The completed mesh.</returns>
        public Mesh Build()
        {
            var mesh = new Mesh { name = _name };
            mesh.indexFormat = _verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
