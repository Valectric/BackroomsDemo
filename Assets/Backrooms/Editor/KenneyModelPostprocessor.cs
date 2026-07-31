using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Backrooms.Editor
{
    /// <summary>
    /// Gives Kenney's furniture models URP materials as they import.
    /// </summary>
    /// <remarks>
    /// The pack's FBX files reference flat-colour materials by name (<c>wood</c>, <c>metal</c>, …)
    /// with no textures. Unity would otherwise create built-in-pipeline materials for them, which
    /// render magenta under URP. This assigns a generated URP Lit material per material name,
    /// using the colours from the pack's own MTL definitions, so the models look correct in a build
    /// without anyone hand-authoring 15 materials.
    /// </remarks>
    public sealed class KenneyModelPostprocessor : AssetPostprocessor
    {
        /// <summary>Folder the Kenney pack lives in; only assets under it are touched.</summary>
        private const string PackRoot = "Assets/ThirdParty/Kenney/FurnitureKit";

        /// <summary>Folder the generated URP materials are written to.</summary>
        private const string MaterialFolder = PackRoot + "/Materials";

        /// <summary>
        /// The pack's palette, taken from the MTL files shipped with the models.
        /// </summary>
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            { "_defaultMat", new Color(1.000f, 1.000f, 1.000f) },
            { "carpet", new Color(0.943f, 0.367f, 0.343f) },
            { "carpetBlue", new Color(0.356f, 0.517f, 0.868f) },
            { "carpetDarker", new Color(0.608f, 0.298f, 0.285f) },
            { "carpetWhite", new Color(0.973f, 1.000f, 1.000f) },
            { "fur", new Color(0.647f, 0.459f, 0.298f) },
            { "glass", new Color(0.698f, 0.827f, 0.769f) },
            { "lamp", new Color(1.000f, 0.914f, 0.588f) },
            { "metal", new Color(0.741f, 0.823f, 0.840f) },
            { "metalDark", new Color(0.306f, 0.388f, 0.388f) },
            { "metalLight", new Color(0.937f, 0.980f, 0.957f) },
            { "metalMedium", new Color(0.369f, 0.467f, 0.467f) },
            { "plant", new Color(0.182f, 0.821f, 0.576f) },
            { "wood", new Color(0.896f, 0.602f, 0.393f) },
            { "woodDark", new Color(0.678f, 0.456f, 0.299f) }
        };

        /// <summary>
        /// Configures import settings for the pack's models before Unity reads them.
        /// </summary>
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(PackRoot)) return;

            var importer = (ModelImporter)assetImporter;

            // The pack is authored at 5 units per metre, so imported at file scale a chair stands
            // 4.7m tall. Measured against real furniture dimensions; verify with the size probe in
            // PropCatalogBuilder if the pack is ever updated.
            importer.useFileScale = false;
            importer.globalScale = 0.2f;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
        }

        /// <summary>
        /// Supplies a URP material for each material slot the model references.
        /// </summary>
        /// <param name="material">The material Unity would otherwise create.</param>
        /// <param name="renderer">The renderer being assigned.</param>
        /// <returns>The URP material to use, or <c>null</c> to accept Unity's default.</returns>
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!assetPath.StartsWith(PackRoot)) return null;
            return GetOrCreateMaterial(material.name);
        }

        /// <summary>
        /// Finds or creates the URP material for a Kenney material name.
        /// </summary>
        /// <param name="materialName">Name from the model's material slot.</param>
        /// <returns>A URP Lit material asset.</returns>
        private static Material GetOrCreateMaterial(string materialName)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{materialName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kenney] URP Lit shader not found; models would render magenta.");
                return null;
            }

            Color colour = Palette.TryGetValue(materialName, out Color known)
                ? known
                : new Color(0.7f, 0.7f, 0.7f);

            var mat = new Material(shader) { name = materialName };
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", materialName.StartsWith("metal") ? 0.45f : 0.12f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Reimports the whole pack, which regenerates every material. Useful after changing the
        /// palette or when the models were imported before this postprocessor existed.
        /// </summary>
        [MenuItem("Backrooms/Reimport Kenney Furniture Kit")]
        public static void ReimportPack()
        {
            AssetDatabase.ImportAsset(PackRoot, ImportAssetOptions.ImportRecursive
                                                | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log("[Kenney] Reimported the furniture kit with URP materials.");
        }
    }
}
