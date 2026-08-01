using System.Collections.Generic;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using Backrooms.RelicManager;
using Backrooms.UIManager;
using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Turns the relics a player is carrying into things that actually happen: the compass arrows on
    /// the HUD, the blink, the banisher shot, and the ward that takes a Dweller for you.
    /// </summary>
    /// <remarks>
    /// This lives in the application layer because every power reaches across modules — a compass
    /// needs the maze and the Dwellers, a blink needs the player, a banisher needs both. Keeping the
    /// wiring here leaves each module knowing only about itself.
    /// </remarks>
    internal sealed class PowerDirector
    {
        /// <summary>How far a blink carries, in metres.</summary>
        private const float BlinkMetres = 9f;

        /// <summary>How far a banisher shot reaches, in metres.</summary>
        private const float BanishMetres = 22f;

        /// <summary>Half-angle of the banisher's cone, in degrees.</summary>
        private const float BanishHalfAngle = 32f;

        /// <summary>Reused between frames so the HUD is not handed a fresh list every frame.</summary>
        private readonly List<CompassMark> _marks = new List<CompassMark>();

        /// <summary>
        /// Works out which arrows the HUD should show, from what the player is carrying.
        /// </summary>
        /// <param name="relics">The relic module.</param>
        /// <param name="player">The player module.</param>
        /// <param name="maze">The maze module.</param>
        /// <param name="dwellers">The floor's Dwellers.</param>
        /// <returns>One mark per compass relic held that currently has something to point at.</returns>
        public IReadOnlyList<CompassMark> Compasses(RelicFacade relics, PlayerFacade player,
            MazeFacade maze, DwellerDirector dwellers)
        {
            _marks.Clear();
            if (relics == null || player == null) return _marks;

            if (relics.Holds(RelicKind.HunterEye)
                && dwellers != null
                && dwellers.TryGetNearestPosition(player.Position, out Vector3 dweller))
            {
                Add(RelicKind.HunterEye, player, dweller);
            }

            if (relics.Holds(RelicKind.WayfinderStone) && maze != null && maze.CurrentLayout != null)
            {
                Add(RelicKind.WayfinderStone, player, maze.GetNearestStairsPosition(player.Position));
            }

            if (relics.Holds(RelicKind.HoarderCharm)
                && TryNearest(relics.StandingPositions(), player.Position, out Vector3 relic))
            {
                Add(RelicKind.HoarderCharm, player, relic);
            }

            return _marks;
        }

        /// <summary>
        /// Acts on the player's gestures: a double tap on the look side blinks, a double tap on the
        /// move side fires the banisher.
        /// </summary>
        /// <param name="relics">The relic module.</param>
        /// <param name="player">The player module.</param>
        /// <param name="dwellers">The floor's Dwellers.</param>
        /// <param name="used">Receives the relic that fired, if any.</param>
        /// <returns><c>true</c> if a power was used this frame.</returns>
        public bool TryUsePowers(RelicFacade relics, PlayerFacade player, DwellerDirector dwellers,
            out RelicKind used)
        {
            used = RelicKind.Ward;
            if (relics == null || player == null) return false;

            if (player.DoubleTappedLookSide && relics.Holds(RelicKind.BlinkShard))
            {
                if (player.Blink(BlinkMetres) > 0.05f)
                {
                    used = RelicKind.BlinkShard;
                    return true;
                }
            }

            if (player.DoubleTappedMoveSide && relics.Holds(RelicKind.Banisher) && dwellers != null)
            {
                Vector3 forward = player.Forward;
                forward.y = 0f;

                // Only spend a charge if something actually dies. A shot into an empty corridor that
                // still costs a charge reads as the relic being broken.
                if (dwellers.TryBanishInFront(player.Position, forward.normalized, BanishMetres,
                        BanishHalfAngle)
                    && relics.Spend(RelicKind.Banisher))
                {
                    used = RelicKind.Banisher;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Spends the ward to survive a Dweller that has reached the player, taking that Dweller off
        /// the floor with it.
        /// </summary>
        /// <remarks>
        /// The Dweller has to go too. A ward that only cancels the catch leaves the creature standing
        /// on top of the player, and it simply catches them again on the next frame — the ward would
        /// read as having done nothing.
        /// </remarks>
        /// <param name="relics">The relic module.</param>
        /// <param name="dwellers">The floor's Dwellers.</param>
        /// <returns><c>true</c> if the ward was spent and the run continues.</returns>
        public bool TrySpendWard(RelicFacade relics, DwellerDirector dwellers)
        {
            if (relics == null || dwellers == null) return false;
            if (!relics.Holds(RelicKind.Ward)) return false;
            if (!relics.Spend(RelicKind.Ward)) return false;

            dwellers.BanishWhicheverCaughtThePlayer();
            return true;
        }

        /// <summary>
        /// Adds one compass mark pointing from the player to a world position.
        /// </summary>
        /// <param name="kind">Which relic the arrow belongs to.</param>
        /// <param name="player">The player module.</param>
        /// <param name="target">World position to point at.</param>
        private void Add(RelicKind kind, PlayerFacade player, Vector3 target)
        {
            Vector3 to = target - player.Position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;

            Vector3 forward = player.Forward;
            forward.y = 0f;

            // Signed bearing relative to where the player is looking, so the arrow means "that way"
            // rather than "north".
            float bearing = Vector3.SignedAngle(forward.normalized, to.normalized, Vector3.up);
            RelicArchetype archetype = RelicArchetypes.For(kind);
            _marks.Add(new CompassMark(bearing, to.magnitude, archetype.Colour));
        }

        /// <summary>
        /// The closest of a set of world positions.
        /// </summary>
        /// <param name="positions">Positions to search.</param>
        /// <param name="from">Where to measure from.</param>
        /// <param name="nearest">Receives the closest position.</param>
        /// <returns><c>true</c> if there was one.</returns>
        private static bool TryNearest(IEnumerable<Vector3> positions, Vector3 from,
            out Vector3 nearest)
        {
            nearest = Vector3.zero;
            float best = float.PositiveInfinity;
            bool found = false;

            foreach (Vector3 at in positions)
            {
                float away = Vector3.Distance(new Vector3(from.x, 0f, from.z),
                    new Vector3(at.x, 0f, at.z));
                if (away >= best) continue;

                best = away;
                nearest = at;
                found = true;
            }

            return found;
        }
    }
}
