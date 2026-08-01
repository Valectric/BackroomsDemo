using Backrooms.AudioManager.Internal;
using Backrooms.AudioManager.Internal.Synthesis;

namespace Backrooms.AudioManager
{
    /// <summary>
    /// Test seam for the AudioManager module. Its constructor takes the internal router, so only the
    /// production <see cref="AudioFacade"/> can create one. Not intended for production use — only
    /// for automated testing. Exposes the generated waveforms so their shape can be asserted without
    /// anything having to be listened to.
    /// </summary>
    public sealed class AudioManagerTestFacade
    {
        private readonly AudioRouter _router;

        /// <summary>
        /// Creates the test facade over the module's internal router.
        /// </summary>
        /// <param name="router">The module's internal router.</param>
        internal AudioManagerTestFacade(AudioRouter router)
        {
            _router = router;
        }

        /// <summary>Volume the pursuit drone is currently at.</summary>
        public float DroneVolume => _router.DroneVolume;

        /// <summary>Whether the room hum is playing.</summary>
        public bool HumPlaying => _router.HumPlaying;

        /// <summary>Sample rate every generated clip uses, in hertz.</summary>
        public int SampleRate => ToneGenerator.SampleRate;

        /// <summary>
        /// Generates the room hum waveform.
        /// </summary>
        /// <param name="fundamental">Base frequency in hertz.</param>
        /// <param name="cycles">How many cycles of the fundamental the loop lasts.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples.</returns>
        public float[] Hum(float fundamental, int cycles, float amplitude)
            => ToneGenerator.Hum(fundamental, cycles, amplitude);

        /// <summary>
        /// Generates the pursuit drone waveform.
        /// </summary>
        /// <param name="fundamental">Base frequency in hertz.</param>
        /// <param name="cycles">How many cycles of the fundamental the loop lasts.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples.</returns>
        public float[] Drone(float fundamental, int cycles, float amplitude)
            => ToneGenerator.Drone(fundamental, cycles, amplitude);

        /// <summary>
        /// Generates one footstep waveform.
        /// </summary>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="seed">Seed for the noise.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples.</returns>
        public float[] Footstep(float seconds, int seed, float amplitude)
            => ToneGenerator.Footstep(seconds, seed, amplitude);

        /// <summary>
        /// Generates the relic chime waveform.
        /// </summary>
        /// <param name="root">Root frequency in hertz.</param>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples.</returns>
        public float[] Chime(float root, float seconds, float amplitude)
            => ToneGenerator.Chime(root, seconds, amplitude);

        /// <summary>
        /// Generates the descent waveform.
        /// </summary>
        /// <param name="startFrequency">Frequency at the start, in hertz.</param>
        /// <param name="endFrequency">Frequency at the end, in hertz.</param>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples.</returns>
        public float[] Descend(float startFrequency, float endFrequency, float seconds, float amplitude)
            => ToneGenerator.Descend(startFrequency, endFrequency, seconds, amplitude);
    }
}
