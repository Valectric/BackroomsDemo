using Backrooms.MazeManager;
using UnityEngine;

namespace Backrooms.RelicManager.Internal
{
    /// <summary>
    /// Builds the visible relic: a floating shard on a plinth, lit so it can be picked out across a
    /// room, and turning slowly so it reads as a thing rather than a decal.
    /// </summary>
    /// <remarks>
    /// Violet on purpose. Green already means "this is the way down" and the player has learned it;
    /// the creatures own red, cold blue and amber. A relic has to say "worth going to" without saying
    /// either "exit" or "danger", so it gets the one saturated hue nothing else in the game uses.
    /// </remarks>
    internal sealed class RelicBuilder
    {
        /// <summary>Height of the shard's centre above the floor, in metres.</summary>
        private const float FloatHeight = 1.15f;

        /// <summary>
        /// Builds one relic in a cell.
        /// </summary>
        /// <param name="layout">The floor being dressed.</param>
        /// <param name="cell">Cell to place the relic in.</param>
        /// <param name="archetype">Which relic this is, which sets its colour.</param>
        /// <param name="parent">Parent transform for the relic.</param>
        /// <returns>The relic's root object, carrying its spinner.</returns>
        public GameObject Build(MazeLayout layout, Vector2Int cell, RelicArchetype archetype,
            Transform parent)
        {
            Vector3 centre = layout.CellCenterToWorld(cell);

            var root = new GameObject($"Relic_{archetype.Kind}_{cell.x}_{cell.y}");
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = centre;

            BuildPlinth(root.transform);
            BuildShard(root.transform, archetype.Colour);
            BuildGlow(root.transform, archetype.Colour);

            return root;
        }

        /// <summary>
        /// Builds the plinth the shard hovers over. Without something under it the shard reads as
        /// pasted onto the background — nothing else in this level touches the ground either, and it
        /// is the reason props look like stickers.
        /// </summary>
        /// <param name="parent">Relic root transform.</param>
        private static void BuildPlinth(Transform parent)
        {
            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth.name = "Plinth";
            Object.Destroy(plinth.GetComponent<BoxCollider>());
            plinth.transform.SetParent(parent, worldPositionStays: false);
            plinth.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            plinth.transform.localScale = new Vector3(0.62f, 0.44f, 0.62f);
            plinth.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(0.14f, 0.12f, 0.17f));
        }

        /// <summary>
        /// Builds the shard itself: two stretched cubes crossed into a rough double pyramid, which at
        /// this budget reads as "an object with facets" rather than "a box".
        /// </summary>
        /// <param name="parent">Relic root transform.</param>
        /// <param name="colour">Colour this relic glows.</param>
        private static void BuildShard(Transform parent, Color colour)
        {
            var shard = new GameObject("Shard");
            shard.transform.SetParent(parent, worldPositionStays: false);
            shard.transform.localPosition = new Vector3(0f, FloatHeight, 0f);
            shard.AddComponent<RelicSpinner>();

            Material glow = Unlit(colour);
            foreach (float tilt in new[] { 0f, 45f })
            {
                var facet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                facet.name = $"Facet_{tilt:F0}";
                Object.Destroy(facet.GetComponent<BoxCollider>());
                facet.transform.SetParent(shard.transform, worldPositionStays: false);
                facet.transform.localRotation = Quaternion.Euler(0f, tilt, 45f);
                facet.transform.localScale = new Vector3(0.19f, 0.19f, 0.19f);
                facet.GetComponent<MeshRenderer>().sharedMaterial = glow;
            }
        }

        /// <summary>
        /// Adds the light the relic throws. Its range is deliberately generous: the point of a relic
        /// is that you notice it from somewhere you were not going.
        /// </summary>
        /// <param name="parent">Relic root transform.</param>
        /// <param name="colour">Colour of the light it throws.</param>
        private static void BuildGlow(Transform parent, Color colour)
        {
            var go = new GameObject("RelicGlow");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, FloatHeight, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = 2.2f;
            light.renderMode = LightRenderMode.ForceVertex;
            light.range = 7f;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Creates an opaque lit material.
        /// </summary>
        /// <param name="colour">Base colour.</param>
        /// <returns>A new material.</returns>
        private static Material Lit(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
            return mat;
        }

        /// <summary>
        /// Creates a self-lit material. Strength stays at 1 — brighter clips the channels and the
        /// violet washes to white, which is how the stairs sign lost its colour.
        /// </summary>
        /// <param name="colour">Base colour.</param>
        /// <returns>A new material.</returns>
        private static Material Unlit(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
            return mat;
        }
    }

    /// <summary>
    /// Turns a relic shard slowly on the spot. Presentation only — nothing in gameplay reads this
    /// rotation, so using the frame clock here does not make anything non-deterministic.
    /// </summary>
    public sealed class RelicSpinner : MonoBehaviour
    {
        /// <summary>Degrees per second the shard turns.</summary>
        private const float DegreesPerSecond = 42f;

        /// <summary>
        /// Advances the spin each frame.
        /// </summary>
        private void Update()
        {
            transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
