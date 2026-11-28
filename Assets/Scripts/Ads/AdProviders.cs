using System;
#if ARROW_UNITY_ADS
using UnityEngine.Advertisements;
#endif
#if ARROW_ADMOB
using GoogleMobileAds.Api;
#endif

namespace ArrowRoute
{
    public enum ShowResult { Completed, Skipped, Failed }

    /// <summary>One ad network. Callbacks may come on any thread; AdService moves them to the main thread.</summary>
    public interface IAdProvider
    {
        string Name { get; }
        bool RewardedReady { get; }
        bool InterstitialReady { get; }
        void Init(Action<bool, string> done);
        void LoadRewarded(Action<bool, string> done);
        void LoadInterstitial(Action<bool, string> done);
        void ShowRewarded(Action<ShowResult> done);
        void ShowInterstitial(Action done);
    }

    /// <summary>Used when no ad SDK is compiled in. In the Unity editor it pretends that ads work, so the flows can be tested.</summary>
    public sealed class NullProvider : IAdProvider
    {
        public string Name { get { return "none"; } }

#if UNITY_EDITOR
        public bool RewardedReady { get { return true; } }
        public bool InterstitialReady { get { return true; } }
        public void Init(Action<bool, string> done) { done(true, ""); }
        public void LoadRewarded(Action<bool, string> done) { done(true, ""); }
        public void LoadInterstitial(Action<bool, string> done) { done(true, ""); }
        public void ShowRewarded(Action<ShowResult> done) { done(ShowResult.Completed); }
        public void ShowInterstitial(Action done) { done(); }
#else
        public bool RewardedReady { get { return false; } }
        public bool InterstitialReady { get { return false; } }
        public void Init(Action<bool, string> done) { done(false, "no ad SDK is compiled into this build"); }
        public void LoadRewarded(Action<bool, string> done) { done(false, "no ad SDK"); }
        public void LoadInterstitial(Action<bool, string> done) { done(false, "no ad SDK"); }
        public void ShowRewarded(Action<ShowResult> done) { done(ShowResult.Failed); }
        public void ShowInterstitial(Action done) { done(); }
#endif
    }

#if ARROW_UNITY_ADS
    /// <summary>Unity Ads (package com.unity.ads). Compiled when ARROW_UNITY_ADS is defined (Assets/csc.rsp).</summary>
    public sealed class UnityAdsProvider : IAdProvider, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
    {
        bool rewardedReady, interstitialReady;
        Action<bool, string> initDone, rewardedLoadDone, interstitialLoadDone;
        Action<ShowResult> rewardedShowDone;
        Action interstitialShowDone;

        public string Name { get { return "Unity"; } }
        public bool RewardedReady { get { return rewardedReady; } }
        public bool InterstitialReady { get { return interstitialReady; } }

        public void Init(Action<bool, string> done)
        {
            if (Advertisement.isInitialized) { done(true, ""); return; }
            initDone = done;
            Advertisement.Initialize(AdIds.UnityGameId, AdIds.UnityTestMode, this);
        }

        public void OnInitializationComplete() { if (initDone != null) initDone(true, ""); }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            if (initDone != null) initDone(false, error + ": " + message);
        }

        public void LoadRewarded(Action<bool, string> done)
        {
            rewardedLoadDone = done;
            Advertisement.Load(AdIds.UnityRewarded, this);
        }

        public void LoadInterstitial(Action<bool, string> done)
        {
            interstitialLoadDone = done;
            Advertisement.Load(AdIds.UnityInterstitial, this);
        }

        public void OnUnityAdsAdLoaded(string placementId)
        {
            if (placementId == AdIds.UnityRewarded) { rewardedReady = true; if (rewardedLoadDone != null) rewardedLoadDone(true, ""); }
            else if (placementId == AdIds.UnityInterstitial) { interstitialReady = true; if (interstitialLoadDone != null) interstitialLoadDone(true, ""); }
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            if (placementId == AdIds.UnityRewarded) { rewardedReady = false; if (rewardedLoadDone != null) rewardedLoadDone(false, error + ": " + message); }
            else if (placementId == AdIds.UnityInterstitial) { interstitialReady = false; if (interstitialLoadDone != null) interstitialLoadDone(false, error + ": " + message); }
        }

        public void ShowRewarded(Action<ShowResult> done)
        {
            rewardedShowDone = done;
            rewardedReady = false;
            Advertisement.Show(AdIds.UnityRewarded, this);
        }

        public void ShowInterstitial(Action done)
        {
            interstitialShowDone = done;
            interstitialReady = false;
            Advertisement.Show(AdIds.UnityInterstitial, this);
        }

        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            if (placementId == AdIds.UnityRewarded) { if (rewardedShowDone != null) rewardedShowDone(ShowResult.Failed); }
            else if (interstitialShowDone != null) interstitialShowDone();
        }

        public void OnUnityAdsShowStart(string placementId) { }
        public void OnUnityAdsShowClick(string placementId) { }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
        {
            if (placementId == AdIds.UnityRewarded)
            {
                if (rewardedShowDone != null)
                    rewardedShowDone(state == UnityAdsShowCompletionState.COMPLETED ? ShowResult.Completed : ShowResult.Skipped);
            }
            else if (interstitialShowDone != null) interstitialShowDone();
        }
    }
#endif

#if ARROW_ADMOB
    /// <summary>
    /// AdMob (Google Mobile Ads Unity plugin). Compiled when ARROW_ADMOB is defined (Assets/csc.rsp).
    /// The AdMob app id is written into the plugin settings by Editor/AdSetup.cs before every build.
    /// </summary>
    public sealed class AdMobProvider : IAdProvider
    {
        RewardedAd rewarded;
        InterstitialAd interstitial;

        public string Name { get { return "AdMob"; } }
        public bool RewardedReady { get { return rewarded != null && rewarded.CanShowAd(); } }
        public bool InterstitialReady { get { return interstitial != null && interstitial.CanShowAd(); } }

        public void Init(Action<bool, string> done)
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(status => done(true, ""));
        }

        public void LoadRewarded(Action<bool, string> done)
        {
            RewardedAd.Load(AdIds.AdMobRewarded, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null) { done(false, error != null ? error.GetMessage() : "empty ad"); return; }
                rewarded = ad;
                done(true, "");
            });
        }

        public void LoadInterstitial(Action<bool, string> done)
        {
            InterstitialAd.Load(AdIds.AdMobInterstitial, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null) { done(false, error != null ? error.GetMessage() : "empty ad"); return; }
                interstitial = ad;
                done(true, "");
            });
        }

        public void ShowRewarded(Action<ShowResult> done)
        {
            RewardedAd ad = rewarded;
            rewarded = null;
            if (ad == null || !ad.CanShowAd()) { done(ShowResult.Failed); return; }
            bool earned = false;
            ad.OnAdFullScreenContentClosed += () => done(earned ? ShowResult.Completed : ShowResult.Skipped);
            ad.OnAdFullScreenContentFailed += (AdError e) => done(ShowResult.Failed);
            ad.Show((Reward r) => { earned = true; });
        }

        public void ShowInterstitial(Action done)
        {
            InterstitialAd ad = interstitial;
            interstitial = null;
            if (ad == null || !ad.CanShowAd()) { done(); return; }
            ad.OnAdFullScreenContentClosed += () => done();
            ad.OnAdFullScreenContentFailed += (AdError e) => done();
            ad.Show();
        }
    }
#endif
}
