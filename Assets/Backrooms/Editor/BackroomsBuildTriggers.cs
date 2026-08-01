using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Backrooms.Editor
{
    /// <summary>
    /// Watches for sentinel files at the project root and runs the matching build step when one
    /// appears. This is how an agent drives the editor headlessly: create the file, and the editor
    /// picks it up on its next update.
    /// </summary>
    /// <remarks>
    /// Polling on <see cref="EditorApplication.update"/> rather than only on domain reload matters:
    /// a recompile request with no changed source does not reload the domain, so a reload-only
    /// trigger silently does nothing. Polling makes the trigger work regardless.
    /// </remarks>
    [InitializeOnLoad]
    public static class BackroomsBuildTriggers
    {
        /// <summary>Creating this file rebuilds the gameplay scene.</summary>
        private const string SceneSentinel = ".backrooms-build-scene";

        /// <summary>Creating this file builds the WebGL player into <c>docs/</c>.</summary>
        private const string WebGLSentinel = ".backrooms-build-webgl";

        /// <summary>Creating this file reimports the Kenney pack with URP materials.</summary>
        private const string KenneySentinel = ".backrooms-reimport-kenney";

        /// <summary>Creating this file regenerates the furniture catalogue.</summary>
        private const string CatalogSentinel = ".backrooms-build-catalog";

        /// <summary>Editor updates between sentinel checks, to keep polling cheap.</summary>
        private const int PollInterval = 30;

        private static int _counter;

        /// <summary>
        /// Starts polling when the editor loads or reloads its domain.
        /// </summary>
        static BackroomsBuildTriggers()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        /// <summary>
        /// Checks for sentinel files periodically and runs the corresponding build, deleting the
        /// sentinel first so a failure cannot put the editor into a build loop.
        /// </summary>
        private static void Poll()
        {
            if (++_counter < PollInterval) return;
            _counter = 0;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            // Scene authoring cannot run in Play Mode — EditorSceneManager.NewScene throws. The
            // editor is left in Play Mode by every test run, so this is the normal state after a
            // working loop, and consuming the sentinel here would silently ship a stale scene while
            // the build "succeeded". Leave the sentinel alone and pick it up on the way out.
            if (EditorApplication.isPlaying)
            {
                if (File.Exists(SceneSentinel))
                {
                    Debug.Log("[Backrooms] Scene rebuild deferred until Play Mode exits.");
                }

                return;
            }

            TryRun(SceneSentinel, BackroomsSceneBuilder.BuildScene);
            TryRun(WebGLSentinel, BackroomsWebGLBuilder.BuildWebGL);
            TryRun(KenneySentinel, KenneyModelPostprocessor.ReimportPack);
            TryRun(CatalogSentinel, PropCatalogBuilder.BuildCatalog);
        }

        /// <summary>
        /// Runs an action if its sentinel file exists, consuming the file first.
        /// </summary>
        /// <param name="sentinel">Sentinel file path relative to the project root.</param>
        /// <param name="action">Build step to run.</param>
        private static void TryRun(string sentinel, Action action)
        {
            if (!File.Exists(sentinel)) return;

            File.Delete(sentinel);
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Backrooms] Build step for '{sentinel}' threw: {e}");
            }
        }
    }
}
