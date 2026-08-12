using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// The end-of-run screen a Dweller leaves behind, and the pacing that goes with it: the world
    /// fades out under the numbers, and the way back into the game is withheld until it has.
    /// </summary>
    /// <remarks>
    /// A run ending is the only moment this game asks the player to stop and read something. Offering
    /// the retry immediately made that impossible — the same click that ended the run started the
    /// next one, so the screen was gone before the first line had been read, and dying registered as
    /// a stutter rather than as a loss. Holding it shut for ten seconds is the whole point: the first
    /// five reveal the numbers while the floor you died on fades to black, and the last five leave
    /// nothing on screen but what you managed.
    /// <para>
    /// The clock and the drawing live in the same class deliberately. The gate on the retry and the
    /// fade have to agree about when the fade is over, and they only stay agreed while there is one
    /// set of numbers for both to read.
    /// </para>
    /// </remarks>
    internal sealed class DeathScreen
    {
        /// <summary>Seconds the world takes to fade out completely.</summary>
        public const float FadeSeconds = 5f;

        /// <summary>Seconds before the player is allowed to start another run.</summary>
        public const float RetrySeconds = 10f;

        /// <summary>Seconds the retry prompt takes to fade up once it is due.</summary>
        private const float RetryFadeSeconds = 0.8f;

        /// <summary>
        /// How opaque the black covering the world should be, for a given age of the screen.
        /// </summary>
        /// <param name="sinceCaught">Seconds since the run ended.</param>
        /// <returns>0 while the floor is still fully visible, 1 once it has gone.</returns>
        public static float Fade(float sinceCaught)
            => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sinceCaught / FadeSeconds));

        /// <summary>
        /// Whether the player may start another run yet.
        /// </summary>
        /// <param name="sinceCaught">Seconds since the run ended.</param>
        /// <returns><c>true</c> once the screen has been up long enough to have been read.</returns>
        public static bool RetryOffered(float sinceCaught) => sinceCaught >= RetrySeconds;

        /// <summary>
        /// Draws the whole screen for one frame.
        /// </summary>
        /// <param name="floor">Floor the player died on.</param>
        /// <param name="elapsedSeconds">How long they lasted.</param>
        /// <param name="relics">How many relics they were carrying.</param>
        /// <param name="bestFloors">Deepest floor reached in any run.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        /// <param name="seed">Seed the run was generated from, or 0 to omit it.</param>
        /// <param name="sinceCaught">Seconds since the run ended.</param>
        public void Draw(int floor, float elapsedSeconds, int relics, int bestFloors,
            int bestRelics, int seed, float sinceCaught)
        {
            // The black goes down first, so everything below is drawn over it rather than under it.
            HudText.Fill(new Color(0f, 0f, 0f, Fade(sinceCaught)));

            int size = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.07f));
            Line(new Rect(0f, Screen.height * 0.26f, Screen.width, size * 2f),
                "A DWELLER FOUND YOU", size, sinceCaught, at: 0.15f, over: 1f);

            // Each figure gets its own labelled row, revealed one at a time. Three bare numbers on one
            // line — "3  2  01:14" — never said which was which, and all four rows appearing at once
            // is a wall of text at the exact moment the player is not yet reading.
            int row = Mathf.Max(15, Mathf.RoundToInt(Screen.height * 0.040f));
            float top = Screen.height * 0.40f;
            float step = row * 1.65f;
            StatRow(top, row, "REACHED", $"FLOOR {floor}", Reveal(sinceCaught, 1.4f, 0.5f));
            StatRow(top + step, row, "RELICS FOUND", relics.ToString(),
                Reveal(sinceCaught, 2f, 0.5f));
            StatRow(top + step * 2f, row, "SURVIVED", HudRenderer.FormatTime(elapsedSeconds),
                Reveal(sinceCaught, 2.6f, 0.5f));

            // The record is submitted before this screen is drawn, so on a best run the "best" line
            // repeated the run's own numbers and read as the same thing printed twice. When the run
            // IS the record, say that instead of restating it.
            bool runIsTheRecord = bestFloors <= floor && bestRelics <= relics;
            int best = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.032f));
            Line(new Rect(0f, top + step * 3.3f, Screen.width, best * 2f),
                runIsTheRecord
                    ? "NEW BEST"
                    : $"BEST SO FAR    FLOOR {bestFloors}    {bestRelics} RELICS",
                best, sinceCaught, at: 3.4f, over: 0.6f,
                colour: runIsTheRecord ? HudText.RelicColor : Dim(0.65f));

            // Withheld until the ten seconds are up, and it is the only thing on screen that moves —
            // so the moment the game becomes playable again is unmistakable without saying so.
            int hint = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.035f));
            Line(new Rect(0f, Screen.height * 0.70f, Screen.width, hint * 2f),
                "TAP OR CLICK TO TRY AGAIN", hint, sinceCaught,
                at: RetrySeconds, over: RetryFadeSeconds);

            // Every run draws its own seed, so without showing it a bug report describes a floor
            // nobody can ever visit again. Dim and out of the way — it is for the one player in a
            // hundred who reports something, not for the other ninety-nine.
            if (seed == 0) return;

            int small = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.024f));
            Line(new Rect(0f, Screen.height * 0.80f, Screen.width, small * 2f),
                $"seed {seed}", small, sinceCaught, at: 4.2f, over: 0.6f, colour: Dim(0.45f));
        }

        /// <summary>
        /// How far through its entrance a line of text is.
        /// </summary>
        /// <param name="sinceCaught">Seconds since the run ended.</param>
        /// <param name="at">When this line starts appearing, in seconds since the run ended.</param>
        /// <param name="over">How long it takes to reach full strength, in seconds.</param>
        /// <returns>0 before it is due, 1 once it has fully arrived.</returns>
        private static float Reveal(float sinceCaught, float at, float over)
            => Mathf.Clamp01((sinceCaught - at) / Mathf.Max(over, 0.0001f));

        /// <summary>
        /// The HUD's text colour at a reduced opacity.
        /// </summary>
        /// <param name="alpha">Opacity to use, 0 to 1.</param>
        /// <returns>The dimmed colour.</returns>
        private static Color Dim(float alpha) => new Color(
            HudText.TextColor.r, HudText.TextColor.g, HudText.TextColor.b, alpha);

        /// <summary>
        /// Draws one line of the screen, faded in on its own schedule.
        /// </summary>
        /// <param name="rect">Screen rectangle to draw in.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <param name="sinceCaught">Seconds since the run ended.</param>
        /// <param name="at">When this line starts appearing, in seconds since the run ended.</param>
        /// <param name="over">How long it takes to reach full strength, in seconds.</param>
        /// <param name="colour">Colour at full strength, or <c>null</c> for the standard HUD white.</param>
        private static void Line(Rect rect, string text, int fontSize, float sinceCaught,
            float at, float over, Color? colour = null)
        {
            float reveal = Reveal(sinceCaught, at, over);
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
        private static void StatRow(float y, int size, string label, string value, float reveal)
        {
            if (reveal <= 0f) return;

            float gap = size * 0.7f;
            float half = Screen.width * 0.5f;

            HudText.DrawLabel(new Rect(0f, y, half - gap, size * 1.5f), label, size,
                TextAnchor.UpperRight, Dim(0.6f * reveal));
            HudText.DrawLabel(new Rect(half + gap, y, half - gap, size * 1.5f), value, size,
                TextAnchor.UpperLeft, Dim(reveal));
        }
    }
}
