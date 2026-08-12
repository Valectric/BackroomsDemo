using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// The end-of-run screen a Dweller leaves behind: the world fades out under the numbers, and the
    /// way back into the game is withheld until it has.
    /// </summary>
    /// <remarks>
    /// A run ending is one of only two moments this game asks the player to stop and read something.
    /// Offering the retry immediately made that impossible — the same click that ended the run
    /// started the next one, so the screen was gone before the first line had been read, and dying
    /// registered as a stutter rather than as a loss. Holding it shut is the whole point: the first
    /// five seconds reveal the numbers while the floor you died on fades to black, and the last five
    /// leave nothing on screen but what you managed.
    /// <para>
    /// Pacing comes from <see cref="EndScreenTiming"/>, shared with the victory screen.
    /// </para>
    /// </remarks>
    internal sealed class DeathScreen
    {
        /// <summary>
        /// Draws the whole screen for one frame.
        /// </summary>
        /// <param name="floor">Floor the player died on.</param>
        /// <param name="elapsedSeconds">How long they lasted.</param>
        /// <param name="relics">How many relics they were carrying.</param>
        /// <param name="bestFloors">Deepest floor reached in any run.</param>
        /// <param name="bestRelics">Most relics carried in any run.</param>
        /// <param name="seed">Seed the run was generated from, or 0 to omit it.</param>
        /// <param name="age">Seconds since the run ended.</param>
        /// <param name="score">What the run scored, or 0 to omit the line.</param>
        public void Draw(int floor, float elapsedSeconds, int relics, int bestFloors,
            int bestRelics, int seed, float age, int score = 0)
        {
            // The black goes down first, so everything below is drawn over it rather than under it.
            HudText.Fill(new Color(0f, 0f, 0f, EndScreenTiming.Fade(age)));

            int size = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.07f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.24f, Screen.width, size * 2f),
                "A DWELLER FOUND YOU", size, age, at: 0.15f, over: 1f);

            // Each figure gets its own labelled row, revealed one at a time. Three bare numbers on one
            // line — "3  2  01:14" — never said which was which, and all four rows appearing at once
            // is a wall of text at the exact moment the player is not yet reading.
            int row = Mathf.Max(15, Mathf.RoundToInt(Screen.height * 0.040f));
            float top = Screen.height * 0.38f;
            float step = row * 1.65f;
            EndScreenTiming.StatRow(top, row, "REACHED", $"FLOOR {floor}",
                EndScreenTiming.Reveal(age, 1.4f, 0.5f));
            EndScreenTiming.StatRow(top + step, row, "RELICS FOUND", relics.ToString(),
                EndScreenTiming.Reveal(age, 2f, 0.5f));
            EndScreenTiming.StatRow(top + step * 2f, row, "SURVIVED",
                HudRenderer.FormatTime(elapsedSeconds), EndScreenTiming.Reveal(age, 2.6f, 0.5f));

            if (score > 0)
            {
                EndScreenTiming.StatRow(top + step * 3.2f, row, "SCORE", $"{score:N0}",
                    EndScreenTiming.Reveal(age, 3.2f, 0.5f), HudText.RelicColor);
            }

            // The record is submitted before this screen is drawn, so on a best run the "best" line
            // repeated the run's own numbers and read as the same thing printed twice. When the run
            // IS the record, say that instead of restating it.
            bool runIsTheRecord = bestFloors <= floor && bestRelics <= relics;
            int best = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.032f));
            EndScreenTiming.Line(new Rect(0f, top + step * 4.6f, Screen.width, best * 2f),
                runIsTheRecord
                    ? "NEW BEST"
                    : $"BEST SO FAR    FLOOR {bestFloors}    {bestRelics} RELICS",
                best, age, at: 3.9f, over: 0.6f,
                colour: runIsTheRecord ? HudText.RelicColor : EndScreenTiming.Dim(0.65f));

            // Withheld until the ten seconds are up, and it is the only thing on screen that moves —
            // so the moment the game becomes playable again is unmistakable without saying so.
            int hint = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.035f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.72f, Screen.width, hint * 2f),
                "TAP OR CLICK TO TRY AGAIN", hint, age,
                at: EndScreenTiming.RetrySeconds, over: EndScreenTiming.RetryFadeSeconds);

            // Every run draws its own seed, so without showing it a bug report describes a floor
            // nobody can ever visit again. Dim and out of the way — it is for the one player in a
            // hundred who reports something, not for the other ninety-nine.
            if (seed == 0) return;

            int small = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.024f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.82f, Screen.width, small * 2f),
                $"seed {seed}", small, age, at: 4.6f, over: 0.6f,
                colour: EndScreenTiming.Dim(0.45f));
        }
    }
}
