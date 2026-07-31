using System.Collections.Generic;
using System.IO;
using Backrooms.MazeManager;
using UnityEditor;
using UnityEngine;

namespace Backrooms.Editor
{
    /// <summary>
    /// Builds the furniture catalogue by picking models from the Kenney pack and assigning them to
    /// the floor styles they suit. Keeping the mapping in code rather than hand-wiring an asset means
    /// the catalogue can be regenerated after any change to the pack, and the reasoning behind each
    /// choice stays readable.
    /// </summary>
    public static class PropCatalogBuilder
    {
        /// <summary>Where the imported models live.</summary>
        private const string ModelFolder = "Assets/ThirdParty/Kenney/FurnitureKit/Models";

        /// <summary>Resources folder the catalogue is written to so it loads at runtime.</summary>
        private const string CatalogFolder = "Assets/Backrooms/Resources";

        /// <summary>
        /// Model choices per floor style. Split by whether a piece reads better in the open or
        /// pushed back against a wall.
        /// </summary>
        private static readonly (PropStyle style, string[] open, string[] wall)[] Mapping =
        {
            // Offices: desks and storage, the mundane furniture of the entry floor.
            (PropStyle.Office,
                new[] { "desk", "deskCorner", "chairDesk", "cardboardBoxClosed", "cardboardBoxOpen",
                        "tableCross", "chair" },
                new[] { "bookcaseClosed", "bookcaseClosedDoors", "bookcaseClosedWide", "bookcaseOpen",
                        "coatRackStanding", "trashcan", "cabinetTelevision" }),

            // Malls: seating, planting and the odd abandoned counter.
            (PropStyle.Mall,
                new[] { "bench", "benchCushion", "benchCushionLow", "pottedPlant", "plantSmall1",
                        "tableCoffee", "loungeSofa", "stoolBar" },
                new[] { "kitchenBar", "kitchenBarEnd", "bookcaseOpenLow", "trashcan", "coatRack" }),

            // Laundromats: the pack has actual washers and dryers, which is the whole look.
            (PropStyle.Laundromat,
                new[] { "washerDryerStacked", "bench", "benchCushionLow", "table", "chair" },
                new[] { "washer", "dryer", "kitchenCabinet", "kitchenSink", "trashcan" }),

            // Carnivals: stools, round tables and speakers stand in for fairground clutter.
            (PropStyle.Carnival,
                new[] { "stoolBar", "stoolBarSquare", "tableRound", "tableCloth", "speaker",
                        "lampRoundFloor" },
                new[] { "kitchenBar", "speakerSmall", "radio", "coatRackStanding" }),

            // Asylums: beds, basins and hard chairs.
            (PropStyle.Asylum,
                new[] { "bedSingle", "bedBunk", "chair", "chairCushion", "sideTable" },
                new[] { "bathroomSink", "toilet", "cabinetBed", "trashcan", "bathroomCabinet" })
        };

        /// <summary>
        /// Regenerates the catalogue asset from the models currently in the project.
        /// </summary>
        [MenuItem("Backrooms/Build Prop Catalog")]
        public static void BuildCatalog()
        {
            Directory.CreateDirectory(CatalogFolder);
            string path = $"{CatalogFolder}/{PropCatalog.ResourcePath}.asset";

            PropCatalog catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(path);
            bool isNew = catalog == null;
            if (isNew) catalog = ScriptableObject.CreateInstance<PropCatalog>();

            var entries = new List<PropCatalog.StyleEntry>();
            int missing = 0;

            foreach ((PropStyle style, string[] open, string[] wall) in Mapping)
            {
                entries.Add(new PropCatalog.StyleEntry
                {
                    Style = style,
                    Freestanding = Load(open, ref missing),
                    AgainstWall = Load(wall, ref missing)
                });
            }

            catalog.SetEntries(entries);

            if (isNew) AssetDatabase.CreateAsset(catalog, path);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Report real-world size of a few models: a scale mismatch is invisible in code and
            // only shows up as furniture the size of a building.
            foreach (string probe in new[] { "chair", "desk", "washer", "bedSingle" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{probe}.fbx");
                if (model == null) continue;
                var mf = model.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Vector3 size = mf.sharedMesh.bounds.size;
                Debug.Log($"[Backrooms] size {probe}: {size.x:F2} x {size.y:F2} x {size.z:F2} m");
            }

            int total = 0;
            foreach (PropCatalog.StyleEntry e in entries) total += e.Freestanding.Length + e.AgainstWall.Length;
            Debug.Log($"[Backrooms] Prop catalog built: {total} models across {entries.Count} styles"
                      + (missing > 0 ? $" ({missing} names not found)" : ""));
        }

        /// <summary>
        /// Loads model assets by name, skipping and counting any that are missing.
        /// </summary>
        /// <param name="names">Model file names without extension.</param>
        /// <param name="missing">Running count of names that could not be found.</param>
        /// <returns>The models that were found.</returns>
        private static GameObject[] Load(string[] names, ref int missing)
        {
            var found = new List<GameObject>(names.Length);
            foreach (string name in names)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{name}.fbx");
                if (model == null)
                {
                    Debug.LogWarning($"[Backrooms] Model not found: {name}");
                    missing++;
                    continue;
                }

                found.Add(model);
            }

            return found.ToArray();
        }
    }
}
