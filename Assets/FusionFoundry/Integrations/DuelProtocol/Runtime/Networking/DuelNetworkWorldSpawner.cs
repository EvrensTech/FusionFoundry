using System;
using System.Collections.Generic;
using System.Globalization;
using Fusion;
using FusionFoundry.Sessions;
using DuelProtocol.Match;
using UnityEngine;

namespace DuelProtocol.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class DuelNetworkWorldSpawner : NetworkRunnerCallbacksBehaviour
    {
        [SerializeField] private NetworkObject worldPrefab;
        [SerializeField, Min(1f)] private float regulationSeconds = 180f;
        [SerializeField, Min(1f)] private float reconnectGraceSeconds = 20f;
        private NetworkObject _world;
        private string _pendingReconnectToken = string.Empty;
        private float _pendingReconnectDeadline;
        private int _pendingWinnerIndex = -1;
        private readonly Dictionary<PlayerRef, string> _playerTokens =
            new Dictionary<PlayerRef, string>();

        public NetworkObject WorldPrefab => worldPrefab;
        public NetworkObject SpawnedWorld => _world;

        public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                var connectionToken = GetConnectionTokenKey(runner, player);
                if (!string.IsNullOrEmpty(connectionToken)) _playerTokens[player] = connectionToken;
            }
            if (runner.IsServer && _world != null && !string.IsNullOrEmpty(_pendingReconnectToken))
            {
                var joinedToken = GetConnectionTokenKey(runner, player);
                if (joinedToken == _pendingReconnectToken)
                {
                    _pendingReconnectToken = string.Empty;
                    _pendingReconnectDeadline = 0f;
                    _pendingWinnerIndex = -1;
                    _world.GetComponent<DuelNetworkMatchState>()?.ResumeAfterReconnect();
                    return;
                }
            }

            if (!runner.IsServer || _world != null || CountPlayers(runner) != 2 || worldPrefab == null)
            {
                return;
            }

            _world = runner.Spawn(worldPrefab, new Vector3(0f, 0.6f, 0f), Quaternion.identity);
            var matchState = _world != null ? _world.GetComponent<DuelNetworkMatchState>() : null;
            matchState?.PrepareReadyCheck(
                ReadVerificationSeconds("--duel-match-seconds", regulationSeconds, 1f),
                ReadVerificationSeconds("--duel-overtime-seconds", -1f, 0f));
            Debug.Log($"DUEL_NETWORK_READY_CHECK_STARTED tick={runner.Tick.Raw} players=2 world={_world.Id}");
        }

        public override void OnConnectRequest(
            NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
            if (!runner.IsServer)
            {
                request.Refuse();
                return;
            }

            if (_world == null && CountPlayers(runner) < 2)
            {
                request.Accept();
                return;
            }

            var tokenKey = token == null || token.Length == 0
                ? string.Empty
                : Convert.ToBase64String(token);
            if (!string.IsNullOrEmpty(_pendingReconnectToken) && tokenKey == _pendingReconnectToken)
            {
                request.Accept();
                return;
            }

            Debug.LogWarning("DUEL_NETWORK_CONNECTION_REFUSED reason=ActivePlayerLimitOrInvalidReconnectToken");
            request.Refuse();
        }

        public override void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"DUEL_NETWORK_PLAYER_LEFT tick={runner.Tick.Raw} player={player}");
            if (!runner.IsServer || _world == null)
            {
                return;
            }
            _playerTokens.TryGetValue(player, out var tokenKey);
            _playerTokens.Remove(player);
            if (string.IsNullOrEmpty(tokenKey)) tokenKey = GetConnectionTokenKey(runner, player);
            if (string.IsNullOrEmpty(tokenKey))
            {
                FinishForfeit(runner);
                return;
            }

            _pendingReconnectToken = tokenKey;
            _pendingReconnectDeadline = Time.unscaledTime + reconnectGraceSeconds;
            _pendingWinnerIndex = GetOnlyPlayerIndex(runner);
            _world.GetComponent<DuelNetworkMatchState>()?.BeginReconnectPause();
            Debug.Log(
                $"DUEL_NETWORK_RECONNECT_RESERVED tick={runner.Tick.Raw} player={player} " +
                $"seconds={reconnectGraceSeconds:0}");
        }

        public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _world = null;
            _pendingReconnectToken = string.Empty;
            _pendingReconnectDeadline = 0f;
            _pendingWinnerIndex = -1;
            _playerTokens.Clear();
        }

        private void Update()
        {
            if (_world == null || string.IsNullOrEmpty(_pendingReconnectToken) ||
                Time.unscaledTime < _pendingReconnectDeadline)
            {
                return;
            }

            var state = _world.GetComponent<DuelNetworkMatchState>();
            state?.Finish(_pendingWinnerIndex, DuelTerminationReason.ClientForfeit);
            Debug.Log($"DUEL_NETWORK_RECONNECT_EXPIRED winner={_pendingWinnerIndex}");
            _pendingReconnectToken = string.Empty;
            _pendingReconnectDeadline = 0f;
            _pendingWinnerIndex = -1;
        }

        private void FinishForfeit(NetworkRunner runner)
        {
            var remaining = GetOnlyPlayerIndex(runner);
            _world.GetComponent<DuelNetworkMatchState>()?.Finish(
                remaining,
                DuelTerminationReason.ClientForfeit);
        }

        private static int CountPlayers(NetworkRunner runner)
        {
            var count = 0;
            foreach (var unused in runner.ActivePlayers)
            {
                count++;
            }
            return count;
        }

        private static int GetOnlyPlayerIndex(NetworkRunner runner)
        {
            foreach (var player in runner.ActivePlayers)
            {
                return player.AsIndex - 1;
            }
            return -1;
        }

        private static string GetConnectionTokenKey(NetworkRunner runner, PlayerRef player)
        {
            var token = runner.GetPlayerConnectionToken(player);
            return token == null || token.Length == 0
                ? string.Empty
                : Convert.ToBase64String(token);
        }

        private static float ReadVerificationSeconds(string argumentName, float fallback, float minimum)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
            {
                if (!string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase)) continue;
                if (float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    return Mathf.Max(minimum, value);
                }
            }
            return fallback;
        }
    }
}
