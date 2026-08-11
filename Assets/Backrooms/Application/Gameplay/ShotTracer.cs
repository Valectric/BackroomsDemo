using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Draws the Banisher's shot: a brief bright line from the player along the direction fired.
    /// </summary>
    /// <remarks>
    /// Without this the Banisher was invisible. On a hit the Dweller simply stopped existing, and on
    /// a miss absolutely nothing happened — no sound, no mark, no charge — so pressing the key read
    /// as the key being broken rather than as the shot going wide. A weapon has to show its shot even
    /// when it misses, or the player cannot learn to aim it.
    /// </remarks>
    internal static class ShotTracer
    {
        /// <summary>How long the tracer stays on screen, in seconds.</summary>
        private const float LifeSeconds = 0.14f;

        /// <summary>How thick the beam is, in metres.</summary>
        private const float Thickness = 0.09f;

        /// <summary>Height above the floor the beam is drawn at, roughly chest height.</summary>
        private const float BeamHeight = 1.25f;

        /// <summary>
        /// Fires a tracer from a point along a direction.
        /// </summary>
        /// <param name="from">World position the shot starts at.</param>
        /// <param name="direction">Direction fired; flattened and normalised here.</param>
        /// <param name="metres">How far the beam reaches.</param>
        /// <param name="colour">Beam colour — the relic's own, so the shot is recognisably its.</param>
        /// <param name="hit">Whether the shot found something, which brightens it.</param>
        public static void Fire(Vector3 from, Vector3 direction, float metres, Color colour, bool hit)
        {
            Vector3 heading = new Vector3(direction.x, 0f, direction.z);
            if (heading.sqrMagnitude < 1e-6f) return;
            heading.Normalize();

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "BanisherShot";

            // A collider on a decoration would shove the player and block the Dwellers it is meant
            // to be shooting.
            Object.Destroy(beam.GetComponent<Collider>());

            var start = new Vector3(from.x, from.y + BeamHeight, from.z);
            beam.transform.position = start + heading * (metres * 0.5f);

            // Unity's cylinder is two units tall along Y, hence the halving.
            beam.transform.rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(90f, 0f, 0f);
            beam.transform.localScale = new Vector3(Thickness, metres * 0.5f, Thickness);

            var renderer = beam.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                Color lit = hit ? colour : colour * 0.55f;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", lit);
                if (material.HasProperty("_Color")) material.SetColor("_Color", lit);
                renderer.material = material;
            }

            Object.Destroy(beam, LifeSeconds);
        }
    }
}
