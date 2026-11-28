using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArrowRoute
{
    /// <summary>
    /// Ad mediation inside the app (a "waterfall"): AdMob is asked first, Unity Ads when AdMob has no ad.
    ///  1. Both networks start in the background and ads are preloaded.
    ///  2. When the player wants an ad, the first network that has one ready shows it.
    ///  3. If none is ready, the networks are asked now and the first ad that arrives is shown
    ///     (after AdIds.WaitSeconds the request is given up).
    /// Rewarded ads give the reward only when the video was watched to the end.
    /// </summary>
    public sealed class AdService : MonoBehaviour
    {
        readonly List<IAdProvider> providers = new List<IAdProvider>();
        readonly Queue<Action> mainQueue = new Queue<Action>();
        bool[] inited, rLoading, iLoading;
        int[] rFails, iFails;

        public string LastError = "";

        float lastInterstitialAt = -999f;
        Action pendingReward, pendingUnavailable;
        Action<bool> pendingWait;
        float pendingDeadline;
        int pendingFailed;

        void Post(Action a)
        {
            lock (mainQueue) mainQueue.Enqueue(a);
        }

        void Update()
        {
            while (true)
            {
                Action a;
                lock (mainQueue)
                {
                    if (mainQueue.Count == 0) break;
                    a = mainQueue.Dequeue();
                }
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (pendingReward != null && Time.realtimeSinceStartup > pendingDeadline) FinishPending();
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && providers.Count > 0)
            {
                for (int i = 0; i < providers.Count; i++) { rFails[i] = 0; iFails[i] = 0; }
                FillRewarded();
                FillInterstitial();
            }
        }

        // ------------------------------------------------------------ start

        public void StartAds()
        {
            providers.Clear();
#if ARROW_ADMOB
            providers.Add(new AdMobProvider());
#endif
#if ARROW_UNITY_ADS
            providers.Add(new UnityAdsProvider());
#endif
            if (providers.Count == 0) providers.Add(new NullProvider());
            int n = providers.Count;
            inited = new bool[n]; rLoading = new bool[n]; iLoading = new bool[n];
            rFails = new int[n]; iFails = new int[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                try
                {
                    providers[i].Init((ok, msg) => Post(() =>
                    {
                        if (ok)
                        {
                            inited[idx] = true;
                            FillRewarded();
                            FillInterstitial();
                            if (pendingReward != null) LoadRewardedFrom(idx);
                        }
                        else
                        {
                            LastError = providers[idx].Name + " init: " + msg;
                            Debug.LogWarning(LastError);
                            NoteWaitFailure();
                        }
                    }));
                }
                catch (Exception e)
                {
                    LastError = providers[i].Name + " init: " + e.Message;
                    Debug.LogWarning(LastError);
                }
            }
        }

        // ------------------------------------------------------------ preloading (waterfall)

        void FillRewarded()
        {
            for (int i = 0; i < providers.Count; i++) if (providers[i].RewardedReady) return;
            TryLoadRewarded(0);
        }

        void FillInterstitial()
        {
            for (int i = 0; i < providers.Count; i++) if (providers[i].InterstitialReady) return;
            TryLoadInterstitial(0);
        }

        /// <summary>Loads from provider i; if it fails, the next provider is tried.</summary>
        void TryLoadRewarded(int i)
        {
            if (i >= providers.Count) return;
            if (!inited[i] || rLoading[i]) { if (!inited[i]) TryLoadRewarded(i + 1); return; }
            LoadRewardedFrom(i, true);
        }

        void LoadRewardedFrom(int i, bool chain = false)
        {
            if (!inited[i] || rLoading[i] || providers[i].RewardedReady) return;
            rLoading[i] = true;
            IAdProvider p = providers[i];
            try
            {
                p.LoadRewarded((ok, msg) => Post(() =>
                {
                    rLoading[i] = false;
                    if (ok)
                    {
                        rFails[i] = 0;
                        ServePending();
                    }
                    else
                    {
                        rFails[i]++;
                        LastError = p.Name + " rewarded: " + msg;
                        Debug.LogWarning(LastError);
                        NoteWaitFailure();
                        if (chain) TryLoadRewarded(i + 1);
                        if (rFails[i] < 6) StartCoroutine(Retry(RetryDelay(rFails[i]), FillRewarded));
                    }
                }));
            }
            catch (Exception e)
            {
                rLoading[i] = false;
                LastError = p.Name + " rewarded: " + e.Message;
            }
        }

        void TryLoadInterstitial(int i)
        {
            if (i >= providers.Count) return;
            if (!inited[i] || iLoading[i]) { if (!inited[i]) TryLoadInterstitial(i + 1); return; }
            iLoading[i] = true;
            IAdProvider p = providers[i];
            try
            {
                p.LoadInterstitial((ok, msg) => Post(() =>
                {
                    iLoading[i] = false;
                    if (ok) { iFails[i] = 0; return; }
                    iFails[i]++;
                    LastError = p.Name + " interstitial: " + msg;
                    Debug.LogWarning(LastError);
                    TryLoadInterstitial(i + 1);
                    if (iFails[i] < 6) StartCoroutine(Retry(RetryDelay(iFails[i]), FillInterstitial));
                }));
            }
            catch (Exception e)
            {
                iLoading[i] = false;
                LastError = p.Name + " interstitial: " + e.Message;
            }
        }

        static float RetryDelay(int fails) { return Mathf.Min(60f, 8f * (1 << Mathf.Min(fails - 1, 3))); }

        IEnumerator Retry(float seconds, Action what)
        {
            yield return new WaitForSecondsRealtime(seconds);
            what();
        }

        // ------------------------------------------------------------ rewarded

        /// <summary>
        /// Shows a rewarded ad. onWait(true) is called while waiting for an ad to load and onWait(false) when the wait is over.
        /// </summary>
        public void ShowRewarded(Action onReward, Action onUnavailable, Action<bool> onWait)
        {
            for (int i = 0; i < providers.Count; i++)
            {
                if (providers[i].RewardedReady) { ShowRewardedFrom(i, onReward, onUnavailable); return; }
            }
            if (pendingReward != null) return;   // already waiting for one
            pendingReward = onReward;
            pendingUnavailable = onUnavailable;
            pendingWait = onWait;
            pendingFailed = 0;
            pendingDeadline = Time.realtimeSinceStartup + AdIds.WaitSeconds;
            if (onWait != null) onWait(true);
            // ask every network now; the first ad that arrives is shown
            for (int i = 0; i < providers.Count; i++)
            {
                for (int k = 0; k < 1; k++) rFails[i] = 0;
                LoadRewardedFrom(i);
            }
        }

        void ShowRewardedFrom(int i, Action onReward, Action onUnavailable)
        {
            providers[i].ShowRewarded(result => Post(() =>
            {
                FillRewarded();
                if (result == ShowResult.Completed) onReward();
                else if (result == ShowResult.Failed)
                {
                    for (int k = 0; k < providers.Count; k++)
                    {
                        if (k != i && providers[k].RewardedReady) { ShowRewardedFrom(k, onReward, onUnavailable); return; }
                    }
                    onUnavailable();
                }
            }));
        }

        void ServePending()
        {
            if (pendingReward == null) return;
            Action r = pendingReward, u = pendingUnavailable;
            Action<bool> w = pendingWait;
            ClearPending();
            if (w != null) w(false);
            ShowRewarded(r, u, null);
        }

        void NoteWaitFailure()
        {
            if (pendingReward == null) return;
            pendingFailed++;
            if (pendingFailed >= providers.Count) FinishPending();
        }

        void ClearPending()
        {
            pendingReward = null;
            pendingUnavailable = null;
            pendingWait = null;
        }

        void FinishPending()
        {
            Action u = pendingUnavailable;
            Action<bool> w = pendingWait;
            ClearPending();
            if (w != null) w(false);
            if (u != null) u();
        }

        // ------------------------------------------------------------ interstitial

        /// <summary>Shows an interstitial if one is ready and the last one was over a minute ago, then always calls onDone.</summary>
        public void ShowInterstitial(Action onDone)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastInterstitialAt >= 60f)
            {
                for (int i = 0; i < providers.Count; i++)
                {
                    if (!providers[i].InterstitialReady) continue;
                    lastInterstitialAt = now;
                    providers[i].ShowInterstitial(() => Post(() =>
                    {
                        FillInterstitial();
                        onDone();
                    }));
                    return;
                }
            }
            FillInterstitial();
            onDone();
        }
    }
}
