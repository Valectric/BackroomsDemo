using UnityEngine;

namespace Backrooms.EntityManager.Internal.Behaviour
{
    /// <summary>
    /// Builds and drives the visible shape of a Dweller for a given archetype: the body, the eyes
    /// that open when it hunts, and the light it casts while hunting.
    /// </summary>
    /// <remarks>
    /// Silhouette carries the archetype. A player has no stat sheet, so the only way they learn that
    /// the tall pale thing is slow and the low wide one is fast is by meeting each and living; that
    /// only works if the two are told apart instantly, at fog distance, from any angle.
    /// </remarks>
    internal sealed class DwellerBody
    {
        /// <summary>The archetype this body was built for.</summary>
        public DwellerArchetype Archetype { get; private set; }

        private GameObject _root;
        private GameObject _body;
        private GameObject _eyes;
        private Light _chaseLight;
        private Material _bodyMaterial;

        /// <summary>
        /// Builds the shape for an archetype under a parent, replacing anything built before. Kind is
        /// chosen per floor, so the same Dweller object is rebuilt rather than respawned.
        /// </summary>
        /// <param name="parent">Transform to build under.</param>
        /// <param name="archetype">The kind to build.</param>
        public void Build(Transform parent, DwellerArchetype archetype)
        {
            if (_root != null) Object.Destroy(_root);

            Archetype = archetype;
            _root = new GameObject("DwellerShape");
            _root.transform.SetParent(parent, worldPositionStays: false);

            BuildBody(archetype);
            BuildEyes(archetype);
            BuildChaseLight(archetype);
            ShowPursuit(false);
        }

        /// <summary>
        /// Switches between lurking and hunting appearance.
        /// </summary>
        /// <param name="hunting">Whether the Dweller is chasing the player.</param>
        public void ShowAlarm(bool alarmed)
        {
            if (_bodyMaterial == null || Archetype == null) return;

            Color colour = alarmed ? AlarmColour : Archetype.HuntingColour;
            if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", colour);
            if (_bodyMaterial.HasProperty("_Color")) _bodyMaterial.SetColor("_Color", colour);
            if (_chaseLight != null) _chaseLight.color = alarmed ? AlarmColour : Archetype.GlowColour;
        }

        /// <summary>The warning colour a charger flashes before it commits.</summary>
        private static readonly Color AlarmColour = new Color(1f, 0.05f, 0.04f);

        /// <summary>
        /// Switches the body between its lurking and hunting look.
        /// </summary>
        /// <param name="hunting">Whether it is hunting.</param>
        public void ShowPursuit(bool hunting)
        {
            if (_eyes != null) _eyes.SetActive(hunting);
            if (_chaseLight != null) _chaseLight.enabled = hunting;
            if (_bodyMaterial == null || Archetype == null) return;

            Color colour = hunting ? Archetype.HuntingColour : Archetype.LurkingColour;
            if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", colour);
            if (_bodyMaterial.HasProperty("_Color")) _bodyMaterial.SetColor("_Color", colour);
        }

        /// <summary>
        /// Shows or hides the whole shape.
        /// </summary>
        /// <param name="visible">Whether the Dweller should be on screen.</param>
        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        /// <summary>Whether a shape has been built yet.</summary>
        public bool Exists => _root != null;

        /// <summary>
        /// Builds the capsule that gives the Dweller its silhouette. A Unity capsule is 2 units tall
        /// at unit scale, so the Y scale is half the height wanted.
        /// </summary>
        /// <param name="archetype">The kind being built.</param>
        private void BuildBody(DwellerArchetype archetype)
        {
            _body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _body.name = $"{archetype.Kind}Body";
            Object.Destroy(_body.GetComponent<Collider>());
            _body.transform.SetParent(_root.transform, worldPositionStays: false);
            _body.transform.localPosition = new Vector3(0f, archetype.BodyHeight * 0.5f, 0f);
            _body.transform.localScale = new Vector3(
                archetype.BodyWidth, archetype.BodyHeight * 0.5f, archetype.BodyWidth);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _bodyMaterial = new Material(shader);
            if (_bodyMaterial.HasProperty("_Smoothness")) _bodyMaterial.SetFloat("_Smoothness", 0.1f);
            _body.GetComponent<MeshRenderer>().sharedMaterial = _bodyMaterial;
        }

        /// <summary>
        /// Builds the eyes, which are hidden until the Dweller hunts. They sit high on the body and
        /// far enough along its local +Z to clear the capsule — inside its own radius they render
        /// invisibly, which looks exactly like the feature not working.
        /// </summary>
        /// <param name="archetype">The kind being built.</param>
        private void BuildEyes(DwellerArchetype archetype)
        {
            _eyes = new GameObject("Eyes");
            _eyes.transform.SetParent(_root.transform, worldPositionStays: false);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Standard");
            var glow = new Material(unlit);
            if (glow.HasProperty("_BaseColor")) glow.SetColor("_BaseColor", archetype.GlowColour);
            if (glow.HasProperty("_Color")) glow.SetColor("_Color", archetype.GlowColour);

            // Sit the eyes just below the crown, but never further down than the body is tall — a
            // fixed drop measured for a 2.2m Lurker puts a squat Skitter's eyes at its ankles.
            float eyeHeight = archetype.BodyHeight - Mathf.Min(
                archetype.BodyHeight * 0.28f, Mathf.Max(0.22f, archetype.BodyWidth * 0.55f));

            float ring = SilhouetteRadiusAt(eyeHeight, archetype);
            float spread = ring * (archetype.EyeCount > 2 ? 0.62f : 0.42f);

            for (int i = 0; i < archetype.EyeCount; i++)
            {
                // Spread the eyes evenly across the face, centred on it.
                float t = archetype.EyeCount == 1
                    ? 0f
                    : i / (float)(archetype.EyeCount - 1) * 2f - 1f;

                // Each eye is placed on the curved surface at its own sideways offset. Placing them
                // all at one flat distance leaves the outer ones hanging in mid-air beside a wide
                // body, which reads as a bug rather than as a face.
                float sideways = t * spread;
                float forward = Mathf.Sqrt(Mathf.Max(0.0025f, ring * ring - sideways * sideways))
                                + archetype.EyeSize * 0.45f;

                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = $"Eye{i}";
                Object.Destroy(eye.GetComponent<Collider>());
                eye.transform.SetParent(_eyes.transform, worldPositionStays: false);
                eye.transform.localPosition = new Vector3(sideways, eyeHeight, forward);
                eye.transform.localScale = Vector3.one * archetype.EyeSize;
                eye.GetComponent<MeshRenderer>().sharedMaterial = glow;
            }
        }

        /// <summary>
        /// The radius of the body's silhouette at a given height. A Unity capsule scaled to
        /// width × height is a cylinder between quarter and three-quarter height with an ellipsoidal
        /// cap at each end, so the body is narrower near the crown than at its waist.
        /// </summary>
        /// <param name="height">Height above the floor, in metres.</param>
        /// <param name="archetype">The kind being built.</param>
        /// <returns>Radius of the cross-section at that height, in metres.</returns>
        private static float SilhouetteRadiusAt(float height, DwellerArchetype archetype)
        {
            float radius = archetype.BodyWidth * 0.5f;
            float capHeight = archetype.BodyHeight * 0.25f;
            float bandLow = capHeight;
            float bandHigh = archetype.BodyHeight - capHeight;

            if (height >= bandLow && height <= bandHigh) return radius;

            float beyond = height < bandLow ? bandLow - height : height - bandHigh;
            float t = Mathf.Clamp01(beyond / Mathf.Max(0.0001f, capHeight));
            return radius * Mathf.Sqrt(Mathf.Max(0.04f, 1f - t * t));
        }

        /// <summary>
        /// Builds the light a hunting Dweller throws ahead of it, tinted to its own glow so the
        /// colour washing the corridor says which kind is coming before it is in view.
        /// </summary>
        /// <param name="archetype">The kind being built.</param>
        private void BuildChaseLight(DwellerArchetype archetype)
        {
            var go = new GameObject("ChaseLight");
            go.transform.SetParent(_root.transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(
                0f, archetype.BodyHeight * 0.75f, archetype.BodyWidth * 0.5f);

            _chaseLight = go.AddComponent<Light>();
            _chaseLight.type = LightType.Point;
            _chaseLight.color = archetype.GlowColour;
            _chaseLight.intensity = 2.4f;
            _chaseLight.range = 6f;
            _chaseLight.shadows = LightShadows.None;
        }
    }
}
