using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuelProtocol.Match;
using DuelProtocol.Presentation;
using DuelProtocol.Services;
using UnityEngine;

namespace DuelProtocol.Networking
{
    public static class DuelNetworkSessionContext
    {
        private static Task<DuelMatchTicket> s_TicketRefresh;

        public static string LocalPlayerId { get; private set; } = string.Empty;
        public static DuelMatchMode Mode { get; private set; } = DuelMatchMode.Private;
        public static int Rating { get; private set; } = DuelRating.InitialRating;
        public static string BuildId { get; private set; } = string.Empty;
        public static string Platform { get; private set; } = string.Empty;
        public static DuelMatchTicket ActiveTicket { get; private set; }
        public static IDuelMatchResultService ResultService { get; private set; }
        public static IDuelMatchTicketService TicketService { get; private set; }

        public static void Configure(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform,
            IDuelMatchTicketService ticketService,
            IDuelMatchResultService resultService,
            DuelMatchTicket activeTicket = null)
        {
            s_TicketRefresh = null;
            LocalPlayerId = playerId ?? string.Empty;
            Mode = mode;
            Rating = Mathf.Max(0, rating);
            BuildId = buildId ?? string.Empty;
            Platform = platform ?? string.Empty;
            TicketService = ticketService;
            ResultService = resultService;
            ActiveTicket = activeTicket;
        }

        public static bool HasReadyTicket(long nowUnixSeconds)
        {
            return ActiveTicket != null &&
                   !ActiveTicket.IsExpired(nowUnixSeconds) &&
                   string.Equals(ActiveTicket.PlayerId, LocalPlayerId, StringComparison.Ordinal) &&
                   ActiveTicket.Mode == Mode;
        }

        public static Task<DuelMatchTicket> EnsureTicketAsync()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (HasReadyTicket(now))
            {
                return Task.FromResult(ActiveTicket);
            }
            if (s_TicketRefresh != null)
            {
                return s_TicketRefresh;
            }
            s_TicketRefresh = CreateTicketAsync();
            return s_TicketRefresh;
        }

        public static void ConsumeTicket(DuelMatchTicket ticket)
        {
            if (ReferenceEquals(ActiveTicket, ticket) ||
                (ActiveTicket != null && ticket != null &&
                 string.Equals(ActiveTicket.TicketId, ticket.TicketId, StringComparison.Ordinal)))
            {
                ActiveTicket = null;
            }
        }

        public static void Reset()
        {
            LocalPlayerId = string.Empty;
            ActiveTicket = null;
            ResultService = null;
            TicketService = null;
            s_TicketRefresh = null;
        }

        private static async Task<DuelMatchTicket> CreateTicketAsync()
        {
            // Prevent a synchronously completed service task from clearing the shared
            // refresh slot before EnsureTicketAsync has assigned it.
            await Task.Yield();
            try
            {
                if (TicketService == null || string.IsNullOrWhiteSpace(LocalPlayerId) ||
                    string.IsNullOrWhiteSpace(BuildId) || string.IsNullOrWhiteSpace(Platform))
                {
                    return null;
                }
                ActiveTicket = await TicketService.CreateAsync(
                    LocalPlayerId,
                    Mode,
                    Rating,
                    BuildId,
                    Platform);
                Debug.Log(
                    $"DUEL_MATCH_TICKET_READY ticketId={ActiveTicket?.TicketId} " +
                    $"player={ActiveTicket?.PlayerId} mode={ActiveTicket?.Mode}");
                return ActiveTicket;
            }
            finally
            {
                s_TicketRefresh = null;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelMatchSettlementCoordinator : MonoBehaviour
    {
        private readonly HashSet<string> _submittedMatchIds = new HashSet<string>();
        private DuelProfilePanel _profilePanel;
        private bool _busy;

        public string LastStatus { get; private set; } = string.Empty;
        public string LastErrorCode { get; private set; } = string.Empty;
        public string LastErrorMessage { get; private set; } = string.Empty;

        public void Configure(DuelProfilePanel profilePanel)
        {
            _profilePanel = profilePanel;
        }

        private void Update()
        {
            if (_busy || DuelNetworkSessionContext.ActiveTicket == null ||
                DuelNetworkSessionContext.ResultService == null)
            {
                return;
            }
            var state = FindAnyObjectByType<DuelNetworkMatchState>();
            if (state == null || state.Phase != DuelMatchPhase.Finished)
            {
                return;
            }
            var matchId = state.MatchId.ToString();
            if (string.IsNullOrWhiteSpace(matchId) || _submittedMatchIds.Contains(matchId))
            {
                return;
            }
            var record = state.CreateMatchRecord(DuelNetworkSessionContext.ActiveTicket);
            if (record == null)
            {
                return;
            }
            _ = SubmitAsync(matchId, record, DuelNetworkSessionContext.ActiveTicket);
        }

        private async Task SubmitAsync(
            string matchId,
            DuelMatchRecord record,
            DuelMatchTicket ticket)
        {
            _busy = true;
            _profilePanel?.SetSettlementPending(true);
            LastStatus = "Pending";
            LastErrorCode = string.Empty;
            LastErrorMessage = string.Empty;
            try
            {
                var result = await DuelNetworkSessionContext.ResultService.SubmitAsync(record);
                LastStatus = result?.Status ?? "InvalidResponse";
                LastErrorCode = result?.ErrorCode ?? string.Empty;
                if (result != null && (result.Accepted || result.Status == "Disputed"))
                {
                    _submittedMatchIds.Add(matchId);
                    DuelNetworkSessionContext.ConsumeTicket(ticket);
                    await DuelNetworkSessionContext.EnsureTicketAsync();
                    if (_profilePanel != null) await _profilePanel.RefreshAsync();
                }
                Debug.Log(
                    $"DUEL_SETTLEMENT_RESULT matchId={matchId} status={LastStatus} " +
                    $"accepted={result?.Accepted} duplicate={result?.AlreadyProcessed} " +
                    $"error={LastErrorCode}");
            }
            catch (Exception exception)
            {
                LastStatus = "Pending";
                LastErrorCode = exception.GetType().Name;
                LastErrorMessage = exception.Message ?? string.Empty;
                Debug.LogWarning(
                    $"DUEL_SETTLEMENT_PENDING matchId={matchId} error={LastErrorCode} " +
                    $"message={LastErrorMessage}");
            }
            finally
            {
                _profilePanel?.SetSettlementPending(LastStatus == "Pending");
                _busy = false;
            }
        }
    }
}
