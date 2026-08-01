using System.Collections.Generic;
using Backrooms.MazeManager;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.RelicManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for what relics actually do once carried: which kinds a floor offers,
    /// how charges accumulate and drain, and that nothing survives a new run.
    /// </summary>
    public class RelicPowerTests
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
        /// Every kind must have an archetype with a name and an effect the player can read, and no
        /// two may share a name.
        /// </summary>
        [Test]
        public void EveryKind_HasANamedArchetype()
        {
            var names = new HashSet<string>();
            foreach (RelicKind kind in System.Enum.GetValues(typeof(RelicKind)))
            {
                RelicArchetype archetype = RelicArchetypes.For(kind);
                Assert.AreEqual(kind, archetype.Kind, $"{kind} must map to its own archetype");
                Assert.IsNotEmpty(archetype.DisplayName, $"{kind} needs a name");
                Assert.IsNotEmpty(archetype.Effect, $"{kind} needs to tell the player what it does");
                Assert.IsTrue(names.Add(archetype.DisplayName), $"two kinds are called {archetype.DisplayName}");
            }

            Assert.AreEqual(RelicArchetypes.Count, names.Count, "one archetype per kind");
        }

        /// <summary>
        /// The three compasses must be told apart by colour while all three are on screen together,
        /// which is the only situation in which their colours matter.
        /// </summary>
        [Test]
        public void TheThreeCompasses_AreDistinctColours()
        {
            var compasses = new List<RelicArchetype>();
            foreach (RelicKind kind in System.Enum.GetValues(typeof(RelicKind)))
            {
                RelicArchetype archetype = RelicArchetypes.For(kind);
                if (archetype.IsCompass) compasses.Add(archetype);
            }

            Assert.AreEqual(3, compasses.Count, "three relics point at things");

            for (int i = 0; i < compasses.Count; i++)
            {
                for (int j = i + 1; j < compasses.Count; j++)
                {
                    Color a = compasses[i].Colour;
                    Color b = compasses[j].Colour;
                    float apart = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    Assert.Greater(apart, 0.5f,
                        $"{compasses[i].DisplayName} and {compasses[j].DisplayName} look alike");
                }
            }
        }

        /// <summary>
        /// Descending must offer a different relic each floor rather than the same one repeatedly —
        /// a second copy of a compass you already carry is not a reason to cross a floor.
        /// </summary>
        [Test]
        public void DescendingFloors_OfferDifferentRelics()
        {
            (RelicFacade facade, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            var offered = new HashSet<RelicKind>();
            for (int floor = 0; floor < RelicArchetypes.Count; floor++)
            {
                MazeLayout layout = NewFloor(floor);
                List<Vector2Int> placed = relics.Place(layout, 1, floor, floor, parent);
                Assert.AreEqual(1, placed.Count, $"floor {floor} should carry a relic");

                // Collect it, so the next floor sees it as already carried.
                relics.TryCollect(layout.CellCenterToWorld(placed[0]), 1.6f);
                offered.Add(relics.LastCollected);
            }

            Assert.AreEqual(RelicArchetypes.Count, offered.Count,
                "six floors should have offered six different relics");
        }

        /// <summary>
        /// An always-on relic is held forever; a charged one drains and then stops being held.
        /// </summary>
        [Test]
        public void Charges_DrainOnlyForChargedRelics()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            MazeLayout layout = NewFloor(5);

            // The Banisher: five shots, then nothing.
            List<Vector2Int> placed = relics.Place(layout, 1, 5, (int)RelicKind.Banisher, parent);
            relics.TryCollect(layout.CellCenterToWorld(placed[0]), 1.6f);

            Assert.AreEqual(RelicKind.Banisher, relics.LastCollected, "the Banisher was offered");
            Assert.AreEqual(5, relics.ChargesOf(RelicKind.Banisher), "it arrives with five shots");

            for (int shot = 1; shot <= 5; shot++)
            {
                Assert.IsTrue(relics.Spend(RelicKind.Banisher), $"shot {shot} should be available");
            }

            Assert.IsFalse(relics.Spend(RelicKind.Banisher), "a sixth shot must not fire");
            Assert.IsFalse(relics.Holds(RelicKind.Banisher), "and it is no longer carried");
        }

        /// <summary>
        /// A compass never runs down — it is a property of carrying it, not a consumable.
        /// </summary>
        [Test]
        public void ACompass_NeverRunsOut()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            MazeLayout layout = NewFloor(2);
            List<Vector2Int> placed = relics.Place(layout, 1, 2, (int)RelicKind.WayfinderStone, parent);
            relics.TryCollect(layout.CellCenterToWorld(placed[0]), 1.6f);

            Assert.IsTrue(relics.Holds(RelicKind.WayfinderStone), "it is carried");
            for (int use = 0; use < 50; use++) relics.Spend(RelicKind.WayfinderStone);
            Assert.IsTrue(relics.Holds(RelicKind.WayfinderStone), "and it still is after fifty uses");
        }

        /// <summary>
        /// Once every relic is carried, further floors offer Banishers, and their charges stack. A
        /// floor that could only offer a duplicate the player already has would be a floor with
        /// nothing on it.
        /// </summary>
        [Test]
        public void OnceEverythingIsCarried_FurtherFloorsStackBanisherCharges()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            // Clear the roster first: every kind collected once.
            for (int floor = 0; floor < RelicArchetypes.Count; floor++)
            {
                MazeLayout layout = NewFloor(floor);
                List<Vector2Int> placed = relics.Place(layout, 1, floor, floor, parent);
                relics.TryCollect(layout.CellCenterToWorld(placed[0]), 1.6f);
            }

            int before = relics.ChargesOf(RelicKind.Banisher);
            Assert.AreEqual(5, before, "the Banisher was collected once along the way");

            MazeLayout extra = NewFloor(99);
            List<Vector2Int> last = relics.Place(extra, 1, 99, 0, parent);
            relics.TryCollect(extra.CellCenterToWorld(last[0]), 1.6f);

            Assert.AreEqual(RelicKind.Banisher, relics.LastCollected,
                "with nothing new to give, a floor offers more shots");
            Assert.AreEqual(10, relics.ChargesOf(RelicKind.Banisher), "and they stack");
        }

        /// <summary>
        /// A new run starts empty-handed. Powers carrying across a death would make each attempt
        /// easier than the last for no reason the player earned.
        /// </summary>
        [Test]
        public void ANewRun_StartsWithNothingCarried()
        {
            (RelicFacade facade, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            MazeLayout layout = NewFloor(9);
            List<Vector2Int> placed = relics.Place(layout, 1, 9, (int)RelicKind.Ward, parent);
            relics.TryCollect(layout.CellCenterToWorld(placed[0]), 1.6f);
            Assert.IsTrue(relics.Holds(RelicKind.Ward), "the ward is carried");

            facade.ResetRun();

            Assert.IsFalse(relics.Holds(RelicKind.Ward), "a new run carries nothing");
            Assert.AreEqual(0, facade.Collected, "and counts nothing");
        }

        /// <summary>
        /// The ward absorbs exactly one Dweller and is then gone.
        /// </summary>
        [Test]
        public void TheWard_AbsorbsExactlyOnce()
        {
            (RelicFacade _, RelicManagerTestFacade relics) = NewRelics();
            var parent = new GameObject("RelicRoot").transform;

            MazeLayout layout = NewFloor(11);
            List<Vector2Int> placed = relics.Place(layout, 1, 11, (int)RelicKind.Ward, parent);
            relics.TryCollect(layout.CellCenterToWorld(placed[0]), 1.6f);

            Assert.IsTrue(relics.Spend(RelicKind.Ward), "it takes the first Dweller");
            Assert.IsFalse(relics.Spend(RelicKind.Ward), "and there is nothing left for a second");
            Assert.IsFalse(relics.Holds(RelicKind.Ward), "so it is no longer carried");
        }
    }
}
