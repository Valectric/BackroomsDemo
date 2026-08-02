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

        /// <summary>Thematic one-shots for the current floor, loaded from Resources.</summary>
        private AudioClip[] _ambience = System.Array.Empty<AudioClip>();

        /// <summary>Seeded generator choosing which ambience plays and when.</summary>
        private System.Random _ambienceRng = new System.Random(1);

        /// <summary>Seconds until the next ambient one-shot.</summary>
        private float _nextAmbienceIn = 12f;

        /// <summary>Eased pursuit level, before the pulse is applied on top.</summary>
        private float _huntLevel;

        /// <summary>Phase of the pursuit pulse, 0..1.</summary>
        private float _pulsePhase;

        /// <summary>The floor tone that has been asked for, whether or not it can play yet.</summary>
        private int _wantedFloor;

        /// <summary>Whether a user gesture has happened, so the browser will let sound out.</summary>
        private bool _unlocked;

        /// <summary>Whether the audio engine has been seen to actually run.</summary>
        private bool _running;

        /// <summary>Previous DSP clock reading, or -1 before the first look.</summary>
        private double _lastDspTime = -1.0;

        /// <summary>Seconds since the loops were last re-issued while waiting for the engine.</summary>
        private float _retryTimer;

        /// <summary>Transform the voices hang from, remembered so they can be rebuilt.</summary>
        private Transform _host;

        /// <summary>How many times the whole audio stack has been rebuilt waiting for the engine.</summary>
        private int _rebuilds;

        /// <summary>Last playback position of the hum, used to tell whether it is really sounding.</summary>
        private int _lastHumSamples = -1;

        /// <summary>How many rebuilds to attempt before settling for re-issuing Play.</summary>
        private const int MaxRebuilds = 6;

        /// <summary>Seconds since the first gesture, used to drive the restart schedule.</summary>
        private float _sinceUnlock;

        /// <summary>How many scheduled restarts have already been done.</summary>
        private int _restartsDone;

        /// <summary>
        /// When to rebuild the audio after the first gesture, in seconds.
        /// </summary>
        /// <remarks>
        /// Fixed times rather than a condition, because every condition tried so far has lied.
        /// <c>isPlaying</c> reports true against a suspended context; the DSP clock and the hum's own
        /// playback position both advanced while nothing came out. A player reported having to
        /// release and touch the screen a second time, several seconds in, before any sound arrived —
        /// which says the audio graph is still being built too early even now, and that simply doing
        /// it again later is what works. So it is done again later, unconditionally.
        /// </remarks>
        private static readonly float[] RestartSchedule = { 1f, 2f, 4f, 5f, 10f };

        /// <summary>Whether sound is allowed to play yet.</summary>
        public bool Unlocked => _unlocked;

        /// <summary>Whether the audio engine has been seen to actually produce time.</summary>
        public bool Running => _running;

        /// <summary>Volume the pursuit drone is currently at.</summary>
        public float DroneVolume => _drone == null ? 0f : _drone.volume;

        /// <summary>Whether the room hum is playing.</summary>
        public bool HumPlaying => _hum != null && _hum.isPlaying;

        /// <summary>
        /// Remembers where the voices should hang, and creates them if the audio context is already
        /// open. Nothing is created before the first gesture — see <see cref="NoteInteraction"/>.
        /// </summary>
        /// <param name="host">Transform to hang the audio sources from.</param>
        public void Build(Transform host)
        {
            if (host != null) _host = host;
            if (_hum != null || !_unlocked) return;
            CreateVoices();
        }

        /// <summary>
        /// Creates the voices and every clip. Generation is a few hundred kilobytes of float maths,
        /// so it happens once rather than per floor.
        /// </summary>
        private void CreateVoices()
        {
            Transform host = _host;

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

            ApplyFloorClip();
        }

        /// <summary>
        /// Destroys every voice and clip and makes them again, so each one is created against
        /// whatever audio context is live now rather than the one that existed at startup.
        /// </summary>
        private void Rebuild()
        {
            DestroyVoice(_hum);
            DestroyVoice(_drone);
            DestroyVoice(_oneShot);
            DestroyVoice(_steps);

            _hum = null;
            _drone = null;
            _oneShot = null;
            _steps = null;
            _footsteps = null;
            _lastHumSamples = -1;

            CreateVoices();
        }

        /// <summary>
        /// Destroys one voice's GameObject.
        /// </summary>
        /// <param name="source">The voice to destroy; ignored when null.</param>
        private static void DestroyVoice(AudioSource source)
        {
            if (source == null) return;

            if (Application.isPlaying) Object.Destroy(source.gameObject);
            else Object.DestroyImmediate(source.gameObject);
        }

        /// <summary>
        /// Gives the hum the clip for the floor that has been asked for.
        /// </summary>
        private void ApplyFloorClip()
        {
            if (_hum == null || _wantedFloor <= 0) return;

            // Drop a little deeper every floor and level off, so floor 20 is not inaudible.
            float fundamental = 50f * Mathf.Pow(0.97f, Mathf.Min(_wantedFloor - 1, 18));
            _hum.clip = Clip($"Hum{_wantedFloor}", ToneGenerator.Hum(fundamental, 24, 0.34f));
            _hum.volume = 0.30f;
        }

        /// <summary>
        /// Starts both looping voices, if they have anything to play.
        /// </summary>
        private void PlayLoops()
        {
            if (_hum == null) return;

            if (_wantedFloor > 0 && _hum.clip != null) _hum.Play();
            if (_drone.clip != null) _drone.Play();
        }

        /// <summary>
        /// Sets the room tone for a floor. Each floor hums at its own pitch, so descending is
        /// audible as well as visible.
        /// </summary>
        /// <param name="floor">One-based floor number.</param>
        public void SetFloor(int floor)
        {
            // Recorded before anything else: floors are set while the game is still behind the title
            // screen, long before there is a voice to put the tone on.
            _wantedFloor = floor;
            if (_hum == null) return;

            ApplyFloorClip();
            if (_unlocked) _hum.Play();
        }

        /// <summary>
        /// Notes that the player has interacted, which is the moment a browser will allow sound.
        /// </summary>
        /// <remarks>
        /// Browsers keep the audio context suspended until a real user gesture, and the game loads
        /// long before that gesture arrives. Two earlier fixes assumed the problem was *when Play was
        /// called* and both failed: starting the loops on the gesture did not help, and neither did
        /// re-issuing Play until the DSP clock moved. Probing the shipped build from a browser showed
        /// the context resuming correctly on the click, which ruled that whole family out. What is
        /// left is *when the objects are made* — every source and clip was being created in Awake,
        /// against a context that was still suspended. So nothing is created until here.
        /// </remarks>
        /// <param name="interacted">Whether the player did anything this frame.</param>
        public void NoteInteraction(bool interacted)
        {
            if (_unlocked || !interacted) return;

            _unlocked = true;
            AudioListener.pause = false;

            Build(_host);
            PlayLoops();
        }

        /// <summary>
        /// Sets how loudly the pursuit drone plays.
        /// </summary>
        /// <param name="hunted">Whether any Dweller is chasing.</param>
        /// <param name="closeness">How close the nearest one is, 0 at the edge of its range to 1 on top of you.</param>
        public void SetHunted(bool hunted, float closeness)
        {
            if (_drone == null) return;

            float near = Mathf.Clamp01(closeness);

            // Convex rather than linear. A Dweller two rooms away should be a rumour and one in the
            // corridor with you should be the loudest thing in the game; a straight line makes those
            // two sound nearly the same, which is why the chase read as flat.
            float target = hunted ? Mathf.Pow(near, DroneCurve) * DroneMaxVolume : 0f;

            // Rises fast, falls away slowly: being found is sudden, and losing something should feel
            // like relief you had to earn rather than a switch flipping back.
            float rate = target > _huntLevel ? DroneAttack : DroneRelease;
            _huntLevel = Mathf.MoveTowards(_huntLevel, target, Time.deltaTime * rate);

            // A pulse that quickens as it closes, which is the tell that turns "something is near"
            // into "it is coming". Applied after the easing, or the easing would smooth it flat.
            _pulsePhase += Time.deltaTime * Mathf.Lerp(PulseSlowHz, PulseFastHz, near);
            _pulsePhase -= Mathf.Floor(_pulsePhase);

            float swell = 1f - PulseDepth * near
                * (0.5f + 0.5f * Mathf.Cos(_pulsePhase * Mathf.PI * 2f));

            _drone.volume = _huntLevel * swell;
            _drone.pitch = 1f + near * DronePitchRise;
        }

        /// <summary>Shape of the volume ramp. Above 1 keeps distant Dwellers quiet and near ones loud.</summary>
        private const float DroneCurve = 2.2f;

        /// <summary>How fast the drone swells when a Dweller closes, in volume per second.</summary>
        private const float DroneAttack = 2.4f;

        /// <summary>How fast it falls away once nothing is hunting.</summary>
        private const float DroneRelease = 0.7f;

        /// <summary>How far the pitch climbs as a Dweller closes.</summary>
        private const float DronePitchRise = 0.9f;

        /// <summary>Pulse rate at the edge of a Dweller's range, in beats per second.</summary>
        private const float PulseSlowHz = 1.1f;

        /// <summary>Pulse rate with a Dweller on top of the player.</summary>
        private const float PulseFastHz = 5.5f;

        /// <summary>How deeply the pulse dips the drone, at its strongest.</summary>
        private const float PulseDepth = 0.38f;

        /// <summary>The pursuit level with the pulse removed, for tests and for the HUD.</summary>
        public float HuntLevel => _huntLevel;

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
        /// Loads the thematic one-shots for a floor and restarts the schedule.
        /// </summary>
        /// <remarks>
        /// These are recordings rather than synthesis, because the point of them is that they are
        /// recognisably of a real place: a dishwasher finishing, a street organ, a heavy door. They
        /// play rarely and at random, which is what makes them unsettling — a sound heard once with
        /// nothing to explain it is worse than the same sound on a loop.
        /// </remarks>
        /// <param name="key">Ambience folder under Resources, matching the theme's prop style.</param>
        /// <param name="seed">Seed, so a floor's ambience is the same every time it is built.</param>
        public void SetAmbience(string key, int seed)
        {
            _ambience = string.IsNullOrEmpty(key)
                ? System.Array.Empty<AudioClip>()
                : Resources.LoadAll<AudioClip>("Ambience/" + key);

            _ambienceRng = new System.Random(seed);
            _nextAmbienceIn = NextAmbienceGap();
        }

        /// <summary>
        /// Counts down to the next ambient one-shot and plays it when it is due.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last update.</param>
        public void TickAmbience(float deltaTime)
        {
            if (!_unlocked || _oneShot == null || _ambience == null || _ambience.Length == 0) return;

            _nextAmbienceIn -= deltaTime;
            if (_nextAmbienceIn > 0f) return;

            _nextAmbienceIn = NextAmbienceGap();
            _oneShot.PlayOneShot(_ambience[_ambienceRng.Next(_ambience.Length)], AmbienceVolume);
        }

        /// <summary>
        /// How long to wait before the next ambient one-shot.
        /// </summary>
        /// <returns>A gap in seconds.</returns>
        private float NextAmbienceGap()
            => AmbienceMinGap + (float)_ambienceRng.NextDouble() * (AmbienceMaxGap - AmbienceMinGap);

        /// <summary>Shortest gap between ambient one-shots, in seconds.</summary>
        private const float AmbienceMinGap = 16f;

        /// <summary>Longest gap between ambient one-shots, in seconds.</summary>
        private const float AmbienceMaxGap = 48f;

        /// <summary>How loudly ambience plays. Under the mix — it is the room, not an event.</summary>
        private const float AmbienceVolume = 0.5f;

        /// <summary>How many ambient clips the current floor has.</summary>
        public int AmbienceCount => _ambience == null ? 0 : _ambience.Length;

        /// <summary>Seconds until the next ambient one-shot, for tests.</summary>
        public float NextAmbienceIn => _nextAmbienceIn;

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
        /// Keeps re-issuing the loops until the audio engine is observed running, then restarts any
        /// that later fall silent.
        /// </summary>
        /// <remarks>
        /// A browser resumes its audio context asynchronously, some frames after the gesture that
        /// permits it. A source started on that same frame is begun against a context that is still
        /// suspended, and Unity then reports it as playing while it produces nothing — so asking
        /// isPlaying can never notice the failure. The DSP clock can: it only advances while the
        /// engine is genuinely running, which makes it the one honest signal available here. This is
        /// why the game still had no sound on the first run even after the first tap started the
        /// loops, and why dying fixed it — restarting happened long after the context had come up.
        /// </remarks>
        /// <param name="deltaTime">Seconds since the last update, unaffected by pausing.</param>
        public void KeepLoopsAlive(float deltaTime)
        {
            if (!_unlocked || _hum == null) return;

            // The schedule comes first and ignores every liveness signal, because they have all
            // been wrong at least once. Restarting a working loop costs a barely audible seam in a
            // drone; not restarting a dead one costs the entire soundtrack.
            _sinceUnlock += deltaTime;
            if (_restartsDone < RestartSchedule.Length
                && _sinceUnlock >= RestartSchedule[_restartsDone])
            {
                _restartsDone++;
                AudioListener.pause = false;
                Rebuild();
                PlayLoops();
                return;
            }

            double dsp = AudioSettings.dspTime;
            bool dspAdvanced = _lastDspTime >= 0.0 && dsp > _lastDspTime + 1e-4;
            _lastDspTime = dsp;

            // The hum's own playback position is the stricter of the two signals: the DSP clock says
            // the engine is turning over, this says sound is actually coming out of this source.
            int samples = _hum.clip == null ? -1 : _hum.timeSamples;
            bool humAdvanced = samples >= 0 && _lastHumSamples >= 0 && samples != _lastHumSamples;
            _lastHumSamples = samples;

            if (!_running && dspAdvanced && humAdvanced) _running = true;

            if (_running)
            {
                // A backgrounded tab leaves looping sources stopped, so they still need watching.
                if (_wantedFloor > 0 && !_hum.isPlaying && _hum.clip != null) _hum.Play();
                if (!_drone.isPlaying && _drone.clip != null) _drone.Play();
                return;
            }

            _retryTimer += deltaTime;
            if (_retryTimer < RetrySeconds) return;

            // (the scheduled restarts above run whether or not this fast retry ever fires)

            _retryTimer = 0f;
            AudioListener.pause = false;

            // Re-issuing Play was the fix that did not work. Make the objects again instead.
            if (_rebuilds < MaxRebuilds)
            {
                _rebuilds++;
                Rebuild();
            }

            PlayLoops();
        }

        /// <summary>
        /// How often to re-issue the loops while waiting for the engine. Short enough that the wait
        /// is imperceptible, long enough not to thrash a loop that is about to start.
        /// </summary>
        private const float RetrySeconds = 0.25f;

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
