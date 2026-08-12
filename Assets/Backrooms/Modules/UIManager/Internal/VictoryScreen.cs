using UnityEngine;

namespace Backrooms.UIManager.Internal
{
    /// <summary>
    /// The screen for finishing the demo: what the player scored, thanks, and where to say what they
    /// thought.
    /// </summary>
    /// <remarks>
    /// This is the only screen in the game that is not about failure, and it is the one moment a
    /// player is most willing to say something — they have just finished, and they are pleased. It is
    /// paced like the death screen on purpose: same fade, same withheld way out, so the ending has
    /// the same weight as a death rather than flashing past on the click that earned it.
    /// <para>
    /// The score shows its arithmetic. A bare number at the end of a demo means nothing to someone
    /// seeing it once — there is no leaderboard to compare against and no second run to beat yet — so
    /// the two things it is made of are printed above it. A player who wants a better one can then
    /// see that going deeper is worth eighty relics.
    /// </para>
    /// </remarks>
    internal sealed class VictoryScreen
    {
        /// <summary>Warm gold for the congratulation, distinct from the relic violet.</summary>
        private static readonly Color WinColor = new Color(1f, 0.86f, 0.45f);

        /// <summary>
        /// Draws the whole screen for one frame.
        /// </summary>
        /// <param name="floors">How many floors were cleared.</param>
        /// <param name="relics">How many relics were found.</param>
        /// <param name="elapsedSeconds">How long the run took.</param>
        /// <param name="floorPoints">Points each floor was worth.</param>
        /// <param name="relicPoints">Points each relic was worth.</param>
        /// <param name="score">The run's total score.</param>
        /// <param name="age">Seconds since the run ended.</param>
        public void Draw(int floors, int relics, float elapsedSeconds, int floorPoints,
            int relicPoints, int score, float age)
        {
            HudText.Fill(new Color(0f, 0f, 0f, EndScreenTiming.Fade(age)));

            int size = Mathf.Max(22, Mathf.RoundToInt(Screen.height * 0.075f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.16f, Screen.width, size * 2f),
                "YOU BEAT THE DEMO", size, age, at: 0.15f, over: 1f, colour: WinColor);

            int sub = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.030f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.27f, Screen.width, sub * 2f),
                $"{floors} floors down, and you walked back out of all of them.", sub, age,
                at: 1f, over: 0.8f, colour: EndScreenTiming.Dim(0.8f));

            // The sum, written out. Each line arrives in turn so it reads as being totalled up.
            int row = Mathf.Max(15, Mathf.RoundToInt(Screen.height * 0.040f));
            float top = Screen.height * 0.40f;
            float step = row * 1.65f;

            EndScreenTiming.StatRow(top, row, "FLOORS CLEARED",
                $"{floors} × {floorPoints:N0} = {floors * floorPoints:N0}",
                EndScreenTiming.Reveal(age, 1.8f, 0.5f));
            EndScreenTiming.StatRow(top + step, row, "RELICS FOUND",
                $"{relics} × {relicPoints:N0} = {relics * relicPoints:N0}",
                EndScreenTiming.Reveal(age, 2.4f, 0.5f));
            EndScreenTiming.StatRow(top + step * 2f, row, "SURVIVED",
                HudRenderer.FormatTime(elapsedSeconds), EndScreenTiming.Reveal(age, 3f, 0.5f));

            int big = Mathf.Max(20, Mathf.RoundToInt(Screen.height * 0.055f));
            EndScreenTiming.StatRow(top + step * 3.4f, big, "SCORE", $"{score:N0}",
                EndScreenTiming.Reveal(age, 3.8f, 0.7f), WinColor);

            int thanks = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.030f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.73f, Screen.width, thanks * 2f),
                "Thank you for playing the demo.", thanks, age, at: 5f, over: 0.8f);

            // Asked for only once the score has landed, so it reads as a request rather than as a
            // banner over the thing they are still looking at.
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.79f, Screen.width, thanks * 2f),
                "Please leave some feedback below — it is the whole reason this exists.",
                thanks, age, at: 5.8f, over: 0.8f, colour: EndScreenTiming.Dim(0.75f));

            int hint = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.033f));
            EndScreenTiming.Line(new Rect(0f, Screen.height * 0.88f, Screen.width, hint * 2f),
                "TAP OR CLICK TO PLAY AGAIN", hint, age,
                at: EndScreenTiming.RetrySeconds, over: EndScreenTiming.RetryFadeSeconds);
        }
    }
}
