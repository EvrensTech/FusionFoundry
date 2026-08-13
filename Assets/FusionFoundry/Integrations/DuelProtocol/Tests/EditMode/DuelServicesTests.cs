using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Networking;
using DuelProtocol.Services;
using NUnit.Framework;
using UnityEngine;

namespace DuelProtocol.Tests
{
    public sealed class DuelServicesTests
    {
        [TestCase(0f, 100)]
        [TestCase(14.99f, 100)]
        [TestCase(15f, 200)]
        [TestCase(30f, 300)]
        [TestCase(45f, 400)]
        [TestCase(90f, 400)]
        public void RatingBand_ExpandsOnSchedule(float elapsed, int expected)
        {
            Assert.That(new RatingBandPolicy().GetBand(elapsed), Is.EqualTo(expected));
        }

        [Test]
        public void Rating_IsSymmetricAtEqualRatings()
        {
            Assert.That(DuelRating.Calculate(1000, 1000, 1f), Is.EqualTo(1016));
            Assert.That(DuelRating.Calculate(1000, 1000, 0f), Is.EqualTo(984));
            Assert.That(DuelRating.Calculate(1000, 1000, 0.5f), Is.EqualTo(1000));
        }

        [Test]
        public void ReadyCoordinator_RequiresExactlyTwoReadyParticipants()
        {
            var ready = new DuelReadyCoordinator();

            Assert.That(ready.AddParticipant("one"), Is.True);
            Assert.That(ready.AddParticipant("two"), Is.True);
            Assert.That(ready.AddParticipant("three"), Is.False);
            ready.SetReady("one", true);
            Assert.That(ready.CanStart, Is.False);
            ready.SetReady("two", true);
            Assert.That(ready.CanStart, Is.True);
        }

        [Test]
        public void Rematch_RequiresBothPlayers_AndCreatesNewMatchId()
        {
            var ready = new DuelReadyCoordinator();
            ready.AddParticipant("one");
            ready.AddParticipant("two");
            ready.RequestRematch("one");
            Assert.That(ready.BeginRematch(), Is.Empty);
            ready.RequestRematch("two");

            var matchId = ready.BeginRematch();

            Assert.That(matchId, Is.Not.Empty);
            Assert.That(ready.CanRematch, Is.False);
        }

        [Test]
        public void SessionInputRequests_PersistUntilExplicitAuthorityAcknowledgement()
        {
            var gameObject = new GameObject("Session Input Test");
            var source = gameObject.AddComponent<LocalDuelCommandSource>();

            source.RequestReady();
            source.RequestRematch();
            Assert.That(source.ReadyRequested, Is.True);
            Assert.That(source.RematchRequested, Is.True);

            source.ClearReadyRequest();
            source.ClearRematchRequest();
            Assert.That(source.ReadyRequested, Is.False);
            Assert.That(source.RematchRequested, Is.False);

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public async Task MatchTicket_IsShortLivedAndBoundToPlayer()
        {
            var backend = new InMemoryDuelBackend("local");
            var ticket = await backend.CreateAsync("local", DuelMatchMode.Ranked, 1000, "build", "Windows");

            Assert.That(ticket.PlayerId, Is.EqualTo("local"));
            Assert.That(ticket.Nonce, Is.Not.Empty);
            Assert.That(ticket.ExpiresUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Is.InRange(80, 90));
        }

        [Test]
        public void MatchTicketValidator_RejectsMutatedSearchFields()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ticket = new DuelMatchTicket
            {
                TicketId = "ticket",
                PlayerId = "player",
                Mode = DuelMatchMode.Ranked,
                Rating = 1000,
                BuildId = "build",
                Platform = "WindowsPlayer",
                ExpiresUnixSeconds = now + 90,
                Nonce = "nonce"
            };

            Assert.That(DuelMatchmakingService.IsTicketValid(
                ticket, "player", DuelMatchMode.Ranked, 1000, "build", "WindowsPlayer", now), Is.True);
            Assert.That(DuelMatchmakingService.IsTicketValid(
                ticket, "player", DuelMatchMode.Ranked, 1200, "build", "WindowsPlayer", now), Is.False);
            Assert.That(DuelMatchmakingService.IsTicketValid(
                ticket, "player", DuelMatchMode.Ranked, 1000, "other-build", "WindowsPlayer", now), Is.False);
        }

        [Test]
        public void PhotonSessionProperties_FilterModeBuildCapacityAndSharedRatingBand()
        {
            var host = Ticket("host", DuelMatchMode.Ranked, 1000, "build", "WindowsPlayer");
            var client = Ticket("client", DuelMatchMode.Ranked, 1250, "build", "Android");
            IReadOnlyDictionary<string, SessionProperty> properties =
                DuelPhotonSessionProperties.Create(host, 300, "match", "eu");

            Assert.That(DuelPhotonSessionProperties.IsCompatible(
                properties, 1, true, client, 300, "eu"), Is.True);
            Assert.That((string)properties[DuelPhotonSessionProperties.Region], Is.EqualTo("eu"));
            Assert.That(DuelPhotonSessionProperties.IsCompatible(
                properties, 2, true, client, 300, "eu"), Is.False);
            Assert.That(DuelPhotonSessionProperties.IsCompatible(
                properties, 1, false, client, 300, "eu"), Is.False);
            Assert.That(DuelPhotonSessionProperties.IsCompatible(
                properties, 1, true, client, 300, "us"), Is.False);

            client.BuildId = "other";
            Assert.That(DuelPhotonSessionProperties.IsCompatible(
                properties, 1, true, client, 300, "eu"), Is.False);
        }

        [Test]
        public async Task MatchmakingCancellation_CleansWaitingTransportAndAllowsSafeReturn()
        {
            var transport = new BlockingMatchmakingTransport();
            var service = new DuelMatchmakingService(
                new InMemoryDuelBackend("player"),
                transport);
            using var cancellation = new CancellationTokenSource();

            var search = service.SearchAsync(
                "player", DuelMatchMode.Unranked, 1000, "build", "WindowsPlayer", cancellation.Token);
            await transport.Started.Task;
            service.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await search);
            Assert.That(service.State, Is.EqualTo(DuelSearchState.Cancelled));
            Assert.That(transport.CancelCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Settlement_IsIdempotentAndUpdatesRankedRating()
        {
            var backend = new InMemoryDuelBackend("local");
            var record = await IssuedRecord(backend, DuelMatchMode.Ranked);

            var first = await backend.SubmitAsync(record);
            var second = await backend.SubmitAsync(record);
            var profile = await backend.GetProfileAsync();

            Assert.That(first.Accepted, Is.True);
            Assert.That(first.RatingAfter, Is.GreaterThan(first.RatingBefore));
            Assert.That(second.AlreadyProcessed, Is.True);
            Assert.That(profile.MatchHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task NetworkSessionContext_RefreshesOnce_ValidatesAndConsumesTicket()
        {
            DuelNetworkSessionContext.Reset();
            var tickets = new RecordingTicketService();
            var results = new InMemoryDuelBackend("local");
            DuelNetworkSessionContext.Configure(
                "local",
                DuelMatchMode.Ranked,
                1000,
                "build-1",
                "WindowsPlayer",
                tickets,
                results);

            var firstTask = DuelNetworkSessionContext.EnsureTicketAsync();
            var secondTask = DuelNetworkSessionContext.EnsureTicketAsync();
            var first = await firstTask;
            var second = await secondTask;

            Assert.That(tickets.CreateCount, Is.EqualTo(1));
            Assert.That(second, Is.SameAs(first));
            Assert.That(DuelNetworkSessionContext.HasReadyTicket(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()), Is.True);

            DuelNetworkSessionContext.ConsumeTicket(first);
            Assert.That(DuelNetworkSessionContext.ActiveTicket, Is.Null);

            var refreshed = await DuelNetworkSessionContext.EnsureTicketAsync();
            Assert.That(tickets.CreateCount, Is.EqualTo(2));
            Assert.That(refreshed.TicketId, Is.Not.EqualTo(first.TicketId));
            DuelNetworkSessionContext.Reset();
        }

        [Test]
        public async Task ProfileRating_AndNearbyLeaderboardScore_RemainIdenticalAfterSettlement()
        {
            var backend = new InMemoryDuelBackend("local");
            Assert.That((await backend.SubmitAsync(
                await IssuedRecord(backend, DuelMatchMode.Ranked))).Accepted, Is.True);

            var profile = await backend.GetProfileAsync();
            var nearby = await backend.GetLeaderboardAroundPlayerAsync(3);
            var localEntry = nearby.Single(entry => entry.PlayerId == profile.PlayerId);

            Assert.That(localEntry.Rating, Is.EqualTo(profile.Rating));
        }

        [Test]
        public async Task AiSettlement_AwardsXpWithoutChangingRating()
        {
            var backend = new InMemoryDuelBackend("local");
            var result = await backend.SubmitAsync(await IssuedRecord(backend, DuelMatchMode.Ai));

            Assert.That(result.RatingAfter, Is.EqualTo(1000));
            Assert.That(result.ExperienceAwarded, Is.EqualTo(50));
        }

        [Test]
        public async Task InvalidSettlement_IsRejected()
        {
            var backend = new InMemoryDuelBackend("local");
            var record = ValidRecord(DuelMatchMode.Ranked);
            record.PlayerTwoId = "local";

            var result = await backend.SubmitAsync(record);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("InvalidParticipants"));
        }

        [Test]
        public void RatingChange_NeverExceedsKFactor()
        {
            foreach (var current in new[] { 100, 1000, 2500 })
            foreach (var opponent in new[] { 100, 1000, 2500 })
            foreach (var score in new[] { 0f, 0.5f, 1f })
            {
                var after = DuelRating.Calculate(current, opponent, score);
                Assert.That(Math.Abs(after - current), Is.LessThanOrEqualTo(DuelRating.KFactor));
            }
        }

        [Test]
        public async Task MatchHistory_IsCappedAtTwentyNewestRecords()
        {
            var backend = new InMemoryDuelBackend("local");
            var ids = new List<string>();
            for (var index = 0; index < 21; index++)
            {
                var record = await IssuedRecord(backend, DuelMatchMode.Ai);
                ids.Add(record.MatchId);
                Assert.That((await backend.SubmitAsync(record)).Accepted, Is.True);
            }

            var profile = await backend.GetProfileAsync();
            Assert.That(profile.MatchHistory.Count, Is.EqualTo(20));
            Assert.That(profile.MatchHistory[0].MatchId, Is.EqualTo(ids[20]));
            Assert.That(profile.MatchHistory.Exists(item => item.MatchId == ids[0]), Is.False);
        }

        [Test]
        public async Task DailyGoals_ResetExactlyAtInjectedUtcDayBoundary()
        {
            var now = new DateTime(2026, 8, 6, 23, 59, 59, DateTimeKind.Utc);
            var backend = new InMemoryDuelBackend("local", () => now);
            for (var index = 0; index < 3; index++)
            {
                var record = await IssuedRecord(backend, DuelMatchMode.Ai);
                record.PlayerOneCarrySeconds = 20f;
                await backend.SubmitAsync(record);
            }

            var firstDay = await backend.GetProfileAsync();
            Assert.That(firstDay.Daily.UtcDate, Is.EqualTo("2026-08-06"));
            Assert.That(firstDay.Daily.PlayThreeComplete, Is.True);
            Assert.That(firstDay.Daily.WinOneComplete, Is.True);
            Assert.That(firstDay.Daily.CarrySixtyComplete, Is.True);

            now = now.AddSeconds(1);
            await backend.SubmitAsync(await IssuedRecord(backend, DuelMatchMode.Ai));
            var secondDay = await backend.GetProfileAsync();
            Assert.That(secondDay.Daily.UtcDate, Is.EqualTo("2026-08-07"));
            Assert.That(secondDay.Daily.MatchesPlayed, Is.EqualTo(1));
            Assert.That(secondDay.Daily.PlayThreeComplete, Is.False);
        }

        [Test]
        public async Task Settlement_RejectsChangedOrExpiredSingleUseTicket()
        {
            var now = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
            var backend = new InMemoryDuelBackend("local", () => now);
            var changedNonce = await IssuedRecord(backend, DuelMatchMode.Ranked);
            changedNonce.Nonce = "tampered";
            var changedResult = await backend.SubmitAsync(changedNonce);
            Assert.That(changedResult.ErrorCode, Is.EqualTo("InvalidTicket"));

            var expired = await IssuedRecord(backend, DuelMatchMode.Ranked);
            expired.StartedUnixSeconds += 91;
            expired.EndedUnixSeconds = expired.StartedUnixSeconds + 60;
            var expiredResult = await backend.SubmitAsync(expired);
            Assert.That(expiredResult.ErrorCode, Is.EqualTo("InvalidTicket"));
        }

        private static async Task<DuelMatchRecord> IssuedRecord(
            InMemoryDuelBackend backend,
            DuelMatchMode mode)
        {
            var ticket = await backend.CreateAsync(
                "local", mode, 1000, "build-1", "Windows");
            var record = ValidRecord(mode);
            record.TicketId = ticket.TicketId;
            record.Nonce = ticket.Nonce;
            record.StartedUnixSeconds = ticket.IssuedUnixSeconds;
            record.EndedUnixSeconds = ticket.IssuedUnixSeconds + 60;
            return record;
        }

        private static DuelMatchRecord ValidRecord(DuelMatchMode mode)
        {
            return new DuelMatchRecord
            {
                MatchId = Guid.NewGuid().ToString("N"),
                Mode = mode,
                PlayerOneId = "local",
                PlayerTwoId = "opponent",
                PlayerOneScore = 3,
                PlayerTwoScore = 1,
                WinnerIndex = 0,
                TerminationReason = DuelTerminationReason.ScoreLimit,
                StartedUnixSeconds = 100,
                EndedUnixSeconds = 200,
                Nonce = Guid.NewGuid().ToString("N")
            };
        }

        private static DuelMatchTicket Ticket(
            string playerId,
            DuelMatchMode mode,
            int rating,
            string buildId,
            string platform)
        {
            return new DuelMatchTicket
            {
                TicketId = Guid.NewGuid().ToString("N"),
                PlayerId = playerId,
                Mode = mode,
                Rating = rating,
                BuildId = buildId,
                Platform = platform,
                IssuedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 90,
                Nonce = Guid.NewGuid().ToString("N")
            };
        }

        private sealed class BlockingMatchmakingTransport : IDuelMatchmakingTransport
        {
            public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
            public int CancelCount { get; private set; }

            public async Task<DuelMatchAssignment> FindAsync(
                DuelMatchTicket ticket,
                int ratingBand,
                CancellationToken cancellationToken)
            {
                Started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            }

            public Task CancelAsync()
            {
                CancelCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingTicketService : IDuelMatchTicketService
        {
            public int CreateCount { get; private set; }

            public Task<DuelMatchTicket> CreateAsync(
                string playerId,
                DuelMatchMode mode,
                int rating,
                string buildId,
                string platform)
            {
                CreateCount++;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return Task.FromResult(new DuelMatchTicket
                {
                    TicketId = Guid.NewGuid().ToString("N"),
                    PlayerId = playerId,
                    Mode = mode,
                    Rating = rating,
                    BuildId = buildId,
                    Platform = platform,
                    IssuedUnixSeconds = now,
                    ExpiresUnixSeconds = now + 90,
                    Nonce = Guid.NewGuid().ToString("N")
                });
            }
        }
    }
}
