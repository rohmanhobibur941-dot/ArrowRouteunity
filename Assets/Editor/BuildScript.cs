using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowRoute.EditorTools
{
    /// <summary>
    /// Builds the Android APK. Used by GitHub Actions (game-ci passes -customBuildPath, -androidVersionCode,
    /// -buildVersion and, for a release build, the -androidKeystore... arguments), and from the menu in the editor.
    /// Everything (package name, portrait, scenes) is set here, so nothing needs to be clicked in Unity.
    /// </summary>
    public static class BuildScript
    {
        const string PackageName = "com.jubahir.arrowroute";
        const string ProductName = "Arrow Route";
        const string CompanyName = "Jubahir";

        [MenuItem("Arrow Route/Build Android APK")]
        public static void BuildFromMenu() { Build("build/Android/ArrowRoute.apk", 1, "1.0", null, null, null, null, true); }

        /// <summary>Entry point for GitHub Actions: -executeMethod ArrowRoute.EditorTools.BuildScript.BuildAndroid</summary>
        public static void BuildAndroid()
        {
            string[] a = Environment.GetCommandLineArgs();
            string path = Arg(a, "-customBuildPath", "build/Android/ArrowRoute.apk");
            int code;
            if (!int.TryParse(Arg(a, "-androidVersionCode", "1"), out code)) code = 1;
            string version = Arg(a, "-buildVersion", "1.0");
            Build(path, code, version,
                  Arg(a, "-androidKeystoreName", ""), Arg(a, "-androidKeystorePass", ""),
                  Arg(a, "-androidKeyaliasName", ""), Arg(a, "-androidKeyaliasPass", ""), false);
        }

        static string Arg(string[] args, string name, string def)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name && !args[i + 1].StartsWith("-")) return args[i + 1];
            }
            return def;
        }

        static void Build(string path, int versionCode, string versionName,
                          string ksName, string ksPass, string aliasName, string aliasPass, bool fromMenu)
        {
            try
            {
                if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "ArrowRoute.apk");
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                ApplySettings(versionCode, versionName, ksName, ksPass, aliasName, aliasPass);
                AdSetup.ApplyAdMobAppId();
                string[] scenes = SceneBuilder.CreateAll();

                BuildPlayerOptions opts = new BuildPlayerOptions();
                opts.scenes = scenes;
                opts.locationPathName = path;
                opts.target = BuildTarget.Android;
                opts.targetGroup = BuildTargetGroup.Android;
                opts.options = BuildOptions.None;

                BuildReport report = BuildPipeline.BuildPlayer(opts);
                bool ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log("Arrow Route build: " + report.summary.result + " -> " + path + " (" + report.summary.totalSize + " bytes)");
                if (!fromMenu) EditorApplication.Exit(ok ? 0 : 2);
            }
            catch (Exception e)
            {
                Debug.LogError("Arrow Route build failed: " + e);
                if (!fromMenu) EditorApplication.Exit(3);
            }
        }

        static void ApplySettings(int versionCode, string versionName, string ksName, string ksPass, string aliasName, string aliasPass)
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.bundleVersion = versionName;
            PlayerSettings.Android.bundleVersionCode = versionCode;

            // portrait only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Android player
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // add | AndroidArchitecture.ARMv7 for very old phones
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.buildAppBundle = false;

            // the old Input Manager (Input.touches) is used
            try
            {
                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (all != null && all.Length > 0)
                {
                    SerializedObject so = new SerializedObject(all[0]);
                    SerializedProperty p = so.FindProperty("activeInputHandler");
                    if (p != null && p.intValue != 0) { p.intValue = 0; so.ApplyModifiedProperties(); }
                }
            }
            catch (Exception e) { Debug.LogWarning("Input handler setting skipped: " + e.Message); }

            // signing: a debug keystore is used unless a release keystore is given
            if (!string.IsNullOrEmpty(ksName))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(ksName);
                PlayerSettings.Android.keystorePass = ksPass;
                PlayerSettings.Android.keyaliasName = aliasName;
                PlayerSettings.Android.keyaliasPass = aliasPass;
                Debug.Log("Arrow Route: signing with the release keystore");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("Arrow Route: signing with the debug keystore");
            }
        }
    }
}
