using System;
using UnityEngine;

namespace ArrowRoute
{
    [Serializable] public class MixRow { public int[] counts; }

    [Serializable]
    public class TierCfg
    {
        public int longMin;
        public int longMax;
        public int keepFree;
        public float straight;
    }

    /// <summary>
    /// Tunable numbers of the level generator. Loaded from Resources/GameConfig.json,
    /// so levels can be tuned without touching the code. Missing values fall back to defaults.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public int arrowBase = 60;
        public int arrowStep = 12;
        public int arrowMin = 50;
        public int arrowMax = 360;
        public float[] difficultyFactor = { 0.8f, 1.0f, 1.15f, 1.3f };
        /// <summary>For every block of 10 levels: how many easy, medium, hard, extreme levels.</summary>
        public MixRow[] mix;
        public TierCfg[] tiers;

        public static GameConfig Current = CreateDefault();

        static GameConfig CreateDefault()
        {
            GameConfig c = new GameConfig();
            c.FillDefaults();
            return c;
        }

        public static void Load()
        {
            GameConfig c = null;
            TextAsset ta = Resources.Load<TextAsset>("GameConfig");
            if (ta != null)
            {
                try { c = JsonUtility.FromJson<GameConfig>(ta.text); }
                catch (Exception e) { Debug.LogWarning("GameConfig.json: " + e.Message); }
            }
            if (c == null) c = new GameConfig();
            c.FillDefaults();
            Current = c;
        }

        void FillDefaults()
        {
            if (difficultyFactor == null || difficultyFactor.Length < 4) difficultyFactor = new[] { 0.8f, 1.0f, 1.15f, 1.3f };
            if (mix == null || mix.Length == 0)
            {
                mix = new[]
                {
                    Row(5, 3, 1, 1),   // levels 1-10
                    Row(3, 4, 2, 1),   // 11-20
                    Row(0, 3, 7, 0),   // 21-30: 7 hard + 3 medium
                    Row(0, 2, 5, 3),   // 31-40
                    Row(0, 1, 4, 5),   // 41-50
                    Row(0, 0, 4, 6)    // 51 and on
                };
            }
            if (tiers == null || tiers.Length < 4)
            {
                tiers = new[]
                {
                    Tier(4, 7, 8, 0.60f),
                    Tier(4, 9, 6, 0.50f),
                    Tier(5, 11, 5, 0.45f),
                    Tier(6, 13, 4, 0.40f)
                };
            }
        }

        static MixRow Row(int a, int b, int c, int d) { MixRow r = new MixRow(); r.counts = new[] { a, b, c, d }; return r; }

        static TierCfg Tier(int lmin, int lmax, int keep, float straight)
        {
            TierCfg t = new TierCfg();
            t.longMin = lmin; t.longMax = lmax; t.keepFree = keep; t.straight = straight;
            return t;
        }
    }
}
