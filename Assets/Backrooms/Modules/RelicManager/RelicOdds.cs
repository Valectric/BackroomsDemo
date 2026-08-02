using UnityEngine;

namespace Backrooms.RelicManager
{
    /// <summary>
    /// How likely each relic is to be the one a floor offers. The odds change with depth, so a floor
    /// tends to hand you the thing that floor is about rather than a uniform draw from the roster.
    /// </summary>
    /// <remarks>
    /// Every kind has a floor it peaks on, and its weight falls away either side of that floor:
    /// <list type="bullet">
    /// <item><description>1 — <b>Ward</b>. The first floor is where a player learns what a Dweller
    /// is, and the kindest thing to hand them is one free mistake.</description></item>
    /// <item><description>2 — <b>Wayfinder Stone</b>. Once they are not dying immediately, the
    /// question becomes where the stairs are.</description></item>
    /// <item><description>3 — <b>Hunter's Eye</b>. Deep enough that Dwellers are a real threat, so
    /// knowing where one is starts to beat knowing where the exit is.</description></item>
    /// <item><description>4 — <b>Hoarder's Charm</b>. By here a player is building a kit rather than
    /// surviving, so the relic that finds relics is what compounds.</description></item>
    /// <item><description>5 — <b>Surveyor's Lens</b>. Floors blur together by now; a map is
    /// orientation rather than a single arrow.</description></item>
    /// <item><description>6 — <b>Blink Shard</b>. Escape, for when avoidance has stopped
    /// working.</description></item>
    /// <item><description>7 — <b>Banisher</b>. The deepest answer, and the only one that removes a
    /// Dweller rather than surviving it.</description></item>
    /// </list>
    /// <para>
    /// The peaks then repeat, so floor 8 reads like floor 1 again. That is deliberate: the roster is
    /// finite and the dungeon is not, and a player who has gone deep enough to lose their Ward should
    /// be offered another one rather than a flat lottery for the rest of the run.
    /// </para>
    /// </remarks>
    public static class RelicOdds
    {
        /// <summary>How many floors the pattern of peaks takes to come back around.</summary>
        private const int Cycle = 7;

        /// <summary>Weight every kind carries regardless of floor, so nothing is ever impossible.</summary>
        private const float Floor = 1f;

        /// <summary>Extra weight at a kind's own floor, shared out as the floor moves away from it.</summary>
        private const float Peak = 8f;

        /// <summary>
        /// How much further the kind that actually peaks here is favoured, on top of the curve.
        /// </summary>
        /// <remarks>
        /// Applied only at distance zero. Scaling <see cref="Peak"/> instead would scale the whole
        /// curve and barely move the share at all, since every kind's weight would grow together —
        /// what makes a floor read as being <i>about</i> one relic is the gap, not the height.
        /// </remarks>
        private const float PeakMultiplier = 4f;

        /// <summary>The floor each kind peaks on, indexed to match the roster order.</summary>
        private static readonly RelicKind[] PeakOrder =
        {
            RelicKind.Ward,             // floor 1
            RelicKind.WayfinderStone,   // floor 2
            RelicKind.HunterEye,        // floor 3
            RelicKind.HoarderCharm,     // floor 4
            RelicKind.SurveyorsLens,    // floor 5
            RelicKind.BlinkShard,       // floor 6
            RelicKind.Banisher          // floor 7
        };

        /// <summary>
        /// How strongly a floor favours one kind of relic.
        /// </summary>
        /// <param name="kind">The kind being weighed.</param>
        /// <param name="floor">One-based floor number.</param>
        /// <returns>A positive weight; larger means more likely.</returns>
        public static float Weight(RelicKind kind, int floor)
        {
            int peak = PeakFloorOf(kind);
            if (peak <= 0) return Floor;

            // Cyclic distance, so floor 1 is one step from the kind that peaks on floor 7 rather
            // than six — the pattern is a loop, not a line.
            int raw = Mathf.Abs(Wrap(floor) - peak);
            int distance = Mathf.Min(raw, Cycle - raw);

            float weight = Floor + Peak / (1 + distance);
            return distance == 0 ? weight * PeakMultiplier : weight;
        }

        /// <summary>
        /// The floor a kind peaks on, or 0 if it has no peak.
        /// </summary>
        /// <param name="kind">The kind to look up.</param>
        /// <returns>A one-based floor number, or 0.</returns>
        public static int PeakFloorOf(RelicKind kind)
        {
            for (int i = 0; i < PeakOrder.Length; i++)
            {
                if (PeakOrder[i] == kind) return i + 1;
            }

            return 0;
        }

        /// <summary>
        /// Folds a floor number into the range the peaks are defined over.
        /// </summary>
        /// <param name="floor">One-based floor number, of any depth.</param>
        /// <returns>A floor in 1..<see cref="Cycle"/>.</returns>
        private static int Wrap(int floor)
        {
            int zeroBased = floor - 1;
            return (((zeroBased % Cycle) + Cycle) % Cycle) + 1;
        }
    }
}
