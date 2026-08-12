using System.Collections.Generic;
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
        private static readonly Color TextColor = HudText.TextColor;

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
        private static readonly Color RelicColor = HudText.RelicColor;

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
        /// <param name="relics">Uncollected relics as fractions of the floor.</param>
        /// <param name="topEdge">Bottom of whatever is above the map, in pixels.</param>
        public void DrawMap(Texture2D map, Vector2 player, float window,
            IReadOnlyList<Vector2> relics, float topEdge = 0f)
        {
            if (map == null) return;

            int size = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.22f), 88, 220);
            int margin = Mathf.Max(10, Mathf.RoundToInt(Screen.height * 0.03f));

            // Below the compass band, which runs across the top: at the right-hand end the two
            // overlapped and the map sat on top of an arrow's distance label. The band itself moves
            // down when the player is carrying a lot, so the map follows it rather than assuming.
            float top = Mathf.Max(Screen.height * 0.17f, topEdge + Screen.height * 0.10f);
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

            float dotSize = Mathf.Max(5f, size * 0.045f);

            // Relics first, so the player marker is never hidden under one they are standing on.
            if (relics != null)
            {
                foreach (Vector2 relic in relics)
                {
                    if (relic.x < x || relic.x > x + window) continue;
                    if (relic.y < y || relic.y > y + window) continue;

                    DrawMarker(
                        frame.x + (relic.x - x) / window * frame.width,
                        frame.yMax - (relic.y - y) / window * frame.height,
                        dotSize * 0.85f, RelicColor);
                }
            }

            // The player, drawn where they actually are inside the window rather than always at its
            // centre — at the floor's edge the window stops moving and they walk to the corner.
            DrawMarker(
                frame.x + (player.x - x) / window * frame.width,
                frame.yMax - (player.y - y) / window * frame.height,
                dotSize, new Color(1f, 0.35f, 0.3f));

            GUI.color = previous;
        }

        /// <summary>
        /// Draws one map marker: a dark square with a coloured one inside it.
        /// </summary>
        /// <remarks>
        /// The backing is what makes it legible. The map is a pale beige, and a few violet pixels on
        /// it read as noise at the size this sits on a phone.
        /// </remarks>
        /// <param name="centreX">Marker centre, in pixels.</param>
        /// <param name="centreY">Marker centre, in pixels.</param>
        /// <param name="size">Marker size in pixels.</param>
        /// <param name="colour">Marker colour.</param>
        private static void DrawMarker(float centreX, float centreY, float size, Color colour)
        {
            GUI.color = new Color(0.05f, 0.05f, 0.04f, 0.9f);
            GUI.DrawTexture(
                new Rect(centreX - size * 0.5f - 1f, centreY - size * 0.5f - 1f, size + 2f,
                    size + 2f), Texture2D.whiteTexture);

            GUI.color = colour;
            GUI.DrawTexture(new Rect(centreX - size * 0.5f, centreY - size * 0.5f, size, size),
                Texture2D.whiteTexture);
        }

        /// <summary>
        /// Draws the flash confirming a relic was just picked up.
        /// </summary>
        /// <param name="relics">How many the player now carries.</param>
        /// <param name="message">What to say, or null for the pickup line.</param>
        /// <param name="colour">Colour to say it in, used only with a message.</param>
        public void DrawRelicFlash(int relics, string message = null, Color default_ = default)
        {
            int size = Mathf.Max(18, Mathf.RoundToInt(Screen.height * 0.05f));
            var rect = new Rect(0f, Screen.height * 0.62f, Screen.width, size * 2f);

            string text = string.IsNullOrEmpty(message)
                ? $"{RelicMark} RELIC RECOVERED  ({relics})"
                : message;

            DrawLabel(rect, text, size, TextAnchor.UpperCenter,
                string.IsNullOrEmpty(message) ? RelicColor : default_);
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
            => HudText.DrawLabel(rect, text, fontSize, anchor, colour);
    }
}
