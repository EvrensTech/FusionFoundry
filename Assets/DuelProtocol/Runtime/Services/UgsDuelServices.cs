using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuelProtocol.Match;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards;

namespace DuelProtocol.Services
{
    /// <summary>
    /// Authentication adapter. Credentials are handled only by UGS and are never persisted by game code.
    /// </summary>
    public sealed class UgsDuelIdentityService : IDuelIdentityService
    {
        private static readonly object InitializationGate = new object();
        private static Task s_Initialization;
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        public async Task InitializeAsync(string environmentName)
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                return;
            }

            var environment = string.IsNullOrWhiteSpace(environmentName)
                ? "development"
                : environmentName.Trim();
            Task initialization;
            lock (InitializationGate)
            {
                initialization = s_Initialization ??= UnityServices.InitializeAsync(
                    new InitializationOptions().SetEnvironmentName(environment));
            }
            try
            {
                await initialization;
            }
            catch
            {
                lock (InitializationGate)
                {
                    if (ReferenceEquals(s_Initialization, initialization))
                    {
                        s_Initialization = null;
                    }
                }
                throw;
            }
        }

        public Task SignInAnonymouslyAsync()
        {
            return IsSignedIn
                ? Task.CompletedTask
                : AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public Task SignInWithUsernamePasswordAsync(string username, string password)
        {
            RequireCredentials(username, password);
            return AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
        }

        public Task UpgradeAnonymousAccountAsync(string username, string password)
        {
            RequireCredentials(username, password);
            if (!IsSignedIn)
            {
                throw new InvalidOperationException("Anonymous sign-in is required before account upgrade.");
            }
            return AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);
        }

        public void SignOut()
        {
            if (IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }
        }

        private static void RequireCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Username and password are required.");
            }
        }
    }

    /// <summary>
    /// Thin client for server-authoritative Cloud Code endpoints. The client never writes rating or XP.
    /// </summary>
    public sealed class UgsDuelBackend :
        IDuelMatchTicketService,
        IDuelMatchResultService,
        IDuelProgressionService,
        IDuelPlayerPreferencesService
    {
        public const string RatingLeaderboardId = "duel-rating";

        public Task<DuelMatchTicket> CreateAsync(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform)
        {
            return CloudCodeService.Instance.CallEndpointAsync<DuelMatchTicket>(
                "CreateMatchTicket",
                new Dictionary<string, object>
                {
                    ["mode"] = mode.ToString(),
                    ["buildId"] = buildId ?? string.Empty,
                    ["platform"] = platform ?? string.Empty
                });
        }

        public Task<DuelSettlementResult> SubmitAsync(DuelMatchRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }
            return CloudCodeService.Instance.CallEndpointAsync<DuelSettlementResult>(
                "SubmitMatchResult",
                new Dictionary<string, object>
                {
                    ["record"] = ToArguments(record)
                });
        }

        public Task<DuelPlayerProfile> GetProfileAsync()
        {
            return CloudCodeService.Instance.CallEndpointAsync<DuelPlayerProfile>(
                "GetDuelProfile",
                new Dictionary<string, object>());
        }

        public async Task<IReadOnlyList<DuelLeaderboardEntry>> GetLeaderboardAroundPlayerAsync(
            int rangeLimit)
        {
            var scores = await LeaderboardsService.Instance.GetPlayerRangeAsync(
                RatingLeaderboardId,
                new GetPlayerRangeOptions { RangeLimit = Math.Max(1, rangeLimit) });
            return scores.Results.Select(entry => new DuelLeaderboardEntry
            {
                PlayerId = entry.PlayerId,
                DisplayName = entry.PlayerName ?? entry.PlayerId,
                Rank = entry.Rank,
                Rating = (int)Math.Round(entry.Score)
            }).ToList();
        }

        public async Task<DuelPlayerPreferencesData> GetPreferencesAsync()
        {
            var profile = await GetProfileAsync();
            return profile?.Preferences ?? new DuelPlayerPreferencesData();
        }

        public async Task SavePreferencesAsync(DuelPlayerPreferencesData preferences)
        {
            preferences ??= new DuelPlayerPreferencesData();
            await CloudCodeService.Instance.CallEndpointAsync<object>(
                "UpdateDuelPreferences",
                new Dictionary<string, object>
                {
                    ["inputBindingsJson"] = preferences.InputBindingsJson ?? string.Empty,
                    ["reducedMotion"] = preferences.ReducedMotion,
                    ["highContrast"] = preferences.HighContrast,
                    ["hudScale"] = Math.Max(0.8f, Math.Min(1.5f, preferences.HudScale))
                });
        }

        private static Dictionary<string, object> ToArguments(DuelMatchRecord record)
        {
            return new Dictionary<string, object>
            {
                ["matchId"] = record.MatchId,
                ["ticketId"] = record.TicketId,
                ["mode"] = record.Mode.ToString(),
                ["playerOneId"] = record.PlayerOneId,
                ["playerTwoId"] = record.PlayerTwoId,
                ["playerOneScore"] = record.PlayerOneScore,
                ["playerTwoScore"] = record.PlayerTwoScore,
                ["winnerIndex"] = record.WinnerIndex,
                ["terminationReason"] = record.TerminationReason.ToString(),
                ["startedUnixSeconds"] = record.StartedUnixSeconds,
                ["endedUnixSeconds"] = record.EndedUnixSeconds,
                ["nonce"] = record.Nonce,
                ["playerOneCarrySeconds"] = record.PlayerOneCarrySeconds,
                ["playerTwoCarrySeconds"] = record.PlayerTwoCarrySeconds
            };
        }
    }

    public sealed class UgsDuelTelemetrySink : IDuelTelemetrySink
    {
        private bool _consented;

        public void SetConsent(bool granted)
        {
            _consented = granted;
#pragma warning disable CS0618 // Fallback retained until Unity Consent is enabled in Player Settings.
            if (granted)
            {
                AnalyticsService.Instance.StartDataCollection();
            }
            else
            {
                AnalyticsService.Instance.StopDataCollection();
            }
#pragma warning restore CS0618
        }

        public void Record(string eventName)
        {
            if (_consented && !string.IsNullOrWhiteSpace(eventName))
            {
                AnalyticsService.Instance.RecordEvent(eventName);
            }
        }

        public void Flush()
        {
            if (_consented)
            {
                AnalyticsService.Instance.Flush();
            }
        }
    }
}
