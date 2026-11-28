using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArrowRoute.EditorTools
{
    /// <summary>
    /// Creates the three scenes (Splash, Menu, Game) and the board material from code, and puts the scenes into the
    /// build settings. Nothing has to be done by hand in the Unity editor; the build script calls this every time.
    /// </summary>
    public static class SceneBuilder
    {
        public const string SceneDir = "Assets/Scenes";
        public const string MaterialPath = "Assets/Resources/Materials/BoardMat.mat";

        [MenuItem("Arrow Route/Generate scenes and settings")]
        public static void MenuCreate()
        {
            string[] scenes = CreateAll();
            Debug.Log("Arrow Route: created " + string.Join(", ", scenes));
        }

        /// <summary>Returns the scene paths in build order.</summary>
        public static string[] CreateAll()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory("Assets/Resources/Materials");
            CreateMaterial();

            string[] paths = { SceneDir + "/Splash.unity", SceneDir + "/Menu.unity", SceneDir + "/Game.unity" };
            CreateScene(paths[0], go => go.AddComponent<SplashScreen>());
            CreateScene(paths[1], go => go.AddComponent<MenuScreen>());
            CreateScene(paths[2], go => go.AddComponent<GameScreen>());

            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return paths;
        }

        static void CreateMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            Shader sh = Shader.Find("Sprites/Default");
            Material m = new Material(sh);
            m.name = "BoardMat";
            AssetDatabase.CreateAsset(m, MaterialPath);
        }

        static void CreateScene(string path, Action<GameObject> setup)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = new GameObject("Screen");
            setup(go);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
