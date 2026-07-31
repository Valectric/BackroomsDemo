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
        private const float AmbientLevel = 0.20f;

        /// <summary>
        /// Target luminance of fully-fogged distance. Fog must not be brighter than the surfaces near
        /// the camera or depth inverts: corridors end in a glowing wall while your feet are black.
        /// </summary>
        private const float FogLevel = 0.45f;

        /// <summary>
        /// Sets ambient light and fog for a floor.
        /// </summary>
        /// <param name="theme">Palette of the floor being entered.</param>
        public static void Apply(FloorTheme theme)
        {
            if (theme == null) return;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            // Take only the HUE from the palette and set the LEVEL explicitly. Scaling the fog colour
            // directly coupled brightness to a hue choice, so floors with dark fog were lit six times
            // more dimly than the entry floor — nobody chose that, it fell out of the palette.
            RenderSettings.ambientLight = Normalised(theme.Fog) * AmbientLevel;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            // Distance should read as glow rather than sludge, so lift the fog towards the floor's
            // own light colour instead of using the raw palette value.
            RenderSettings.fogColor = Normalised(Color.Lerp(theme.Fog, theme.Light, 0.15f)) * FogLevel;
            RenderSettings.fogDensity = 0.038f;

            // Keep every camera's clear colour matching the fog, so the far plane is invisible.
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.clearFlags != CameraClearFlags.SolidColor) continue;
                cam.backgroundColor = RenderSettings.fogColor;
            }
        }

        /// <summary>
        /// Strips brightness from a colour, keeping its hue at unit luminance.
        /// </summary>
        /// <param name="colour">Colour to normalise.</param>
        /// <returns>The same hue at luminance 1, or white if the input is black.</returns>
        private static Color Normalised(Color colour)
        {
            float luminance = colour.r * 0.2126f + colour.g * 0.7152f + colour.b * 0.0722f;
            return luminance > 0.001f ? colour / luminance : Color.white;
        }
    }
}
