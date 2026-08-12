using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// How long the demo is and what a run is worth. Plain rules with no Unity in them, so the
    /// scoring can be asserted directly rather than played through.
    /// </summary>
    /// <remarks>
    /// The dungeon in the book is 999 floors; this is a demo of it, and a demo needs an end or it is
    /// just a treadmill that eventually crashes. Six floors is the length of the authored content —
    /// five palettes plus the loop back to the first — which is exactly the point at which a player
    /// starts seeing rooms they recognise. Ending there means the last thing they see is a
    /// congratulation rather than the moment they work out it repeats.
    /// </remarks>
    public static class DemoRun
    {
        /// <summary>
        /// The last floor of the demo. Reaching a way down here finishes the game instead of
        /// descending.
        /// </summary>
        public const int FinalFloor = 6;

        /// <summary>Points for each floor cleared.</summary>
        /// <remarks>
        /// Deliberately far larger than a relic is worth. The first floor alone carries about forty
        /// relics, so at any generous per-relic rate the optimal play would be to sweep floor 1 and
        /// never descend — which is the exact opposite of what the score is for. At these weights a
        /// full sweep of floor 1 is worth less than simply reaching floor 2.
        /// </remarks>
        public const int FloorPoints = 2000;

        /// <summary>Points for each relic found.</summary>
        public const int RelicPoints = 25;

        /// <summary>
        /// What a run scored.
        /// </summary>
        /// <param name="floorsCleared">Deepest floor reached, counting from 1.</param>
        /// <param name="relics">How many relics were found.</param>
        /// <returns>The run's score, never negative.</returns>
        public static int Score(int floorsCleared, int relics)
            => Mathf.Max(0, floorsCleared) * FloorPoints + Mathf.Max(0, relics) * RelicPoints;

        /// <summary>
        /// Whether reaching a way down on this floor finishes the demo.
        /// </summary>
        /// <param name="floor">One-based floor number the player is standing on.</param>
        /// <returns><c>true</c> if there is nothing below this floor.</returns>
        public static bool IsFinalFloor(int floor) => floor >= FinalFloor;
    }
}
