using System.Threading;
using Backrooms.MazeManager;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Backrooms.EntityManager.Tests
{
    /// <summary>
    /// PlayMode tests that a hunting Dweller can actually reach the player wherever they are standing
    /// inside a cell, not only when they happen to be near its centre.
    /// </summary>
    /// <remarks>
    /// These exist because of a defect no grid-level test could see. Pathing is a grid and the
    /// Dweller walked cell centre to cell centre, but the player is a continuous position: standing
    /// against a wall puts them 2m off centre on a 4m cell, so the Dweller passed by at arm's length
    /// and never landed a catch. Every unit test of pathing, state and distance still passed. These
    /// drive the real component through real physics steps and check the outcome that matters.
    /// </remarks>
    public class DwellerCatchTests
    {
        /// <summary>Floor size the game ships.</summary>
        private const int FloorCells = 24;

        /// <summary>How many physics steps to allow before calling it a miss.</summary>
        private const int Steps = 900;

        /// <summary>
        /// Cleans the scene before each test so every test starts from a known, empty state.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// Stands a target at an offset inside a cell and lets a hunting Dweller come for it.
        /// </summary>
        /// <param name="offset">Where in the cell the target stands, relative to its centre.</param>
        /// <param name="label">Name for the log line.</param>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        /// <returns><c>true</c> if the Dweller caught the target.</returns>
        private static async UniTask<bool> ChaseFrom(Vector3 offset, string label,
            CancellationToken ct)
        {
            var mazeGo = new GameObject("MazeManager");
            MazeLayout layout = mazeGo.AddComponent<MazeFacade>()
                .GetTestFacade()
                .Generate(new MazeSettings(FloorCells, FloorCells, seed: 4));

            // A cell with room around it, and a neighbour for the Dweller to start in.
            Vector2Int cell = OpenCell(layout);
            Vector2Int from = cell;
            foreach (Direction dir in Directions.All)
            {
                if (!layout.CanMove(cell.x, cell.y, dir)) continue;
                from = cell + Directions.Delta(dir);
                break;
            }

            var prey = new GameObject("Prey");
            prey.transform.position = layout.CellCenterToWorld(cell) + offset;

            var dwellerGo = new GameObject("Dweller");
            DwellerFacade dweller = dwellerGo.AddComponent<DwellerFacade>();
            dweller.Place(layout, from, prey.transform, 2.2f, seed: 1);

            for (int step = 0; step < Steps && !dweller.HasCaught; step++)
            {
                await UniTask.WaitForFixedUpdate(ct);
            }

            float away = Vector3.Distance(
                new Vector3(dweller.transform.position.x, 0f, dweller.transform.position.z),
                new Vector3(prey.transform.position.x, 0f, prey.transform.position.z));
            MooseRunnerFacade.Log($"{label}: caught={dweller.HasCaught}, ended {away:F2}m away");

            return dweller.HasCaught;
        }

        /// <summary>
        /// A Dweller must catch a target standing dead centre in its cell.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask ADweller_CatchesATargetAtTheCellCentre(CancellationToken ct)
        {
            Assert.IsTrue(await ChaseFrom(Vector3.zero, "centre", ct),
                "a hunting Dweller should reach a target standing in the middle of a cell");
        }

        /// <summary>
        /// And a target pressed against the wall, which is where a frightened player actually stands.
        /// This is the case that was broken.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask ADweller_CatchesATargetHuggingTheWall(CancellationToken ct)
        {
            // 1.7m off centre on a 4m cell: as close to the wall as a 0.3m-radius body can get.
            Assert.IsTrue(await ChaseFrom(new Vector3(1.7f, 0f, 0f), "against +X wall", ct),
                "a Dweller must reach a player pinned against a wall, not pass them at arm's length");
        }

        /// <summary>
        /// And a target wedged into a corner, which is two walls' worth of the same problem.
        /// </summary>
        /// <param name="ct">Cancellation token supplied by the runner.</param>
        [Test]
        public async UniTask ADweller_CatchesATargetInACorner(CancellationToken ct)
        {
            Assert.IsTrue(await ChaseFrom(new Vector3(1.7f, 0f, -1.7f), "in a corner", ct),
                "a corner must not be a safe square");
        }

        /// <summary>
        /// Finds a cell with open passages on all four sides.
        /// </summary>
        /// <param name="layout">The floor to search.</param>
        /// <returns>An open cell, or the grid centre as a fallback.</returns>
        private static Vector2Int OpenCell(MazeLayout layout)
        {
            for (int y = 2; y < layout.Height - 2; y++)
            {
                for (int x = 2; x < layout.Width - 2; x++)
                {
                    if (layout.IsStairs(new Vector2Int(x, y))) continue;
                    bool open = true;
                    foreach (Direction dir in Directions.All)
                    {
                        if (!layout.CanMove(x, y, dir)) open = false;
                    }

                    if (open) return new Vector2Int(x, y);
                }
            }

            return new Vector2Int(layout.Width / 2, layout.Height / 2);
        }
    }
}
