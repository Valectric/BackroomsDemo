using UnityEngine;

namespace Backrooms.RelicManager
{
    /// <summary>
    /// The relics a survivor can find. Each does something different, so which relic a floor is
    /// offering changes whether the detour is worth taking.
    /// </summary>
    public enum RelicKind
    {
        /// <summary>Points at the nearest Dweller, so you know what to walk away from.</summary>
        HunterEye = 0,

        /// <summary>Points at the nearest way down.</summary>
        WayfinderStone = 1,

        /// <summary>Points at the nearest uncollected relic.</summary>
        HoarderCharm = 2,

        /// <summary>Absorbs one Dweller that reaches you, and is destroyed doing it.</summary>
        Ward = 3,

        /// <summary>Throws you forward through the dark. Double-tap the look side.</summary>
        BlinkShard = 4,

        /// <summary>Kills a Dweller you are facing. Five charges. Double-tap the move side.</summary>
        Banisher = 5,

        /// <summary>Draws a map of the rooms around you in the corner of the screen.</summary>
        SurveyorsLens = 6
    }

    /// <summary>
    /// What one kind of relic is called, what colour it glows, and what it tells the player it does.
    /// Held as data rather than as subclasses so the whole roster reads in one table.
    /// </summary>
    public sealed class RelicArchetype
    {
        /// <summary>Which kind this describes.</summary>
        public RelicKind Kind { get; }

        /// <summary>Name shown when the relic is picked up.</summary>
        public string DisplayName { get; }

        /// <summary>One line telling the player what they just got, and how to use it.</summary>
        public string Effect { get; }

        /// <summary>Colour the relic glows, and the colour of its compass arrow if it has one.</summary>
        public Color Colour { get; }

        /// <summary>Whether this relic shows an arrow on the HUD.</summary>
        public bool IsCompass { get; }

        /// <summary>
        /// How to use it, shown beside the name in the carried list, or empty for the ones that
        /// simply work while carried.
        /// </summary>
        /// <remarks>
        /// The pickup line says this once and then is gone, which is no use twenty seconds later
        /// when there is something behind you. A relic that needs a gesture has to keep saying so.
        /// </remarks>
        public string Gesture { get; set; } = string.Empty;

        /// <summary>How many uses it carries, or 0 for something that is always on.</summary>
        public int Charges { get; }

        /// <summary>
        /// Creates an archetype.
        /// </summary>
        /// <param name="kind">Which kind this describes.</param>
        /// <param name="displayName">Name shown on pickup.</param>
        /// <param name="effect">One line describing the effect.</param>
        /// <param name="colour">Glow and arrow colour.</param>
        /// <param name="isCompass">Whether it shows a HUD arrow.</param>
        /// <param name="charges">Uses carried, or 0 for always on.</param>
        public RelicArchetype(RelicKind kind, string displayName, string effect, Color colour,
            bool isCompass, int charges)
        {
            Kind = kind;
            DisplayName = displayName;
            Effect = effect;
            Colour = colour;
            IsCompass = isCompass;
            Charges = charges;
        }
    }

    /// <summary>
    /// The roster of relics. Lookup is a pure function of the kind, so a floor built from the same
    /// seed always offers the same relic.
    /// </summary>
    /// <remarks>
    /// Colours are chosen against what the game already uses. Green is the way down and red, cold
    /// blue and amber belong to the three Dwellers, so the relics take the rest of the wheel — and
    /// the three compasses in particular have to be told apart from each other at a glance while all
    /// three are on screen at once.
    /// </remarks>
    public static class RelicArchetypes
    {
        private static readonly RelicArchetype[] Roster =
        {
            new RelicArchetype(RelicKind.HunterEye, "HUNTER'S EYE",
                "An arrow now points at the nearest Dweller",
                new Color(1f, 0.35f, 0.75f), isCompass: true, charges: 0),

            new RelicArchetype(RelicKind.WayfinderStone, "WAYFINDER STONE",
                "An arrow now points at the nearest way down",
                new Color(0.45f, 1f, 0.8f), isCompass: true, charges: 0),

            new RelicArchetype(RelicKind.HoarderCharm, "HOARDER'S CHARM",
                "An arrow now points at the nearest relic",
                new Color(0.72f, 0.42f, 1f), isCompass: true, charges: 0),

            new RelicArchetype(RelicKind.Ward, "DEFENSE WARD",
                "It will take one Dweller for you, once",
                new Color(1f, 0.92f, 0.55f), isCompass: false, charges: 1),

            new RelicArchetype(RelicKind.BlinkShard, "BLINK SHARD",
                "Press F, or double-tap the right side, to slip through the walls",
                new Color(0.55f, 0.78f, 1f), isCompass: false, charges: 0)
            { Gesture = "(F / double-tap right)" },

            new RelicArchetype(RelicKind.Banisher, "BANISHER",
                "Press G, or double-tap the left side, to unmake what you face",
                new Color(1f, 0.62f, 0.25f), isCompass: false, charges: 5)
            { Gesture = "(G / double-tap left)" },

            new RelicArchetype(RelicKind.SurveyorsLens, "SURVEYOR'S LENS",
                "The rooms around you are drawn in the corner",
                new Color(0.62f, 0.92f, 0.72f), isCompass: false, charges: 0)
        };

        /// <summary>How many distinct kinds exist.</summary>
        public static int Count => Roster.Length;

        /// <summary>
        /// The archetype for a kind.
        /// </summary>
        /// <param name="kind">Kind to look up.</param>
        /// <returns>Its archetype.</returns>
        public static RelicArchetype For(RelicKind kind)
        {
            foreach (RelicArchetype archetype in Roster)
            {
                if (archetype.Kind == kind) return archetype;
            }

            return Roster[0];
        }

        /// <summary>
        /// The kind offered at an index, wrapping so descending deals out the whole roster in turn
        /// rather than repeating one relic.
        /// </summary>
        /// <param name="index">Zero-based index, normally the floor number.</param>
        /// <returns>The kind for that slot.</returns>
        public static RelicKind AtIndex(int index)
            => Roster[((index % Roster.Length) + Roster.Length) % Roster.Length].Kind;
    }
}
