using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.AudioManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for the synthesised waveforms. Nobody can listen to a test, but the
    /// faults that make generated audio unusable are all measurable: clipping, a discontinuity at a
    /// loop seam, silence, or a constant offset. These assert the shape of the samples directly.
    /// </summary>
    public class ToneGeneratorTests
    {
        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();

            // The real game's listener rides the player's head camera, which these tests do not
            // build. Without one Unity warns on every run, and a suite that leaves warnings behind
            // is not a clean pass.
            var ears = new GameObject("TestAudioListener");
            ears.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Creates an audio module and returns its test seam.
        /// </summary>
        /// <returns>A test facade over a new audio module.</returns>
        private static AudioManagerTestFacade NewAudio()
        {
            var go = new GameObject("Audio");
            return go.AddComponent<AudioFacade>().GetTestFacade();
        }

        /// <summary>
        /// The loudest absolute sample in a buffer.
        /// </summary>
        /// <param name="samples">Samples to measure.</param>
        /// <returns>Peak absolute amplitude.</returns>
        private static float Peak(float[] samples)
        {
            float peak = 0f;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            return peak;
        }

        /// <summary>
        /// The mean of a buffer, which should sit near zero for anything audible.
        /// </summary>
        /// <param name="samples">Samples to measure.</param>
        /// <returns>Mean sample value.</returns>
        private static float Mean(float[] samples)
        {
            float total = 0f;
            foreach (float s in samples) total += s;
            return total / samples.Length;
        }

        /// <summary>
        /// No generator may clip. A waveform that exceeds the legal range is not merely loud — it is
        /// distorted in a way that sounds like a broken build rather than an intended noise.
        /// </summary>
        [Test]
        public void NoWaveform_Clips()
        {
            AudioManagerTestFacade audio = NewAudio();

            var waveforms = new (string name, float[] samples)[]
            {
                ("hum", audio.Hum(50f, 24, 0.34f)),
                ("drone", audio.Drone(46f, 30, 0.9f)),
                ("footstep", audio.Footstep(0.16f, 4021, 0.5f)),
                ("chime", audio.Chime(784f, 1.5f, 0.5f)),
                ("descend", audio.Descend(320f, 70f, 1.2f, 0.45f))
            };

            foreach ((string name, float[] samples) in waveforms)
            {
                float peak = Peak(samples);
                MooseRunnerFacade.Log($"{name}: {samples.Length} samples, peak {peak:F3}");
                Assert.LessOrEqual(peak, 1f, $"{name} clips at {peak:F3}");
                Assert.Greater(peak, 0.05f, $"{name} is effectively silent at {peak:F3}");
            }
        }

        /// <summary>
        /// The looping sounds must meet at the seam. A loop whose last sample is far from its first
        /// clicks audibly on every repeat, once a second forever, which is worse than no audio.
        /// </summary>
        [Test]
        public void LoopingWaveforms_MeetAtTheSeam()
        {
            AudioManagerTestFacade audio = NewAudio();

            foreach ((string name, float[] samples) in new (string, float[])[]
                     {
                         ("hum", audio.Hum(50f, 24, 0.34f)),
                         ("drone", audio.Drone(46f, 30, 0.9f))
                     })
            {
                // Compare the wrap-around step against a typical step inside the buffer: the seam
                // should be no more of a jump than any ordinary neighbouring pair.
                float seam = Mathf.Abs(samples[0] - samples[samples.Length - 1]);
                float interior = Mathf.Abs(samples[samples.Length / 2]
                                           - samples[samples.Length / 2 - 1]);

                MooseRunnerFacade.Log($"{name}: seam step {seam:F5}, interior step {interior:F5}");
                Assert.Less(seam, Mathf.Max(interior * 4f, 0.02f),
                    $"{name} jumps {seam:F4} at the loop seam and will click");
            }
        }

        /// <summary>
        /// Waveforms must be centred near zero. A constant offset wastes headroom, can thump when a
        /// clip starts, and on some hardware is inaudible while still reducing how loud the rest can be.
        /// </summary>
        [Test]
        public void Waveforms_AreCentredNearZero()
        {
            AudioManagerTestFacade audio = NewAudio();

            foreach ((string name, float[] samples) in new (string, float[])[]
                     {
                         ("hum", audio.Hum(50f, 24, 0.34f)),
                         ("drone", audio.Drone(46f, 30, 0.9f)),
                         ("chime", audio.Chime(784f, 1.5f, 0.5f))
                     })
            {
                float mean = Mean(samples);
                Assert.Less(Mathf.Abs(mean), 0.05f, $"{name} sits at a mean of {mean:F4}");
            }
        }

        /// <summary>
        /// Generation must be deterministic. The same arguments have to produce the same samples, or
        /// a floor would not sound the same twice and nothing here could be regression-tested.
        /// </summary>
        [Test]
        public void Generation_IsDeterministic()
        {
            AudioManagerTestFacade audio = NewAudio();

            float[] first = audio.Footstep(0.16f, 99, 0.5f);
            float[] second = audio.Footstep(0.16f, 99, 0.5f);

            Assert.AreEqual(first.Length, second.Length, "same length");
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i], second[i], 1e-6f, $"sample {i} differs between runs");
            }

            float[] other = audio.Footstep(0.16f, 100, 0.5f);
            Assert.AreNotEqual(first[first.Length / 2], other[other.Length / 2],
                "different seeds should give different footsteps");
        }

        /// <summary>
        /// A clip's length must follow the requested duration and the sample rate, so a sound asked
        /// for in seconds lasts that long.
        /// </summary>
        [Test]
        public void ClipLength_FollowsTheRequestedDuration()
        {
            AudioManagerTestFacade audio = NewAudio();

            Assert.AreEqual(audio.SampleRate, audio.Chime(440f, 1f, 0.5f).Length, 2,
                "one second is one sample rate of samples");
            Assert.AreEqual(audio.SampleRate / 2, audio.Descend(300f, 80f, 0.5f, 0.4f).Length, 2,
                "half a second is half that");
        }

        /// <summary>
        /// Nothing may sound before the player has touched the screen. Browsers keep the audio
        /// context suspended until a real gesture, and anything begun against a dead context stays
        /// silent even after it resumes — which is why the game had no sound at all until the player
        /// died once and tapped to retry.
        /// </summary>
        [Test]
        public void NothingSounds_UntilThePlayerHasInteracted()
        {
            var go = new GameObject("Audio");
            AudioFacade audio = go.AddComponent<AudioFacade>();

            audio.SetFloor(1);
            Assert.IsFalse(audio.Unlocked, "no gesture has happened yet");
            Assert.IsFalse(audio.HumPlaying, "so the room tone must not have started");

            // Frames where the player does nothing must not unlock it either.
            audio.NoteInteraction(false);
            Assert.IsFalse(audio.Unlocked, "an idle frame is not a gesture");
            Assert.IsFalse(audio.HumPlaying, "and still nothing is playing");

            audio.NoteInteraction(true);
            Assert.IsTrue(audio.Unlocked, "the first real input opens the audio");
            Assert.IsTrue(audio.HumPlaying, "and the room tone starts with it");
        }

        /// <summary>
        /// A floor tone asked for before the first gesture must still play once the gesture arrives,
        /// rather than being lost.
        /// </summary>
        [Test]
        public void AFloorToneAskedForTooEarly_StillPlaysOnceUnlocked()
        {
            var go = new GameObject("Audio");
            AudioFacade audio = go.AddComponent<AudioFacade>();

            audio.SetFloor(3);
            audio.NoteInteraction(true);

            Assert.IsTrue(audio.HumPlaying, "the floor asked for before the gesture should sound");
        }

        /// <summary>
        /// After a gesture, the module must go on working at the loops until the audio engine is
        /// observed actually running — and must then stop, rather than restarting them forever.
        /// </summary>
        /// <remarks>
        /// This is the fault that survived the first fix. A browser resumes its audio context some
        /// frames after the gesture that permits it, so the loops started on that frame were begun
        /// against a suspended context and produced nothing, while Unity reported them as playing —
        /// so the old retry, which asked isPlaying, could never fire. In the editor the engine is
        /// already running, so what this can prove is the other half: that the detector terminates.
        /// A Running that stays false would mean the loops are restarted every quarter second for
        /// the whole run, which is audible as a stutter and is the obvious way to get this wrong.
        /// </remarks>
        [Test]
        public async UniTask AudioEngine_IsObservedRunning_AfterAGesture(CancellationToken ct)
        {
            var go = new GameObject("Audio");
            AudioFacade audio = go.AddComponent<AudioFacade>();

            audio.SetFloor(1);
            Assert.IsFalse(audio.Running, "nothing has been observed before the first look");

            audio.NoteInteraction(true);

            // Two readings of the DSP clock are needed to see it move, so give it real frames.
            for (int i = 0; i < 30 && !audio.Running; i++)
            {
                audio.NoteInteraction(true);
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(audio.Running,
                "the engine runs in the editor, so it must be seen to run and the retry must stop");
        }

        /// <summary>
        /// The pursuit drone must be silent until something is hunting, and must rise as it closes.
        /// The drone is the loudest thing in the mix, so a drone that leaks while nothing is chasing
        /// would sit under the whole game.
        /// </summary>
        [Test]
        public void PursuitDrone_IsSilentUntilSomethingHunts()
        {
            var go = new GameObject("Audio");
            AudioFacade facade = go.AddComponent<AudioFacade>();

            // The voices do not exist until the first gesture, because a browser will not open its
            // audio context before one — so a test about the drone has to get past that first.
            facade.NoteInteraction(true);

            Assert.AreEqual(0f, facade.DroneVolume, 1e-4f, "nothing is hunting yet");

            // Volume eases towards its target, so drive it for a while rather than once.
            for (int i = 0; i < 400; i++) facade.SetHunted(true, 1f);
            float loud = facade.DroneVolume;
            Assert.Greater(loud, 0.05f, "a Dweller on top of the player should be audible");

            for (int i = 0; i < 400; i++) facade.SetHunted(false, 0f);
            Assert.Less(facade.DroneVolume, loud * 0.5f, "it should fall away once the chase ends");
        }
    }
}
