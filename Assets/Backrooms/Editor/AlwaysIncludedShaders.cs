using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.Editor
{
    /// <summary>
    /// Guarantees that the shaders the game looks up at runtime are actually shipped in player
    /// builds.
    /// </summary>
    /// <remarks>
    /// The maze geometry is procedural, so its materials are created at runtime with
    /// <see cref="Shader.Find"/>. Unity only includes a shader in a build when some asset references
    /// it; a shader reached solely through <c>Shader.Find</c> is stripped, and
    /// <see cref="Shader.Find"/> then returns <c>null</c> in the player. Unity substitutes the
    /// magenta error shader, so the level renders bright pink — and nothing fails in the editor,
    /// where every shader is available. Registering them here in Graphics Settings' always-included
    /// list is the supported fix.
    /// </remarks>
    public static class AlwaysIncludedShaders
    {
        /// <summary>Shaders the runtime geometry builder looks up by name.</summary>
        private static readonly string[] Required =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Simple Lit"
        };

        /// <summary>
        /// Adds any missing required shader to Graphics Settings' always-included list.
        /// </summary>
        [MenuItem("Backrooms/Ensure Always-Included Shaders")]
        public static void Ensure()
        {
            var graphics = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty list = graphics.FindProperty("m_AlwaysIncludedShaders");

            var present = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var existing = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (existing != null) present.Add(existing.name);
            }

            var added = new List<string>();
            foreach (string name in Required.Where(n => !present.Contains(n)))
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"[Backrooms] Shader not found in project: {name}");
                    continue;
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added.Add(name);
            }

            if (added.Count == 0)
            {
                Debug.Log("[Backrooms] All required shaders already always-included.");
                return;
            }

            graphics.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Backrooms] Added always-included shaders: {string.Join(", ", added)}");
        }
    }
}
