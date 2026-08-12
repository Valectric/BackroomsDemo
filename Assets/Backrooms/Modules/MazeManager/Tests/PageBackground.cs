using System.IO;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.MazeManager.Tests
{
    /// <summary>
    /// Exports the game's own wallpaper texture for use as the store page background.
    /// </summary>
    /// <remarks>
    /// The texture asset, not a screenshot of a wall. A screenshot carries the lighting of the room
    /// it was taken in, and tiling that repeats its bright and dark patches into obvious banding
    /// across the page. The texture has no lighting in it, which is exactly why the game can tile it
    /// across every wall on the floor without a seam.
    /// </remarks>
    public class PageBackground
    {
        /// <summary>Cleans the scene before each test.</summary>
        [SetUp]
        public void SetUp() => DoNotDestroyOnTeardown.CleanSceneImmediate();

        /// <summary>Writes the Yellow Rooms wallpaper to Screenshots/press.</summary>
        [Test]
        public void ExportWallpaper()
        {
            var go = new GameObject("MazeManager");
            MazeManagerTestFacade seam = go.AddComponent<MazeFacade>().GetTestFacade();

            FloorTheme theme = FloorThemes.ForFloor(1);
            Texture2D wall = seam.WallTexture(theme.Wall, seed: 977);

            Assert.IsNotNull(wall, "the wallpaper should have been generated");
            Assert.Greater(wall.width, 8, "a usable texture, not a stub");

            string dir = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots", "press"));
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "page-tile.png");
            File.WriteAllBytes(path, wall.EncodeToPNG());

            MooseRunnerFacade.Log($"wallpaper {wall.width}x{wall.height} -> {path}");
        }
    }
}
