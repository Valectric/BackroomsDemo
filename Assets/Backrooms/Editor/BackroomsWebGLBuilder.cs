using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Backrooms.Editor
{
    /// <summary>
    /// Builds the mobile-friendly WebGL player into the repository's <c>docs/</c> folder, which is
    /// what GitHub Pages serves. Scripting the build keeps the player settings that Pages depends on
    /// from drifting, and lets an agent produce a deployable build without clicking through the
    /// editor UI.
    /// </summary>
    public static class BackroomsWebGLBuilder
    {
        /// <summary>Output folder, relative to the project root. GitHub Pages serves this.</summary>
        private const string OutputFolder = "docs";

        /// <summary>
        /// Applies the required player settings and builds the WebGL player into <c>docs/</c>.
        /// </summary>
        [MenuItem("Backrooms/Build WebGL to docs")]
        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError(
                    "[Backrooms] WebGL Build Support is not installed for this Unity version. " +
                    "Install it via Unity Hub > Installs > Add modules > WebGL Build Support.");
                return;
            }

            ApplyWebGLPlayerSettings();

            string output = Path.GetFullPath(OutputFolder);
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { BackroomsSceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[Backrooms] Building WebGL to {output} ...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                double mb = summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[Backrooms] WebGL build succeeded: {mb:F1} MB in {summary.totalTime}");
            }
            else
            {
                Debug.LogError($"[Backrooms] WebGL build {summary.result} " +
                               $"with {summary.totalErrors} error(s)");
            }
        }

        /// <summary>
        /// Configures the player for a small build that GitHub Pages can actually serve.
        /// </summary>
        /// <remarks>
        /// The critical setting is decompression fallback. GitHub Pages cannot send a
        /// <c>Content-Encoding</c> header, so a normally-compressed build fails to load; the fallback
        /// makes the loader decompress in-browser instead.
        /// </remarks>
        private static void ApplyWebGLPlayerSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";

            PlayerSettings.stripEngineCode = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetManagedStrippingLevel(
                UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            PlayerSettings.companyName = "Valectric";
            PlayerSettings.productName = "Backrooms Demo";
        }

    }
}
