using UnityEngine;

namespace Backrooms.EntityManager
{
    /// <summary>
    /// The kinds of Dweller that roam a floor. In this setting the Backrooms are stitched together
    /// from many different places, and what lives in them is not one creature repeated — so a floor
    /// carries one of each rather than three of the same.
    /// </summary>
    public enum DwellerKind
    {
        /// <summary>The baseline: average pace, average senses, a tall dark shape.</summary>
        Lurker = 0,

        /// <summary>Nearly ceiling height, slow, and sees a long way. It never stops coming.</summary>
        Watcher = 1,

        /// <summary>Low, wide and fast, but half-blind. Dangerous once it is already close.</summary>
        Skitter = 2
    }

    /// <summary>
    /// What makes one kind of Dweller different from another: how it moves, how far it senses, and
    /// what it looks like. Held as data rather than as subclasses so the differences are all visible
    /// in one table and a designer can read the whole roster at a glance.
    /// </summary>
    public sealed class DwellerArchetype
    {
        /// <summary>Which kind this describes.</summary>
        public DwellerKind Kind { get; }

        /// <summary>Name shown to the player when this Dweller gives chase.</summary>
        public string DisplayName { get; }

        /// <summary>Patrol speed relative to the floor's base Dweller speed.</summary>
        public float SpeedMultiplier { get; }

        /// <summary>
        /// Speed relative to the base while hunting. Every kind must land above the player's walk and
        /// below their sprint, or the chase is not a chase: below walk it can never close, and above
        /// sprint it can never be escaped.
        /// </summary>
        public float ChaseMultiplier { get; }

        /// <summary>Sense range relative to the base sense range.</summary>
        public float SenseMultiplier { get; }

        /// <summary>Patrol trip length relative to the base patrol span.</summary>
        public float PatrolMultiplier { get; }

        /// <summary>Body width in metres.</summary>
        public float BodyWidth { get; }

        /// <summary>Body height in metres, floor to crown.</summary>
        public float BodyHeight { get; }

        /// <summary>Body colour while it has not noticed the player.</summary>
        public Color LurkingColour { get; }

        /// <summary>Body colour once it is hunting.</summary>
        public Color HuntingColour { get; }

        /// <summary>Colour of the eyes, and of the light a hunting one throws.</summary>
        public Color GlowColour { get; }

        /// <summary>How many eyes open when it hunts.</summary>
        public int EyeCount { get; }

        /// <summary>Radius of one eye in metres.</summary>
        public float EyeSize { get; }

        /// <summary>
        /// Creates an archetype.
        /// </summary>
        /// <param name="kind">Which kind this describes.</param>
        /// <param name="displayName">Name shown to the player.</param>
        /// <param name="speed">Patrol speed relative to the floor's base Dweller speed.</param>
        /// <param name="chase">Hunting speed relative to the floor's base Dweller speed.</param>
        /// <param name="sense">Sense range relative to the base.</param>
        /// <param name="patrol">Patrol span relative to the base.</param>
        /// <param name="width">Body width in metres.</param>
        /// <param name="height">Body height in metres.</param>
        /// <param name="lurking">Body colour while unaware.</param>
        /// <param name="hunting">Body colour while hunting.</param>
        /// <param name="glow">Eye and chase-light colour.</param>
        /// <param name="eyes">How many eyes it opens.</param>
        /// <param name="eyeSize">Radius of one eye in metres.</param>
        public DwellerArchetype(DwellerKind kind, string displayName, float speed, float chase,
            float sense, float patrol, float width, float height, Color lurking, Color hunting,
            Color glow, int eyes, float eyeSize)
        {
            Kind = kind;
            DisplayName = displayName;
            SpeedMultiplier = speed;
            ChaseMultiplier = chase;
            SenseMultiplier = sense;
            PatrolMultiplier = patrol;
            BodyWidth = width;
            BodyHeight = height;
            LurkingColour = lurking;
            HuntingColour = hunting;
            GlowColour = glow;
            EyeCount = eyes;
            EyeSize = eyeSize;
        }
    }

    /// <summary>
    /// The roster of Dweller kinds. Lookup is a pure function of the kind, so a floor built from the
    /// same seed always carries the same creatures.
    /// </summary>
    /// <remarks>
    /// The three are deliberately opposed rather than scaled copies: the Watcher trades speed for
    /// sight, the Skitter trades sight for speed, and the Lurker sits between them. That way meeting
    /// one is a different problem from meeting another — a Watcher you can outrun but not lose, a
    /// Skitter you can hide from but not outpace.
    /// </remarks>
    public static class DwellerArchetypes
    {
        private static readonly DwellerArchetype[] Roster =
        {
            new DwellerArchetype(DwellerKind.Lurker, "LURKER",
                speed: 1f, chase: 1.77f, sense: 1f, patrol: 1f,
                width: 0.7f, height: 2.2f,
                lurking: new Color(0.06f, 0.05f, 0.07f),
                hunting: new Color(0.16f, 0.03f, 0.04f),
                glow: new Color(1f, 0.22f, 0.16f),
                eyes: 2, eyeSize: 0.12f),

            new DwellerArchetype(DwellerKind.Watcher, "WATCHER",
                // Ambles while unaware, but once it has seen you it closes faster than a walk. Its
                // slowness is meant to be something you exploit, not something that makes it harmless.
                speed: 0.72f, chase: 1.55f, sense: 1.5f, patrol: 1.4f,
                width: 0.5f, height: 2.85f,
                lurking: new Color(0.62f, 0.60f, 0.55f),
                hunting: new Color(0.74f, 0.72f, 0.66f),
                glow: new Color(0.55f, 0.85f, 1f),
                eyes: 2, eyeSize: 0.15f),

            new DwellerArchetype(DwellerKind.Skitter, "SKITTER",
                // Lurks slowly and nearly blind, then bursts: the fastest of the three once it has
                // seen you, and the slowest of the three before it does.
                speed: 0.8f, chase: 2.1f, sense: 0.55f, patrol: 0.6f,
                width: 1.15f, height: 1.05f,
                lurking: new Color(0.11f, 0.06f, 0.05f),
                hunting: new Color(0.26f, 0.08f, 0.04f),
                glow: new Color(1f, 0.72f, 0.18f),
                eyes: 4, eyeSize: 0.075f)
        };

        /// <summary>How many distinct kinds exist.</summary>
        public static int Count => Roster.Length;

        /// <summary>
        /// The archetype for a kind.
        /// </summary>
        /// <param name="kind">Kind to look up.</param>
        /// <returns>Its archetype.</returns>
        public static DwellerArchetype For(DwellerKind kind)
        {
            foreach (DwellerArchetype archetype in Roster)
            {
                if (archetype.Kind == kind) return archetype;
            }

            return Roster[0];
        }

        /// <summary>
        /// The kind at an index, wrapping so a floor can deal out one of each in turn however many
        /// Dwellers it carries.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        /// <returns>The kind for that slot.</returns>
        public static DwellerKind AtIndex(int index)
            => Roster[((index % Roster.Length) + Roster.Length) % Roster.Length].Kind;
    }
}
