using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// Draws the heads-up display. Rendering goes through IMGUI rather than a uGUI canvas because
    /// the HUD is a handful of labels and IMGUI needs no font assets, prefabs or canvas wiring —
    /// which keeps the whole scene reproducible from code.
    /// </summary>
    /// <remarks>
    /// Sizes are derived from screen height so the HUD stays legible on a phone as well as a
    /// desktop browser.
    /// </remarks>
    internal sealed class HudRenderer
    {
        /// <summary>Bone-white HUD text, slightly warm to match the level.</summary>
        private static readonly Color TextColor = new Color(0.96f, 0.95f, 0.88f);

        /// <summary>Dim backing so text stays readable against pale walls.</summary>
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>
        /// Draws the in-run timer in the top-left corner.
        /// </summary>
        /// <param name="elapsedSeconds">Seconds since the run started.</param>
        public void DrawTimer(float elapsedSeconds)
        {
            int size = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.035f));
            var rect = new Rect(size, size, Screen.width * 0.5f, size * 2f);
            DrawLabel(rect, FormatTime(elapsedSeconds), size, TextAnchor.UpperLeft);
        }

        /// <summary>
        /// Draws the end-of-run banner across the middle of the screen.
        /// </summary>
        /// <param name="elapsedSeconds">Final run time in seconds.</param>
        public void DrawEscaped(float elapsedSeconds)
        {
            int size = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.07f));
            var rect = new Rect(0f, Screen.height * 0.38f, Screen.width, size * 3f);
            DrawLabel(rect, $"YOU FOUND THE EXIT\n{FormatTime(elapsedSeconds)}", size,
                TextAnchor.UpperCenter);
        }

        /// <summary>
        /// Formats a duration as minutes and seconds.
        /// </summary>
        /// <param name="seconds">Duration in seconds.</param>
        /// <returns>The formatted duration, for example <c>01:07</c>.</returns>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>
        /// Draws a label with a one-pixel drop shadow so it reads against any background.
        /// </summary>
        /// <param name="rect">Screen rectangle to draw in.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <param name="anchor">Alignment within the rectangle.</param>
        private static void DrawLabel(Rect rect, string text, int fontSize, TextAnchor anchor)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = anchor,
                wordWrap = false
            };

            style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);

            style.normal.textColor = TextColor;
            GUI.Label(rect, text, style);
        }
    }
}
