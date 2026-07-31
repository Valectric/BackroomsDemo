using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Generates the surface textures at runtime. Flat untinted colour is what makes a procedural
    /// level look like an early-90s game; a little pattern and grain across the walls, carpet and
    /// ceiling does more for the look than any amount of extra geometry — and generating them in
    /// code keeps the browser download small.
    /// </summary>
    /// <remarks>
    /// Every texture is built from a seeded generator so a floor always looks identical, and they are
    /// small (256px) and tiled, which suits a mobile GPU.
    /// </remarks>
    internal static class ProceduralTextures
    {
        /// <summary>Side length of every generated texture, in pixels.</summary>
        private const int Size = 256;

        /// <summary>
        /// Wallpaper: faint vertical striping, grain, and damp staining that pools towards the
        /// skirting the way it does in a room nobody maintains.
        /// </summary>
        /// <param name="baseColour">Wall colour to tint towards.</param>
        /// <param name="seed">Seed for the grain.</param>
        /// <returns>A tiling wall texture.</returns>
        public static Texture2D Wall(Color baseColour, int seed)
        {
            var rng = new System.Random(seed);
            var tex = NewTexture("WallpaperTexture");
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                // Vertical position 0 at the skirting, 1 at the ceiling.
                float up = y / (float)(Size - 1);
                float damp = Mathf.SmoothStep(0.86f, 1f, up);

                for (int x = 0; x < Size; x++)
                {
                    // Wallpaper striping, plus a seam every quarter to read as hung strips.
                    float stripe = 1f + Mathf.Sin(x * Mathf.PI * 2f / 32f) * 0.022f;
                    if (x % 64 == 0) stripe *= 0.93f;

                    float grain = 1f + (float)(rng.NextDouble() - 0.5) * 0.05f;
                    float blotch = 1f + Mathf.PerlinNoise(x * 0.021f + seed, y * 0.021f) * 0.10f - 0.05f;

                    pixels[y * Size + x] = Scale(baseColour, stripe * grain * blotch * damp);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Damp carpet: fine speckle with larger patches of wear.
        /// </summary>
        /// <param name="baseColour">Carpet colour.</param>
        /// <param name="seed">Seed for the speckle.</param>
        /// <returns>A tiling floor texture.</returns>
        public static Texture2D Carpet(Color baseColour, int seed)
        {
            var rng = new System.Random(seed + 7);
            var tex = NewTexture("CarpetTexture");
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float speckle = 1f + (float)(rng.NextDouble() - 0.5) * 0.16f;
                    float wear = 1f + (Mathf.PerlinNoise(x * 0.035f, y * 0.035f + seed) - 0.5f) * 0.20f;
                    pixels[y * Size + x] = Scale(baseColour, speckle * wear);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Suspended ceiling: a grid of tiles with darker grout lines and slight per-tile variation,
        /// so the ceiling stops being one unbroken slab.
        /// </summary>
        /// <param name="baseColour">Tile colour.</param>
        /// <param name="seed">Seed for tile variation.</param>
        /// <returns>A tiling ceiling texture.</returns>
        public static Texture2D CeilingTiles(Color baseColour, int seed)
        {
            var rng = new System.Random(seed + 13);
            var tex = NewTexture("CeilingTexture");
            var pixels = new Color32[Size * Size];

            const int tile = 64;
            var tileShade = new float[(Size / tile) * (Size / tile)];
            for (int i = 0; i < tileShade.Length; i++)
            {
                tileShade[i] = 1f + (float)(rng.NextDouble() - 0.5) * 0.08f;
            }

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int tx = x % tile;
                    int ty = y % tile;
                    bool grout = tx < 2 || ty < 2;

                    float shade = tileShade[(y / tile) * (Size / tile) + (x / tile)];
                    float speck = 1f + (float)(rng.NextDouble() - 0.5) * 0.04f;
                    float value = grout ? 0.72f : shade * speck;

                    pixels[y * Size + x] = Scale(baseColour, value);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Creates an empty tiling texture with mipmaps and no filtering surprises.
        /// </summary>
        /// <param name="name">Texture name.</param>
        /// <returns>The new texture.</returns>
        private static Texture2D NewTexture(string name)
        {
            return new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
        }

        /// <summary>
        /// Multiplies a colour's brightness, keeping it in range.
        /// </summary>
        /// <param name="colour">Colour to scale.</param>
        /// <param name="factor">Brightness multiplier.</param>
        /// <returns>The scaled colour.</returns>
        private static Color32 Scale(Color colour, float factor)
        {
            return new Color(
                Mathf.Clamp01(colour.r * factor),
                Mathf.Clamp01(colour.g * factor),
                Mathf.Clamp01(colour.b * factor),
                1f);
        }
    }
}
