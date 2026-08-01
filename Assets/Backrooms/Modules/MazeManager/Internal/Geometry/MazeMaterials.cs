using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Creates the runtime materials the generated level renders with. Shared by every builder so
    /// there is one place that knows how to reach a URP shader, and one place that shouts when the
    /// shader is missing from a player build.
    /// </summary>
    internal static class MazeMaterials
    {
        /// <summary>
        /// Creates an opaque URP-lit material. Falls back to the legacy standard shader only if the
        /// URP shader is unavailable, so the geometry never renders as magenta.
        /// </summary>
        /// <param name="color">Base colour.</param>
        /// <returns>A new material instance.</returns>
        public static Material Lit(Color color)
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
        /// Creates a URP-lit material carrying a generated texture, so surfaces have grain and
        /// pattern instead of reading as flat blocks of colour.
        /// </summary>
        /// <param name="colour">Base colour.</param>
        /// <param name="texture">Generated surface texture.</param>
        /// <returns>A new textured material.</returns>
        public static Material Textured(Color colour, Texture2D texture)
        {
            Material mat = Lit(colour);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            mat.mainTexture = texture;
            return mat;
        }

        /// <summary>
        /// Creates a material that reads as a light source: unlit, so it stays bright regardless of
        /// what falls on it, and pushed past full white for HDR headroom.
        /// </summary>
        /// <param name="colour">Colour of the glow.</param>
        /// <param name="strength">Multiplier past 1 that gives the surface its headroom.</param>
        /// <returns>A new emissive-looking material.</returns>
        public static Material Glowing(Color colour, float strength)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour * strength);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour * strength);
            return mat;
        }

        /// <summary>
        /// Creates a collider-free cube with the given material.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">Object name.</param>
        /// <param name="centre">World centre.</param>
        /// <param name="size">Full size on each axis.</param>
        /// <param name="material">Material to render with.</param>
        /// <returns>The created cube.</returns>
        public static GameObject Cube(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<BoxCollider>());
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
