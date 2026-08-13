using System.Collections;
using System.Threading.Tasks;
using DuelProtocol.Services;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace DuelProtocol.Monetization.LevelPlay
{
    /// <summary>
    /// Opt-in rewarded-ad adapter. No ad is initialized until a Resources/DuelAdsSettings asset
    /// contains dashboard identifiers. Rewards are confirmed only by LevelPlay's reward callback.
    /// </summary>
    public sealed class LevelPlayDuelRewardedAdService : MonoBehaviour, IDuelRewardedAdService
    {
        private const float CloseGraceSeconds = 3f;
        private const float ShowTimeoutSeconds = 120f;
        private const float ReloadDelaySeconds = 10f;

        private DuelAdsSettings _settings;
        private LevelPlayRewardedAd _rewardedAd;
        private TaskCompletionSource<bool> _pendingShow;
        private bool _initialized;
        private bool _isShuttingDown;

        public bool IsAvailable => _initialized && _pendingShow == null &&
                                   _rewardedAd != null && _rewardedAd.IsAdReady();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<LevelPlayDuelRewardedAdService>() != null) return;
            var host = new GameObject("Duel LevelPlay Rewarded Ads");
            DontDestroyOnLoad(host);
            host.AddComponent<LevelPlayDuelRewardedAdService>();
        }

        private void Awake()
        {
            _settings = Resources.Load<DuelAdsSettings>("DuelAdsSettings");
            DuelRewardedAds.Register(this);

            if (_settings == null || !_settings.IsConfigured)
            {
                Debug.Log("[Duel Ads] LevelPlay is installed but inactive. Configure Resources/DuelAdsSettings to enable rewarded ads.");
                return;
            }

            // Privacy signals must be supplied before SDK initialization. Contextual/no-sale values
            // are the safe default; production must replace these with a compliant CMP/age-gate flow.
            LevelPlayPrivacySettings.SetGDPRConsent(_settings.GdprConsent);
            LevelPlayPrivacySettings.SetCCPA(_settings.CcpaDoNotSell);
            LevelPlayPrivacySettings.SetCOPPA(_settings.CoppaChildDirected);
            Unity.Services.LevelPlay.LevelPlay.SetPauseGame(true);
            Unity.Services.LevelPlay.LevelPlay.OnInitSuccess += OnInitializationSucceeded;
            Unity.Services.LevelPlay.LevelPlay.OnInitFailed += OnInitializationFailed;
            Unity.Services.LevelPlay.LevelPlay.Init(_settings.AppKey.Trim());
        }

        public Task<bool> ShowRewardedAsync(string placementId)
        {
            if (!IsAvailable) return Task.FromResult(false);

            _pendingShow = new TaskCompletionSource<bool>();
            var request = _pendingShow;
            var placement = string.IsNullOrWhiteSpace(placementId)
                ? _settings.StoreBonusPlacement
                : placementId.Trim();
            _rewardedAd.ShowAd(placement);
            StartCoroutine(ResolveAfterTimeout(request));
            return request.Task;
        }

        private void OnInitializationSucceeded(LevelPlayConfiguration configuration)
        {
            if (_isShuttingDown || _initialized) return;
            _initialized = true;
            _rewardedAd = new LevelPlayRewardedAd(_settings.RewardedAdUnitId.Trim());
            _rewardedAd.OnAdRewarded += OnAdRewarded;
            _rewardedAd.OnAdClosed += OnAdClosed;
            _rewardedAd.OnAdDisplayFailed += OnAdDisplayFailed;
            _rewardedAd.OnAdLoadFailed += OnAdLoadFailed;
            _rewardedAd.LoadAd();
            Debug.Log("[Duel Ads] LevelPlay rewarded ads initialized.");
        }

        private void OnInitializationFailed(LevelPlayInitError error)
        {
            Debug.LogWarning($"[Duel Ads] LevelPlay initialization failed: {error}");
            CompletePending(false);
        }

        private void OnAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            CompletePending(true);
        }

        private void OnAdClosed(LevelPlayAdInfo adInfo)
        {
            var request = _pendingShow;
            if (request != null) StartCoroutine(ResolveAfterClose(request));
            Reload();
        }

        private void OnAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogWarning($"[Duel Ads] Rewarded ad display failed: {error}");
            CompletePending(false);
            Reload();
        }

        private void OnAdLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"[Duel Ads] Rewarded ad load failed: {error}");
            if (!_isShuttingDown) StartCoroutine(ReloadAfterDelay());
        }

        private IEnumerator ResolveAfterClose(TaskCompletionSource<bool> request)
        {
            // LevelPlay documents that reward and close callbacks can arrive in either order.
            yield return new WaitForSecondsRealtime(CloseGraceSeconds);
            if (ReferenceEquals(_pendingShow, request)) CompletePending(false);
        }

        private IEnumerator ResolveAfterTimeout(TaskCompletionSource<bool> request)
        {
            yield return new WaitForSecondsRealtime(ShowTimeoutSeconds);
            if (ReferenceEquals(_pendingShow, request)) CompletePending(false);
        }

        private IEnumerator ReloadAfterDelay()
        {
            yield return new WaitForSecondsRealtime(ReloadDelaySeconds);
            Reload();
        }

        private void Reload()
        {
            if (!_isShuttingDown && _initialized && _rewardedAd != null) _rewardedAd.LoadAd();
        }

        private void CompletePending(bool rewarded)
        {
            var request = _pendingShow;
            _pendingShow = null;
            request?.TrySetResult(rewarded);
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;
            CompletePending(false);
            DuelRewardedAds.Register(null);
            Unity.Services.LevelPlay.LevelPlay.OnInitSuccess -= OnInitializationSucceeded;
            Unity.Services.LevelPlay.LevelPlay.OnInitFailed -= OnInitializationFailed;
            if (_rewardedAd == null) return;
            _rewardedAd.OnAdRewarded -= OnAdRewarded;
            _rewardedAd.OnAdClosed -= OnAdClosed;
            _rewardedAd.OnAdDisplayFailed -= OnAdDisplayFailed;
            _rewardedAd.OnAdLoadFailed -= OnAdLoadFailed;
            _rewardedAd.DestroyAd();
            _rewardedAd = null;
        }
    }
}
