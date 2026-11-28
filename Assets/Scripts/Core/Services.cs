using UnityEngine;

namespace ArrowRoute
{
    /// <summary>Things that live for the whole app run: sound, ads and the game session.</summary>
    public static class Services
    {
        public static Fx Fx;
        public static AdService Ads;
        public static GameSession Session;
        static bool ready;

        public static void Init()
        {
            if (ready && Session != null) return;
            ready = true;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameConfig.Load();

            GameObject go = new GameObject("Services");
            Object.DontDestroyOnLoad(go);
            Fx = go.AddComponent<Fx>();
            Ads = go.AddComponent<AdService>();
            Ads.StartAds();

            // play area is the screen minus header and bottom bar (about 22% of the height)
            float aspect = Mathf.Clamp(Screen.width / (Screen.height * 0.78f), 0.45f, 1.0f);
            Session = new GameSession(Fx, aspect);
            Session.Begin();
        }
    }
}
