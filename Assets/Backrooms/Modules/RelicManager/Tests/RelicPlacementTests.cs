using System.Collections.Generic;
using Backrooms.MazeManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.RelicManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for relic placement and collection. A relic exists to make descending
    /// a decision, and it only does that if it is genuinely out of the player's way — so the tests
    /// measure the detour rather than merely checking a relic exists.
    /// </summary>
    public class RelicPlacementTests
    {
        /// <summary>Floor size the game ships.</summary>
        private const int FloorCells = 24;

        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// Creates a relic module and returns its facade and test seam.
        /// </summary>
        /// <returns>The facade and its test seam.</returns>
        private static (RelicFacade facade, RelicManagerTestFacade test) NewRelics()
        {
            var go = new GameObject("Relics");
            RelicFacade facade = go.AddComponent<RelicFacade>();
            return (facade, facade.GetTestFacade());
        }

        /// <summary>
        /// Generates a floor the size the game ships.
        /// </summary>
        /// <param name="seed">Generation seed.</param>
        /// <returns>The layout.</returns>
        private static MazeLayout NewFloor(int seed)
        {
            var go = new GameObject("MazeManager");
            return go.AddComponent<MazeFacade>()
                .GetTestFacade()
                .Generate(new MazeSettings(FloorCells, FloorCells, seed));
        }

        /// <summary>
        /// Walking distance from a set of cells to every cell, through open passages only.
        /// </summary>
        /// <param name="layout">The floor to measure.</param>
        /// <param name="sources">Cells to measure from.</param>
        /// <returns>Distance per cell, indexed row-major; -1 where unreachable.</returns>
        private static int[] Distances(MazeLayout layout, IEnumerable<Vector2Int> sources)
        {
            var distance = new int[layout.Width * layout.Height];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;

            var queue = new Queue<Vector2Int>();
            foreach (Vector2Int source in sources)
            {
                distance[source.y * layout.Width + source.x] = 0;
                queue.Enqueue(source);
            }

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                int here = distance[cur.y * layout.Width + cur.x];

                foreach (Direction dir in Directions.All)
                {
                    if (!layout.CanMove(cur.x, cur.y, dir)) continue;
                    Vector2Int d = Directions.Delta(dir);
                    var next = new Vector2Int(cur.x + d.x, cur.y + d.y);
                    int index = next.y * layout.Width + next.x;
                    if (distance[index] != -1) continue;

                    distance[index] = here + 1;
                    queue.Enqueue(next);
                }
            }

            return distance;
        }

        /// <summary>
        /// A relic must be a real detour: far enough from every stairwell and from the spawn that
        /// going for it is a choice rather than something collected on the way past. A relic beside
        /// the exit is not a decision, it is scenery.
        /// </summary>
        [Test]
        public void ARelic_IsAGenuineDetour()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            int worstSeen = int.MaxValue;
            int worstSeed = 0;
            float total = 0f;

            const int seeds = 20;
            for (int seed = 0; seed < seeds; seed++)
            {
                MazeLayout layout = NewFloor(seed);
                List<Vector2Int> placed = relics.Place(layout, 1, seed, parent);
                Assert.AreEqual(1, placed.Count, $"seed {seed}: the floor should carry a relic");

                var sources = new List<Vector2Int>(layout.Stairs) { layout.Spawn };
                int[] distance = Distances(layout, sources);
                int detour = distance[placed[0].y * layout.Width + placed[0].x];

                total += detour;
                if (detour >= worstSeen) continue;
                worstSeen = detour;
                worstSeed = seed;
            }

            MooseRunnerFacade.Log(
                $"shortest relic detour: {worstSeen} cells (seed {worstSeed}); mean {total / seeds:F1}");

            Assert.GreaterOrEqual(worstSeen, 6,
                $"seed {worstSeed} put a relic {worstSeen} cells from a stairwell or the spawn");
        }

        /// <summary>
        /// A relic may never share a cell with a stairwell or the spawn, or it would be collected
        /// without any decision at all.
        /// </summary>
        [Test]
        public void ARelic_NeverSitsOnAStairwellOrTheSpawn()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            for (int seed = 0; seed < 15; seed++)
            {
                MazeLayout layout = NewFloor(seed);
                foreach (Vector2Int cell in relics.Place(layout, 2, seed, parent))
                {
                    Assert.AreNotEqual(layout.Spawn, cell, $"seed {seed}: relic on the spawn");
                    Assert.IsFalse(layout.IsStairs(cell), $"seed {seed}: relic on a stairwell");
                }
            }
        }

        /// <summary>
        /// Reaching a relic collects it exactly once, and the tally follows.
        /// </summary>
        [Test]
        public void ReachingARelic_CollectsItOnce()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            MazeLayout layout = NewFloor(3);
            List<Vector2Int> placed = relics.Place(layout, 1, seed: 3, parent);
            Vector3 at = layout.CellCenterToWorld(placed[0]);

            Assert.AreEqual(1, relics.Remaining, "the relic should be standing");
            Assert.IsTrue(relics.HasRelicAt(placed[0]), "and standing where it was placed");

            Assert.IsFalse(relics.TryCollect(at + new Vector3(9f, 0f, 0f), 1.6f),
                "a relic nine metres away must not be collected");

            Assert.IsTrue(relics.TryCollect(at, 1.6f), "standing on it should collect it");
            Assert.AreEqual(1, relics.Collected, "the tally should count it");
            Assert.AreEqual(0, relics.Remaining, "and it should no longer be standing");

            Assert.IsFalse(relics.TryCollect(at, 1.6f), "it must not be collectable twice");
            Assert.AreEqual(1, relics.Collected, "so the tally must not move again");
        }

        /// <summary>
        /// Two relics on one floor must land in different places, or the second is pointless.
        /// </summary>
        [Test]
        public void TwoRelics_LandApartFromEachOther()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            for (int seed = 0; seed < 10; seed++)
            {
                MazeLayout layout = NewFloor(seed);
                List<Vector2Int> placed = relics.Place(layout, 2, seed, parent);

                Assert.AreEqual(2, placed.Count, $"seed {seed}: two relics requested");
                int apart = Distances(layout, new[] { placed[0] })[
                    placed[1].y * layout.Width + placed[1].x];
                Assert.Greater(apart, 5,
                    $"seed {seed}: two relics only {apart} cells apart is one relic with a spare");
            }
        }

        /// <summary>
        /// Placing a new floor's relics clears the previous floor's, so they do not accumulate in the
        /// scene run after run.
        /// </summary>
        [Test]
        public void PlacingANewFloor_ClearsTheOldRelics()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            relics.Place(NewFloor(1), 2, seed: 1, parent);
            Assert.AreEqual(2, relics.Remaining, "first floor carries two");

            relics.Place(NewFloor(2), 1, seed: 2, parent);
            Assert.AreEqual(1, relics.Remaining, "the next floor should carry only its own");
        }

        /// <summary>
        /// Placement must be reproducible: the same floor and seed put relics in the same cells.
        /// </summary>
        [Test]
        public void Placement_IsDeterministic()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            MazeLayout layout = NewFloor(7);
            List<Vector2Int> first = new List<Vector2Int>(relics.Place(layout, 2, seed: 7, parent));
            List<Vector2Int> second = relics.Place(layout, 2, seed: 7, parent);

            CollectionAssert.AreEqual(first, second, "same floor and seed, same relic cells");
        }
    }
}
