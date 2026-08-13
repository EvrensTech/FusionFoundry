using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DuelProtocol.Match;
using Fusion;
using Fusion.Photon.Realtime;
using FusionFoundry.Bootstrap;
using FusionFoundry.Sessions;
using UnityEngine;
using FoundryBootstrap = FusionFoundry.Bootstrap.FusionBootstrap;

namespace DuelProtocol.Services
{
    /// <summary>
    /// Photon lobby property contract used by WP-13/WP-14. Platform is published
    /// for telemetry, but never used as a compatibility filter.
    /// </summary>
    public static class DuelPhotonSessionProperties
    {
        public const string Mode = "dm";
        public const string Build = "db";
        public const string HostRating = "dr";
        public const string RatingBand = "dw";
        public const string MatchId = "di";
        public const string HostPlayerId = "dp";
        public const string HostPlatform = "pf";
        public const string Region = "rg";

        public static Dictionary<string, SessionProperty> Create(
            DuelMatchTicket ticket,
            int ratingBand,
            string matchId,
            string region)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new ArgumentException("Photon region is required.", nameof(region));
            }

            return new Dictionary<string, SessionProperty>
            {
                [Mode] = (int)ticket.Mode,
                [Build] = ticket.BuildId,
                [HostRating] = ticket.Rating,
                [RatingBand] = ClampBand(ratingBand),
                [MatchId] = matchId,
                [HostPlayerId] = ticket.PlayerId,
                [HostPlatform] = ticket.Platform,
                [Region] = region.Trim().ToLowerInvariant()
            };
        }

        public static bool IsCompatible(
            IReadOnlyDictionary<string, SessionProperty> properties,
            int playerCount,
            bool isOpen,
            DuelMatchTicket ticket,
            int localRatingBand,
            string localRegion)
        {
            if (properties == null || ticket == null || !isOpen || playerCount >= 2)
            {
                return false;
            }
            if (!TryGetInt(properties, Mode, out var mode) || mode != (int)ticket.Mode ||
                !TryGetString(properties, Build, out var buildId) ||
                !string.Equals(buildId, ticket.BuildId, StringComparison.Ordinal) ||
                !TryGetInt(properties, HostRating, out var hostRating) ||
                !TryGetInt(properties, RatingBand, out var hostBand) ||
                !TryGetString(properties, MatchId, out var matchId) ||
                string.IsNullOrWhiteSpace(matchId) ||
                !TryGetString(properties, HostPlayerId, out var hostPlayerId) ||
                !TryGetString(properties, Region, out var region) ||
                !string.Equals(
                    region,
                    localRegion?.Trim(),
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hostPlayerId, ticket.PlayerId, StringComparison.Ordinal))
            {
                return false;
            }

            var sharedBand = Math.Min(ClampBand(localRatingBand), ClampBand(hostBand));
            return Math.Abs(hostRating - ticket.Rating) <= sharedBand;
        }

        public static DuelMatchAssignment ToAssignment(
            string sessionName,
            IReadOnlyDictionary<string, SessionProperty> properties,
            DuelMatchMode fallbackMode,
            string fallbackBuildId)
        {
            TryGetString(properties, MatchId, out var matchId);
            TryGetString(properties, HostPlayerId, out var hostPlayerId);
            var mode = fallbackMode;
            if (TryGetInt(properties, Mode, out var modeValue) &&
                Enum.IsDefined(typeof(DuelMatchMode), modeValue))
            {
                mode = (DuelMatchMode)modeValue;
            }
            var buildId = fallbackBuildId;
            if (TryGetString(properties, Build, out var publishedBuild))
            {
                buildId = publishedBuild;
            }

            return new DuelMatchAssignment
            {
                MatchId = matchId ?? string.Empty,
                SessionName = sessionName ?? string.Empty,
                OpponentPlayerId = hostPlayerId ?? string.Empty,
                BuildId = buildId ?? string.Empty,
                Mode = mode
            };
        }

        private static int ClampBand(int band)
        {
            return Math.Max(
                RatingBandPolicy.InitialBand,
                Math.Min(RatingBandPolicy.MaximumBand, band));
        }

        private static bool TryGetInt(
            IReadOnlyDictionary<string, SessionProperty> properties,
            string key,
            out int value)
        {
            if (properties != null && properties.TryGetValue(key, out var property) && property.IsInt)
            {
                value = property;
                return true;
            }
            value = default;
            return false;
        }

        private static bool TryGetString(
            IReadOnlyDictionary<string, SessionProperty> properties,
            string key,
            out string value)
        {
            if (properties != null && properties.TryGetValue(key, out var property) && property.IsString)
            {
                value = property;
                return true;
            }
            value = null;
            return false;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunner))]
    internal sealed class DuelPhotonLobbyBrowser : NetworkRunnerCallbacksBehaviour
    {
        private TaskCompletionSource<IReadOnlyList<SessionInfo>> _sessions;

        public async Task<IReadOnlyList<SessionInfo>> BrowseAsync(
            string fixedRegion,
            CancellationToken cancellationToken)
        {
            _sessions = new TaskCompletionSource<IReadOnlyList<SessionInfo>>();
            var runner = GetComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runner.AddCallbacks(this);

            var appSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
            if (!string.IsNullOrWhiteSpace(fixedRegion))
            {
                appSettings.FixedRegion = fixedRegion.Trim().ToLowerInvariant();
            }

            var result = await runner.JoinSessionLobby(
                SessionLobby.ClientServer,
                null,
                null,
                appSettings,
                cancellationToken,
                false);
            if (!result.Ok)
            {
                throw new InvalidOperationException(
                    $"Photon lobby connection failed: {result.ShutdownReason} {result.ErrorMessage}");
            }

            using (cancellationToken.Register(
                       () => _sessions.TrySetCanceled(cancellationToken)))
            {
                var completed = await Task.WhenAny(
                    _sessions.Task,
                    Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
                if (completed == _sessions.Task)
                {
                    return await _sessions.Task;
                }
                cancellationToken.ThrowIfCancellationRequested();
                return Array.Empty<SessionInfo>();
            }
        }

        public override void OnSessionListUpdated(
            NetworkRunner runner,
            List<SessionInfo> sessionList)
        {
            _sessions?.TrySetResult(sessionList?.ToArray() ?? Array.Empty<SessionInfo>());
        }
    }

    /// <summary>
    /// Searches the Photon Client/Server lobby, filters visible sessions by the
    /// server-issued ticket and widening rating band, then joins the selected
    /// room. If no compatible room exists, this peer publishes a host room and
    /// keeps it open until a compatible opponent arrives or the search is cancelled.
    /// </summary>
    public sealed class FusionDuelMatchmakingTransport : IDuelMatchmakingTransport
    {
        private static readonly TimeSpan WaitingRoomStabilization = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan[] JoinRetryDelays =
        {
            TimeSpan.FromMilliseconds(350),
            TimeSpan.FromMilliseconds(750),
            TimeSpan.FromMilliseconds(1250)
        };
        private readonly FoundryBootstrap _bootstrap;
        private readonly string _fixedRegion;
        private DuelMatchTicket _activeTicket;
        private Dictionary<string, SessionProperty> _hostProperties;
        private bool _ownsWaitingRoom;
        private DateTime _waitingRoomCreatedUtc;

        public FusionDuelMatchmakingTransport(
            FoundryBootstrap bootstrap,
            string fixedRegion = "eu")
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            _fixedRegion = string.IsNullOrWhiteSpace(fixedRegion)
                ? "eu"
                : fixedRegion.Trim().ToLowerInvariant();
        }

        public async Task<DuelMatchAssignment> FindAsync(
            DuelMatchTicket ticket,
            int ratingBand,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureStableTicket(ticket);

            if (_ownsWaitingRoom)
            {
                return await ObserveWaitingRoomAsync(ratingBand, cancellationToken);
            }
            if (_bootstrap.State != FusionSessionState.Idle)
            {
                throw new InvalidOperationException("The Fusion session is busy.");
            }

            // Every search first publishes its own waiting room. Waiting rooms are
            // then paired deterministically in ObserveWaitingRoomAsync. This avoids
            // several simultaneous clients racing to join the same single host.
            var matchId = Guid.NewGuid().ToString("N");
            _hostProperties = DuelPhotonSessionProperties.Create(
                ticket,
                ratingBand,
                matchId,
                _fixedRegion);
            var hostResult = await _bootstrap.CreateMatchmakingSessionAsync(_hostProperties);
            if (!hostResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Photon matchmaking host failed: {hostResult.DiagnosticCode} {hostResult.DiagnosticMessage}");
            }
            _ownsWaitingRoom = true;
            _waitingRoomCreatedUtc = DateTime.UtcNow;
            Debug.Log(
                $"DUEL_MATCHMAKING_WAITING role=host session={_bootstrap.ActiveRoomCode} " +
                $"matchId={matchId} band={ratingBand} region={_fixedRegion} " +
                $"platform={ticket.Platform}");
            return null;
        }

        public async Task CancelAsync()
        {
            if (_ownsWaitingRoom && _bootstrap.State == FusionSessionState.Running)
            {
                await _bootstrap.LeaveSessionAsync();
            }
            _ownsWaitingRoom = false;
            _activeTicket = null;
            _hostProperties = null;
            _waitingRoomCreatedUtc = default;
        }

        private async Task<DuelMatchAssignment> ObserveWaitingRoomAsync(
            int ratingBand,
            CancellationToken cancellationToken)
        {
            if (_bootstrap.State != FusionSessionState.Running || _bootstrap.ActiveRunner == null)
            {
                throw new InvalidOperationException("The waiting matchmaking room stopped unexpectedly.");
            }

            _hostProperties[DuelPhotonSessionProperties.RatingBand] =
                Math.Max(RatingBandPolicy.InitialBand, Math.Min(RatingBandPolicy.MaximumBand, ratingBand));
            _bootstrap.ActiveRunner.SessionInfo.UpdateCustomProperties(
                new Dictionary<string, SessionProperty>
                {
                    [DuelPhotonSessionProperties.RatingBand] =
                        _hostProperties[DuelPhotonSessionProperties.RatingBand]
                });

            if (_bootstrap.ActiveRunner.ActivePlayers.Count() < 2)
            {
                if (DateTime.UtcNow - _waitingRoomCreatedUtc < WaitingRoomStabilization)
                {
                    return null;
                }
                var ownSessionName = _bootstrap.ActiveRoomCode;
                var sessions = await BrowseAndDisposeAsync(cancellationToken);
                var assignmentAfterBrowse = CompleteHostAssignmentIfOpponentArrived(ratingBand);
                if (assignmentAfterBrowse != null)
                {
                    return assignmentAfterBrowse;
                }
                Debug.Log(
                    $"DUEL_MATCHMAKING_LOBBY sessions={sessions.Count} band={ratingBand} " +
                    $"waiting={ownSessionName}");
                // Photon does not guarantee that a runner browsing from a second lobby
                // connection receives the room owned by this process in the first list update.
                // Always inject our known room into the deterministic queue and de-duplicate it.
                var queue = sessions
                    .Where(session => session != null && session.IsValid && session.IsVisible)
                    .Where(session => !string.Equals(
                        session.Name,
                        ownSessionName,
                        StringComparison.Ordinal))
                    .Where(session => DuelPhotonSessionProperties.IsCompatible(
                        session.Properties,
                        session.PlayerCount,
                        session.IsOpen,
                        _activeTicket,
                        ratingBand,
                        _fixedRegion))
                    .Select(session => new MatchmakingCandidate(
                        session.Name,
                        new Dictionary<string, SessionProperty>(session.Properties)))
                    .ToList();
                queue.Add(new MatchmakingCandidate(
                    ownSessionName,
                    new Dictionary<string, SessionProperty>(_hostProperties)));
                queue = queue
                    .OrderBy(candidate => ReadString(
                        candidate.Properties,
                        DuelPhotonSessionProperties.HostPlayerId), StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                    .ToList();
                var ownIndex = queue.FindIndex(candidate => string.Equals(
                    candidate.Name,
                    ownSessionName,
                    StringComparison.Ordinal));
                var earlierCandidate = ownIndex > 0 && ownIndex % 2 == 1
                    ? queue[ownIndex - 1]
                    : null;

                if (earlierCandidate == null)
                {
                    return CompleteHostAssignmentIfOpponentArrived(ratingBand);
                }

                var targetName = earlierCandidate.Name;
                var targetProperties = new Dictionary<string, SessionProperty>(earlierCandidate.Properties);
                Debug.Log(
                    $"DUEL_MATCHMAKING_RECONCILE from={ownSessionName} to={targetName} " +
                    $"band={ratingBand}");
                var assignmentBeforeLeave = CompleteHostAssignmentIfOpponentArrived(ratingBand);
                if (assignmentBeforeLeave != null)
                {
                    return assignmentBeforeLeave;
                }
                await _bootstrap.LeaveSessionAsync();
                _ownsWaitingRoom = false;

                var connectionToken = Guid.NewGuid().ToByteArray();
                var playerUniqueId = BitConverter.ToInt64(connectionToken, 0);
                if (playerUniqueId == 0L) playerUniqueId = 1L;
                var joinResult = await JoinMatchedRoomWithRetryAsync(
                    targetName,
                    playerUniqueId,
                    connectionToken,
                    cancellationToken);
                if (!joinResult.IsSuccess)
                {
                    _hostProperties = null;
                    _waitingRoomCreatedUtc = default;
                    Debug.LogWarning(
                        $"DUEL_MATCHMAKING_RETRY reason={joinResult.DiagnosticCode} " +
                        $"target={targetName}");
                    return null;
                }
                return DuelPhotonSessionProperties.ToAssignment(
                    targetName,
                    targetProperties,
                    _activeTicket.Mode,
                    _activeTicket.BuildId);
            }

            return CompleteHostAssignmentIfOpponentArrived(ratingBand);
        }

        private async Task<FusionSessionStartResult> JoinMatchedRoomWithRetryAsync(
            string sessionName,
            long playerUniqueId,
            byte[] connectionToken,
            CancellationToken cancellationToken)
        {
            FusionSessionStartResult lastResult = null;
            for (var attempt = 0; attempt < JoinRetryDelays.Length; attempt++)
            {
                await Task.Delay(JoinRetryDelays[attempt], cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                lastResult = await _bootstrap.JoinSessionAsync(
                    sessionName,
                    playerUniqueId,
                    connectionToken);
                if (lastResult.IsSuccess) return lastResult;

                Debug.LogWarning(
                    $"DUEL_MATCHMAKING_JOIN_RETRY target={sessionName} " +
                    $"attempt={attempt + 1}/{JoinRetryDelays.Length} " +
                    $"reason={lastResult.DiagnosticCode}");
            }

            return lastResult ?? FusionSessionStartResult.Failed(
                "The matched room could not be joined. Please try again.",
                "MissingJoinResult");
        }

        private DuelMatchAssignment CompleteHostAssignmentIfOpponentArrived(int ratingBand)
        {
            if (_bootstrap.State != FusionSessionState.Running ||
                _bootstrap.ActiveRunner == null ||
                _bootstrap.ActiveRunner.ActivePlayers.Count() < 2)
            {
                return null;
            }

            _bootstrap.ActiveRunner.SessionInfo.IsOpen = false;
            _bootstrap.ActiveRunner.SessionInfo.IsVisible = false;
            _ownsWaitingRoom = false;
            Debug.Log(
                $"DUEL_MATCHMAKING_MATCHED role=host session={_bootstrap.ActiveRoomCode} " +
                $"matchId={ReadString(_hostProperties, DuelPhotonSessionProperties.MatchId)} " +
                $"band={ratingBand} region={_fixedRegion} platform={_activeTicket.Platform}");
            return DuelPhotonSessionProperties.ToAssignment(
                _bootstrap.ActiveRoomCode,
                _hostProperties,
                _activeTicket.Mode,
                _activeTicket.BuildId);
        }

        private async Task<IReadOnlyList<SessionInfo>> BrowseAndDisposeAsync(
            CancellationToken cancellationToken)
        {
            var browserObject = new GameObject("Duel Photon Lobby Browser");
            var runner = browserObject.AddComponent<NetworkRunner>();
            var browser = browserObject.AddComponent<DuelPhotonLobbyBrowser>();
            try
            {
                return await browser.BrowseAsync(_fixedRegion, cancellationToken);
            }
            finally
            {
                if (runner != null && !runner.IsShutdown)
                {
                    runner.RemoveCallbacks(browser);
                    try
                    {
                        await runner.Shutdown(destroyGameObject: false);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"Photon lobby browser cleanup failed: {exception.Message}");
                    }
                }
                if (browserObject != null)
                {
                    UnityEngine.Object.Destroy(browserObject);
                }
            }
        }

        private void EnsureStableTicket(DuelMatchTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (_activeTicket == null)
            {
                _activeTicket = ticket;
                return;
            }
            if (!string.Equals(_activeTicket.TicketId, ticket.TicketId, StringComparison.Ordinal) ||
                !string.Equals(_activeTicket.Nonce, ticket.Nonce, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The active matchmaking ticket changed during search.");
            }
        }

        private static string ReadString(
            IReadOnlyDictionary<string, SessionProperty> properties,
            string key)
        {
            return properties != null && properties.TryGetValue(key, out var value) && value.IsString
                ? (string)value
                : string.Empty;
        }

        private sealed class MatchmakingCandidate
        {
            public MatchmakingCandidate(
                string name,
                Dictionary<string, SessionProperty> properties)
            {
                Name = name ?? string.Empty;
                Properties = properties ?? new Dictionary<string, SessionProperty>();
            }

            public string Name { get; }
            public Dictionary<string, SessionProperty> Properties { get; }
        }
    }
}
