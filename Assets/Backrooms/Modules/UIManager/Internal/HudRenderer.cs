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
        /// Draws the corner map: a window of the floor around the player, with the player marked.
        /// </summary>
        /// <remarks>
        /// Deliberately small and plain. A map that fills the screen answers the question the game is
        /// asking — the floor is meant to be disorienting — so this shows only the rooms immediately
        /// around the player, and shows them flat, without a heading.
        /// </remarks>
        /// <param name="map">Baked floor map.</param>
        /// <param name="player">Player position as a fraction of the floor in each axis.</param>
        /// <param name="window">Fraction of the floor to show around the player.</param>
        public void DrawMap(Texture2D map, Vector2 player, float window)
        {
            if (map == null) return;

            int size = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.22f), 88, 220);
            int margin = Mathf.Max(10, Mathf.RoundToInt(Screen.height * 0.03f));

            // Below the compass band, which runs across the top: at the right-hand end the two
            // overlapped and the map sat on top of an arrow's distance label.
            float top = Screen.height * 0.17f;
            var frame = new Rect(Screen.width - size - margin, top, size, size);

            // Clamped so the window stops at the floor's edge instead of sliding off it, which would
            // stretch the last row of cells across the corner.
            float half = window * 0.5f;
            float x = Mathf.Clamp(player.x - half, 0f, 1f - window);
            float y = Mathf.Clamp(player.y - half, 0f, 1f - window);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(frame.x - 2f, frame.y - 2f, frame.width + 4f, frame.height + 4f),
                Texture2D.whiteTexture);
            GUI.color = previous;

            // Texture coordinates run bottom-up, and so does the maze grid, so no flip is needed.
            GUI.DrawTextureWithTexCoords(frame, map, new Rect(x, y, window, window));

            // The player, drawn where they actually are inside the window rather than always at its
            // centre — at the floor's edge the window stops moving and they walk to the corner.
            float dotSize = Mathf.Max(4f, size * 0.035f);
            float px = frame.x + (player.x - x) / window * frame.width;
            float py = frame.yMax - (player.y - y) / window * frame.height;

            GUI.color = new Color(1f, 0.35f, 0.3f);
            GUI.DrawTexture(new Rect(px - dotSize * 0.5f, py - dotSize * 0.5f, dotSize, dotSize),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

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
            DrawLabel(new Rect(0f, Screen.height * 0.26f, Screen.width, size * 2f),
                "A DWELLER FOUND YOU", size, TextAnchor.UpperCenter);

            // Each figure gets its own labelled row. Three bare numbers on one line — "3  2  01:14"
            // — never said which was which, and the relic glyph explained nothing on its own.
            int row = Mathf.Max(15, Mathf.RoundToInt(Screen.height * 0.040f));
            float top = Screen.height * 0.40f;
            float step = row * 1.65f;
            DrawStatRow(top, row, "REACHED", $"FLOOR {floor}");
            DrawStatRow(top + step, row, "RELICS FOUND", relics.ToString());
            DrawStatRow(top + step * 2f, row, "SURVIVED", FormatTime(elapsedSeconds));

            // The record is submitted before this screen is drawn, so on a best run the "best" line
            // repeated the run's own numbers and read as the same thing printed twice. When the run
            // IS the record, say that instead of restating it.
            bool runIsTheRecord = bestFloors <= floor && bestRelics <= relics;
            int best = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.032f));
            DrawLabel(new Rect(0f, top + step * 3.3f, Screen.width, best * 2f),
                runIsTheRecord
                    ? "NEW BEST"
                    : $"BEST SO FAR    FLOOR {bestFloors}    {bestRelics} RELICS",
                best, TextAnchor.UpperCenter,
                runIsTheRecord
                    ? RelicColor
                    : new Color(TextColor.r, TextColor.g, TextColor.b, 0.65f));

            int hint = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.035f));
            DrawLabel(new Rect(0f, Screen.height * 0.70f, Screen.width, hint * 2f),
                "TAP OR CLICK TO TRY AGAIN", hint, TextAnchor.UpperCenter);
        }

        /// <summary>
        /// Draws one statistic as a centred two-column row: a dim label on the left of the middle,
        /// its value on the right, so the pairing is unambiguous.
        /// </summary>
        /// <param name="y">Top of the row in pixels.</param>
        /// <param name="size">Font size for the row.</param>
        /// <param name="label">What the number means.</param>
        /// <param name="value">The number itself.</param>
        private static void DrawStatRow(float y, int size, string label, string value)
        {
            float gap = size * 0.7f;
            float half = Screen.width * 0.5f;

            DrawLabel(new Rect(0f, y, half - gap, size * 1.5f), label, size, TextAnchor.UpperRight,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.6f));
            DrawLabel(new Rect(half + gap, y, half - gap, size * 1.5f), value, size,
                TextAnchor.UpperLeft);
        }

        /// <summary>
        /// Draws the title screen the game waits on before a run begins.
        /// </summary>
        /// <remarks>
        /// This exists for two reasons at once. It is a front door, and it is the user gesture a
        /// browser demands before it will let any sound out — the game was silent until the player
        /// died once, because the tap to retry was the first gesture it ever received.
        /// </remarks>
        /// <param name="bestFloors">Deepest floor reached in any run, or 0 if none yet.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        public void DrawTitle(int bestFloors, int bestRelics)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            int size = Mathf.Max(24, Mathf.RoundToInt(Screen.height * 0.085f));
            DrawLabel(new Rect(0f, Screen.height * 0.26f, Screen.width, size * 2f),
                "THE BACKROOMS", size, TextAnchor.UpperCenter);

            int sub = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.028f));
            DrawLabel(new Rect(0f, Screen.height * 0.40f, Screen.width, sub * 2f),
                "Find the stairs. Something else is already here.", sub, TextAnchor.UpperCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.75f));

            int button = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.055f));
            DrawLabel(new Rect(0f, Screen.height * 0.55f, Screen.width, button * 2f),
                "▶  START RUN", button, TextAnchor.UpperCenter);

            int hint = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.026f));
            DrawLabel(new Rect(0f, Screen.height * 0.65f, Screen.width, hint * 2f),
                "tap or click anywhere", hint, TextAnchor.UpperCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.55f));

            if (bestFloors <= 0) return;
            DrawLabel(new Rect(0f, Screen.height * 0.76f, Screen.width, hint * 2f),
                $"BEST    FLOOR {bestFloors}    {bestRelics} RELICS", hint,
                TextAnchor.UpperCenter, new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f));
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
