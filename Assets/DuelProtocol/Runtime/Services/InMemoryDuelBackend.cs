using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DuelProtocol.Match;

namespace DuelProtocol.Services
{
    public sealed class InMemoryDuelBackend :
        IDuelMatchTicketService,
        IDuelMatchResultService,
        IDuelProgressionService,
        IDuelPlayerPreferencesService
    {
        private readonly Dictionary<string, DuelPlayerProfile> _profiles =
            new Dictionary<string, DuelPlayerProfile>();
        private readonly HashSet<string> _processedMatchIds = new HashSet<string>();
        private readonly Dictionary<string, DuelMatchTicket> _issuedTickets =
            new Dictionary<string, DuelMatchTicket>();
        private readonly string _localPlayerId;
        private readonly Func<DateTime> _utcNow;

        public InMemoryDuelBackend(
            string localPlayerId = "local-player",
            Func<DateTime> utcNow = null)
        {
            _localPlayerId = string.IsNullOrWhiteSpace(localPlayerId)
                ? "local-player"
                : localPlayerId;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            GetOrCreate(_localPlayerId);
        }

        public Task<DuelMatchTicket> CreateAsync(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform)
        {
            var now = new DateTimeOffset(_utcNow().ToUniversalTime()).ToUnixTimeSeconds();
            var ticket = new DuelMatchTicket
            {
                TicketId = Guid.NewGuid().ToString("N"),
                PlayerId = playerId,
                Mode = mode,
                Rating = rating,
                BuildId = buildId ?? string.Empty,
                Platform = platform ?? string.Empty,
                IssuedUnixSeconds = now,
                ExpiresUnixSeconds = now + 90,
                Nonce = Guid.NewGuid().ToString("N")
            };
            _issuedTickets[ticket.TicketId] = ticket;
            return Task.FromResult(ticket);
        }

        public Task<DuelSettlementResult> SubmitAsync(DuelMatchRecord record)
        {
            if (!ValidateRecord(record, out var error))
            {
                return Task.FromResult(new DuelSettlementResult
                {
                    Accepted = false,
                    Status = "Rejected",
                    ErrorCode = error
                });
            }

            if (record.PlayerOneId != _localPlayerId && record.PlayerTwoId != _localPlayerId)
            {
                return Task.FromResult(new DuelSettlementResult
                {
                    Accepted = false,
                    Status = "Rejected",
                    ErrorCode = "LocalPlayerNotParticipant"
                });
            }

            if (!_processedMatchIds.Add(record.MatchId))
            {
                return Task.FromResult(new DuelSettlementResult
                {
                    Accepted = true,
                    AlreadyProcessed = true,
                    Status = "Settled"
                });
            }

            if (!TryConsumeTicket(record, out error))
            {
                _processedMatchIds.Remove(record.MatchId);
                return Task.FromResult(new DuelSettlementResult
                {
                    Accepted = false,
                    Status = "Rejected",
                    ErrorCode = error
                });
            }

            var localIndex = record.PlayerOneId == _localPlayerId ? 0 : 1;
            var opponentId = localIndex == 0 ? record.PlayerTwoId : record.PlayerOneId;
            var profile = GetOrCreate(_localPlayerId);
            var opponent = GetOrCreate(opponentId);
            var won = record.WinnerIndex == localIndex;
            var draw = record.WinnerIndex < 0;
            var before = profile.Rating;

            if (record.Mode == DuelMatchMode.Ranked &&
                record.TerminationReason != DuelTerminationReason.HostLost)
            {
                profile.Rating = DuelRating.Calculate(
                    profile.Rating,
                    opponent.Rating,
                    draw ? 0.5f : (won ? 1f : 0f));
                profile.League = DuelRating.GetLeague(profile.Rating);
            }

            var xp = DuelRating.GetExperienceAward(record.Mode, won);
            profile.Experience += xp;
            profile.Level = Math.Max(1, 1 + profile.Experience / 500);
            if (draw) profile.Draws++;
            else if (won) profile.Wins++;
            else profile.Losses++;
            UpdateDaily(
                profile,
                won,
                localIndex == 0 ? record.PlayerOneCarrySeconds : record.PlayerTwoCarrySeconds);
            profile.MatchHistory.Insert(0, record);
            if (profile.MatchHistory.Count > 20)
            {
                profile.MatchHistory.RemoveRange(20, profile.MatchHistory.Count - 20);
            }
            profile.ProcessedMatchIds.Insert(0, record.MatchId);
            if (profile.ProcessedMatchIds.Count > 100)
            {
                profile.ProcessedMatchIds.RemoveRange(100, profile.ProcessedMatchIds.Count - 100);
            }

            return Task.FromResult(new DuelSettlementResult
            {
                Accepted = true,
                Status = "Settled",
                RatingBefore = before,
                RatingAfter = profile.Rating,
                ExperienceAwarded = xp
            });
        }

        public Task<DuelPlayerProfile> GetProfileAsync()
        {
            return Task.FromResult(CloneProfile(GetOrCreate(_localPlayerId)));
        }

        public Task<IReadOnlyList<DuelLeaderboardEntry>> GetLeaderboardAroundPlayerAsync(int rangeLimit)
        {
            var entries = _profiles.Values
                .OrderByDescending(profile => profile.Rating)
                .Take(Math.Max(1, rangeLimit * 2 + 1))
                .Select((profile, index) => new DuelLeaderboardEntry
                {
                    PlayerId = profile.PlayerId,
                    DisplayName = profile.DisplayName,
                    Rank = index,
                    Rating = profile.Rating
                })
                .ToList();
            return Task.FromResult<IReadOnlyList<DuelLeaderboardEntry>>(entries);
        }

        public Task<DuelPlayerPreferencesData> GetPreferencesAsync()
        {
            return Task.FromResult(ClonePreferences(GetOrCreate(_localPlayerId).Preferences));
        }

        public Task SavePreferencesAsync(DuelPlayerPreferencesData preferences)
        {
            GetOrCreate(_localPlayerId).Preferences = ClonePreferences(preferences);
            return Task.CompletedTask;
        }

        private static bool ValidateRecord(DuelMatchRecord record, out string error)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.MatchId))
            {
                error = "InvalidMatchId";
                return false;
            }
            if (string.IsNullOrWhiteSpace(record.Nonce))
            {
                error = "InvalidNonce";
                return false;
            }
            if (string.IsNullOrWhiteSpace(record.PlayerOneId) ||
                string.IsNullOrWhiteSpace(record.PlayerTwoId) ||
                record.PlayerOneId == record.PlayerTwoId)
            {
                error = "InvalidParticipants";
                return false;
            }
            if (record.PlayerOneScore < 0 || record.PlayerTwoScore < 0 ||
                record.PlayerOneScore > 3 || record.PlayerTwoScore > 3)
            {
                error = "InvalidScore";
                return false;
            }
            if (record.WinnerIndex < -1 || record.WinnerIndex > 1)
            {
                error = "InvalidWinner";
                return false;
            }
            if (record.EndedUnixSeconds < record.StartedUnixSeconds)
            {
                error = "InvalidDuration";
                return false;
            }
            if (record.EndedUnixSeconds - record.StartedUnixSeconds > 300)
            {
                error = "InvalidDuration";
                return false;
            }
            if ((record.WinnerIndex == 0 && record.PlayerOneScore <= record.PlayerTwoScore) ||
                (record.WinnerIndex == 1 && record.PlayerTwoScore <= record.PlayerOneScore) ||
                (record.WinnerIndex < 0 && record.PlayerOneScore != record.PlayerTwoScore &&
                 record.TerminationReason != DuelTerminationReason.HostLost &&
                 record.TerminationReason != DuelTerminationReason.Cancelled))
            {
                error = "WinnerScoreMismatch";
                return false;
            }
            if (record.TerminationReason == DuelTerminationReason.ScoreLimit &&
                Math.Max(record.PlayerOneScore, record.PlayerTwoScore) != 3)
            {
                error = "ScoreLimitMismatch";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool TryConsumeTicket(DuelMatchRecord record, out string error)
        {
            if (string.IsNullOrWhiteSpace(record.TicketId) ||
                !_issuedTickets.TryGetValue(record.TicketId, out var ticket) ||
                record.StartedUnixSeconds < ticket.IssuedUnixSeconds ||
                record.StartedUnixSeconds > ticket.ExpiresUnixSeconds ||
                !string.Equals(ticket.PlayerId, _localPlayerId, StringComparison.Ordinal) ||
                !string.Equals(ticket.Nonce, record.Nonce, StringComparison.Ordinal) ||
                ticket.Mode != record.Mode)
            {
                error = "InvalidTicket";
                return false;
            }
            _issuedTickets.Remove(record.TicketId);
            error = string.Empty;
            return true;
        }

        private DuelPlayerProfile GetOrCreate(string playerId)
        {
            if (_profiles.TryGetValue(playerId, out var profile))
            {
                return profile;
            }
            profile = new DuelPlayerProfile
            {
                PlayerId = playerId,
                DisplayName = playerId,
                Rating = DuelRating.InitialRating,
                League = DuelRating.GetLeague(DuelRating.InitialRating)
            };
            _profiles[playerId] = profile;
            return profile;
        }

        private void UpdateDaily(DuelPlayerProfile profile, bool won, float carrySeconds)
        {
            var utcDate = _utcNow().ToUniversalTime().ToString("yyyy-MM-dd");
            if (profile.Daily == null || profile.Daily.UtcDate != utcDate)
            {
                profile.Daily = new DuelDailyProgress { UtcDate = utcDate };
            }
            profile.Daily.MatchesPlayed++;
            if (won) profile.Daily.Wins++;
            profile.Daily.CoreCarrySeconds += Math.Max(0f, carrySeconds);
        }

        private static DuelPlayerProfile CloneProfile(DuelPlayerProfile source)
        {
            return new DuelPlayerProfile
            {
                SchemaVersion = source.SchemaVersion,
                PlayerId = source.PlayerId,
                DisplayName = source.DisplayName,
                Level = source.Level,
                Experience = source.Experience,
                Rating = source.Rating,
                League = source.League,
                Wins = source.Wins,
                Losses = source.Losses,
                Draws = source.Draws,
                MatchHistory = new List<DuelMatchRecord>(source.MatchHistory),
                ProcessedMatchIds = new List<string>(source.ProcessedMatchIds),
                Daily = source.Daily == null
                    ? new DuelDailyProgress()
                    : new DuelDailyProgress
                    {
                        UtcDate = source.Daily.UtcDate,
                        MatchesPlayed = source.Daily.MatchesPlayed,
                        Wins = source.Daily.Wins,
                        CoreCarrySeconds = source.Daily.CoreCarrySeconds,
                        PlayThreeClaimed = source.Daily.PlayThreeClaimed,
                        WinOneClaimed = source.Daily.WinOneClaimed,
                        CarrySixtyClaimed = source.Daily.CarrySixtyClaimed
                    },
                Preferences = source.Preferences == null
                    ? new DuelPlayerPreferencesData()
                    : new DuelPlayerPreferencesData
                    {
                        InputBindingsJson = source.Preferences.InputBindingsJson,
                        ReducedMotion = source.Preferences.ReducedMotion,
                        HighContrast = source.Preferences.HighContrast,
                        HudScale = source.Preferences.HudScale
                    }
            };
        }

        private static DuelPlayerPreferencesData ClonePreferences(DuelPlayerPreferencesData source)
        {
            source ??= new DuelPlayerPreferencesData();
            return new DuelPlayerPreferencesData
            {
                InputBindingsJson = source.InputBindingsJson ?? string.Empty,
                ReducedMotion = source.ReducedMotion,
                HighContrast = source.HighContrast,
                HudScale = Math.Max(0.8f, Math.Min(1.5f, source.HudScale))
            };
        }
    }
}
