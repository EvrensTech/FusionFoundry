using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuelProtocol.Services;
using UnityEngine;

namespace DuelProtocol.Presentation
{
    public enum DuelIdentityState
    {
        Uninitialized,
        Busy,
        SignedIn,
        SignedOut,
        Error
    }

    public sealed class DuelIdentitySession
    {
        private readonly IDuelIdentityService _service;

        public DuelIdentitySession(IDuelIdentityService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public DuelIdentityState State { get; private set; }
        public string PlayerId => _service.IsSignedIn ? _service.PlayerId : string.Empty;
        public string ErrorCode { get; private set; } = string.Empty;
        public string ErrorMessage { get; private set; } = string.Empty;

        public Task InitializeAnonymousAsync(string environment = "development")
        {
            return RunAsync(async () =>
            {
                await _service.InitializeAsync(environment);
                await _service.SignInAnonymouslyAsync();
            });
        }

        public Task SignInAsync(string username, string password)
        {
            return RunAsync(async () =>
            {
                if (_service.IsSignedIn)
                {
                    _service.SignOut();
                }
                await _service.SignInWithUsernamePasswordAsync(username, password);
            });
        }

        public Task UpgradeAsync(string username, string password)
        {
            return RunAsync(() => _service.UpgradeAnonymousAccountAsync(username, password));
        }

        public void SignOut()
        {
            _service.SignOut();
            State = DuelIdentityState.SignedOut;
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (State == DuelIdentityState.Busy)
            {
                return;
            }
            State = DuelIdentityState.Busy;
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
            try
            {
                await action();
                State = _service.IsSignedIn
                    ? DuelIdentityState.SignedIn
                    : DuelIdentityState.SignedOut;
            }
            catch (Exception exception)
            {
                State = DuelIdentityState.Error;
                ErrorCode = exception.GetType().Name;
                ErrorMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "Authentication service unavailable."
                    : exception.Message;
            }
        }
    }

    public enum DuelProfilePanelState
    {
        Idle,
        Loading,
        Ready,
        Offline,
        Error
    }

    [DisallowMultipleComponent]
    public sealed class DuelProfilePanel : MonoBehaviour
    {
        private IDuelProgressionService _service;
        private IReadOnlyList<DuelLeaderboardEntry> _leaderboard =
            Array.Empty<DuelLeaderboardEntry>();
        private bool _visible;
        private bool _settlementPending;

        public DuelProfilePanelState State { get; private set; }
        public DuelPlayerProfile Profile { get; private set; }
        public IReadOnlyList<DuelLeaderboardEntry> Leaderboard => _leaderboard;
        public string ErrorCode { get; private set; } = string.Empty;
        public bool IsVisible => _visible;

        public void Configure(IDuelProgressionService service, bool offline = false)
        {
            _service = service;
            State = offline ? DuelProfilePanelState.Offline : DuelProfilePanelState.Idle;
            Profile = null;
            _leaderboard = Array.Empty<DuelLeaderboardEntry>();
            ErrorCode = string.Empty;
        }

        public void ToggleVisible()
        {
            _visible = !_visible;
            if (_visible && Profile == null && State != DuelProfilePanelState.Loading)
            {
                _ = RefreshAsync();
            }
        }

        public void SetSettlementPending(bool pending)
        {
            _settlementPending = pending;
        }

        public async Task RefreshAsync()
        {
            if (_service == null || State == DuelProfilePanelState.Loading)
            {
                return;
            }
            var fallbackState = State == DuelProfilePanelState.Offline
                ? DuelProfilePanelState.Offline
                : DuelProfilePanelState.Ready;
            State = DuelProfilePanelState.Loading;
            ErrorCode = string.Empty;
            try
            {
                Profile = await _service.GetProfileAsync();
                _leaderboard = await _service.GetLeaderboardAroundPlayerAsync(3) ??
                               Array.Empty<DuelLeaderboardEntry>();
                State = fallbackState;
            }
            catch (Exception exception)
            {
                State = DuelProfilePanelState.Error;
                ErrorCode = exception.GetType().Name;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            var width = Mathf.Min(460f, Screen.width - 32f);
            var height = Mathf.Min(420f, Screen.height - 32f);
            GUILayout.BeginArea(
                new Rect(Screen.width - width - 16f, Screen.height - height - 16f, width, height),
                GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("DUEL PROFILE");
            if (GUILayout.Button("Refresh", GUILayout.Width(84f))) _ = RefreshAsync();
            if (GUILayout.Button("Close", GUILayout.Width(72f))) _visible = false;
            GUILayout.EndHorizontal();
            GUILayout.Label($"State: {State}{(string.IsNullOrEmpty(ErrorCode) ? string.Empty : $" • {ErrorCode}")}");
            if (_settlementPending)
            {
                GUILayout.Label("Match settlement pending — rewards are not final yet.");
            }
            if (Profile != null)
            {
                GUILayout.Label($"{Profile.DisplayName} • Lv {Profile.Level} • {Profile.Experience} XP");
                GUILayout.Label($"{Profile.League} • Rating {Profile.Rating} • W/L/D {Profile.Wins}/{Profile.Losses}/{Profile.Draws}");
                var daily = Profile.Daily ?? new DuelDailyProgress();
                GUILayout.Label(
                    $"Daily ({daily.UtcDate}): matches {daily.MatchesPlayed}/3 • wins {daily.Wins}/1 • carry {daily.CoreCarrySeconds:0}/60s");
                GUILayout.Label("Leaderboard around player:");
                foreach (var entry in _leaderboard)
                {
                    GUILayout.Label($"#{entry.Rank + 1}  {entry.DisplayName}  {entry.Rating}");
                }
                GUILayout.Label($"Recent matches: {Mathf.Min(20, Profile.MatchHistory?.Count ?? 0)}");
            }
            GUILayout.EndArea();
        }
    }
}
