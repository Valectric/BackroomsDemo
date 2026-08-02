using UnityEngine;

namespace Backrooms.EntityManager.Internal.Behaviour
{
    /// <summary>
    /// What a Dweller does with a frame: how fast it moves, whether it ignores the grid and runs in
    /// a straight line, and whether it is showing its warning colour.
    /// </summary>
    /// <remarks>
    /// Speed alone never made the three kinds feel different. A faster Dweller and a slower one are
    /// the same encounter at two tempos — you back away from both, and the only thing that changes is
    /// how long it takes. What separates them is the <i>rule</i> each one plays by, because a rule is
    /// something the player can learn and then exploit. Kept out of the facade so each rule can be
    /// read on its own, and driven entirely by supplied state so a test can step it deterministically.
    /// </remarks>
    internal sealed class DwellerGait
    {
        /// <summary>How wide the player's gaze is, in degrees either side of where they face.</summary>
        private const float GazeHalfAngle = 55f;

        /// <summary>How close a charger must be before it bothers winding up, in metres.</summary>
        private const float ChargeTriggerMetres = 26f;

        /// <summary>How long a charge runs before it gives up and goes back to stalking.</summary>
        private const float ChargeSeconds = 2.6f;

        /// <summary>How long the charger rests after a charge, so it cannot chain them.</summary>
        private const float RecoverSeconds = 1.4f;

        /// <summary>Where the charger is in its cycle.</summary>
        private enum ChargePhase
        {
            /// <summary>Creeping, waiting to see the player.</summary>
            Stalk = 0,

            /// <summary>Stopped and flashing, about to commit.</summary>
            Wind = 1,

            /// <summary>Running in a straight line at the point it committed to.</summary>
            Run = 2,

            /// <summary>Spent, briefly, so charges cannot chain into each other.</summary>
            Recover = 3
        }

        private ChargePhase _phase = ChargePhase.Stalk;
        private float _phaseSeconds;
        private Vector3 _chargeTarget;

        /// <summary>Whether the body should show its warning colour this frame.</summary>
        public bool Alarmed { get; private set; }

        /// <summary>Whether the Dweller is running its straight-line charge.</summary>
        public bool Charging => _phase == ChargePhase.Run;

        /// <summary>
        /// A point to run straight at, ignoring the grid, or <c>null</c> to follow the usual path.
        /// </summary>
        public Vector3? StraightTarget { get; private set; }

        /// <summary>
        /// Works out this frame's movement for a Dweller.
        /// </summary>
        /// <param name="archetype">The kind's rules.</param>
        /// <param name="self">Where the Dweller is.</param>
        /// <param name="player">Where the player is.</param>
        /// <param name="playerForward">Which way the player is facing, flattened.</param>
        /// <param name="chasing">Whether the brain has the player.</param>
        /// <param name="patrolSpeed">Speed to use while unaware, in metres per second.</param>
        /// <param name="chaseSpeed">Speed to use while hunting, in metres per second.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <returns>Metres per second to move this frame.</returns>
        public float Step(DwellerArchetype archetype, Vector3 self, Vector3 player,
            Vector3 playerForward, bool chasing, float patrolSpeed, float chaseSpeed,
            float deltaTime)
        {
            Alarmed = false;
            StraightTarget = null;

            float ordinary = chasing ? chaseSpeed : patrolSpeed;
            if (archetype == null) return ordinary;

            switch (archetype.Movement)
            {
                case DwellerMovement.Freezes:
                    return StepFreezer(archetype, self, player, playerForward, ordinary);

                case DwellerMovement.Charges:
                    return StepCharger(archetype, self, player, patrolSpeed, deltaTime);

                default:
                    return ordinary;
            }
        }

        /// <summary>
        /// The one that only moves while it is not being looked at.
        /// </summary>
        /// <remarks>
        /// It is faster than a sprint on purpose. If it could be outrun, looking at it would be
        /// optional and the rule would be decoration; because it cannot, the player has to walk
        /// backwards watching it, which costs them the thing they most need on a floor like this,
        /// which is knowing what is ahead of them.
        /// </remarks>
        /// <param name="archetype">The kind's rules.</param>
        /// <param name="self">Where the Dweller is.</param>
        /// <param name="player">Where the player is.</param>
        /// <param name="playerForward">Which way the player is facing, flattened.</param>
        /// <param name="ordinary">The speed it would otherwise move at.</param>
        /// <returns>Metres per second to move this frame.</returns>
        private float StepFreezer(DwellerArchetype archetype, Vector3 self, Vector3 player,
            Vector3 playerForward, float ordinary)
        {
            if (IsWatchedBy(self, player, playerForward)) return 0f;

            return archetype.UnobservedSpeed > 0f ? archetype.UnobservedSpeed : ordinary;
        }

        /// <summary>
        /// The one that creeps, stops dead, flashes, and then throws itself at you.
        /// </summary>
        /// <param name="archetype">The kind's rules.</param>
        /// <param name="self">Where the Dweller is.</param>
        /// <param name="player">Where the player is.</param>
        /// <param name="patrolSpeed">Speed to use while unaware, in metres per second.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <returns>Metres per second to move this frame.</returns>
        private float StepCharger(DwellerArchetype archetype, Vector3 self, Vector3 player,
            float patrolSpeed, float deltaTime)
        {
            _phaseSeconds += deltaTime;

            float creep = archetype.StalkSpeed > 0f ? archetype.StalkSpeed : patrolSpeed;
            float apart = Flat(player - self).magnitude;

            switch (_phase)
            {
                case ChargePhase.Stalk:
                    // Creeping until it has a clear run at the player. Line of sight is what makes
                    // the straight-line charge fair: if it can see you, the path it takes is a path
                    // you could have seen it take.
                    if (apart <= ChargeTriggerMetres && CanSee(self, player)) Enter(ChargePhase.Wind);
                    return creep;

                case ChargePhase.Wind:
                    Alarmed = Blinking(archetype);
                    if (_phaseSeconds >= archetype.WindUpSeconds)
                    {
                        _chargeTarget = Flat(player) + Vector3.up * self.y;
                        Enter(ChargePhase.Run);
                    }

                    return 0f;

                case ChargePhase.Run:
                    Alarmed = true;
                    StraightTarget = _chargeTarget;

                    // Ends on a timer rather than on arrival, so a charge that misses still ends —
                    // and a miss is the opening the player earned by moving.
                    if (_phaseSeconds >= ChargeSeconds
                        || Flat(_chargeTarget - self).magnitude <= 0.35f)
                    {
                        Enter(ChargePhase.Recover);
                    }

                    return archetype.ChargeSpeed;

                default:
                    if (_phaseSeconds >= RecoverSeconds) Enter(ChargePhase.Stalk);
                    return creep * 0.5f;
            }
        }

        /// <summary>
        /// Whether the warning flash is lit this instant.
        /// </summary>
        /// <param name="archetype">The kind's rules.</param>
        /// <returns><c>true</c> while a flash is on.</returns>
        private bool Blinking(DwellerArchetype archetype)
        {
            int blinks = Mathf.Max(1, archetype.WindUpBlinks);
            float period = archetype.WindUpSeconds / blinks;
            return _phaseSeconds % period < period * 0.5f;
        }

        /// <summary>
        /// Moves to a phase and restarts its clock.
        /// </summary>
        /// <param name="phase">The phase to enter.</param>
        private void Enter(ChargePhase phase)
        {
            _phase = phase;
            _phaseSeconds = 0f;
        }

        /// <summary>
        /// Whether the player is looking at a point, and could actually see it.
        /// </summary>
        /// <param name="self">The point being looked at.</param>
        /// <param name="player">Where the player is.</param>
        /// <param name="playerForward">Which way the player is facing, flattened.</param>
        /// <returns><c>true</c> if it is being watched.</returns>
        private static bool IsWatchedBy(Vector3 self, Vector3 player, Vector3 playerForward)
        {
            Vector3 toSelf = Flat(self - player);
            Vector3 facing = Flat(playerForward);
            if (toSelf.sqrMagnitude < 1e-4f || facing.sqrMagnitude < 1e-4f) return true;

            if (Vector3.Angle(facing, toSelf) > GazeHalfAngle) return false;

            // Facing it through a wall is not looking at it. Without this the player freezes it
            // through solid geometry, which reads as the rule being broken rather than used.
            return CanSee(player, self);
        }

        /// <summary>
        /// Whether two points can see each other through the level.
        /// </summary>
        /// <param name="from">One end, at foot height.</param>
        /// <param name="to">The other end, at foot height.</param>
        /// <returns><c>true</c> if nothing solid is between them.</returns>
        private static bool CanSee(Vector3 from, Vector3 to)
        {
            Vector3 a = new Vector3(from.x, from.y + EyeHeight, from.z);
            Vector3 b = new Vector3(to.x, to.y + EyeHeight, to.z);

            // The player's own collider sits at the far end, so a hit on it means the line arrived.
            if (!Physics.Linecast(a, b, out RaycastHit hit)) return true;
            return Vector3.Distance(hit.point, b) <= 0.6f;
        }

        /// <summary>Height the sight lines are cast at, so they clear the floor and skirting.</summary>
        private const float EyeHeight = 1.4f;

        /// <summary>
        /// Flattens a vector onto the floor plane.
        /// </summary>
        /// <param name="v">Vector to flatten.</param>
        /// <returns>The same vector with no height.</returns>
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>
        /// Forgets any charge in progress, for a Dweller being placed or reused.
        /// </summary>
        public void Reset()
        {
            _phase = ChargePhase.Stalk;
            _phaseSeconds = 0f;
            Alarmed = false;
            StraightTarget = null;
        }
    }
}
