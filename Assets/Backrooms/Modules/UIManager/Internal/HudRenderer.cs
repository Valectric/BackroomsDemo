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
        /// Draws the persistent status line — time survived and how deep the player has gone.
        /// </summary>
        /// <param name="elapsedSeconds">Seconds since the run started.</param>
        /// <param name="floor">Current floor number.</param>
        /// <param name="relics">How many relics the player is carrying.</param>
        public void DrawStatus(float elapsedSeconds, int floor, int relics)
        {
            int size = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.035f));
            var rect = new Rect(size, size, Screen.width * 0.6f, size * 2f);
            string carried = relics > 0 ? $"    {RelicMark} {relics}" : string.Empty;
            DrawLabel(rect, $"FLOOR {floor}    {FormatTime(elapsedSeconds)}{carried}", size,
                TextAnchor.UpperLeft);
        }

        /// <summary>Colour relics are shown in, matching the violet they glow in the world.</summary>
        private static readonly Color RelicColor = new Color(0.78f, 0.55f, 1f);

        /// <summary>
        /// Symbol standing in for a relic. A four-pointed star is in every font the browser will
        /// fall back to, which a rarer glyph is not — a missing-glyph box on the HUD would be the
        /// first thing a player sees.
        /// </summary>
        private const string RelicMark = "✦";

        /// <summary>
        /// Draws the flash confirming a relic was just picked up.
        /// </summary>
        /// <param name="relics">How many the player now carries.</param>
        public void DrawRelicFlash(int relics)
        {
            int size = Mathf.Max(18, Mathf.RoundToInt(Screen.height * 0.05f));
            var rect = new Rect(0f, Screen.height * 0.62f, Screen.width, size * 2f);
            DrawLabel(rect, $"{RelicMark} RELIC RECOVERED  ({relics})", size, TextAnchor.UpperCenter,
                RelicColor);
        }

        /// <summary>
        /// Draws the arrival banner naming the floor the player just descended into.
        /// </summary>
        /// <param name="floor">Floor number.</param>
        /// <param name="name">Display name of the floor.</param>
        public void DrawFloorBanner(int floor, string name)
        {
            int size = Mathf.Max(18, Mathf.RoundToInt(Screen.height * 0.05f));
            var rect = new Rect(0f, Screen.height * 0.30f, Screen.width, size * 3f);
            DrawLabel(rect, $"FLOOR {floor}\n{name}", size, TextAnchor.UpperCenter);
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
        /// Draws the end-of-run banner for being caught by a Dweller.
        /// </summary>
        /// <param name="floor">Floor the player died on.</param>
        /// <param name="elapsedSeconds">How long they lasted.</param>
        /// <param name="relics">How many relics they were carrying.</param>
        /// <param name="bestFloors">Deepest floor reached in any run.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        public void DrawCaught(int floor, float elapsedSeconds, int relics, int bestFloors,
            int bestRelics)
        {
            int size = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.07f));
            DrawLabel(new Rect(0f, Screen.height * 0.30f, Screen.width, size * 2f),
                "A DWELLER FOUND YOU", size, TextAnchor.UpperCenter);

            // The run's own numbers, then the numbers to beat. Without the second line the first is
            // just a record of losing.
            int summary = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.045f));
            DrawLabel(new Rect(0f, Screen.height * 0.44f, Screen.width, summary * 2f),
                $"{floor} FLOORS    {RelicMark} {relics}    {FormatTime(elapsedSeconds)}", summary,
                TextAnchor.UpperCenter);

            int best = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.032f));
            DrawLabel(new Rect(0f, Screen.height * 0.53f, Screen.width, best * 2f),
                $"BEST    {Mathf.Max(bestFloors, floor)} FLOORS    {RelicMark} {Mathf.Max(bestRelics, relics)}",
                best, TextAnchor.UpperCenter);

            int hint = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.035f));
            DrawLabel(new Rect(0f, Screen.height * 0.66f, Screen.width, hint * 2f),
                "TAP OR CLICK TO TRY AGAIN", hint, TextAnchor.UpperCenter);
        }

        /// <summary>Warning colour for the pursuit alert.</summary>
        private static readonly Color AlertColor = new Color(0.95f, 0.24f, 0.18f);

        /// <summary>
        /// Draws the pursuit warning: a red border that closes in as the Dweller does, and a line of
        /// text naming what is happening. Both pulse, because a static red edge stops being read
        /// after a few seconds whereas a moving one does not.
        /// </summary>
        /// <param name="hunterName">What the nearest hunting Dweller is called.</param>
        /// <param name="closeness">How close the nearest hunting Dweller is, 0..1.</param>
        /// <param name="phase">
        /// Seconds used to drive the pulse. The run timer is passed in rather than a wall clock so
        /// the HUD has no clock of its own and a test can reproduce any frame of it exactly.
        /// </param>
        public void DrawHunted(string hunterName, float closeness, float phase)
        {
            float pulse = 0.72f + 0.28f * Mathf.Sin(phase * 7f);
            float thickness = Mathf.Lerp(Screen.height * 0.02f, Screen.height * 0.075f, closeness);
            float alpha = Mathf.Lerp(0.16f, 0.46f, closeness) * pulse;

            Color previous = GUI.color;
            GUI.color = new Color(AlertColor.r, AlertColor.g, AlertColor.b, alpha);

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - thickness, Screen.width, thickness),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, 0f, thickness, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - thickness, 0f, thickness, Screen.height),
                Texture2D.whiteTexture);

            GUI.color = previous;

            int size = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.045f));
            var rect = new Rect(0f, Screen.height * 0.12f, Screen.width, size * 2f);
            DrawLabel(rect, $"A {hunterName} HAS SEEN YOU", size, TextAnchor.UpperCenter, AlertColor);
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
        /// <param name="colour">Text colour, or <c>null</c> for the standard HUD white.</param>
        private static void DrawLabel(Rect rect, string text, int fontSize, TextAnchor anchor,
            Color? colour = null)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = anchor,
                wordWrap = false
            };

            style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);

            style.normal.textColor = colour ?? TextColor;
            GUI.Label(rect, text, style);
        }
    }
}
