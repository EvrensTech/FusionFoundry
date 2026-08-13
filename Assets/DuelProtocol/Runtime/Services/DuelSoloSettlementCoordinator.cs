using System;
using System.Threading.Tasks;
using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Presentation;
using UnityEngine;

namespace DuelProtocol.Services
{
    /// <summary>
    /// Connects the local AI duel to the same UGS ticket and settlement pipeline
    /// used by network matches. Online progression is never emulated locally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelSoloSettlementCoordinator : MonoBehaviour
    {
        private DuelMatchController _controller;
        private DuelIdentitySession _identity;
        private UgsDuelBackend _backend;
        private Task<DuelMatchTicket> _ticketTask;
        private string _activeMatchId = string.Empty;

        public bool IsPending { get; private set; }
        public string LastStatus { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        public void Configure(DuelMatchController controller)
        {
            if (_controller != null)
            {
                _controller.MatchStarted -= HandleMatchStarted;
                _controller.MatchFinished -= HandleMatchFinished;
            }
            _controller = controller;
            if (_controller == null)
            {
                return;
            }
            _controller.MatchStarted += HandleMatchStarted;
            _controller.MatchFinished += HandleMatchFinished;
        }

        private void OnDestroy()
        {
            if (_controller == null)
            {
                return;
            }
            _controller.MatchStarted -= HandleMatchStarted;
            _controller.MatchFinished -= HandleMatchFinished;
        }

        private void HandleMatchStarted(DuelMatchSnapshot snapshot)
        {
            _activeMatchId = snapshot?.MatchId ?? string.Empty;
            LastStatus = "PreparingTicket";
            LastError = string.Empty;
            _ticketTask = PrepareTicketAsync();
        }

        private void HandleMatchFinished(DuelMatchResult result)
        {
            if (string.IsNullOrWhiteSpace(result.MatchId) ||
                !string.Equals(result.MatchId, _activeMatchId, StringComparison.Ordinal))
            {
                return;
            }
            _ = SubmitAsync(result, _ticketTask);
        }

        private async Task<DuelMatchTicket> PrepareTicketAsync()
        {
            try
            {
                _identity ??= new DuelIdentitySession(new UgsDuelIdentityService());
                await _identity.InitializeAnonymousAsync("development");
                if (_identity.State != DuelIdentityState.SignedIn)
                {
                    LastStatus = "UGSUnavailable";
                    LastError = string.IsNullOrWhiteSpace(_identity.ErrorMessage)
                        ? _identity.ErrorCode
                        : _identity.ErrorMessage;
                    return null;
                }

                _backend ??= new UgsDuelBackend();
                var profile = await _backend.GetProfileAsync();
                var buildId = string.IsNullOrWhiteSpace(Application.buildGUID)
                    ? Application.version
                    : Application.buildGUID;
                var ticket = await _backend.CreateAsync(
                    _identity.PlayerId,
                    DuelMatchMode.Ai,
                    profile?.Rating ?? 1000,
                    buildId,
                    Application.platform.ToString());
                LastStatus = "TicketReady";
                Debug.Log(
                    $"DUEL_SOLO_TICKET_READY matchId={_activeMatchId} " +
                    $"ticketId={ticket?.TicketId} player={_identity.PlayerId}");
                return ticket;
            }
            catch (Exception exception)
            {
                LastStatus = "TicketFailed";
                LastError = $"{exception.GetType().Name}: {exception.Message}";
                Debug.LogWarning(
                    $"DUEL_SOLO_TICKET_FAILED matchId={_activeMatchId} error={LastError}");
                return null;
            }
        }

        private async Task SubmitAsync(
            DuelMatchResult result,
            Task<DuelMatchTicket> ticketTask)
        {
            IsPending = true;
            LastStatus = "Pending";
            LastError = string.Empty;
            try
            {
                var ticket = ticketTask == null ? null : await ticketTask;
                if (ticket == null || _backend == null || _identity?.State != DuelIdentityState.SignedIn)
                {
                    LastStatus = "UGSUnavailable";
                    if (string.IsNullOrWhiteSpace(LastError))
                    {
                        LastError = "A live AI match ticket could not be created.";
                    }
                    return;
                }

                var endedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var startedUnixSeconds = Math.Max(
                    ticket.IssuedUnixSeconds,
                    endedUnixSeconds - Math.Max(0L, (long)Math.Ceiling(result.DurationSeconds)));
                var record = new DuelMatchRecord
                {
                    MatchId = result.MatchId,
                    TicketId = ticket.TicketId,
                    Mode = DuelMatchMode.Ai,
                    PlayerOneId = _identity.PlayerId,
                    PlayerTwoId = "duel-protocol-ai",
                    PlayerOneScore = result.PlayerOneScore,
                    PlayerTwoScore = result.PlayerTwoScore,
                    WinnerIndex = result.WinnerIndex,
                    TerminationReason = result.Reason,
                    StartedUnixSeconds = startedUnixSeconds,
                    EndedUnixSeconds = endedUnixSeconds,
                    Nonce = ticket.Nonce,
                    PlayerOneCarrySeconds = result.PlayerOneCarrySeconds,
                    PlayerTwoCarrySeconds = result.PlayerTwoCarrySeconds
                };
                var settlement = await _backend.SubmitAsync(record);
                LastStatus = settlement?.Status ?? "InvalidResponse";
                LastError = settlement?.ErrorCode ?? string.Empty;
                Debug.Log(
                    $"DUEL_SOLO_SETTLEMENT_RESULT matchId={result.MatchId} " +
                    $"status={LastStatus} accepted={settlement?.Accepted} " +
                    $"duplicate={settlement?.AlreadyProcessed} error={LastError}");
            }
            catch (Exception exception)
            {
                LastStatus = "Failed";
                LastError = $"{exception.GetType().Name}: {exception.Message}";
                Debug.LogWarning(
                    $"DUEL_SOLO_SETTLEMENT_FAILED matchId={result.MatchId} error={LastError}");
            }
            finally
            {
                IsPending = false;
            }
        }
    }
}
