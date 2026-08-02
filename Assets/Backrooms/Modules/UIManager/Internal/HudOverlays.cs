using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// Draws the parts of the HUD that sit over the world rather than reporting run state: the relic
    /// compass arrows, the touch-pad hints, and the prompt to turn a portrait phone sideways.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="HudRenderer"/>, which was carrying both the run readouts and these, and
    /// had grown past the size a file should be.
    /// </remarks>
    internal sealed class HudOverlays
    {
        /// <summary>Bone-white HUD text, slightly warm to match the level.</summary>
        private static readonly Color TextColor = new Color(0.96f, 0.95f, 0.88f);

        /// <summary>Dim backing so text stays readable against pale walls.</summary>
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>
        /// Draws one arrow per compass relic, laid along a strip under the top of the screen.
        /// </summary>
        /// <remarks>
        /// Position along the strip encodes the bearing, so an arrow drifting left means turn left.
        /// A ring around the centre would be more literal but would sit over the middle of the
        /// screen, which in a first-person horror game is the one place nothing may cover.
        /// </remarks>
        /// <param name="marks">The arrows to draw.</param>
        /// <param name="topEdge">Bottom of whatever is above the compass, in pixels.</param>
        public void DrawCompass(IReadOnlyList<CompassMark> marks, float topEdge)
        {
            if (marks == null || marks.Count == 0) return;

            float width = Screen.width;
            float centre = width * 0.5f;
            int size = Mathf.Max(15, Mathf.RoundToInt(Screen.height * 0.038f));

            // Sits at its normal height when nothing is above it, and slides down to clear the
            // carried list when there is one. Both are centred, so without this they stack up in the
            // same place and the distances become unreadable exactly when the player has most to read.
            float y = Mathf.Max(Screen.height * 0.055f, topEdge + Screen.height * 0.012f);

            foreach (CompassMark mark in marks)
            {
                // Map -180..180 degrees across the screen. Anything behind the player pins to an
                // edge, which still reads correctly as "turn that way".
                float t = Mathf.Clamp(mark.Bearing / 90f, -1f, 1f);
                float x = centre + t * (width * 0.42f);

                string glyph = mark.Bearing < -6f ? "◀" : mark.Bearing > 6f ? "▶" : "▲";
                var rect = new Rect(x - size * 2f, y, size * 4f, size * 2.4f);
                DrawLabel(rect, $"{glyph}\n{Mathf.RoundToInt(mark.Distance)}m", size,
                    TextAnchor.UpperCenter, mark.Colour);
            }
        }

        /// <summary>
        /// Draws what the player is carrying that has uses left, across the top of the screen.
        /// </summary>
        /// <remarks>
        /// Top-centre rather than tucked into a bottom corner: what you are carrying decides what you
        /// can do about the thing in front of you, so it belongs where the player is already looking
        /// rather than where they have to go and check.
        /// </remarks>
        /// <param name="lines">One line per carried relic.</param>
        /// <param name="colours">Colour for each line, same order.</param>
        /// <returns>The y coordinate the list ends at, so the compass can sit under it.</returns>
        public float DrawCarried(IReadOnlyList<string> lines, IReadOnlyList<Color> colours)
        {
            float top = Screen.height * 0.012f;
            if (lines == null || lines.Count == 0) return top;

            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.028f));
            float step = size * 1.22f;

            for (int i = 0; i < lines.Count; i++)
            {
                var rect = new Rect(0f, top + i * step, Screen.width, size * 1.5f);
                DrawLabel(rect, lines[i], size, TextAnchor.UpperCenter,
                    i < colours.Count ? colours[i] : TextColor);
            }

            return top + lines.Count * step;
        }

        /// <summary>
        /// Draws the touch zones: which half of the screen moves you and which half looks.
        /// </summary>
        /// <remarks>
        /// The touch scheme has no on-screen widgets on purpose, which keeps it out of the way but
        /// also makes it invisible — nothing ever told the player the left half was a stick. These
        /// hints are strong for the first few seconds of a floor and then fade to almost nothing, so
        /// they teach without becoming furniture.
        /// </remarks>
        /// <param name="strength">How visible the hints should be, 0 to 1.</param>
        public void DrawPadHints(float strength)
        {
            if (strength <= 0.01f) return;

            float half = Screen.width * 0.5f;
            Color previous = GUI.color;

            // A seam down the middle, so the two halves read as two controls.
            GUI.color = new Color(1f, 1f, 1f, 0.10f * strength);
            GUI.DrawTexture(new Rect(half - 1f, 0f, 2f, Screen.height), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.05f * strength);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.45f, half, Screen.height * 0.55f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(half, Screen.height * 0.45f, half, Screen.height * 0.55f),
                Texture2D.whiteTexture);
            GUI.color = previous;

            int size = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.032f));
            var label = new Color(TextColor.r, TextColor.g, TextColor.b, strength);
            float y = Screen.height * 0.86f;

            DrawLabel(new Rect(0f, y, half, size * 3f),
                "DRAG TO MOVE\nDOUBLE-TAP: BANISHER", size, TextAnchor.UpperCenter, label);
            DrawLabel(new Rect(half, y, half, size * 3f),
                "DRAG TO LOOK\nDOUBLE-TAP: BLINK", size, TextAnchor.UpperCenter, label);
        }

        /// <summary>
        /// Draws the prompt asking for a landscape phone, covering everything.
        /// </summary>
        /// <remarks>
        /// The level is built around a wide field of view and a HUD anchored to the corners. In
        /// portrait the corners crowd the middle and the fog fills the frame, so the game is not
        /// merely uglier — it is harder to play. Better to ask than to let someone judge it sideways.
        /// </remarks>
        public void DrawRotatePrompt()
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.03f, 0.03f, 0.04f, 0.97f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            int size = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.038f));
            DrawLabel(new Rect(0f, Screen.height * 0.42f, Screen.width, size * 4f),
                "↻\nTURN YOUR PHONE SIDEWAYS", size, TextAnchor.UpperCenter, TextColor);

            int hint = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.024f));
            DrawLabel(new Rect(0f, Screen.height * 0.56f, Screen.width, hint * 3f),
                "The Backrooms are wider than they are tall", hint, TextAnchor.UpperCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f));
        }

        /// <summary>
        /// Draws a label with a one-pixel drop shadow so it reads against any background.
        /// </summary>
        /// <param name="rect">Screen rectangle to draw in.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <param name="anchor">Alignment within the rectangle.</param>
        /// <param name="colour">Text colour.</param>
        private static void DrawLabel(Rect rect, string text, int fontSize, TextAnchor anchor,
            Color colour)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = anchor,
                wordWrap = false
            };

            style.normal.textColor = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b,
                ShadowColor.a * colour.a);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);

            style.normal.textColor = colour;
            GUI.Label(rect, text, style);
        }
    }
}
