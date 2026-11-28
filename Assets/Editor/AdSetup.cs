using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArrowRoute.EditorTools
{
    /// <summary>
    /// Writes the AdMob app id into the Google Mobile Ads plugin settings (if the plugin is installed).
    /// Done with reflection so the project still compiles without the plugin.
    /// </summary>
    public static class AdSetup
    {
        [MenuItem("Arrow Route/Apply AdMob app id")]
        public static void ApplyAdMobAppId()
        {
            try
            {
                Type t = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
                    if (t != null) break;
                }
                if (t == null)
                {
                    Debug.Log("Arrow Route: AdMob plugin is not installed, app id step skipped.");
                    return;
                }
                MethodInfo load = t.GetMethod("LoadInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object inst = load.Invoke(null, null);
                PropertyInfo p = t.GetProperty("GoogleMobileAdsAndroidAppId");
                if (p != null) p.SetValue(inst, AdIds.AdMobAppId, null);
                EditorUtility.SetDirty((UnityEngine.Object)inst);
                AssetDatabase.SaveAssets();
                Debug.Log("Arrow Route: AdMob app id set to " + AdIds.AdMobAppId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Arrow Route: could not set the AdMob app id: " + e.Message);
            }
        }
    }
}
