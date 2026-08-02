using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Bakes a floor's walls into a small texture the HUD can draw as a map.
    /// </summary>
    /// <remarks>
    /// Built once per floor rather than drawn per frame: a 24x24 grid is up to a thousand wall
    /// segments, and issuing that many IMGUI draw calls every frame to show a corner of the screen
    /// would cost more than the rest of the HUD together. One texture is one draw call.
    /// <para>
    /// It lives in the Application layer because it is the seam between two modules — MazeManager
    /// owns the layout and UIManager owns the drawing, and neither may reference the other.
    /// </para>
    /// </remarks>
    internal static class FloorMap
    {
        /// <summary>Pixels per cell. Enough for a wall line and an open middle.</summary>
        private const int CellPixels = 8;

        /// <summary>Open floor.</summary>
        private static readonly Color32 Open = new Color32(196, 190, 168, 190);

        /// <summary>Wall.</summary>
        private static readonly Color32 Wall = new Color32(18, 17, 14, 235);

        /// <summary>A way down, so the map is worth reading rather than merely orienting.</summary>
        private static readonly Color32 Down = new Color32(90, 230, 120, 255);

        /// <summary>
        /// Draws a floor's walls and ways down into a fresh texture.
        /// </summary>
        /// <param name="layout">The floor to draw.</param>
        /// <returns>The map texture, or <c>null</c> when there is no layout.</returns>
        public static Texture2D Build(MazeLayout layout)
        {
            if (layout == null) return null;

            int w = layout.Width * CellPixels;
            int h = layout.Height * CellPixels;
            var pixels = new Color32[w * h];

            for (int cellY = 0; cellY < layout.Height; cellY++)
            {
                for (int cellX = 0; cellX < layout.Width; cellX++)
                {
                    PaintCell(layout, pixels, w, cellX, cellY);
                }
            }

            var map = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            map.SetPixels32(pixels);
            map.Apply();
            return map;
        }

        /// <summary>
        /// Paints one cell: its open middle, then a wall line on each closed side.
        /// </summary>
        /// <param name="layout">The floor being drawn.</param>
        /// <param name="pixels">Destination pixels.</param>
        /// <param name="stride">Texture width in pixels.</param>
        /// <param name="cellX">Cell column.</param>
        /// <param name="cellY">Cell row.</param>
        private static void PaintCell(MazeLayout layout, Color32[] pixels, int stride, int cellX,
            int cellY)
        {
            MazeCell cell = layout.CellAt(cellX, cellY);
            var here = new Vector2Int(cellX, cellY);
            Color32 fill = layout.IsStairs(here) ? Down : Open;

            int x0 = cellX * CellPixels;
            int y0 = cellY * CellPixels;

            for (int y = 0; y < CellPixels; y++)
            {
                for (int x = 0; x < CellPixels; x++)
                {
                    // A closed side is drawn as a one-pixel line on that edge of the cell, so
                    // neighbouring cells share the look of a single wall between them.
                    bool onWall =
                        (!cell.NorthOpen && y == CellPixels - 1) ||
                        (!cell.SouthOpen && y == 0) ||
                        (!cell.EastOpen && x == CellPixels - 1) ||
                        (!cell.WestOpen && x == 0);

                    pixels[(y0 + y) * stride + x0 + x] = onWall ? Wall : fill;
                }
            }
        }
    }
}
