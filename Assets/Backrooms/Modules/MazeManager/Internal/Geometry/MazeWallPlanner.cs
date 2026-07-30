using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Pure-logic submodule that decides <b>where</b> walls go for a given <see cref="MazeLayout"/>.
    /// It emits one <see cref="WallSegment"/> per closed cell edge, without touching Unity scene
    /// objects, so wall placement is fully unit-testable independently of mesh building.
    /// </summary>
    /// <remarks>
    /// To avoid emitting a wall twice for the same shared edge, each cell only contributes its
    /// North and East edges. The outer South and West boundaries are added once for the bottom row
    /// and left column respectively.
    /// </remarks>
    internal sealed class MazeWallPlanner
    {
        /// <summary>
        /// Produces the wall segments for a layout.
        /// </summary>
        /// <param name="layout">The maze to build walls for.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <returns>All wall segments in world space.</returns>
        public List<WallSegment> Plan(MazeLayout layout, float cellSize)
        {
            var walls = new List<WallSegment>();
            float half = cellSize * 0.5f;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    MazeCell cell = layout.CellAt(x, y);
                    float cx = x * cellSize + half;
                    float cz = y * cellSize + half;

                    // North edge (world +Z): panel runs along X.
                    if (!cell.NorthOpen)
                    {
                        walls.Add(new WallSegment(
                            new Vector3(cx, 0f, cz + half), alongX: true, cellSize));
                    }

                    // East edge (world +X): panel runs along Z.
                    if (!cell.EastOpen)
                    {
                        walls.Add(new WallSegment(
                            new Vector3(cx + half, 0f, cz), alongX: false, cellSize));
                    }

                    // Outer boundaries, added once so shared edges are not duplicated.
                    if (y == 0 && !cell.SouthOpen)
                    {
                        walls.Add(new WallSegment(
                            new Vector3(cx, 0f, cz - half), alongX: true, cellSize));
                    }

                    if (x == 0 && !cell.WestOpen)
                    {
                        walls.Add(new WallSegment(
                            new Vector3(cx - half, 0f, cz), alongX: false, cellSize));
                    }
                }
            }

            return walls;
        }
    }
}
