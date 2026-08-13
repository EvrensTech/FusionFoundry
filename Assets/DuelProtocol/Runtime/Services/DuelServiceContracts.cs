using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuelProtocol.Match;

namespace DuelProtocol.Services
{
    [Serializable]
    public sealed class DuelPlayerProfile
    {
        public int SchemaVersion = 2;
        public string PlayerId = string.Empty;
        public string DisplayName = string.Empty;
        public int Level = 1;
        public int Experience;
        public int Rating = 1000;
        public string League = "Silver";
        public int Wins;
        public int Losses;
        public int Draws;
        public List<DuelMatchRecord> MatchHistory = new List<DuelMatchRecord>();
        public List<string> ProcessedMatchIds = new List<string>();
        public DuelDailyProgress Daily = new DuelDailyProgress();
        public DuelPlayerPreferencesData Preferences = new DuelPlayerPreferencesData();
    }

    [Serializable]
    public sealed class DuelPlayerPreferencesData
    {
        public string InputBindingsJson = string.Empty;
        public bool ReducedMotion;
        public bool HighContrast;
        public float HudScale = 1f;
    }

    [Serializable]
    public sealed class DuelDailyProgress
    {
        public string UtcDate = string.Empty;
        public int MatchesPlayed;
        public int Wins;
        public float CoreCarrySeconds;
        public bool PlayThreeClaimed;
        public bool WinOneClaimed;
        public bool CarrySixtyClaimed;

        public bool PlayThreeComplete => MatchesPlayed >= 3;
        public bool WinOneComplete => Wins >= 1;
        public bool CarrySixtyComplete => CoreCarrySeconds >= 60f;
    }

    [Serializable]
    public sealed class DuelMatchRecord
    {
        public string MatchId = string.Empty;
        public string TicketId = string.Empty;
        public DuelMatchMode Mode;
        public string PlayerOneId = string.Empty;
        public string PlayerTwoId = string.Empty;
        public int PlayerOneScore;
        public int PlayerTwoScore;
        public int WinnerIndex = -1;
        public DuelTerminationReason TerminationReason;
        public long StartedUnixSeconds;
        public long EndedUnixSeconds;
        public string Nonce = string.Empty;
        public float PlayerOneCarrySeconds;
        public float PlayerTwoCarrySeconds;
    }

    [Serializable]
    public sealed class DuelMatchTicket
    {
        public string TicketId = string.Empty;
        public string PlayerId = string.Empty;
        public DuelMatchMode Mode;
        public int Rating;
        public string BuildId = string.Empty;
        public string Platform = string.Empty;
        public long IssuedUnixSeconds;
        public long ExpiresUnixSeconds;
        public string Nonce = string.Empty;
        public string ConsumedMatchId = string.Empty;

        public bool IsExpired(long nowUnixSeconds)
        {
            return string.IsNullOrWhiteSpace(TicketId) || nowUnixSeconds >= ExpiresUnixSeconds;
        }
    }

    [Serializable]
    public sealed class DuelSettlementResult
    {
        public bool Accepted;
        public bool AlreadyProcessed;
        public string Status = string.Empty;
        public string ErrorCode = string.Empty;
        public int RatingBefore;
        public int RatingAfter;
        public int ExperienceAwarded;
    }

    [Serializable]
    public sealed class DuelLeaderboardEntry
    {
        public string PlayerId = string.Empty;
        public string DisplayName = string.Empty;
        public int Rank;
        public int Rating;
    }

    public interface IDuelIdentityService
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        Task InitializeAsync(string environmentName);
        Task SignInAnonymouslyAsync();
        Task SignInWithUsernamePasswordAsync(string username, string password);
        Task UpgradeAnonymousAccountAsync(string username, string password);
        void SignOut();
    }

    public interface IDuelMatchTicketService
    {
        Task<DuelMatchTicket> CreateAsync(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform);
    }

    public interface IDuelMatchResultService
    {
        Task<DuelSettlementResult> SubmitAsync(DuelMatchRecord record);
    }

    public interface IDuelProgressionService
    {
        Task<DuelPlayerProfile> GetProfileAsync();
        Task<IReadOnlyList<DuelLeaderboardEntry>> GetLeaderboardAroundPlayerAsync(int rangeLimit);
    }

    public interface IDuelPlayerPreferencesService
    {
        Task<DuelPlayerPreferencesData> GetPreferencesAsync();
        Task SavePreferencesAsync(DuelPlayerPreferencesData preferences);
    }

    public interface IDuelTelemetrySink
    {
        void SetConsent(bool granted);
        void Record(string eventName);
        void Flush();
    }

    public interface IDuelEconomyService
    {
        bool IsAvailable { get; }
        Task<int> GetSoftCurrencyBalanceAsync();
    }

    public interface IDuelRewardedAdService
    {
        bool IsAvailable { get; }
        Task<bool> ShowRewardedAsync(string placementId);
    }

    public interface IDuelSeasonService
    {
        bool IsAvailable { get; }
        Task<string> GetActiveSeasonIdAsync();
    }

    public sealed class DuelOptionalServices
    {
        public DuelOptionalServices(
            IDuelEconomyService economy = null,
            IDuelRewardedAdService rewardedAds = null,
            IDuelSeasonService seasons = null)
        {
            Economy = economy ?? NullDuelEconomyService.Instance;
            RewardedAds = rewardedAds ?? NullDuelRewardedAdService.Instance;
            Seasons = seasons ?? NullDuelSeasonService.Instance;
        }

        public IDuelEconomyService Economy { get; }
        public IDuelRewardedAdService RewardedAds { get; }
        public IDuelSeasonService Seasons { get; }
    }

    internal sealed class NullDuelEconomyService : IDuelEconomyService
    {
        public static readonly NullDuelEconomyService Instance = new NullDuelEconomyService();
        public bool IsAvailable => false;
        public Task<int> GetSoftCurrencyBalanceAsync() => Task.FromResult(0);
    }

    internal sealed class NullDuelRewardedAdService : IDuelRewardedAdService
    {
        public static readonly NullDuelRewardedAdService Instance = new NullDuelRewardedAdService();
        public bool IsAvailable => false;
        public Task<bool> ShowRewardedAsync(string placementId) => Task.FromResult(false);
    }

    /// <summary>
    /// Keeps the game-facing ad contract independent from the selected monetization SDK.
    /// A LevelPlay integration (or a future provider) registers itself at runtime.
    /// </summary>
    public static class DuelRewardedAds
    {
        private static IDuelRewardedAdService s_Current = NullDuelRewardedAdService.Instance;

        public static IDuelRewardedAdService Current => s_Current;

        public static void Register(IDuelRewardedAdService service)
        {
            s_Current = service ?? NullDuelRewardedAdService.Instance;
        }
    }

    internal sealed class NullDuelSeasonService : IDuelSeasonService
    {
        public static readonly NullDuelSeasonService Instance = new NullDuelSeasonService();
        public bool IsAvailable => false;
        public Task<string> GetActiveSeasonIdAsync() => Task.FromResult(string.Empty);
    }

    public static class DuelRating
    {
        public const int InitialRating = 1000;
        public const int KFactor = 32;

        public static int Calculate(int currentRating, int opponentRating, float result)
        {
            var expected = 1.0 / (1.0 + Math.Pow(10.0, (opponentRating - currentRating) / 400.0));
            var change = (int)Math.Round(KFactor * (Math.Max(0f, Math.Min(1f, result)) - expected));
            return Math.Max(0, currentRating + change);
        }

        public static string GetLeague(int rating)
        {
            if (rating < 900) return "Bronze";
            if (rating < 1100) return "Silver";
            if (rating < 1300) return "Gold";
            return "Platinum";
        }

        public static int GetExperienceAward(DuelMatchMode mode, bool won)
        {
            return mode == DuelMatchMode.Ai
                ? (won ? 50 : 20)
                : (won ? 100 : 40);
        }
    }

    public sealed class RatingBandPolicy
    {
        public const int InitialBand = 100;
        public const int ExpansionPerStep = 100;
        public const int MaximumBand = 400;
        public const float StepSeconds = 15f;
        public const float TimeoutSeconds = 60f;

        public int GetBand(float elapsedSeconds)
        {
            var steps = Math.Max(0, (int)(Math.Max(0f, elapsedSeconds) / StepSeconds));
            return Math.Min(MaximumBand, InitialBand + steps * ExpansionPerStep);
        }

        public bool HasTimedOut(float elapsedSeconds)
        {
            return elapsedSeconds >= TimeoutSeconds;
        }

        public bool IsCompatible(int firstRating, int secondRating, float elapsedSeconds)
        {
            return Math.Abs(firstRating - secondRating) <= GetBand(elapsedSeconds);
        }
    }
}
