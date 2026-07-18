using System;
using Fusion;

namespace FusionFoundry.Sessions
{
    public sealed class FusionSessionRequest
    {
        private FusionSessionRequest(GameMode mode, string sessionName, int? maxPlayers)
        {
            Mode = mode;
            SessionName = sessionName;
            MaxPlayers = maxPlayers;
        }

        public GameMode Mode { get; }

        public string SessionName { get; }

        public int? MaxPlayers { get; }

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

            return new FusionSessionRequest(GameMode.Host, sessionName, maxPlayers);
        }

        public static FusionSessionRequest ForClient(string sessionName)
        {
            ValidateSessionName(sessionName);
            return new FusionSessionRequest(GameMode.Client, sessionName, null);
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

