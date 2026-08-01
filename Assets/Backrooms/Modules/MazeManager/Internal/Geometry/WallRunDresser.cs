using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Lays furniture end-to-end along one <see cref="WallRun"/>, at continuous offsets rather than
    /// on cell centres. This is what stops a generated floor reading as a lattice: pieces land
    /// wherever the run's slack puts them, sometimes shoulder to shoulder and sometimes metres apart,
    /// which is how a real wall accumulates furniture.
    /// </summary>
    /// <remarks>
    /// Coverage, not count, is the control. A run is asked to carry roughly a third of its length in
    /// furniture; pieces are drawn until that is met, then the leftover length is scattered into the
    /// gaps between them. That keeps a 4m stub sparse and a 20m mall wall busy without either being
    /// tuned by hand.
    /// </remarks>
    internal sealed class WallRunDresser
    {
        /// <summary>Smallest gap left between two neighbouring pieces, in metres.</summary>
        private const float MinGap = 0.15f;

        /// <summary>
        /// How much wall is left bare either side of a doorway, in metres. Furniture pushed right up
        /// to a threshold reads as blocking it even though nothing here has a collider.
        /// </summary>
        private const float DoorwayClearance = 1.2f;

        /// <summary>Clearance left where a run ends at a corner, which furniture may sit into.</summary>
        private const float CornerClearance = 0.06f;

        /// <summary>Lowest share of a run's usable length covered by furniture.</summary>
        private const float MinCoverage = 0.09f;

        /// <summary>Highest share of a run's usable length covered by furniture.</summary>
        private const float MaxCoverage = 0.17f;

        /// <summary>Largest yaw a piece is turned off true, in degrees.</summary>
        private const float MaxYawDegrees = 6f;

        /// <summary>
        /// Clearance from the wall plane, in metres. The skirting is a box straddling the wall plane,
        /// so it stands 0.11m proud into the room; anything less buries the bottom of a cabinet in it.
        /// </summary>
        private const float WallGap = 0.14f;

        /// <summary>Ceiling on pieces per run, so one long wall cannot swamp the floor's budget.</summary>
        private const int MaxPieces = 3;

        /// <summary>Attempts to draw a model narrow enough to still fit the remaining length.</summary>
        private const int DrawAttempts = 3;

        /// <summary>Slack added to a footprint before testing it against what is already placed.</summary>
        private const float RejectMargin = 0.12f;

        /// <summary>Pieces drawn for the run currently being dressed.</summary>
        private readonly List<GameObject> _pieces = new List<GameObject>();

        /// <summary>Width along the run of each entry in <see cref="_pieces"/>, in metres.</summary>
        private readonly List<float> _widths = new List<float>();

        /// <summary>
        /// Dresses one wall run.
        /// </summary>
        /// <param name="run">The stretch of wall to furnish.</param>
        /// <param name="choices">Models that look right backed onto a wall.</param>
        /// <param name="parent">Transform to parent the furniture under.</param>
        /// <param name="rng">Seeded generator, so a floor furnishes identically every time.</param>
        /// <param name="placed">Footprints already on the floor; extended with what this run adds.</param>
        /// <returns>How many pieces were kept.</returns>
        public int Dress(WallRun run, GameObject[] choices, Transform parent, System.Random rng,
            List<Bounds> placed)
        {
            if (choices == null || choices.Length == 0) return 0;

            float startInset = run.DoorwayAtStart ? DoorwayClearance : CornerClearance;
            float usable = run.Length - startInset - (run.DoorwayAtEnd ? DoorwayClearance : CornerClearance);
            if (usable <= MinGap * 2f) return 0;

            float target = usable * Mathf.Lerp(MinCoverage, MaxCoverage, (float)rng.NextDouble());
            Draw(run, choices, parent, rng, usable, target);
            if (_pieces.Count == 0) return 0;

            float covered = 0f;
            foreach (float width in _widths) covered += width;

            float[] gaps = SplitSlack(usable - covered, _pieces.Count + 1, rng);
            float cursor = startInset;
            int kept = 0;

            for (int i = 0; i < _pieces.Count; i++)
            {
                cursor += gaps[i];
                if (Seat(_pieces[i], run, cursor + _widths[i] * 0.5f, placed)) kept++;
                cursor += _widths[i];
            }

            return kept;
        }

        /// <summary>
        /// Draws pieces for a run until the coverage target is met or nothing narrow enough is left
        /// to fit. Each piece is instantiated to be measured, because the pack's models vary in width
        /// by a factor of four and their bounds are the only honest source of that.
        /// </summary>
        /// <param name="run">The run being dressed.</param>
        /// <param name="choices">Models that look right backed onto a wall.</param>
        /// <param name="parent">Transform to parent the furniture under.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <param name="usable">Length of run available for furniture, in metres.</param>
        /// <param name="target">Length of furniture the run should carry, in metres.</param>
        private void Draw(WallRun run, GameObject[] choices, Transform parent, System.Random rng,
            float usable, float target)
        {
            _pieces.Clear();
            _widths.Clear();
            float covered = 0f;

            while (covered < target && _pieces.Count < MaxPieces)
            {
                float room = usable - covered - (_pieces.Count + 2) * MinGap;
                if (room <= 0f) return;
                if (!TryDraw(run, choices, parent, rng, room, out GameObject piece, out float width))
                {
                    return;
                }

                _pieces.Add(piece);
                _widths.Add(width);
                covered += width;
            }
        }

        /// <summary>
        /// Instantiates a model that fits the remaining length, retrying with a different draw when
        /// the first is too wide. Without the retry a single oversized cabinet ends a run early and
        /// leaves the rest of a long wall bare.
        /// </summary>
        /// <param name="run">The run being dressed.</param>
        /// <param name="choices">Models that look right backed onto a wall.</param>
        /// <param name="parent">Transform to parent the furniture under.</param>
        /// <param name="rng">Seeded generator.</param>
        /// <param name="maxWidth">Widest piece that still fits, in metres.</param>
        /// <param name="piece">Receives the instantiated piece.</param>
        /// <param name="width">Receives its width along the run, in metres.</param>
        /// <returns><c>true</c> if a piece was drawn.</returns>
        private static bool TryDraw(WallRun run, GameObject[] choices, Transform parent,
            System.Random rng, float maxWidth, out GameObject piece, out float width)
        {
            for (int attempt = 0; attempt < DrawAttempts; attempt++)
            {
                GameObject model = choices[rng.Next(choices.Length)];
                if (model == null) continue;

                float yaw = ((float)rng.NextDouble() * 2f - 1f) * MaxYawDegrees;
                Quaternion rotation = Quaternion.LookRotation(-run.IntoRoom)
                                      * Quaternion.Euler(0f, yaw, 0f);

                GameObject instance = Object.Instantiate(model, run.Start, rotation, parent);
                instance.name = model.name;
                PropPlacement.StripColliders(instance);

                if (PropPlacement.TryGetWorldBounds(instance, out Bounds bounds))
                {
                    float measured = run.AlongX ? bounds.size.x : bounds.size.z;
                    if (measured <= maxWidth)
                    {
                        piece = instance;
                        width = measured;
                        return true;
                    }
                }

                Object.Destroy(instance);
            }

            piece = null;
            width = 0f;
            return false;
        }

        /// <summary>
        /// Positions a drawn piece at an offset along the run, seats it against the wall and on the
        /// floor, then rejects it if it lands on something already placed — which happens where two
        /// runs meet at an inside corner.
        /// </summary>
        /// <param name="instance">The piece to seat.</param>
        /// <param name="run">The run being dressed.</param>
        /// <param name="centreAlong">Where the piece's centre goes along the run, in metres.</param>
        /// <param name="placed">Footprints already on the floor.</param>
        /// <returns><c>true</c> if the piece was kept.</returns>
        private static bool Seat(GameObject instance, WallRun run, float centreAlong, List<Bounds> placed)
        {
            if (!PropPlacement.TryGetWorldBounds(instance, out Bounds bounds))
            {
                Object.Destroy(instance);
                return false;
            }

            Vector3 wanted = run.PointAt(centreAlong);
            float have = run.AlongX ? bounds.center.x : bounds.center.z;
            float want = run.AlongX ? wanted.x : wanted.z;
            instance.transform.position += run.Along * (want - have);

            PropPlacement.SeatAgainstWall(instance, wanted, run.IntoRoom, WallGap);
            PropPlacement.SeatOnFloor(instance, 0f);

            if (!PropPlacement.TryGetWorldBounds(instance, out Bounds footprint))
            {
                Object.Destroy(instance);
                return false;
            }

            footprint.Expand(RejectMargin);
            foreach (Bounds other in placed)
            {
                if (!other.Intersects(footprint)) continue;
                Object.Destroy(instance);
                return false;
            }

            placed.Add(footprint);
            return true;
        }

        /// <summary>
        /// Scatters a run's leftover length into the gaps around its pieces. Every gap is guaranteed
        /// the minimum first, and only what remains is shared out by random weight, so pieces cluster
        /// in places and leave the wall bare in others instead of being evenly spaced.
        /// </summary>
        /// <param name="slack">Length not covered by furniture, in metres.</param>
        /// <param name="count">How many gaps to produce (one more than the piece count).</param>
        /// <param name="rng">Seeded generator.</param>
        /// <returns>Gap widths in metres, in order along the run.</returns>
        private static float[] SplitSlack(float slack, int count, System.Random rng)
        {
            var gaps = new float[count];
            float free = Mathf.Max(0f, slack - count * MinGap);
            float sum = 0f;

            for (int i = 0; i < count; i++)
            {
                gaps[i] = 0.4f + (float)rng.NextDouble();
                sum += gaps[i];
            }

            for (int i = 0; i < count; i++) gaps[i] = MinGap + free * gaps[i] / sum;
            return gaps;
        }
    }
}
