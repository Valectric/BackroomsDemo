using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// The pacing both end screens share: how fast the world fades out under the text, when each line
    /// arrives, and how long before the player is allowed to start another run.
    /// </summary>
    /// <remarks>
    /// One set of numbers, because the gate on the way out and the fade have to agree about when the
    /// fade is over. Two copies would drift the first time either was tuned, and the symptom — a
    /// screen that offers a retry while it is still fading in, or holds one shut after it has settled
    /// — is the kind of thing nobody files a bug about, they just stop playing.
    /// </remarks>
    internal static class EndScreenTiming
    {
        /// <summary>Seconds the world takes to fade out completely.</summary>
        public const float FadeSeconds = 5f;

        /// <summary>Seconds before the player is allowed to start another run.</summary>
        public const float RetrySeconds = 10f;

        /// <summary>Seconds the retry prompt takes to fade up once it is due.</summary>
        public const float RetryFadeSeconds = 0.8f;

        /// <summary>
        /// How opaque the black covering the world should be, for a given age of the screen.
        /// </summary>
        /// <param name="age">Seconds since the run ended.</param>
        /// <returns>0 while the floor is still fully visible, 1 once it has gone.</returns>
        public static float Fade(float age)
            => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / FadeSeconds));

        /// <summary>
        /// Whether the player may start another run yet.
        /// </summary>
        /// <param name="age">Seconds since the run ended.</param>
        /// <returns><c>true</c> once the screen has been up long enough to have been read.</returns>
        public static bool RetryOffered(float age) => age >= RetrySeconds;

        /// <summary>
        /// How far through its entrance a line of text is.
        /// </summary>
        /// <param name="age">Seconds since the run ended.</param>
        /// <param name="at">When this line starts appearing, in seconds since the run ended.</param>
        /// <param name="over">How long it takes to reach full strength, in seconds.</param>
        /// <returns>0 before it is due, 1 once it has fully arrived.</returns>
        public static float Reveal(float age, float at, float over)
            => Mathf.Clamp01((age - at) / Mathf.Max(over, 0.0001f));

        /// <summary>
        /// The HUD's text colour at a reduced opacity.
        /// </summary>
        /// <param name="alpha">Opacity to use, 0 to 1.</param>
        /// <returns>The dimmed colour.</returns>
        public static Color Dim(float alpha) => new Color(
            HudText.TextColor.r, HudText.TextColor.g, HudText.TextColor.b, alpha);

        /// <summary>
        /// Draws one line of an end screen, faded in on its own schedule.
        /// </summary>
        /// <param name="rect">Screen rectangle to draw in.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <param name="age">Seconds since the run ended.</param>
        /// <param name="at">When this line starts appearing, in seconds since the run ended.</param>
        /// <param name="over">How long it takes to reach full strength, in seconds.</param>
        /// <param name="colour">Colour at full strength, or <c>null</c> for the standard HUD white.</param>
        public static void Line(Rect rect, string text, int fontSize, float age,
            float at, float over, Color? colour = null)
        {
            float reveal = Reveal(age, at, over);
            if (reveal <= 0f) return;

            Color ink = colour ?? HudText.TextColor;
            HudText.DrawLabel(rect, text, fontSize, TextAnchor.UpperCenter,
                new Color(ink.r, ink.g, ink.b, ink.a * reveal));
        }

        /// <summary>
        /// Draws one statistic as a centred two-column row: a dim label on the left of the middle,
        /// its value on the right, so the pairing is unambiguous.
        /// </summary>
        /// <param name="y">Top of the row in pixels.</param>
        /// <param name="size">Font size for the row.</param>
        /// <param name="label">What the number means.</param>
        /// <param name="value">The number itself.</param>
        /// <param name="reveal">How far through its entrance the row is, 0 to 1.</param>
        /// <param name="valueColour">Colour for the value, or <c>null</c> for the standard HUD white.</param>
        public static void StatRow(float y, int size, string label, string value, float reveal,
            Color? valueColour = null)
        {
            if (reveal <= 0f) return;

            float gap = size * 0.7f;
            float half = Screen.width * 0.5f;
            Color ink = valueColour ?? HudText.TextColor;

            HudText.DrawLabel(new Rect(0f, y, half - gap, size * 1.5f), label, size,
                TextAnchor.UpperRight, Dim(0.6f * reveal));
            HudText.DrawLabel(new Rect(half + gap, y, half - gap, size * 1.5f), value, size,
                TextAnchor.UpperLeft, new Color(ink.r, ink.g, ink.b, ink.a * reveal));
        }
    }
}
