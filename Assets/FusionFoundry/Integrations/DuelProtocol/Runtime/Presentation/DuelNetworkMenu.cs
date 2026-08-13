using System;
using System.Threading;
using System.Threading.Tasks;
using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Networking;
using DuelProtocol.Services;
using Fusion;
using FusionFoundry.Sessions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using FoundryBootstrap = FusionFoundry.Bootstrap.FusionBootstrap;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DuelNetworkMenu : MonoBehaviour
    {
        [SerializeField] private FoundryBootstrap bootstrap;
        private string _roomCode = string.Empty;
        private string _status = "Create a private duel or enter a six-character code.";
        private bool _busy;
        private bool _commandLineHandled;
        private bool _commandLineSession;
        private bool _automaticRematch;
        private int _lastAutomaticReadySequence = -1;
        private int _lastAutomaticRematchSequence = -1;
        private string _lastAutomaticCommandRoom = string.Empty;
        private long _playerUniqueId;
        private byte[] _connectionToken;
        private DuelMatchmakingService _matchmaking;
        private DuelIdentitySession _identity;
        private IDuelMatchTicketService _ticketService;
        private IDuelProgressionService _progressionService;
        private IDuelPlayerPreferencesService _preferencesService;
        private IDuelMatchResultService _resultService;
        private InMemoryDuelBackend _offlineBackend;
        private DuelProfilePanel _profilePanel;
        private DuelMatchSettlementCoordinator _settlementCoordinator;
        private string _localServicePlayerId = string.Empty;
        private CancellationTokenSource _searchCancellation;
        private int _searchRating = DuelRating.InitialRating;
        private DuelMatchMode _searchMode = DuelMatchMode.Unranked;
        private bool _productUiActive;
        private Task _identityInitialization;

        public FusionSessionState SessionState => bootstrap != null ? bootstrap.State : FusionSessionState.Idle;
        public string StatusText => _status;
        public string ActiveRoomCode => bootstrap != null ? bootstrap.ActiveRoomCode : string.Empty;
        public string IdentityText => _identity == null ? "Offline" : _identity.State.ToString();
        public string IdentityError => _identity?.ErrorCode ?? string.Empty;
        public bool Busy => _busy;
        public bool Searching => IsSearchActive();
        public int SearchRating => _searchRating;
        public int SearchBand => _matchmaking?.CurrentRatingBand ?? 0;
        public float SearchElapsed => (float)(_matchmaking?.ElapsedSeconds ?? 0d);
        public string SettlementText
        {
            get
            {
                if (_settlementCoordinator == null ||
                    string.IsNullOrWhiteSpace(_settlementCoordinator.LastStatus))
                {
                    return string.Empty;
                }
                var error = !string.IsNullOrWhiteSpace(_settlementCoordinator.LastErrorMessage)
                    ? _settlementCoordinator.LastErrorMessage
                    : _settlementCoordinator.LastErrorCode;
                return string.IsNullOrWhiteSpace(error)
                    ? $"Settlement: {_settlementCoordinator.LastStatus}"
                    : $"Settlement: {_settlementCoordinator.LastStatus} // {error}";
            }
        }

        public void Configure(FoundryBootstrap value)
        {
            bootstrap = value;
        }

        private void Start()
        {
            DuelArenaCameraRig.Ensure(Camera.main);
            DuelWorldArtDirector.EnhanceArena(GameObject.Find("Arena"));
            DuelWorldArtDirector.EnhanceLighting();
            _profilePanel = GetComponent<DuelProfilePanel>() ??
                            gameObject.AddComponent<DuelProfilePanel>();
            _settlementCoordinator = GetComponent<DuelMatchSettlementCoordinator>() ??
                                     gameObject.AddComponent<DuelMatchSettlementCoordinator>();
            _settlementCoordinator.Configure(_profilePanel);
            UseOfflineBackend();
            CreateMatchmakingService();
            _identity = new DuelIdentitySession(new UgsDuelIdentityService());
            _ = BeginIdentityInitialization();
            var productUi = GetComponent<DuelNetworkUi>() ?? gameObject.AddComponent<DuelNetworkUi>();
            productUi.Configure(this);
            _productUiActive = true;
            TryHandleCommandLine();
        }

        private void OnDestroy()
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _matchmaking?.Dispose();
            DuelNetworkSessionContext.Reset();
        }

        private void Update()
        {
            if (!_commandLineHandled)
            {
                TryHandleCommandLine();
            }
            ResetAutomaticCommandStateWhenRoomChanges();
            TrySubmitAutomaticReady();
            TrySubmitAutomaticRematch();
        }

        private void OnGUI()
        {
            if (_productUiActive)
            {
                return;
            }
            if (bootstrap == null)
            {
                return;
            }

            var width = Mathf.Min(560f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 18f, width, 310f), GUI.skin.box);
            GUILayout.Label("DUEL PROTOCOL — NETWORK ARENA");
            GUILayout.Label($"State: {bootstrap.State}  {_status}");
            GUILayout.Label(
                $"Identity: {_identity?.State.ToString() ?? "Offline"}" +
                (string.IsNullOrEmpty(_identity?.ErrorCode) ? string.Empty : $" • {_identity.ErrorCode}"));
            if (_settlementCoordinator != null && !string.IsNullOrEmpty(_settlementCoordinator.LastStatus))
            {
                GUILayout.Label(
                    $"Settlement: {_settlementCoordinator.LastStatus}" +
                    (string.IsNullOrEmpty(_settlementCoordinator.LastErrorCode)
                        ? string.Empty
                        : $" • {_settlementCoordinator.LastErrorCode}"));
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Profile / progression"))
            {
                _profilePanel?.ToggleVisible();
            }
            if (_identity != null && _identity.State == DuelIdentityState.Error && GUILayout.Button("Retry UGS"))
            {
                _ = InitializeIdentityAsync();
            }
            if (_identity != null && _identity.State == DuelIdentityState.SignedIn &&
                GUILayout.Button("Sync settings"))
            {
                _ = UploadPreferencesAsync();
            }
            GUILayout.EndHorizontal();

            if (bootstrap.State == FusionSessionState.Idle)
            {
                var searching = IsSearchActive();
                GUI.enabled = !_busy && !searching;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Find unranked opponent"))
                {
                    StartMatchmaking(DuelMatchMode.Unranked);
                }
                if (GUILayout.Button("Find ranked opponent"))
                {
                    StartMatchmaking(DuelMatchMode.Ranked);
                }
                GUILayout.EndHorizontal();
                if (searching)
                {
                    GUI.enabled = true;
                    GUILayout.Label(
                        $"Searching • rating {_searchRating} • ±{_matchmaking.CurrentRatingBand} • " +
                        $"{_matchmaking.ElapsedSeconds:0}s / 60s");
                    if (GUILayout.Button("Cancel matchmaking"))
                    {
                        CancelMatchmaking();
                    }
                }

                GUI.enabled = !_busy && !searching;
                if (GUILayout.Button("Create private two-player duel"))
                {
                    CreateRoom();
                }
                _roomCode = RoomCodeGenerator.SanitizeInput(
                    GUILayout.TextField(_roomCode, RoomCodeGenerator.CodeLength));
                if (GUILayout.Button("Join room"))
                {
                    JoinRoom(_roomCode);
                }
                GUI.enabled = true;
            }
            else if (bootstrap.State == FusionSessionState.Running)
            {
                GUILayout.Label($"Room: {bootstrap.ActiveRoomCode}");
                if (GUILayout.Button("Leave"))
                {
                    LeaveRoom();
                }
            }
            GUILayout.EndArea();

            var state = FindAnyObjectByType<DuelNetworkMatchState>();
            if (state == null)
            {
                return;
            }
            GUILayout.BeginArea(new Rect(16f, Screen.height - 120f, 420f, 104f), GUI.skin.box);
            GUILayout.Label($"Score {state.PlayerOneScore} — {state.PlayerTwoScore}");
            GUILayout.Label($"Phase: {state.Phase}");
            var seconds = state.MatchTimer.RemainingTime(state.Runner) ?? 0f;
            GUILayout.Label($"Time: {Mathf.CeilToInt(seconds)}s");
            if (state.Phase == DuelProtocol.Match.DuelMatchPhase.ReadyCheck && GUILayout.Button("Ready"))
            {
                FindAnyObjectByType<LocalDuelCommandSource>()?.RequestReady();
            }
            if (state.Phase == DuelProtocol.Match.DuelMatchPhase.Finished && GUILayout.Button("Request rematch"))
            {
                FindAnyObjectByType<LocalDuelCommandSource>()?.RequestRematch();
            }
            GUILayout.EndArea();
        }

        public void UiFindUnranked() => StartMatchmaking(DuelMatchMode.Unranked);
        public void UiFindRanked() => StartMatchmaking(DuelMatchMode.Ranked);
        public void UiCancelMatchmaking() => CancelMatchmaking();
        public void UiCreateRoom() => CreateRoom();
        public void UiJoinRoom(string roomCode) => JoinRoom(roomCode);
        public void UiLeaveRoom() => LeaveRoom();
        public void UiReady() => FindAnyObjectByType<LocalDuelCommandSource>()?.RequestReady();
        public void UiRematch() => FindAnyObjectByType<LocalDuelCommandSource>()?.RequestRematch();

        private async void CreateRoom()
        {
            if (_busy || bootstrap == null) return;
            _busy = true;
            _status = "Connecting to UGS...";
            if (!await EnsureOnlineBackendReadyAsync())
            {
                _busy = false;
                return;
            }
            _status = "Creating room...";
            if (!await PreparePrivateTicketAsync())
            {
                _busy = false;
                return;
            }
            var result = await bootstrap.CreateSessionAsync();
            _busy = false;
            _status = result.IsSuccess ? "Waiting for opponent." : result.UserMessage;
            if (result.IsSuccess)
            {
                Debug.Log($"DUEL_SESSION_READY mode=host room={bootstrap.ActiveRoomCode}");
            }
        }

        private async void JoinRoom(string code)
        {
            if (_busy || bootstrap == null) return;
            _busy = true;
            _status = "Connecting to UGS...";
            if (!await EnsureOnlineBackendReadyAsync())
            {
                _busy = false;
                return;
            }
            _status = "Joining room...";
            if (!await PreparePrivateTicketAsync())
            {
                _busy = false;
                return;
            }
            EnsureReconnectCredentials();
            var result = await bootstrap.JoinSessionAsync(code, _playerUniqueId, _connectionToken);
            _busy = false;
            _status = result.IsSuccess ? "Connected." : result.UserMessage;
            if (result.IsSuccess)
            {
                Debug.Log(
                    $"DUEL_SESSION_READY mode=client room={bootstrap.ActiveRoomCode} " +
                    $"playerUniqueId={_playerUniqueId}");
            }
        }

        private async void LeaveRoom()
        {
            if (_busy || bootstrap == null) return;
            _busy = true;
            await bootstrap.LeaveSessionAsync();
            DuelNetworkSessionContext.Reset();
            _busy = false;
            _status = "Session closed cleanly.";
            CreateMatchmakingService();
        }

        private void TryHandleCommandLine()
        {
            _commandLineHandled = true;
            var args = Environment.GetCommandLineArgs();
            if (HasArgument(args, "--duel-ai") &&
                !string.Equals(SceneManager.GetActiveScene().name, "DuelProtocol", StringComparison.Ordinal))
            {
                SceneManager.LoadScene("DuelProtocol");
                return;
            }
            var playerIdValue = GetArgumentValue(args, "--duel-player-id");
            if (!string.IsNullOrWhiteSpace(playerIdValue))
            {
                long.TryParse(playerIdValue, out _playerUniqueId);
            }
            var tokenValue = GetArgumentValue(args, "--duel-token");
            if (!string.IsNullOrWhiteSpace(tokenValue))
            {
                _connectionToken = TryParseHex(tokenValue);
            }
            _automaticRematch = HasArgument(args, "--duel-auto-rematch");
            var ratingValue = GetArgumentValue(args, "--duel-rating");
            if (int.TryParse(ratingValue, out var parsedRating))
            {
                _searchRating = Mathf.Max(0, parsedRating);
            }
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--duel-matchmaking", StringComparison.OrdinalIgnoreCase))
                {
                    _commandLineSession = true;
                    var modeValue = GetArgumentValue(args, "--duel-mode");
                    var mode = string.Equals(modeValue, "ranked", StringComparison.OrdinalIgnoreCase)
                        ? DuelMatchMode.Ranked
                        : DuelMatchMode.Unranked;
                    StartMatchmaking(mode);
                    return;
                }
                if (string.Equals(args[i], "--duel-host", StringComparison.OrdinalIgnoreCase))
                {
                    _commandLineSession = true;
                    CreateRoom();
                    return;
                }
                if (string.Equals(args[i], "--duel-client", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _commandLineSession = true;
                    JoinRoom(args[i + 1]);
                    return;
                }
            }
        }

        private void TrySubmitAutomaticReady()
        {
            if (!_commandLineSession || bootstrap == null ||
                bootstrap.State != FusionSessionState.Running)
            {
                return;
            }

            var state = FindAnyObjectByType<DuelNetworkMatchState>();
            if (state == null || state.Phase != DuelProtocol.Match.DuelMatchPhase.ReadyCheck ||
                _lastAutomaticReadySequence == state.MatchSequence)
            {
                return;
            }

            var commandSource = FindAnyObjectByType<LocalDuelCommandSource>();
            if (commandSource == null)
            {
                return;
            }
            commandSource.RequestReady();
            _lastAutomaticReadySequence = state.MatchSequence;
            Debug.Log($"DUEL_NETWORK_LOCAL_READY_REQUESTED sequence={state.MatchSequence}");
        }

        private void ResetAutomaticCommandStateWhenRoomChanges()
        {
            var activeRoom = bootstrap != null && bootstrap.State == FusionSessionState.Running
                ? bootstrap.ActiveRoomCode ?? string.Empty
                : string.Empty;
            if (string.Equals(
                    activeRoom,
                    _lastAutomaticCommandRoom,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastAutomaticCommandRoom = activeRoom;
            _lastAutomaticReadySequence = -1;
            _lastAutomaticRematchSequence = -1;
        }

        private void TrySubmitAutomaticRematch()
        {
            if (!_commandLineSession || !_automaticRematch || bootstrap == null ||
                bootstrap.State != FusionSessionState.Running)
            {
                return;
            }

            var state = FindAnyObjectByType<DuelNetworkMatchState>();
            if (state == null || state.Phase != DuelProtocol.Match.DuelMatchPhase.Finished ||
                _lastAutomaticRematchSequence == state.MatchSequence)
            {
                return;
            }

            var commandSource = FindAnyObjectByType<LocalDuelCommandSource>();
            if (commandSource == null)
            {
                return;
            }
            commandSource.RequestRematch();
            _lastAutomaticRematchSequence = state.MatchSequence;
            Debug.Log($"DUEL_NETWORK_LOCAL_REMATCH_REQUESTED sequence={state.MatchSequence}");
        }

        private void EnsureReconnectCredentials()
        {
            if (_connectionToken == null || _connectionToken.Length < 16)
            {
                _connectionToken = Guid.NewGuid().ToByteArray();
            }
            if (_playerUniqueId == 0L)
            {
                _playerUniqueId = BitConverter.ToInt64(_connectionToken, 0);
                if (_playerUniqueId == 0L) _playerUniqueId = 1L;
            }
        }

        private void CreateMatchmakingService()
        {
            _matchmaking?.Dispose();
            if (_ticketService == null)
            {
                UseOfflineBackend();
            }
            var transport = new FusionDuelMatchmakingTransport(bootstrap, "eu");
            _matchmaking = new DuelMatchmakingService(_ticketService, transport);
            _matchmaking.TicketCreated += HandleMatchmakingTicketCreated;
        }

        private void UseOfflineBackend()
        {
            var localPlayerId = _playerUniqueId == 0L
                ? $"local-{Guid.NewGuid():N}"
                : $"local-{_playerUniqueId}";
            _offlineBackend = new InMemoryDuelBackend(localPlayerId);
            _localServicePlayerId = localPlayerId;
            _ticketService = _offlineBackend;
            _progressionService = _offlineBackend;
            _preferencesService = _offlineBackend;
            _resultService = _offlineBackend;
            _profilePanel?.Configure(_progressionService, true);
        }

        private async Task InitializeIdentityAsync()
        {
            if (_identity == null)
            {
                return;
            }
            await _identity.InitializeAnonymousAsync("development");
            if (_identity.State == DuelIdentityState.SignedIn)
            {
                var onlineBackend = new UgsDuelBackend();
                _ticketService = onlineBackend;
                _progressionService = onlineBackend;
                _preferencesService = onlineBackend;
                _resultService = onlineBackend;
                _localServicePlayerId = _identity.PlayerId;
                _profilePanel?.Configure(_progressionService);
                _status = "UGS profile connected.";
                try
                {
                    await DuelProfilePreferencesSync.DownloadAsync(
                        _preferencesService,
                        FindAnyObjectByType<LocalDuelCommandSource>()?.InputActions);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"DUEL_UGS_PREFERENCES_DOWNLOAD_FAILED error={exception.GetType().Name} " +
                        $"message={exception.Message}");
                }
                if (!IsSearchActive()) CreateMatchmakingService();
            }
            else
            {
                UseOfflineBackend();
                _status = $"UGS unavailable ({_identity.ErrorCode}); online play is disabled.";
                if (!IsSearchActive()) CreateMatchmakingService();
            }
        }

        private async Task UploadPreferencesAsync()
        {
            try
            {
                await DuelProfilePreferencesSync.UploadAsync(
                    _preferencesService,
                    FindAnyObjectByType<LocalDuelCommandSource>()?.InputActions);
                _status = "Control and accessibility settings synced.";
            }
            catch (Exception exception)
            {
                _status = $"Settings sync failed: {exception.GetType().Name}";
            }
        }

        private async void StartMatchmaking(DuelMatchMode mode)
        {
            if (_busy || IsSearchActive() || bootstrap == null)
            {
                return;
            }
            _busy = true;
            _status = "Connecting to UGS...";
            if (!await EnsureOnlineBackendReadyAsync())
            {
                _busy = false;
                return;
            }
            _busy = false;
            if (_matchmaking == null)
            {
                CreateMatchmakingService();
            }

            _searchMode = mode;
            _searchCancellation = new CancellationTokenSource();
            _status = mode == DuelMatchMode.Ranked
                ? "Searching for a ranked opponent..."
                : "Searching for an unranked opponent...";
            var playerId = _localServicePlayerId;
            var buildId = CurrentBuildId();
            var platform = Application.platform.ToString();
            var inputType = CurrentInputType();
            Debug.Log(
                $"DUEL_MATCHMAKING_SEARCH_STARTED mode={mode} rating={_searchRating} " +
                $"build={buildId} platform={platform} input={inputType}");
            try
            {
                var assignment = await _matchmaking.SearchAsync(
                    playerId,
                    mode,
                    _searchRating,
                    buildId,
                    platform,
                    _searchCancellation.Token);
                if (assignment == null)
                {
                    _status = "No compatible opponent was found within 60 seconds.";
                    Debug.Log(
                        $"DUEL_MATCHMAKING_TIMEOUT searchId={_matchmaking.SearchId} " +
                        $"elapsed={_matchmaking.ElapsedSeconds:0.000}");
                }
                else
                {
                    _status = $"Matched • {assignment.MatchId}";
                    Debug.Log(
                        $"DUEL_MATCHMAKING_COMPLETED searchId={_matchmaking.SearchId} " +
                        $"matchId={assignment.MatchId} session={assignment.SessionName} " +
                        $"build={buildId} elapsed={_matchmaking.ElapsedSeconds:0.000} " +
                        $"band={_matchmaking.CurrentRatingBand} " +
                        $"mode={_searchMode} platform={platform} input={inputType}");
                }
            }
            catch (OperationCanceledException)
            {
                _status = "Matchmaking cancelled.";
                Debug.Log(
                    $"DUEL_MATCHMAKING_CANCELLED searchId={_matchmaking.SearchId} " +
                    $"elapsed={_matchmaking.ElapsedSeconds:0.000}");
            }
            catch (Exception exception)
            {
                _status = $"Matchmaking failed: {exception.Message}";
                Debug.LogError(
                    $"DUEL_MATCHMAKING_FAILED searchId={_matchmaking.SearchId} " +
                    $"elapsed={_matchmaking.ElapsedSeconds:0.000} " +
                    $"error={exception.GetType().Name}");
            }
            finally
            {
                _searchCancellation?.Dispose();
                _searchCancellation = null;
            }
        }

        private void CancelMatchmaking()
        {
            _matchmaking?.Cancel();
            _searchCancellation?.Cancel();
        }

        private static string CurrentInputType()
        {
            InputDevice mostRecent = null;
            var mostRecentTime = 0d;
            foreach (var device in InputSystem.devices)
            {
                if (!(device is Keyboard) && !(device is Mouse) &&
                    !(device is Gamepad) && !(device is Touchscreen))
                {
                    continue;
                }
                if (device.lastUpdateTime <= mostRecentTime)
                {
                    continue;
                }
                mostRecent = device;
                mostRecentTime = device.lastUpdateTime;
            }

            if (mostRecent is Touchscreen || Application.isMobilePlatform)
            {
                return "touch";
            }
            if (mostRecent is Gamepad)
            {
                return "gamepad";
            }
            return "keyboard_mouse";
        }

        private async Task<bool> PreparePrivateTicketAsync()
        {
            DuelNetworkSessionContext.Configure(
                _localServicePlayerId,
                DuelMatchMode.Private,
                _searchRating,
                CurrentBuildId(),
                Application.platform.ToString(),
                _ticketService,
                _resultService);
            try
            {
                var ticket = await DuelNetworkSessionContext.EnsureTicketAsync();
                if (ticket != null) return true;
                _status = "A private match ticket could not be created.";
            }
            catch (Exception exception)
            {
                _status = $"Private match ticket failed: {exception.GetType().Name}";
            }
            return false;
        }

        private void HandleMatchmakingTicketCreated(DuelMatchTicket ticket)
        {
            DuelNetworkSessionContext.Configure(
                ticket.PlayerId,
                ticket.Mode,
                ticket.Rating,
                ticket.BuildId,
                ticket.Platform,
                _ticketService,
                _resultService,
                ticket);
        }

        private Task BeginIdentityInitialization()
        {
            if (_identity?.State == DuelIdentityState.SignedIn &&
                _ticketService is UgsDuelBackend &&
                _resultService is UgsDuelBackend)
            {
                return Task.CompletedTask;
            }
            if (_identityInitialization == null || _identityInitialization.IsCompleted)
            {
                _identityInitialization = InitializeIdentityAsync();
            }
            return _identityInitialization;
        }

        private async Task<bool> EnsureOnlineBackendReadyAsync()
        {
            try
            {
                await BeginIdentityInitialization();
            }
            catch (Exception exception)
            {
                _status = $"UGS initialization failed: {exception.Message}";
                Debug.LogWarning(
                    $"DUEL_UGS_REQUIRED error={exception.GetType().Name} message={exception.Message}");
                return false;
            }

            if (_identity?.State == DuelIdentityState.SignedIn &&
                _ticketService is UgsDuelBackend &&
                _resultService is UgsDuelBackend)
            {
                return true;
            }

            var error = string.IsNullOrWhiteSpace(_identity?.ErrorMessage)
                ? _identity?.ErrorCode
                : _identity.ErrorMessage;
            _status = string.IsNullOrWhiteSpace(error)
                ? "UGS sign-in is required for online play."
                : $"UGS sign-in failed: {error}";
            return false;
        }

        private static string CurrentBuildId()
        {
            return string.IsNullOrWhiteSpace(Application.buildGUID)
                ? Application.version
                : Application.buildGUID;
        }

        private bool IsSearchActive()
        {
            return _matchmaking != null &&
                   (_matchmaking.State == DuelSearchState.CreatingTicket ||
                    _matchmaking.State == DuelSearchState.Searching);
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static bool HasArgument(string[] args, string name)
        {
            foreach (var argument in args)
            {
                if (string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static byte[] TryParseHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length % 2 != 0) return null;
            var bytes = new byte[value.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(
                        value.Substring(i * 2, 2),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out bytes[i]))
                {
                    return null;
                }
            }
            return bytes;
        }
    }
}
