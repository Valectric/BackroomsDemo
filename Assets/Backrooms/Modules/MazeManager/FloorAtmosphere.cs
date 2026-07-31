using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// Applies a floor's global render settings — ambient light and fog.
    /// </summary>
    /// <remarks>
    /// This exists as one shared entry point on purpose. The atmosphere used to be set in two places:
    /// once when the scene was authored and again, partially, when descending a floor. The descent
    /// path only updated the fog colour, so every floor below the first was lit with the entry
    /// floor's warm yellow ambient — which is why the mall looked like grey mush and the carnival
    /// like mud. Screenshot tooling calls this too, so what is photographed is what ships.
    /// </remarks>
    public static class FloorAtmosphere
    {
        /// <summary>
        /// How much of a floor's fog colour carries into its ambient light. Ambient is unshadowed and
        /// omnidirectional, so a high value flattens everything; keeping it low lets the ceiling
        /// lights create actual pools of light and dark between them.
        /// </summary>
        private const float AmbientFromFog = 0.30f;

        /// <summary>
        /// Sets ambient light and fog for a floor.
        /// </summary>
        /// <param name="theme">Palette of the floor being entered.</param>
        public static void Apply(FloorTheme theme)
        {
            if (theme == null) return;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = theme.Fog * AmbientFromFog;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            // Distance should read as glow rather than sludge, so lift the fog towards the floor's
            // own light colour instead of using the raw palette value.
            RenderSettings.fogColor = Color.Lerp(theme.Fog, theme.Light, 0.15f);
            RenderSettings.fogDensity = 0.045f;
        }
    }
}
