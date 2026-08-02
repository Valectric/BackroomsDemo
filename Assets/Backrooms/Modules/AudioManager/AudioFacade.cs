using Backrooms.AudioManager.Internal;
using UnityEngine;

namespace Backrooms.AudioManager
{
    /// <summary>
    /// This is a Module. The single public door into AudioManager: everything the game makes a sound
    /// with. Place one on a GameObject in the scene; it self-bootstraps its internal router and
    /// generates every clip at startup. Concrete by design — there is no interface.
    /// </summary>
    /// <remarks>
    /// Nothing here is an imported asset. The repo may only carry CC0 material, which makes a sound
    /// library a licensing problem; a waveform computed from a formula has no licence. It is also the
    /// cheapest tension the game can buy — a drone that swells as something closes says "behind you"
    /// with no words, no HUD and no art budget.
    /// </remarks>
    public sealed class AudioFacade : MonoBehaviour
    {
        private readonly AudioRouter _router = new AudioRouter();
        private AudioManagerTestFacade _testFacade;

        /// <summary>Volume the pursuit drone is currently at, 0 when nothing is hunting.</summary>
        public float DroneVolume => _router.DroneVolume;

        /// <summary>Whether the room hum is playing.</summary>
        public bool HumPlaying => _router.HumPlaying;

        /// <summary>Whether a user gesture has happened, so the browser will let sound out.</summary>
        public bool Unlocked => _router.Unlocked;

        /// <summary>
        /// Whether the audio engine has been observed actually running, rather than merely started.
        /// </summary>
        public bool Running => _router.Running;

        /// <summary>
        /// Notes that the player has interacted, and keeps working at the loops until the audio
        /// engine comes up under them. Browsers keep audio suspended until a real gesture, so
        /// nothing is started until this has been true once.
        /// </summary>
        /// <param name="interacted">Whether the player did anything this frame.</param>
        public void NoteInteraction(bool interacted)
        {
            _router.Build(transform);
            _router.NoteInteraction(interacted);
            _router.KeepLoopsAlive(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Sets the room tone for a floor, so each floor sounds like its own space.
        /// </summary>
        /// <param name="floor">One-based floor number.</param>
        public void SetFloor(int floor)
        {
            _router.Build(transform);
            _router.SetFloor(floor);
        }

        /// <summary>
        /// Tells the module where to hang its voices, without creating them yet.
        /// </summary>
        private void Awake() => _router.Build(transform);

        /// <summary>
        /// Sets how loudly a hunting Dweller is heard.
        /// </summary>
        /// <param name="hunted">Whether any Dweller is chasing.</param>
        /// <param name="closeness">How close the nearest one is, 0 at the edge of its range to 1 on top of you.</param>
        public void SetHunted(bool hunted, float closeness) => _router.SetHunted(hunted, closeness);

        /// <summary>
        /// Advances footsteps for a frame.
        /// </summary>
        /// <param name="moving">Whether the player is moving under their own power.</param>
        /// <param name="sprinting">Whether they are sprinting.</param>
        public void SetMovement(bool moving, bool sprinting)
            => _router.TickFootsteps(moving, sprinting, Time.deltaTime);

        /// <summary>
        /// Loads the thematic ambience for a floor.
        /// </summary>
        /// <param name="key">Ambience folder under Resources, matching the theme's prop style.</param>
        /// <param name="seed">Seed, so a floor's ambience is the same every time it is built.</param>
        public void SetAmbience(string key, int seed) => _router.SetAmbience(key, seed);

        /// <summary>
        /// Advances the ambience schedule for a frame.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last update.</param>
        public void TickAmbience(float deltaTime) => _router.TickAmbience(deltaTime);

        /// <summary>How many ambient clips the current floor has.</summary>
        public int AmbienceCount => _router.AmbienceCount;

        /// <summary>Seconds until the next ambient one-shot.</summary>
        public float NextAmbienceIn => _router.NextAmbienceIn;

        /// <summary>The pursuit level with the pulse removed.</summary>
        public float HuntLevel => _router.HuntLevel;

        /// <summary>
        /// Plays the relic pickup chime.
        /// </summary>
        public void PlayRelic() => _router.PlayRelic();

        /// <summary>
        /// Plays the falling tone for dropping a floor.
        /// </summary>
        public void PlayDescend() => _router.PlayDescend();

        /// <summary>
        /// Silences everything, for the end of a run.
        /// </summary>
        public void Silence() => _router.Silence();

        /// <summary>
        /// Returns the module's test seam, creating it lazily. Not intended for production use —
        /// only for automated testing.
        /// </summary>
        /// <returns>The module's <see cref="AudioManagerTestFacade"/>.</returns>
        public AudioManagerTestFacade GetTestFacade()
            => _testFacade ??= new AudioManagerTestFacade(_router);
    }
}
