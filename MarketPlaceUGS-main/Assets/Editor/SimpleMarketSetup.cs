using System;
using System.IO;
using SimpleMarket;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimpleMarket.Editor
{
    public static class SimpleMarketSetup
    {
        private const string ScenePath = "Assets/Scene/SimpleMarket.unity";
        [MenuItem("Simple Market/1. Create or Open Test Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var app = new GameObject("SimpleMarketApp").AddComponent<SimpleMarketApp>();
                app.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/SDF/NotoSansKR-Medium SDF.asset");
                app.itemVisuals = AssetDatabase.LoadAssetAtPath<ItemVisualData>("Assets/Data/Market/GlobalItemVisuals.asset");
                app.environmentName = "production";
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = UnityEngine.Object.FindFirstObjectByType<SimpleMarketApp>().gameObject;
            AssetDatabase.SaveAssets();
            Debug.Log("Simple Market scene ready. Link your UGS project, then press Play.");
        }

        [MenuItem("Simple Market/2. Build WebGL")]
        public static void BuildWebGL()
        {
            CreateScene();
            if (!File.Exists(ScenePath)) throw new Exception("Create the Simple Market scene first.");
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 900;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed. See Console.");
            Debug.Log("Build complete: Builds/WebGL. Run node Tools/serve-webgl.js and open http://localhost:8080.");
        }
    }
}
