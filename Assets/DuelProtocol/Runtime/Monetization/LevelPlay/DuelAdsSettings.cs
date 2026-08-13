using UnityEngine;

namespace DuelProtocol.Monetization.LevelPlay
{
    [CreateAssetMenu(fileName = "DuelAdsSettings", menuName = "Duel Protocol/Ads Settings")]
    public sealed class DuelAdsSettings : ScriptableObject
    {
        [Header("LevelPlay dashboard identifiers")]
        [Tooltip("LevelPlay app key. This identifier is not a service-account secret.")]
        public string AppKey = string.Empty;

        [Tooltip("Rewarded ad unit ID from Setup > Ad Units in LevelPlay.")]
        public string RewardedAdUnitId = string.Empty;

        [Tooltip("Placement name configured in LevelPlay. Keep this synchronized with the dashboard.")]
        public string StoreBonusPlacement = "store_bonus";

        [Header("Privacy-safe defaults")]
        [Tooltip("False keeps ads contextual unless a compliant consent flow records opt-in.")]
        public bool GdprConsent;

        [Tooltip("True signals that the user opted out of sale/sharing under applicable US privacy rules.")]
        public bool CcpaDoNotSell = true;

        [Tooltip("Enable only when the app/user must receive child-directed treatment.")]
        public bool CoppaChildDirected;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(RewardedAdUnitId);
    }
}
