using System;
using System.Threading;
using System.Threading.Tasks;
using DuelProtocol.Match;

namespace DuelProtocol.Services
{
    public enum DuelSearchState
    {
        Idle,
        CreatingTicket,
        Searching,
        Matched,
        Cancelled,
        TimedOut,
        Failed
    }

    [Serializable]
    public sealed class DuelMatchAssignment
    {
        public string MatchId = string.Empty;
        public string SessionName = string.Empty;
        public string OpponentPlayerId = string.Empty;
        public string BuildId = string.Empty;
        public DuelMatchMode Mode;
    }

    public interface IDuelMatchmakingTransport
    {
        Task<DuelMatchAssignment> FindAsync(
            DuelMatchTicket ticket,
            int ratingBand,
            CancellationToken cancellationToken);

        Task CancelAsync();
    }

    public interface IDuelMatchmakingService
    {
        DuelSearchState State { get; }
        int CurrentRatingBand { get; }
        string SearchId { get; }
        double ElapsedSeconds { get; }
        DuelMatchTicket LastTicket { get; }
        Task<DuelMatchAssignment> SearchAsync(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform,
            CancellationToken cancellationToken);
        void Cancel();
    }

    public sealed class DuelMatchmakingService : IDuelMatchmakingService, IDisposable
    {
        public event Action<DuelMatchTicket> TicketCreated;
        private readonly IDuelMatchTicketService _tickets;
        private readonly IDuelMatchmakingTransport _transport;
        private readonly RatingBandPolicy _policy;
        private CancellationTokenSource _activeSearch;
        private DateTime _searchStartedUtc;

        public DuelSearchState State { get; private set; } = DuelSearchState.Idle;
        public int CurrentRatingBand { get; private set; } = RatingBandPolicy.InitialBand;
        public string SearchId { get; private set; } = string.Empty;
        public DuelMatchTicket LastTicket { get; private set; }
        public double ElapsedSeconds => _searchStartedUtc == default
            ? 0d
            : Math.Max(0d, (DateTime.UtcNow - _searchStartedUtc).TotalSeconds);

        public DuelMatchmakingService(
            IDuelMatchTicketService tickets,
            IDuelMatchmakingTransport transport,
            RatingBandPolicy policy = null)
        {
            _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _policy = policy ?? new RatingBandPolicy();
        }

        public async Task<DuelMatchAssignment> SearchAsync(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform,
            CancellationToken cancellationToken)
        {
            if (_activeSearch != null)
            {
                throw new InvalidOperationException("A matchmaking search is already active.");
            }
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(buildId))
            {
                throw new ArgumentException("Player and build identifiers are required.");
            }

            _activeSearch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _activeSearch.Token;
            _searchStartedUtc = DateTime.UtcNow;
            SearchId = Guid.NewGuid().ToString("N");
            LastTicket = null;
            var matched = false;
            try
            {
                State = DuelSearchState.CreatingTicket;
                var ticket = await _tickets.CreateAsync(playerId, mode, rating, buildId, platform);
                if (!IsTicketValid(
                        ticket,
                        playerId,
                        mode,
                        rating,
                        buildId,
                        platform,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                {
                    throw new InvalidOperationException("The matchmaking ticket is invalid or expired.");
                }
                LastTicket = ticket;
                TicketCreated?.Invoke(ticket);

                State = DuelSearchState.Searching;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var elapsed = (float)ElapsedSeconds;
                    if (_policy.HasTimedOut(elapsed))
                    {
                        State = DuelSearchState.TimedOut;
                        return null;
                    }

                    CurrentRatingBand = _policy.GetBand(elapsed);
                    var assignment = await _transport.FindAsync(ticket, CurrentRatingBand, token);
                    if (assignment != null)
                    {
                        if (assignment.Mode != mode || assignment.BuildId != buildId ||
                            string.IsNullOrWhiteSpace(assignment.MatchId) ||
                            string.IsNullOrWhiteSpace(assignment.SessionName))
                        {
                            throw new InvalidOperationException("Match assignment is incompatible.");
                        }
                        State = DuelSearchState.Matched;
                        matched = true;
                        return assignment;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }
            }
            catch (OperationCanceledException)
            {
                State = DuelSearchState.Cancelled;
                throw;
            }
            catch
            {
                State = DuelSearchState.Failed;
                throw;
            }
            finally
            {
                if (!matched)
                {
                    try
                    {
                        await _transport.CancelAsync();
                    }
                    catch
                    {
                        if (State != DuelSearchState.Cancelled &&
                            State != DuelSearchState.TimedOut)
                        {
                            State = DuelSearchState.Failed;
                        }
                    }
                }
                _activeSearch.Dispose();
                _activeSearch = null;
            }
        }

        public void Cancel()
        {
            _activeSearch?.Cancel();
        }

        public void Dispose()
        {
            Cancel();
            _activeSearch?.Dispose();
            _activeSearch = null;
            _ = _transport.CancelAsync();
        }

        public static bool IsTicketValid(
            DuelMatchTicket ticket,
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform,
            long nowUnixSeconds)
        {
            return ticket != null &&
                   !ticket.IsExpired(nowUnixSeconds) &&
                   !string.IsNullOrWhiteSpace(ticket.TicketId) &&
                   !string.IsNullOrWhiteSpace(ticket.Nonce) &&
                   string.Equals(ticket.PlayerId, playerId, StringComparison.Ordinal) &&
                   ticket.Mode == mode &&
                   ticket.Rating == rating &&
                   string.Equals(ticket.BuildId, buildId, StringComparison.Ordinal) &&
                   string.Equals(ticket.Platform, platform, StringComparison.Ordinal);
        }
    }
}
