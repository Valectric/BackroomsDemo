using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Generates furniture meshes in code. Everyday objects — desks, chairs, shelving, washing
    /// machines, lockers — are almost entirely boxes and cylinders, so they can be assembled from
    /// primitives at runtime rather than imported. That costs nothing in download size, carries no
    /// asset licence, and every prop is deterministic and therefore testable.
    /// </summary>
    /// <remarks>
    /// Each prop is welded into a single mesh so it draws in one call, and each is authored with its
    /// origin on the floor and centred on X and Z, so callers can place it by position alone.
    /// </remarks>
    internal static class PropMeshLibrary
    {
        /// <summary>
        /// A desk or table: a slab top on four legs.
        /// </summary>
        /// <param name="width">Width in metres.</param>
        /// <param name="depth">Depth in metres.</param>
        /// <param name="height">Height to the top surface, in metres.</param>
        /// <returns>The generated mesh.</returns>
        public static Mesh Table(float width, float depth, float height)
        {
            var b = new MeshParts("Table");
            const float leg = 0.08f;
            float topThickness = 0.07f;

            b.Box(new Vector3(0f, height - topThickness * 0.5f, 0f),
                new Vector3(width, topThickness, depth));

            float lx = width * 0.5f - leg;
            float lz = depth * 0.5f - leg;
            float legHeight = height - topThickness;
            foreach (Vector3 corner in Corners(lx, lz))
            {
                b.Box(new Vector3(corner.x, legHeight * 0.5f, corner.z),
                    new Vector3(leg, legHeight, leg));
            }

            return b.Build();
        }

        /// <summary>
        /// A chair: seat, backrest and four legs.
        /// </summary>
        /// <param name="seatHeight">Seat height in metres.</param>
        /// <returns>The generated mesh.</returns>
        public static Mesh Chair(float seatHeight)
        {
            var b = new MeshParts("Chair");
            const float size = 0.46f;
            const float leg = 0.05f;

            b.Box(new Vector3(0f, seatHeight, 0f), new Vector3(size, 0.06f, size));
            b.Box(new Vector3(0f, seatHeight + 0.28f, -size * 0.45f),
                new Vector3(size, 0.52f, 0.06f));

            float offset = size * 0.5f - leg;
            foreach (Vector3 corner in Corners(offset, offset))
            {
                b.Box(new Vector3(corner.x, seatHeight * 0.5f, corner.z),
                    new Vector3(leg, seatHeight, leg));
            }

            return b.Build();
        }

        /// <summary>
        /// A shelving unit: two uprights, a back panel and evenly spaced shelves.
        /// </summary>
        /// <param name="width">Width in metres.</param>
        /// <param name="height">Height in metres.</param>
        /// <param name="shelves">Number of shelves.</param>
        /// <returns>The generated mesh.</returns>
        public static Mesh Shelving(float width, float height, int shelves)
        {
            var b = new MeshParts("Shelving");
            const float depth = 0.42f;
            const float panel = 0.05f;

            b.Box(new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(panel, height, depth));
            b.Box(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(panel, height, depth));
            b.Box(new Vector3(0f, height * 0.5f, -depth * 0.5f), new Vector3(width, height, 0.03f));

            int count = Mathf.Max(2, shelves);
            for (int i = 0; i < count; i++)
            {
                float y = height * (i + 0.35f) / count;
                b.Box(new Vector3(0f, y, 0f), new Vector3(width - panel, 0.04f, depth));
            }

            return b.Build();
        }

        /// <summary>
        /// A bank of washing machines: a body with recessed round doors and a control strip.
        /// </summary>
        /// <param name="count">How many machines side by side.</param>
        /// <returns>The generated mesh.</returns>
        public static Mesh WashingMachines(int count)
        {
            var b = new MeshParts("WashingMachines");
            const float unit = 0.68f;
            const float height = 0.92f;
            const float depth = 0.62f;

            int n = Mathf.Max(1, count);
            float total = unit * n;

            b.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(total, height, depth));

            for (int i = 0; i < n; i++)
            {
                float x = -total * 0.5f + unit * (i + 0.5f);
                // Door: a shallow disc proud of the front face.
                b.Cylinder(new Vector3(x, height * 0.52f, depth * 0.5f + 0.02f), 0.19f, 0.05f, 14);
                // Control strip above the door.
                b.Box(new Vector3(x, height - 0.10f, depth * 0.5f + 0.01f),
                    new Vector3(unit * 0.7f, 0.09f, 0.03f));
            }

            return b.Build();
        }

        /// <summary>
        /// A bank of lockers: tall cabinets with door seams and handles.
        /// </summary>
        /// <param name="count">How many lockers side by side.</param>
        /// <param name="height">Height in metres.</param>
        /// <returns>The generated mesh.</returns>
        public static Mesh Lockers(int count, float height)
        {
            var b = new MeshParts("Lockers");
            const float unit = 0.42f;
            const float depth = 0.45f;

            int n = Mathf.Max(1, count);
            float total = unit * n;
            b.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(total, height, depth));

            for (int i = 0; i < n; i++)
            {
                float x = -total * 0.5f + unit * (i + 0.5f);
                // Door face, slightly proud so the seam between lockers reads.
                b.Box(new Vector3(x, height * 0.5f, depth * 0.5f + 0.015f),
                    new Vector3(unit * 0.88f, height * 0.94f, 0.03f));
                b.Box(new Vector3(x + unit * 0.28f, height * 0.55f, depth * 0.5f + 0.04f),
                    new Vector3(0.05f, 0.16f, 0.03f));
            }

            return b.Build();
        }

        /// <summary>
        /// A wheeled gurney or trolley: a padded top on a frame with castors.
        /// </summary>
        /// <returns>The generated mesh.</returns>
        public static Mesh Gurney()
        {
            var b = new MeshParts("Gurney");
            const float length = 1.85f;
            const float width = 0.68f;
            const float deck = 0.72f;

            b.Box(new Vector3(0f, deck, 0f), new Vector3(width, 0.14f, length));
            b.Box(new Vector3(0f, deck + 0.20f, -length * 0.42f), new Vector3(width * 0.9f, 0.3f, 0.05f));

            float lx = width * 0.5f - 0.08f;
            float lz = length * 0.5f - 0.14f;
            foreach (Vector3 corner in Corners(lx, lz))
            {
                b.Box(new Vector3(corner.x, deck * 0.5f, corner.z), new Vector3(0.05f, deck, 0.05f));
                b.Cylinder(new Vector3(corner.x, 0.07f, corner.z), 0.07f, 0.04f, 10);
            }

            return b.Build();
        }

        /// <summary>
        /// A stack of crates or boxes, leaning slightly so it does not look machine-placed.
        /// </summary>
        /// <param name="seed">Seed for the stack arrangement.</param>
        /// <returns>The generated mesh.</returns>
        public static Mesh CrateStack(int seed)
        {
            var b = new MeshParts("CrateStack");
            var rng = new System.Random(seed);

            float y = 0f;
            int crates = 2 + rng.Next(3);
            for (int i = 0; i < crates; i++)
            {
                float size = 0.42f + (float)rng.NextDouble() * 0.22f;
                float jitterX = (float)(rng.NextDouble() - 0.5) * 0.14f;
                float jitterZ = (float)(rng.NextDouble() - 0.5) * 0.14f;
                b.Box(new Vector3(jitterX, y + size * 0.5f, jitterZ), new Vector3(size, size, size));
                y += size;
            }

            return b.Build();
        }

        /// <summary>
        /// The four corner offsets of a rectangle.
        /// </summary>
        /// <param name="x">Half-width.</param>
        /// <param name="z">Half-depth.</param>
        /// <returns>Four corner positions on the XZ plane.</returns>
        private static IEnumerable<Vector3> Corners(float x, float z)
        {
            yield return new Vector3(-x, 0f, -z);
            yield return new Vector3(x, 0f, -z);
            yield return new Vector3(-x, 0f, z);
            yield return new Vector3(x, 0f, z);
        }
    }
}
