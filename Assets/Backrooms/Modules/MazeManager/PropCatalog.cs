using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// The furniture available to dress each kind of floor. Held as a loadable asset rather than
    /// scene references so the maze module can find it at runtime without the scene wiring anything
    /// up, and so a project without the models still builds — the decorator simply falls back to
    /// generated primitives when no catalogue is present.
    /// </summary>
    public sealed class PropCatalog : ScriptableObject
    {
        /// <summary>Resources path the catalogue is loaded from.</summary>
        public const string ResourcePath = "BackroomsPropCatalog";

        /// <summary>
        /// One floor style's worth of furniture.
        /// </summary>
        [System.Serializable]
        public sealed class StyleEntry
        {
            /// <summary>Which floor style these models dress.</summary>
            public PropStyle Style;

            /// <summary>Models that stand on the floor in the open.</summary>
            public GameObject[] Freestanding;

            /// <summary>Models that look right pushed back against a wall.</summary>
            public GameObject[] AgainstWall;
        }

        /// <summary>Furniture for each floor style.</summary>
        [SerializeField] private StyleEntry[] entries = new StyleEntry[0];

        /// <summary>
        /// Loads the catalogue, or returns <c>null</c> when the project has no furniture models.
        /// </summary>
        /// <returns>The catalogue asset, or <c>null</c>.</returns>
        public static PropCatalog LoadOrNull() => Resources.Load<PropCatalog>(ResourcePath);

        /// <summary>
        /// The freestanding models for a style, or an empty array if the style has none.
        /// </summary>
        /// <param name="style">Floor style.</param>
        /// <returns>Models to place in the open.</returns>
        public GameObject[] FreestandingFor(PropStyle style) => Find(style)?.Freestanding ?? NoModels;

        /// <summary>
        /// The against-wall models for a style, or an empty array if the style has none.
        /// </summary>
        /// <param name="style">Floor style.</param>
        /// <returns>Models to place against walls.</returns>
        public GameObject[] AgainstWallFor(PropStyle style) => Find(style)?.AgainstWall ?? NoModels;

        /// <summary>
        /// Replaces the catalogue's contents. Used by the editor tool that builds it.
        /// </summary>
        /// <param name="newEntries">Entries to store.</param>
        public void SetEntries(IEnumerable<StyleEntry> newEntries)
        {
            var list = new List<StyleEntry>(newEntries);
            entries = list.ToArray();
        }

        /// <summary>Shared empty array so lookups never allocate or return null.</summary>
        private static readonly GameObject[] NoModels = new GameObject[0];

        /// <summary>
        /// Finds the entry for a style.
        /// </summary>
        /// <param name="style">Floor style.</param>
        /// <returns>The entry, or <c>null</c>.</returns>
        private StyleEntry Find(PropStyle style)
        {
            foreach (StyleEntry entry in entries)
            {
                if (entry != null && entry.Style == style) return entry;
            }

            return null;
        }
    }
}
