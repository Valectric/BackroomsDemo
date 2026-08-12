using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// Keeps only the handful of lights near the player switched on. Public because more than the
    /// maze has lights to switch off — relics carry one each, and on the first floor there are forty
    /// of them.
    /// </summary>
    /// <remarks>
    /// A floor carries dozens of fixtures, and the camera can see about 45 metres through fog that is
    /// effectively opaque well before that — so nearly all of them are lighting rooms nobody is
    /// looking at, while still costing the renderer per frame. Reducing the fixture count was the
    /// wrong lever: it made the floors darker to buy frame rate. Switching distant ones off costs
    /// nothing visually, because they were not visible.
    /// <para>
    /// Throttled rather than run every frame: the set only changes as the player walks, and walking
    /// four metres takes far longer than one frame. Distances are compared squared, so this never
    /// takes a square root.
    /// </para>
    /// </remarks>
    public sealed class LightCuller : MonoBehaviour
    {
        /// <summary>How far a light can be from the player and still be worth having on, in metres.</summary>
        /// <remarks>
        /// Distance only, deliberately. Culling to what is in front as well was tried and measured
        /// better — two to eighteen lights on instead of ten to fourteen — and then looked plainly
        /// worse: the ceiling above the player went dark, because a fixture behind or beside you
        /// still lights the surfaces you are looking at. A light's contribution depends on its range,
        /// not on where it sits relative to the view direction, and only a photograph showed it.
        /// <para>
        /// Settable because the right radius follows the range of the lights being managed. A ceiling
        /// fixture reaches far enough to matter across a room; a relic's glow reaches seven metres,
        /// so keeping one lit at thirty is paying for a light that cannot reach anything.
        /// </para>
        /// </remarks>
        public float RadiusMetres { get; set; } = 30f;

        /// <summary>Most lights allowed on at once, whatever the radius says.</summary>
        public int MaxActive { get; set; } = 20;

        /// <summary>Seconds between recalculations.</summary>
        private const float IntervalSeconds = 0.12f;

        private Light[] _lights;
        private float[] _distances;
        private float _nextCheck;

        /// <summary>How many lights are currently switched on.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>How many lights this culler manages in total.</summary>
        public int TotalCount => _lights == null ? 0 : _lights.Length;

        /// <summary>
        /// Takes charge of every light beneath this object.
        /// </summary>
        public void Collect()
        {
            _lights = GetComponentsInChildren<Light>(includeInactive: true);
            _distances = new float[_lights.Length];
            _nextCheck = 0f;
        }

        /// <summary>
        /// Switches lights on or off for the viewer's current position.
        /// </summary>
        /// <param name="viewer">Where the player is.</param>
        public void Apply(Vector3 viewer)
        {
            if (_lights == null || _lights.Length == 0) return;

            float radiusSquared = RadiusMetres * RadiusMetres;
            int near = 0;

            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;

                _distances[i] = (_lights[i].transform.position - viewer).sqrMagnitude;
                if (_distances[i] <= radiusSquared) near++;
            }

            // Past the cap, keep the closest. Selecting the exact nth distance is not worth it for a
            // few dozen lights: tightening the radius until few enough qualify lands in the same
            // place and never allocates.
            float cutoff = radiusSquared;
            while (near > MaxActive && cutoff > 1f)
            {
                cutoff *= 0.8f;
                near = 0;
                for (int i = 0; i < _lights.Length; i++)
                {
                    if (_lights[i] != null && _distances[i] <= cutoff) near++;
                }
            }

            ActiveCount = 0;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;

                bool on = _distances[i] <= cutoff;
                if (_lights[i].enabled != on) _lights[i].enabled = on;
                if (on) ActiveCount++;
            }
        }

        /// <summary>
        /// Finds the camera to cull against.
        /// </summary>
        /// <remarks>
        /// Falls back to any enabled camera rather than trusting <c>Camera.main</c> alone. The tag is
        /// one line in another module, and if it is ever dropped this would quietly stop culling
        /// while every test still passed — the tests call Apply directly and never touch this path.
        /// </remarks>
        /// <returns>A camera, or <c>null</c> if the scene has none.</returns>
        private static Camera Viewer()
        {
            Camera main = Camera.main;
            if (main != null) return main;

            return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
        }

        /// <summary>
        /// Re-evaluates which lights are on, a few times a second.
        /// </summary>
        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + IntervalSeconds;

            Camera viewer = Viewer();
            if (viewer == null) return;

            Apply(viewer.transform.position);
        }
    }
}
