using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DuelProtocol.Networking
{
    // Project-specific Fusion input contract; woven by FusionFoundry.Integrations.DuelProtocol.
    public struct DuelNetworkInputData : INetworkInput
    {
        public Vector2 Movement;
        public Vector2 Aim;
        public NetworkBool AbilityPressed;
        public NetworkBool ReadyRequested;
        public NetworkBool RematchRequested;
        public NetworkString<_64> ServicePlayerId;
    }

    public sealed class DuelReadyCoordinator
    {
        private readonly HashSet<string> _participants = new HashSet<string>();
        private readonly HashSet<string> _readyPlayers = new HashSet<string>();
        private readonly HashSet<string> _rematchPlayers = new HashSet<string>();
        private DateTime? _rematchWindowOpenedUtc;

        public int ParticipantCount => _participants.Count;
        public bool CanStart => _participants.Count == 2 && _readyPlayers.Count == 2;
        public bool CanRematch => _participants.Count == 2 && _rematchPlayers.Count == 2;

        public bool AddParticipant(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                   _participants.Count < 2 &&
                   _participants.Add(playerId);
        }

        public bool RemoveParticipant(string playerId)
        {
            _readyPlayers.Remove(playerId);
            _rematchPlayers.Remove(playerId);
            if (_rematchPlayers.Count == 0) _rematchWindowOpenedUtc = null;
            return _participants.Remove(playerId);
        }

        public bool SetReady(string playerId, bool ready)
        {
            if (!_participants.Contains(playerId)) return false;
            if (ready) _readyPlayers.Add(playerId);
            else _readyPlayers.Remove(playerId);
            return true;
        }

        public bool RequestRematch(string playerId)
        {
            return RequestRematch(playerId, DateTime.UtcNow);
        }

        public bool RequestRematch(string playerId, DateTime utcNow)
        {
            if (!_participants.Contains(playerId)) return false;
            if (_rematchWindowOpenedUtc.HasValue &&
                (utcNow - _rematchWindowOpenedUtc.Value).TotalSeconds > 15d)
            {
                _rematchPlayers.Clear();
                _rematchWindowOpenedUtc = null;
            }
            if (!_rematchWindowOpenedUtc.HasValue) _rematchWindowOpenedUtc = utcNow;
            return _rematchPlayers.Add(playerId);
        }

        public string BeginRematch()
        {
            if (!CanRematch) return string.Empty;
            _readyPlayers.Clear();
            _rematchPlayers.Clear();
            _rematchWindowOpenedUtc = null;
            return Guid.NewGuid().ToString("N");
        }
    }
}
