using UnityEngine;

namespace ArrowRoute
{
    /// <summary>Saved settings and progress (PlayerPrefs).</summary>
    public static class Prefs
    {
        public static int Level
        {
            get { return Mathf.Max(1, PlayerPrefs.GetInt("level", 1)); }
            set { PlayerPrefs.SetInt("level", value); PlayerPrefs.Save(); }
        }

        /// <summary>Hints left. Every player starts with 3 free hints.</summary>
        public static int Hints
        {
            get { return PlayerPrefs.GetInt("hints", 3); }
            set { PlayerPrefs.SetInt("hints", value); PlayerPrefs.Save(); }
        }

        public static int Theme
        {
            get { return PlayerPrefs.GetInt("theme", Themes.Classic); }
            set { PlayerPrefs.SetInt("theme", value); PlayerPrefs.Save(); }
        }

        public static bool Sound
        {
            get { return PlayerPrefs.GetInt("sound", 1) == 1; }
            set { PlayerPrefs.SetInt("sound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Vibrate
        {
            get { return PlayerPrefs.GetInt("vibrate", 1) == 1; }
            set { PlayerPrefs.SetInt("vibrate", value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
