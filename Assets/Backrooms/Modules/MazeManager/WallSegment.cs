using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// One wall panel of the built maze, in world space. Walls are axis-aligned: a segment either
    /// runs along the world X axis or along the world Z axis. The maze grid maps to the world XZ
    /// plane (grid X → world X, grid Y → world Z) with Y up.
    /// </summary>
    public struct WallSegment
    {
        /// <summary>World-space centre of the wall at floor level (Y = 0).</summary>
        public Vector3 Center;

        /// <summary>
        /// <c>true</c> when the wall runs along the world X axis (an east–west panel, blocking
        /// north/south movement); <c>false</c> when it runs along the world Z axis.
        /// </summary>
        public bool AlongX;

        /// <summary>Length of the panel in metres (one cell side).</summary>
        public float Length;

        /// <summary>
        /// Creates a wall segment.
        /// </summary>
        /// <param name="center">World-space centre at floor level.</param>
        /// <param name="alongX">Whether the panel runs along the world X axis.</param>
        /// <param name="length">Panel length in metres.</param>
        public WallSegment(Vector3 center, bool alongX, float length)
        {
            Center = center;
            AlongX = alongX;
            Length = length;
        }
    }
}
