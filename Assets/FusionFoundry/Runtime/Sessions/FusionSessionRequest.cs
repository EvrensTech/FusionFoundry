using System;
using System.Collections.Generic;
using Fusion;

namespace FusionFoundry.Sessions
{
    public sealed class FusionSessionRequest
    {
        private FusionSessionRequest(
            GameMode mode,
            string sessionName,
            int? maxPlayers,
            long playerUniqueId,
            byte[] connectionToken,
            IReadOnlyDictionary<string, SessionProperty> sessionProperties,
            bool isVisible,
            bool isOpen,
            bool reserveReconnectSlot)
        {
            Mode = mode;
            SessionName = sessionName;
            MaxPlayers = maxPlayers;
            PlayerUniqueId = playerUniqueId;
            _connectionToken = connectionToken == null ? null : (byte[])connectionToken.Clone();
            _sessionProperties = sessionProperties == null
                ? null
                : new Dictionary<string, SessionProperty>(sessionProperties);
            IsVisible = isVisible;
            IsOpen = isOpen;
            ReserveReconnectSlot = reserveReconnectSlot;
        }

        public GameMode Mode { get; }

        public string SessionName { get; }

        public int? MaxPlayers { get; }

        public long PlayerUniqueId { get; }

        public bool IsVisible { get; }

        public bool IsOpen { get; }

        public bool ReserveReconnectSlot { get; }

        public IReadOnlyDictionary<string, SessionProperty> SessionProperties =>
            _sessionProperties == null
                ? null
                : new Dictionary<string, SessionProperty>(_sessionProperties);

        public byte[] ConnectionToken => _connectionToken == null
            ? null
            : (byte[])_connectionToken.Clone();

        private readonly byte[] _connectionToken;
        private readonly Dictionary<string, SessionProperty> _sessionProperties;

        public static FusionSessionRequest ForHost(string sessionName, int maxPlayers)
        {
            ValidateSessionName(sessionName);

            if (maxPlayers <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers),
                    maxPlayers,
                    "Maximum player count must be greater than zero.");
            }

            return new FusionSessionRequest(
                GameMode.Host, sessionName, maxPlayers, 0L, null, null, false, true, true);
        }

        public static FusionSessionRequest ForMatchmakingHost(
            string sessionName,
            int maxPlayers,
            IReadOnlyDictionary<string, SessionProperty> sessionProperties)
        {
            ValidateSessionName(sessionName);
            if (maxPlayers <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers),
                    maxPlayers,
                    "Maximum player count must be greater than zero.");
            }
            if (sessionProperties == null || sessionProperties.Count == 0)
            {
                throw new ArgumentException(
                    "Matchmaking session properties are required.",
                    nameof(sessionProperties));
            }

            return new FusionSessionRequest(
                GameMode.Host,
                sessionName,
                maxPlayers,
                0L,
                null,
                sessionProperties,
                true,
                true,
                false);
        }

        public static FusionSessionRequest ForClient(string sessionName)
        {
            ValidateSessionName(sessionName);
            return new FusionSessionRequest(
                GameMode.Client, sessionName, null, 0L, null, null, false, true, false);
        }

        public static FusionSessionRequest ForClient(
            string sessionName,
            long playerUniqueId,
            byte[] connectionToken)
        {
            ValidateSessionName(sessionName);
            if (playerUniqueId == 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(playerUniqueId));
            }
            if (connectionToken == null || connectionToken.Length < 16)
            {
                throw new ArgumentException(
                    "Connection token must contain at least 16 bytes.",
                    nameof(connectionToken));
            }
            return new FusionSessionRequest(
                GameMode.Client,
                sessionName,
                null,
                playerUniqueId,
                connectionToken,
                null,
                false,
                true,
                false);
        }

        private static void ValidateSessionName(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
            {
                throw new ArgumentException(
                    "Session name cannot be null, empty, or whitespace.",
                    nameof(sessionName));
            }
        }
    }
}

