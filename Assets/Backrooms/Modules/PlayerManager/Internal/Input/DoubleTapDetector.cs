using UnityEngine;

namespace Backrooms.PlayerManager.Internal.Input
{
    /// <summary>
    /// Recognises a double tap on one half of the screen, from a stream of press events.
    /// </summary>
    /// <remarks>
    /// Pure logic over a supplied timestamp rather than a wall clock, so a test can drive it through
    /// any timing it likes and the recognition is reproducible. The touch scheme has no on-screen
    /// buttons on purpose — the left half is already a virtual stick and the right half is already
    /// the camera — so a gesture is the only input left that does not steal from either.
    /// </remarks>
    internal sealed class DoubleTapDetector
    {
        /// <summary>Longest gap between two presses that still counts as one gesture, in seconds.</summary>
        private const float WindowSeconds = 0.32f;

        /// <summary>When the previous press landed, or a large negative if there has not been one.</summary>
        private float _previousPress = float.NegativeInfinity;

        /// <summary>
        /// Records a press and reports whether it completed a double tap.
        /// </summary>
        /// <param name="time">Time of the press, in seconds.</param>
        /// <returns><c>true</c> if this press is the second of a pair.</returns>
        public bool Press(float time)
        {
            bool doubled = time - _previousPress <= WindowSeconds;

            // Consume the pair rather than leaving it open, or a third quick press fires again and a
            // triple tap becomes two activations.
            _previousPress = doubled ? float.NegativeInfinity : time;
            return doubled;
        }

        /// <summary>
        /// Forgets any press in progress.
        /// </summary>
        public void Reset() => _previousPress = float.NegativeInfinity;
    }
}
