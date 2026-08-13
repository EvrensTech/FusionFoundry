using UnityEngine;

namespace DuelProtocol.Services
{
    /// <summary>
    /// Local development wallet for opt-in ad rewards. Production economy settlement can replace
    /// this storage without changing the LevelPlay adapter or store UI contract.
    /// </summary>
    public static class DuelAdRewardWallet
    {
        private const string BalanceKey = "duel.rewarded.core.balance.v1";

        public static int Balance => Mathf.Max(0, PlayerPrefs.GetInt(BalanceKey, 0));

        public static int GrantCore(int amount)
        {
            var next = Mathf.Max(0, Balance + Mathf.Max(0, amount));
            PlayerPrefs.SetInt(BalanceKey, next);
            PlayerPrefs.Save();
            return next;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(BalanceKey);
            PlayerPrefs.Save();
        }
    }
}
