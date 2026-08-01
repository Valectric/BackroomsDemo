using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Remembers the staircase the player arrived by, so it cannot immediately send them straight
    /// back the way they came.
    /// </summary>
    /// <remarks>
    /// Both directions trigger on proximity, and the player always arrives standing on a staircase —
    /// on a way up when descending, on a way down when climbing. Without this the two triggers fight
    /// each other and the player ping-pongs between floors on the first frame. The immunity lifts as
    /// soon as they walk off the cell, so the staircase behind them stays usable.
    /// </remarks>
    internal sealed class ArrivalGuard
    {
        /// <summary>The cell the player arrived in on this floor.</summary>
        private Vector2Int _arrivalCell;

        /// <summary>Whether the player is still standing on the staircase they arrived by.</summary>
        public bool StillOnArrivalStairs { get; private set; }

        /// <summary>
        /// Notes where the player just arrived.
        /// </summary>
        /// <param name="cell">The cell they arrived in.</param>
        public void Arrived(Vector2Int cell)
        {
            _arrivalCell = cell;
            StillOnArrivalStairs = true;
        }

        /// <summary>
        /// Lifts the immunity once the player has walked off the staircase.
        /// </summary>
        /// <param name="playerCell">The cell the player currently occupies.</param>
        public void Update(Vector2Int playerCell)
        {
            if (!StillOnArrivalStairs || playerCell == _arrivalCell) return;
            StillOnArrivalStairs = false;
        }
    }
}
