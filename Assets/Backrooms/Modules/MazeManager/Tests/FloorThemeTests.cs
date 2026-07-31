using System.Collections.Generic;
using Backrooms.MazeManager;
using MooseRunner.helper;
using NUnit.Framework;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// White-box PlayMode tests for the per-floor palettes. The Backrooms in this setting are a
    /// dungeon stitched together from different kinds of space, so each floor must look distinct,
    /// and a given floor number must always look the same.
    /// </summary>
    public class FloorThemeTests
    {
        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// The same floor number always yields the same palette, so descending is reproducible.
        /// </summary>
        [Test]
        public void ForFloor_IsDeterministic()
        {
            for (int floor = 1; floor <= 12; floor++)
            {
                FloorTheme a = FloorThemes.ForFloor(floor);
                FloorTheme b = FloorThemes.ForFloor(floor);
                Assert.AreSame(a, b, $"floor {floor} must always return the same theme");
            }
        }

        /// <summary>
        /// The first floor is the familiar yellow entry level players expect to arrive in.
        /// </summary>
        [Test]
        public void FirstFloor_IsTheYellowRooms()
        {
            Assert.AreEqual("THE YELLOW ROOMS", FloorThemes.ForFloor(1).Name);
        }

        /// <summary>
        /// Consecutive floors within one cycle are visually distinct, so descending always looks
        /// like arriving somewhere new.
        /// </summary>
        [Test]
        public void ConsecutiveFloors_AreDistinct()
        {
            var names = new HashSet<string>();
            for (int floor = 1; floor <= FloorThemes.Count; floor++)
            {
                FloorTheme theme = FloorThemes.ForFloor(floor);
                Assert.IsTrue(names.Add(theme.Name), $"duplicate floor name at floor {floor}");
                Assert.IsNotEmpty(theme.Name, "every floor is named");
            }

            Assert.AreEqual(FloorThemes.Count, names.Count, "each authored floor is unique");
        }

        /// <summary>
        /// Past the authored set the palettes wrap around rather than running out, so the dungeon
        /// keeps going as deep as the player is willing to descend.
        /// </summary>
        [Test]
        public void FloorsWrapAround_PastTheAuthoredSet()
        {
            int count = FloorThemes.Count;
            Assert.AreSame(FloorThemes.ForFloor(1), FloorThemes.ForFloor(count + 1),
                "floor after the last authored one repeats the first");
            Assert.AreSame(FloorThemes.ForFloor(2), FloorThemes.ForFloor(count + 2),
                "wrapping keeps its order");
        }

        /// <summary>
        /// Floor numbers below one are treated as the first floor rather than throwing.
        /// </summary>
        [Test]
        public void NonPositiveFloors_ClampToTheFirstFloor()
        {
            Assert.AreSame(FloorThemes.ForFloor(1), FloorThemes.ForFloor(0), "floor zero clamps");
            Assert.AreSame(FloorThemes.ForFloor(1), FloorThemes.ForFloor(-5), "negative clamps");
        }
    }
}
