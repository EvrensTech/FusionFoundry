using System;
using DuelProtocol.Match;
using DuelProtocol.Services;
using Fusion;
using UnityEngine;

namespace DuelProtocol.Networking
{
    // Match result is emitted once by state authority.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DuelNetworkMatchState : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float overtimeSeconds = 60f;
        private int _lastDiagnosticTick = int.MinValue;
        [Networked] public DuelMatchPhase Phase { get; set; }
        [Networked] public int PlayerOneScore { get; set; }
        [Networked] public int PlayerTwoScore { get; set; }
        [Networked] public TickTimer MatchTimer { get; set; }
        [Networked] public DuelTerminationReason TerminationReason { get; set; }
        [Networked] public int WinnerIndex { get; set; }
        [Networked] public NetworkBool PlayerOneReady { get; set; }
        [Networked] public NetworkBool PlayerTwoReady { get; set; }
        [Networked] public NetworkBool PlayerOneRematch { get; set; }
        [Networked] public NetworkBool PlayerTwoRematch { get; set; }
        [Networked] public TickTimer RematchWindow { get; set; }
        [Networked] public float RegulationSeconds { get; set; }
        [Networked] public int MatchSequence { get; set; }
        [Networked] public DuelMatchPhase PhaseBeforeReconnect { get; set; }
        [Networked] public float ReconnectResumeSeconds { get; set; }
        [Networked] public NetworkString<_32> MatchId { get; set; }
        [Networked] public long StartedUnixSeconds { get; set; }
        [Networked] public long EndedUnixSeconds { get; set; }
        [Networked] public float PlayerOneCarrySeconds { get; set; }
        [Networked] public float PlayerTwoCarrySeconds { get; set; }

        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            Phase = DuelMatchPhase.ReadyCheck;
            WinnerIndex = -1;
            TerminationReason = DuelTerminationReason.None;
            MatchSequence = 1;
            MatchId = Guid.NewGuid().ToString("N");
        }

        public override void FixedUpdateNetwork()
        {
            EmitDiagnostics();
            if (HasStateAuthority && Phase == DuelMatchPhase.Finished &&
                RematchWindow.IsRunning && RematchWindow.Expired(Runner))
            {
                PlayerOneRematch = false;
                PlayerTwoRematch = false;
                RematchWindow = TickTimer.None;
                Debug.Log($"DUEL_NETWORK_REMATCH_EXPIRED tick={Runner.Tick.Raw} sequence={MatchSequence}");
            }
            if (!HasStateAuthority || Phase == DuelMatchPhase.Finished || !MatchTimer.Expired(Runner)) return;
            if (PlayerOneScore != PlayerTwoScore)
            {
                Finish(PlayerOneScore > PlayerTwoScore ? 0 : 1, DuelTerminationReason.TimeExpired);
            }
            else if (Phase == DuelMatchPhase.Playing && overtimeSeconds > 0f)
            {
                Phase = DuelMatchPhase.Overtime;
                MatchTimer = TickTimer.CreateFromSeconds(Runner, overtimeSeconds);
            }
            else
            {
                Finish(-1, DuelTerminationReason.Draw);
            }
        }

        public override void Render()
        {
            EmitDiagnostics();
        }

        private void EmitDiagnostics()
        {
            var tick = Runner.Tick.Raw;
            if (_lastDiagnosticTick != int.MinValue && tick - _lastDiagnosticTick < 300) return;
            _lastDiagnosticTick = tick;
            Debug.Log(
                $"DUEL_NETWORK_STATE role={(HasStateAuthority ? "authority" : "proxy")} " +
                $"tick={tick} phase={Phase} score={PlayerOneScore}:{PlayerTwoScore} " +
                $"winner={WinnerIndex} reason={TerminationReason} sequence={MatchSequence} " +
                $"ready={(bool)PlayerOneReady}:{(bool)PlayerTwoReady}");
        }

        public void PrepareReadyCheck(float seconds, float overtimeOverride = -1f)
        {
            if (!HasStateAuthority || Phase == DuelMatchPhase.Finished) return;
            RegulationSeconds = Mathf.Max(1f, seconds);
            if (overtimeOverride >= 0f) overtimeSeconds = overtimeOverride;
            PlayerOneReady = false;
            PlayerTwoReady = false;
            MatchTimer = TickTimer.None;
            Phase = DuelMatchPhase.ReadyCheck;
        }

        public void ApplyReadyFromInput(PlayerRef player)
        {
            if (!HasStateAuthority || Phase != DuelMatchPhase.ReadyCheck || !IsActivePlayer(player)) return;
            var index = player.AsIndex - 1;
            var changed = false;
            if (index == 0 && !PlayerOneReady) { PlayerOneReady = true; changed = true; }
            else if (index == 1 && !PlayerTwoReady) { PlayerTwoReady = true; changed = true; }
            else return;

            if (changed)
            {
                Debug.Log($"DUEL_NETWORK_READY tick={Runner.Tick.Raw} player={player} " +
                          $"ready={(bool)PlayerOneReady}:{(bool)PlayerTwoReady}");
            }
            if (PlayerOneReady && PlayerTwoReady)
            {
                StartMatch(RegulationSeconds);
            }
        }

        public bool IsReady(PlayerRef player)
        {
            var index = player.AsIndex - 1;
            return index == 0 ? PlayerOneReady : index == 1 && PlayerTwoReady;
        }

        public bool IsRematchRequested(PlayerRef player)
        {
            var index = player.AsIndex - 1;
            return index == 0 ? PlayerOneRematch : index == 1 && PlayerTwoRematch;
        }

        private void StartMatch(float seconds)
        {
            if (!HasStateAuthority || Phase != DuelMatchPhase.ReadyCheck) return;
            Phase = DuelMatchPhase.Playing;
            StartedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            EndedUnixSeconds = 0;
            PlayerOneCarrySeconds = 0f;
            PlayerTwoCarrySeconds = 0f;
            MatchTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, seconds));
            Debug.Log($"DUEL_NETWORK_READY_GATE_PASSED tick={Runner.Tick.Raw} sequence={MatchSequence}");
        }

        public void ApplyRematchFromInput(PlayerRef player)
        {
            if (!HasStateAuthority || Phase != DuelMatchPhase.Finished || !IsActivePlayer(player)) return;
            if (!RematchWindow.IsRunning)
            {
                RematchWindow = TickTimer.CreateFromSeconds(Runner, 15f);
            }

            var index = player.AsIndex - 1;
            if (index == 0 && !PlayerOneRematch) PlayerOneRematch = true;
            else if (index == 1 && !PlayerTwoRematch) PlayerTwoRematch = true;
            else return;

            Debug.Log($"DUEL_NETWORK_REMATCH_REQUEST tick={Runner.Tick.Raw} player={player} " +
                      $"accepted={(bool)PlayerOneRematch}:{(bool)PlayerTwoRematch}");
            if (PlayerOneRematch && PlayerTwoRematch)
            {
                BeginRematch();
            }
        }

        private void BeginRematch()
        {
            PlayerOneScore = 0;
            PlayerTwoScore = 0;
            WinnerIndex = -1;
            TerminationReason = DuelTerminationReason.None;
            PlayerOneReady = false;
            PlayerTwoReady = false;
            PlayerOneRematch = false;
            PlayerTwoRematch = false;
            RematchWindow = TickTimer.None;
            MatchTimer = TickTimer.None;
            MatchSequence++;
            MatchId = Guid.NewGuid().ToString("N");
            StartedUnixSeconds = 0;
            EndedUnixSeconds = 0;
            PlayerOneCarrySeconds = 0f;
            PlayerTwoCarrySeconds = 0f;
            Phase = DuelMatchPhase.ReadyCheck;
            Debug.Log($"DUEL_NETWORK_REMATCH_STARTED tick={Runner.Tick.Raw} sequence={MatchSequence}");
        }

        public bool TryAwardPoint(int playerIndex, int scoreLimit)
        {
            if (!HasStateAuthority || Phase == DuelMatchPhase.Finished) return false;
            if (playerIndex == 0) PlayerOneScore++;
            else if (playerIndex == 1) PlayerTwoScore++;
            else return false;

            if (PlayerOneScore >= scoreLimit || PlayerTwoScore >= scoreLimit || Phase == DuelMatchPhase.Overtime)
            {
                Finish(playerIndex, Phase == DuelMatchPhase.Overtime
                    ? DuelTerminationReason.OvertimeScore
                    : DuelTerminationReason.ScoreLimit);
            }
            return true;
        }

        public void RecordCarry(PlayerRef player, float seconds)
        {
            if (!HasStateAuthority || Phase == DuelMatchPhase.Finished || seconds <= 0f)
            {
                return;
            }
            var index = player.AsIndex - 1;
            if (index == 0) PlayerOneCarrySeconds += seconds;
            else if (index == 1) PlayerTwoCarrySeconds += seconds;
        }

        public DuelMatchRecord CreateMatchRecord(DuelMatchTicket ticket)
        {
            if (ticket == null || Phase != DuelMatchPhase.Finished ||
                StartedUnixSeconds <= 0 || EndedUnixSeconds < StartedUnixSeconds)
            {
                return null;
            }
            var playerOneId = GetServicePlayerId(0);
            var playerTwoId = GetServicePlayerId(1);
            if (string.IsNullOrWhiteSpace(playerOneId) || string.IsNullOrWhiteSpace(playerTwoId))
            {
                return null;
            }
            return new DuelMatchRecord
            {
                MatchId = MatchId.ToString(),
                TicketId = ticket.TicketId,
                Mode = ticket.Mode,
                PlayerOneId = playerOneId,
                PlayerTwoId = playerTwoId,
                PlayerOneScore = PlayerOneScore,
                PlayerTwoScore = PlayerTwoScore,
                WinnerIndex = WinnerIndex,
                TerminationReason = TerminationReason,
                StartedUnixSeconds = StartedUnixSeconds,
                EndedUnixSeconds = EndedUnixSeconds,
                Nonce = ticket.Nonce,
                PlayerOneCarrySeconds = PlayerOneCarrySeconds,
                PlayerTwoCarrySeconds = PlayerTwoCarrySeconds
            };
        }

        public void BeginReconnectPause()
        {
            if (!HasStateAuthority || Phase == DuelMatchPhase.Finished ||
                Phase == DuelMatchPhase.ReconnectPause)
            {
                return;
            }
            PhaseBeforeReconnect = Phase;
            ReconnectResumeSeconds = MatchTimer.RemainingTime(Runner) ?? 0f;
            MatchTimer = TickTimer.None;
            Phase = DuelMatchPhase.ReconnectPause;
            Debug.Log(
                $"DUEL_NETWORK_RECONNECT_PAUSED tick={Runner.Tick.Raw} " +
                $"resumePhase={PhaseBeforeReconnect} remaining={ReconnectResumeSeconds:0.000}");
        }

        public void ResumeAfterReconnect()
        {
            if (!HasStateAuthority || Phase != DuelMatchPhase.ReconnectPause) return;
            Phase = PhaseBeforeReconnect;
            if ((Phase == DuelMatchPhase.Playing || Phase == DuelMatchPhase.Overtime) &&
                ReconnectResumeSeconds > 0f)
            {
                MatchTimer = TickTimer.CreateFromSeconds(Runner, ReconnectResumeSeconds);
            }
            Debug.Log(
                $"DUEL_NETWORK_RECONNECTED tick={Runner.Tick.Raw} phase={Phase} " +
                $"remaining={ReconnectResumeSeconds:0.000}");
        }

        public void Finish(int winnerIndex, DuelTerminationReason reason)
        {
            if (!HasStateAuthority || Phase == DuelMatchPhase.Finished) return;
            WinnerIndex = winnerIndex;
            TerminationReason = reason;
            EndedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Phase = DuelMatchPhase.Finished;
            MatchTimer = TickTimer.None;
            Debug.Log(
                $"DUEL_NETWORK_RESULT tick={Runner.Tick.Raw} winner={WinnerIndex} " +
                $"reason={TerminationReason} sequence={MatchSequence}");
        }

        private bool IsActivePlayer(PlayerRef player)
        {
            if (player == PlayerRef.None) return false;
            foreach (var activePlayer in Runner.ActivePlayers)
            {
                if (activePlayer == player) return true;
            }
            return false;
        }

        private string GetServicePlayerId(int index)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (player.AsIndex - 1 != index) continue;
                var playerObject = Runner.GetPlayerObject(player);
                var networkPlayer = playerObject?.GetComponent<DuelNetworkPlayer>();
                return networkPlayer?.ServicePlayerId.ToString() ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
