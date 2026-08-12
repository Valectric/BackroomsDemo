using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// The HUD's one way of putting text on screen: a label with a drop shadow, in the HUD's colours.
    /// </summary>
    /// <remarks>
    /// Extracted because the same routine had already been written twice, and the death screen would
    /// have made three. The copies had quietly diverged — one scaled its shadow with the text alpha
    /// and one did not — which is invisible until something fades, and the death screen fades.
    /// </remarks>
    internal static class HudText
    {
        /// <summary>Bone-white HUD text, slightly warm to match the level.</summary>
        public static readonly Color TextColor = new Color(0.96f, 0.95f, 0.88f);

        /// <summary>Dim backing so text stays readable against pale walls.</summary>
        public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>Colour relics are shown in, matching the violet they glow in the world.</summary>
        public static readonly Color RelicColor = new Color(0.78f, 0.55f, 1f);

        /// <summary>
        /// Draws a label with a one-pixel drop shadow so it reads against any background.
        /// </summary>
        /// <param name="rect">Screen rectangle to draw in.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <param name="anchor">Alignment within the rectangle.</param>
        /// <param name="colour">Text colour, or <c>null</c> for the standard HUD white.</param>
        public static void DrawLabel(Rect rect, string text, int fontSize, TextAnchor anchor,
            Color? colour = null)
        {
            Color ink = colour ?? TextColor;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = anchor,
                wordWrap = false
            };

            // The shadow follows the text's own alpha. A fixed-alpha shadow under fading text leaves
            // a legible black ghost of a line that is supposed to have gone.
            style.normal.textColor = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b,
                ShadowColor.a * ink.a);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);

            style.normal.textColor = ink;
            GUI.Label(rect, text, style);
        }

        /// <summary>
        /// Fills the whole screen with a flat colour, used for fades and dimming.
        /// </summary>
        /// <param name="colour">Colour to fill with, including its alpha.</param>
        public static void Fill(Color colour)
        {
            if (colour.a <= 0f) return;

            Color previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
