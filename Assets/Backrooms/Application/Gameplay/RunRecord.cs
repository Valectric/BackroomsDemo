using UnityEngine;

namespace Backrooms.Gameplay
{
    /// <summary>
    /// Remembers the best run this device has managed, across sessions.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="GameplayController"/>, which had grown past the size a file should
    /// carry. It exists so a relic count has something to be measured against: a number the player
    /// cannot compare with anything is a noise and a tally, not a reason to go again.
    /// </remarks>
    internal sealed class RunRecord
    {
        /// <summary>Where the deepest floor is remembered between sessions.</summary>
        private const string FloorsKey = "Backrooms.BestFloors";

        /// <summary>Where the best relic haul is remembered between sessions.</summary>
        private const string RelicsKey = "Backrooms.BestRelics";

        /// <summary>Deepest floor reached in any run on this device.</summary>
        public int BestFloors { get; private set; }

        /// <summary>Most relics carried in any run on this device.</summary>
        public int BestRelics { get; private set; }

        /// <summary>
        /// Loads whatever this device already remembers.
        /// </summary>
        public RunRecord()
        {
            BestFloors = PlayerPrefs.GetInt(FloorsKey, 0);
            BestRelics = PlayerPrefs.GetInt(RelicsKey, 0);
        }

        /// <summary>
        /// Records a finished run, keeping it only if it beat what was stored.
        /// </summary>
        /// <param name="floors">How deep the run got.</param>
        /// <param name="relics">How many relics it ended holding.</param>
        public void Submit(int floors, int relics)
        {
            BestFloors = Mathf.Max(BestFloors, floors);
            BestRelics = Mathf.Max(BestRelics, relics);

            PlayerPrefs.SetInt(FloorsKey, BestFloors);
            PlayerPrefs.SetInt(RelicsKey, BestRelics);
            PlayerPrefs.Save();
        }
    }
}
