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
        /// <param name="maze">The maze module, which the blink snaps its landing point to.</param>
        /// <param name="used">Receives the relic that fired, if any.</param>
        /// <returns><c>true</c> if a power was used this frame.</returns>
        public bool TryUsePowers(RelicFacade relics, PlayerFacade player, DwellerDirector dwellers,
            MazeFacade maze,
            out RelicKind used)
        {
            used = RelicKind.Ward;
            BanisherMissed = false;
            if (relics == null || player == null) return false;

            // Five uses, like the Banisher. An unlimited teleport is not a relic you spend, it is
            // a movement ability, and it made every other way out of a corridor pointless.
            if (player.BlinkRequested && relics.Holds(RelicKind.BlinkShard)
                && TryBlink(player, maze)
                && relics.Spend(RelicKind.BlinkShard))
            {
                used = RelicKind.BlinkShard;
                return true;
            }

            if (player.BanishRequested && relics.Holds(RelicKind.Banisher) && dwellers != null)
            {
                Vector3 forward = player.Forward;
                forward.y = 0f;

                bool killed = dwellers.TryBanishInFront(player.Position, forward.normalized,
                    BanishMetres, BanishHalfAngle);

                // The shot is drawn either way. Previously a miss did nothing whatsoever — no
                // sound, no mark, no charge — so pressing the key read as the key being broken
                // rather than as the shot going wide, and there was no way to learn to aim it.
                ShotTracer.Fire(player.Position, forward, BanishMetres,
                    RelicArchetypes.For(RelicKind.Banisher).Colour, killed);

                // Still only spend a charge on a kill: a shot into an empty corridor that costs you
                // one of five reads as the relic being broken in the other direction.
                if (killed && relics.Spend(RelicKind.Banisher))
                {
                    used = RelicKind.Banisher;
                    return true;
                }

                BanisherMissed = !killed;
            }

            return false;
        }

        /// <summary>
        /// Whether the last banish attempt fired and hit nothing, so the game can say so.
        /// </summary>
        public bool BanisherMissed { get; private set; }

        /// <summary>
        /// Slips the player forward through whatever is in the way.
        /// </summary>
        /// <remarks>
        /// Straight through walls, deliberately. Stopping short of the first obstacle made it a
        /// slightly longer step, and on a floor this dense the first obstacle is usually about two
        /// metres away — so it almost never did anything. Crossing walls means the player often does
        /// not know which room they will land in, which is the whole trade: it gets you out of a
        /// corridor with a Dweller in it, at the price of not choosing where you go.
        /// <para>
        /// The destination is snapped to a cell centre rather than a raw offset, because a raw offset
        /// can land inside the wall itself. Every cell is open floor, so a cell centre is always
        /// somewhere the player can legally stand.
        /// </para>
        /// </remarks>
        /// <param name="player">The player module.</param>
        /// <param name="maze">The maze module, which owns the grid the landing point snaps to.</param>
        /// <returns><c>true</c> if the player actually moved.</returns>
        private static bool TryBlink(PlayerFacade player, MazeFacade maze)
        {
            if (maze == null || maze.CurrentLayout == null) return false;

            Vector3 heading = player.Forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-4f) return false;
            heading.Normalize();

            MazeLayout layout = maze.CurrentLayout;
            Vector2Int from = layout.WorldToCell(player.Position);
            Vector2Int to = layout.WorldToCell(player.Position + heading * BlinkMetres);

            to.x = Mathf.Clamp(to.x, 0, layout.Width - 1);
            to.y = Mathf.Clamp(to.y, 0, layout.Height - 1);

            // Facing the edge of the floor with nowhere to go: report failure so the gesture is not
            // silently swallowed and the player can try facing somewhere else.
            if (to == from) return false;

            player.TeleportTo(layout.CellCenterToWorld(to));
            return true;
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
