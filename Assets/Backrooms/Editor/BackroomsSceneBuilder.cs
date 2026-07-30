using System.IO;
using Backrooms.Gameplay;
using Backrooms.MazeManager;
using Backrooms.PlayerManager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Backrooms.Editor
{
    /// <summary>
    /// Authors the real, shipped gameplay scene from code. Building the scene programmatically keeps
    /// it reproducible and lets an agent regenerate it headlessly — Unity's scene editing APIs are
    /// Edit-Mode only, so this runs from a menu item or from a sentinel file picked up on domain
    /// reload rather than from a Play Mode test.
    /// </summary>
    public static class BackroomsSceneBuilder
    {
        /// <summary>Asset path of the gameplay scene this builder creates.</summary>
        public const string ScenePath = "Assets/Backrooms/Application/Gameplay/Scenes/Backrooms.unity";

        /// <summary>
        /// Sentinel file at the project root. When present on domain reload the scene is rebuilt and
        /// the file is deleted, which is how the build is triggered without clicking the menu.
        /// </summary>
        private const string SentinelFile = ".backrooms-build-scene";

        /// <summary>
        /// Rebuilds the gameplay scene from scratch and saves it over any existing copy.
        /// </summary>
        [MenuItem("Backrooms/Build Gameplay Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateModules();
            ConfigureAtmosphere();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? ".");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterInBuildSettings();
            Debug.Log($"[Backrooms] Built gameplay scene at {ScenePath}");
        }

        /// <summary>
        /// Creates the module facades and the application controller that composes them. The maze is
        /// told not to self-build, because the gameplay controller owns the run's order of operations.
        /// </summary>
        private static void CreateModules()
        {
            var mazeGo = new GameObject("MazeManager");
            MazeFacade maze = mazeGo.AddComponent<MazeFacade>();
            SetPrivateBool(maze, "buildOnStart", false);

            // A 12x12 grid keeps a blind run to roughly one to three minutes, which is the length a
            // shared browser demo needs to be.
            SetPrivateInt(maze, "width", 12);
            SetPrivateInt(maze, "height", 12);

            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(2f, 0.1f, 2f);
            playerGo.AddComponent<PlayerFacade>();

            var controllerGo = new GameObject("GameplayController");
            controllerGo.AddComponent<GameplayController>();
        }

        /// <summary>
        /// Sets the lighting and fog that give Level 0 its stale, enclosed feel. Fog also bounds how
        /// far the camera can see, which keeps the mobile WebGL build cheap to render.
        /// </summary>
        private static void ConfigureAtmosphere()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.55f, 0.40f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.74f, 0.70f, 0.52f);
            RenderSettings.fogDensity = 0.018f;
        }

        /// <summary>
        /// Makes the gameplay scene the single scene included in player builds.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// Sets a private serialized boolean on a component, so the builder can configure inspector
        /// fields that are intentionally not part of a module's public API.
        /// </summary>
        /// <param name="target">Component to modify.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetPrivateBool(Object target, string fieldName, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Backrooms] No serialized field '{fieldName}' on {target.name}");
                return;
            }

            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sets a private serialized integer on a component.
        /// </summary>
        /// <param name="target">Component to modify.</param>
        /// <param name="fieldName">Serialized field name.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetPrivateInt(Object target, string fieldName, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Backrooms] No serialized field '{fieldName}' on {target.name}");
                return;
            }

            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// On every domain reload, rebuilds the scene if the sentinel file exists. This is the
        /// headless trigger: an agent creates the file, forces a recompile, and the scene is authored
        /// without anyone clicking the menu item.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CheckSentinel()
        {
            if (!File.Exists(SentinelFile)) return;

            // Defer past asset loading: scene APIs are unsafe during the reload itself.
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SentinelFile)) return;
                File.Delete(SentinelFile);
                BuildScene();
            };
        }
    }
}
