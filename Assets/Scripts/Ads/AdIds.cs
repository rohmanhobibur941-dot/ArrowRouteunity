namespace ArrowRoute
{
    /// <summary>
    /// Ad settings: the same AdMob and Unity Ads ids as the Android (Kotlin) version of the game.
    /// Both networks serve real ads. Set UnityTestMode = true while testing on your own phone,
    /// and never click your own live ads.
    /// </summary>
    public static class AdIds
    {
        // ---- AdMob
        public const string AdMobAppId = "ca-app-pub-5693977305113658~2949594287";
        public const string AdMobInterstitial = "ca-app-pub-5693977305113658/8949463213";
        public const string AdMobRewarded = "ca-app-pub-5693977305113658/5380260008";

        // ---- Unity Ads
        public const string UnityGameId = "800005010";
        public const string UnityRewarded = "Rewarded_Android";
        public const string UnityInterstitial = "Interstitial_Android";
        public const string UnityBanner = "Banner_Android";   // not used: the game has no banner ads
        public const bool UnityTestMode = false;

        /// <summary>How long a tapped ad button waits for an ad to load (slow internet) before giving up (seconds).</summary>
        public const float WaitSeconds = 10f;
    }
}
