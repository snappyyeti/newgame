using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SplashSort.EditorTools
{
    /// <summary>
    /// Prepares a freshly downloaded copy of the project so it can be played without manual setup:
    /// portrait player settings, a phone-shaped Windows window, Direct3D 11 on Windows
    /// (Direct3D 12 draws the editor UI pink on some GPUs), the classic Input Manager (the game reads input
    /// through UnityEngine.Input, no package needed) and the Main scene opened on start.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string ProductName = "Splash Sort";
        const string MainScene = "Assets/Scenes/Main.unity";
        const string OpenedKey = "SplashSort.MainSceneOpened";
        const string AskedRestartKey = "SplashSort.AskedRestart";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (PlayerSettings.productName != ProductName) ApplyPlayerSettings();

            if (!SessionState.GetBool(OpenedKey, false))
            {
                SessionState.SetBool(OpenedKey, true);
                var active = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty && File.Exists(MainScene))
                    EditorSceneManager.OpenScene(MainScene);
            }

            bool inputChanged = EnsureClassicInput();
            bool onD3D12 = Application.platform == RuntimePlatform.WindowsEditor && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
            if ((inputChanged || onD3D12) && !SessionState.GetBool(AskedRestartKey, false))
            {
                SessionState.SetBool(AskedRestartKey, true);
                var reasons = "";
                if (inputChanged) reasons += "• input-ul a fost trecut pe Input Manager (clasic), ca să meargă mouse-ul și atingerea;\n";
                if (onD3D12) reasons += "• grafica a fost trecută pe Direct3D 11, care desenează corect editorul pe mai multe plăci video;\n";
                bool restart = EditorUtility.DisplayDialog(
                    ProductName,
                    "Proiectul a fost pregătit:\n" + reasons + "\nRepornești Unity acum ca să se aplice?",
                    "Repornește",
                    "Mai târziu");
                if (restart) EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
        }

        /// <summary>
        /// Active Input Handling: 0 = Input Manager (old), 1 = Input System package (new), 2 = both.
        /// The game uses the old one, so "new only" is switched to "old". Takes effect after a restart.
        /// </summary>
        static bool EnsureClassicInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null) return false;
            var settings = new SerializedObject(assets[0]);
            var handler = settings.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue != 1) return false;
            handler.intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Splash Sort] Active Input Handling trecut pe Input Manager (Old). Repornește Unity ca să se aplice.");
            return true;
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = "snappyyeti";

            // Phones: portrait only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Windows test builds: a resizable, phone-shaped window.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1170;
            PlayerSettings.resizableWindow = true;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });

            AssetDatabase.SaveAssets();
            Debug.Log("[Splash Sort] Player Settings pregătite: portret, fereastră 540×1170 pe Windows, Direct3D 11.");
        }
    }
}
