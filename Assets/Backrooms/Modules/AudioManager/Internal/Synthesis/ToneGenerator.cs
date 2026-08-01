using UnityEngine;

namespace Backrooms.AudioManager.Internal.Synthesis
{
    /// <summary>
    /// Builds every sound the game makes as raw samples, at runtime. Pure arithmetic over float
    /// arrays — no Unity scene access and no imported audio whatsoever.
    /// </summary>
    /// <remarks>
    /// Synthesising rather than importing is what makes audio possible here at all. The repo is a
    /// public showcase and may only carry CC0 assets, which rules out most sound libraries and makes
    /// the rest a licensing audit; a waveform computed from a formula has no licence to audit. It is
    /// also a few kilobytes of code against megabytes of clips, on a build that has to load over a
    /// phone connection.
    /// </remarks>
    internal static class ToneGenerator
    {
        /// <summary>Sample rate every generated clip uses, in hertz.</summary>
        public const int SampleRate = 44100;

        /// <summary>
        /// A seamless looping hum, built from a mains-frequency fundamental and its harmonics with a
        /// slow beat between them — the sound of a room full of failing fluorescent tubes.
        /// </summary>
        /// <remarks>
        /// The loop length is a whole number of cycles of the fundamental, which is what makes the
        /// seam inaudible: end the buffer mid-cycle and every repeat clicks.
        /// </remarks>
        /// <param name="fundamental">Base frequency in hertz. 50 or 60 reads as mains hum.</param>
        /// <param name="cycles">How many cycles of the fundamental the loop lasts.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples in the range -1 to 1.</returns>
        public static float[] Hum(float fundamental, int cycles, float amplitude)
        {
            int length = Mathf.Max(2, Mathf.RoundToInt(cycles * SampleRate / fundamental));
            var samples = new float[length];

            for (int i = 0; i < length; i++)
            {
                // Phase runs over an exact whole number of cycles across the buffer, so the end
                // meets the start.
                double phase = 2.0 * Mathf.PI * cycles * i / length;

                double value = Mathf.Sin((float)phase)
                               + 0.42f * Mathf.Sin((float)(phase * 2.0))
                               + 0.18f * Mathf.Sin((float)(phase * 4.0))
                               + 0.09f * Mathf.Sin((float)(phase * 6.0));

                // A slow tremble across the loop, again on a whole number of cycles.
                double wobble = 1.0 + 0.06 * Mathf.Sin((float)(2.0 * Mathf.PI * 3 * i / length));

                samples[i] = (float)(value * wobble * amplitude / 1.69);
            }

            return samples;
        }

        /// <summary>
        /// A low drone that slides upward across its length, used to voice a Dweller closing in. It
        /// loops, so it can be held for as long as the chase lasts.
        /// </summary>
        /// <param name="fundamental">Starting frequency in hertz.</param>
        /// <param name="cycles">How many cycles of the fundamental the loop lasts.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples in the range -1 to 1.</returns>
        public static float[] Drone(float fundamental, int cycles, float amplitude)
        {
            int length = Mathf.Max(2, Mathf.RoundToInt(cycles * SampleRate / fundamental));
            var samples = new float[length];

            for (int i = 0; i < length; i++)
            {
                double turn = 2.0 * Mathf.PI * i / length;

                // A slightly detuned pair beating against each other, which is far more unsettling
                // than a clean tone and costs one extra sine. The detune is expressed as one extra
                // whole cycle across the loop rather than as a frequency ratio: a ratio like 1.0136
                // leaves the partial part-way through a cycle at the buffer's end, and the loop then
                // clicks on every repeat — audible, and exactly what a ratio that reads as harmless
                // will do.
                double value = Mathf.Sin((float)(turn * cycles))
                               + 0.8f * Mathf.Sin((float)(turn * (cycles + 1)))
                               + 0.30f * Mathf.Sin((float)(turn * cycles * 3));

                samples[i] = (float)(value * amplitude / 2.1);
            }

            return samples;
        }

        /// <summary>
        /// A short broadband thud with a fast decay — one footfall on carpet over concrete.
        /// </summary>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="seed">Seed for the noise, so a given step is reproducible.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples in the range -1 to 1.</returns>
        public static float[] Footstep(float seconds, int seed, float amplitude)
        {
            int length = Mathf.Max(2, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[length];
            var rng = new System.Random(seed);

            // Three one-pole low passes in series, cut very low. Each stage halves what gets
            // through again, and at this cutoff almost nothing above the bottom octaves survives —
            // the noise is there for the scuff of the contact, not to be heard as noise.
            float stage1 = 0f;
            float stage2 = 0f;
            float stage3 = 0f;

            // The body is the sound. A real footfall on a floor is felt more than heard, and on a
            // phone speaker only the bottom of it survives at all, so the thump carries the weight
            // and the filtered noise only takes the edge off its attack.
            const float BodyStartHz = 78f;
            const float BodyEndHz = 42f;

            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                stage1 += (noise - stage1) * 0.022f;
                stage2 += (stage1 - stage2) * 0.022f;
                stage3 += (stage2 - stage3) * 0.022f;

                float t = i / (float)length;

                // Sweep the body downward like a struck surface settling, rather than holding one
                // pitch, which reads as a tone instead of an impact.
                float frequency = Mathf.Lerp(BodyStartHz, BodyEndHz, Mathf.Sqrt(t));
                phase += 2.0 * Mathf.PI * frequency / SampleRate;

                float body = Mathf.Sin((float)phase) * Mathf.Exp(-11f * t);
                float scuff = stage3 * Mathf.Exp(-24f * t);

                samples[i] = (body * 0.95f + scuff * 3.2f) * amplitude;
            }

            return Clamped(samples);
        }

        /// <summary>
        /// A bright two-note chime with a long tail, for picking up a relic. The interval is a
        /// perfect fifth, which is the most unambiguously positive thing available for free.
        /// </summary>
        /// <param name="root">Root frequency in hertz.</param>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples in the range -1 to 1.</returns>
        public static float[] Chime(float root, float seconds, float amplitude)
        {
            int length = Mathf.Max(2, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float decay = Mathf.Exp(-4.2f * i / length);

                double value = Mathf.Sin(2f * Mathf.PI * root * t)
                               + 0.7f * Mathf.Sin(2f * Mathf.PI * root * 1.5f * t)
                               + 0.35f * Mathf.Sin(2f * Mathf.PI * root * 3f * t);

                samples[i] = (float)(value * decay * amplitude / 2.05);
            }

            return samples;
        }

        /// <summary>
        /// A falling tone, for dropping a floor. Pitch slides down across the whole sound, which is
        /// the most direct way to say "downward" without a word.
        /// </summary>
        /// <param name="startFrequency">Frequency at the start, in hertz.</param>
        /// <param name="endFrequency">Frequency at the end, in hertz.</param>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="amplitude">Peak amplitude, 0 to 1.</param>
        /// <returns>Mono samples in the range -1 to 1.</returns>
        public static float[] Descend(float startFrequency, float endFrequency, float seconds,
            float amplitude)
        {
            int length = Mathf.Max(2, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[length];

            // Integrate the frequency ramp rather than plugging it into sin(2*pi*f*t) — doing the
            // latter sweeps the phase, not the pitch, and it warbles.
            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t * t);
                phase += 2.0 * Mathf.PI * frequency / SampleRate;

                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin((float)phase) * envelope * amplitude;
            }

            return samples;
        }

        /// <summary>
        /// Clamps every sample into the legal range, so a generator that overshoots distorts
        /// predictably rather than wrapping.
        /// </summary>
        /// <param name="samples">Samples to clamp, modified in place.</param>
        /// <returns>The same array.</returns>
        private static float[] Clamped(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            return samples;
        }
    }
}
