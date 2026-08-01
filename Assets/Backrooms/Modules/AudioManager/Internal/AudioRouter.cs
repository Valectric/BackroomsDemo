using Backrooms.AudioManager.Internal.Synthesis;
using UnityEngine;

namespace Backrooms.AudioManager.Internal
{
    /// <summary>
    /// Internal coordinator for the AudioManager module. Owns the generated clips and the voices
    /// that play them, and translates gameplay state into what the player hears.
    /// </summary>
    internal sealed class AudioRouter
    {
        /// <summary>Loudest the pursuit drone gets, when a Dweller is on top of the player.</summary>
        private const float DroneMaxVolume = 0.55f;

        /// <summary>Seconds between footfalls at a walk.</summary>
        private const float WalkStepSeconds = 0.52f;

        /// <summary>Seconds between footfalls at a sprint.</summary>
        private const float SprintStepSeconds = 0.34f;

        /// <summary>How many distinct footstep samples to cycle through.</summary>
        private const int FootstepVariants = 4;

        private AudioSource _hum;
        private AudioSource _drone;
        private AudioSource _oneShot;
        private AudioSource _steps;

        private AudioClip[] _footsteps;
        private AudioClip _chime;
        private AudioClip _descend;

        private float _stepTimer;
        private int _stepIndex;

        /// <summary>The floor tone that has been asked for, whether or not it can play yet.</summary>
        private int _wantedFloor;

        /// <summary>Whether a user gesture has happened, so the browser will let sound out.</summary>
        private bool _unlocked;

        /// <summary>Whether sound is allowed to play yet.</summary>
        public bool Unlocked => _unlocked;

        /// <summary>Volume the pursuit drone is currently at.</summary>
        public float DroneVolume => _drone == null ? 0f : _drone.volume;

        /// <summary>Whether the room hum is playing.</summary>
        public bool HumPlaying => _hum != null && _hum.isPlaying;

        /// <summary>
        /// Builds the voices and every clip once. Generation is a few hundred kilobytes of float
        /// maths, so it happens at startup rather than per floor.
        /// </summary>
        /// <param name="host">Transform to hang the audio sources from.</param>
        public void Build(Transform host)
        {
            if (_hum != null) return;

            _hum = Voice(host, "Hum", loop: true);
            _drone = Voice(host, "PursuitDrone", loop: true);
            _oneShot = Voice(host, "OneShot", loop: false);
            _steps = Voice(host, "Footsteps", loop: false);

            _drone.clip = Clip("DwellerDrone", ToneGenerator.Drone(46f, 30, 0.9f));
            _drone.volume = 0f;

            _footsteps = new AudioClip[FootstepVariants];
            for (int i = 0; i < FootstepVariants; i++)
            {
                _footsteps[i] = Clip($"Footstep{i}", ToneGenerator.Footstep(0.22f, 4021 + i, 0.5f));
            }

            _chime = Clip("RelicChime", ToneGenerator.Chime(784f, 1.5f, 0.5f));
            _descend = Clip("Descend", ToneGenerator.Descend(320f, 70f, 1.2f, 0.45f));
        }

        /// <summary>
        /// Sets the room tone for a floor. Each floor hums at its own pitch, so descending is
        /// audible as well as visible.
        /// </summary>
        /// <param name="floor">One-based floor number.</param>
        public void SetFloor(int floor)
        {
            if (_hum == null) return;

            _wantedFloor = floor;

            // Drop a little deeper every floor and level off, so floor 20 is not inaudible.
            float fundamental = 50f * Mathf.Pow(0.97f, Mathf.Min(floor - 1, 18));
            _hum.clip = Clip($"Hum{floor}", ToneGenerator.Hum(fundamental, 24, 0.34f));
            _hum.volume = 0.30f;

            if (_unlocked) _hum.Play();
        }

        /// <summary>
        /// Notes that the player has interacted, which is the moment a browser will allow sound.
        /// </summary>
        /// <remarks>
        /// Browsers keep the audio context suspended until a real user gesture. Anything started
        /// before that is silent, and stays silent even once the context resumes, because the source
        /// was begun against a dead context. That is why the game had no sound until the player died
        /// once: the tap to retry was the first gesture, and restarting happened to re-issue Play.
        /// Starting the loops here instead makes the first gesture the thing that opens them.
        /// </remarks>
        /// <param name="interacted">Whether the player did anything this frame.</param>
        public void NoteInteraction(bool interacted)
        {
            if (_unlocked || !interacted || _hum == null) return;

            _unlocked = true;
            AudioListener.pause = false;

            if (_wantedFloor > 0) _hum.Play();
            _drone.Play();
        }

        /// <summary>
        /// Sets how loudly the pursuit drone plays.
        /// </summary>
        /// <param name="hunted">Whether any Dweller is chasing.</param>
        /// <param name="closeness">How close the nearest one is, 0 at the edge of its range to 1 on top of you.</param>
        public void SetHunted(bool hunted, float closeness)
        {
            if (_drone == null) return;

            float target = hunted ? Mathf.Clamp01(closeness) * DroneMaxVolume : 0f;

            // Ease rather than snap. A drone that appears at full volume reads as a bug; one that
            // swells is the point.
            _drone.volume = Mathf.MoveTowards(_drone.volume, target, Time.deltaTime * 0.9f);
            _drone.pitch = 1f + Mathf.Clamp01(closeness) * 0.55f;
        }

        /// <summary>
        /// Advances the footstep timer and plays a footfall when one is due.
        /// </summary>
        /// <param name="moving">Whether the player is moving under their own power.</param>
        /// <param name="sprinting">Whether they are sprinting.</param>
        /// <param name="deltaTime">Seconds since the last update.</param>
        public void TickFootsteps(bool moving, bool sprinting, float deltaTime)
        {
            if (_steps == null || _footsteps == null) return;

            if (!moving)
            {
                // Reset part-way so the first step after standing still lands promptly.
                _stepTimer = WalkStepSeconds * 0.65f;
                return;
            }

            _stepTimer += deltaTime;
            float interval = sprinting ? SprintStepSeconds : WalkStepSeconds;
            if (_stepTimer < interval) return;

            _stepTimer = 0f;
            _stepIndex = (_stepIndex + 1) % _footsteps.Length;
            _steps.pitch = sprinting ? 1.06f : 0.94f;
            // Present, but under the mix rather than on top of it. Dropping this too far is how
            // the footsteps vanished entirely once the body moved down into the sub-bass.
            _steps.PlayOneShot(_footsteps[_stepIndex], sprinting ? 0.75f : 0.55f);
        }

        /// <summary>
        /// Plays the relic pickup chime.
        /// </summary>
        public void PlayRelic()
        {
            if (_oneShot != null && _chime != null) _oneShot.PlayOneShot(_chime, 0.8f);
        }

        /// <summary>
        /// Plays the falling tone for dropping a floor.
        /// </summary>
        public void PlayDescend()
        {
            if (_oneShot != null && _descend != null) _oneShot.PlayOneShot(_descend, 0.9f);
        }

        /// <summary>
        /// Silences everything, for the end of a run.
        /// </summary>
        public void Silence()
        {
            if (_hum != null) _hum.Stop();
            if (_drone != null) _drone.volume = 0f;
        }

        /// <summary>
        /// Restarts any loop that has fallen silent. A browser tab that was backgrounded, or an
        /// audio context that resumed late, can leave a looping source stopped.
        /// </summary>
        public void KeepLoopsAlive()
        {
            if (!_unlocked || _hum == null) return;
            if (_wantedFloor > 0 && !_hum.isPlaying && _hum.clip != null) _hum.Play();
            if (!_drone.isPlaying && _drone.clip != null) _drone.Play();
        }

        /// <summary>
        /// Creates one audio source on the host.
        /// </summary>
        /// <param name="host">Transform to hang the source from.</param>
        /// <param name="name">Object name.</param>
        /// <param name="loop">Whether the source loops.</param>
        /// <returns>The created source.</returns>
        private static AudioSource Voice(Transform host, string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host, worldPositionStays: false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;

            // 2D: these are the player's own sounds and the mood of the room, not objects in space.
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>
        /// Wraps generated samples in an audio clip.
        /// </summary>
        /// <param name="name">Clip name.</param>
        /// <param name="samples">Mono samples in the range -1 to 1.</param>
        /// <returns>The created clip.</returns>
        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(
                name, samples.Length, 1, ToneGenerator.SampleRate, stream: false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
