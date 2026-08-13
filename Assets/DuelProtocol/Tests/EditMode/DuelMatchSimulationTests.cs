using DuelProtocol.Match;
using DuelProtocol.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DuelProtocol.Tests
{
    public sealed class DuelMatchSimulationTests
    {
        [Test]
        public void DefaultRules_MatchProductContract()
        {
            var rules = DuelRuleSet.Default;

            Assert.That(rules.RegulationSeconds, Is.EqualTo(180f));
            Assert.That(rules.OvertimeSeconds, Is.EqualTo(60f));
            Assert.That(rules.ScoreLimit, Is.EqualTo(3));
            Assert.That(rules.HoldSecondsPerPoint, Is.EqualTo(15f));
            Assert.That(rules.StunSeconds, Is.EqualTo(1.5f));
            Assert.That(rules.ReconnectSeconds, Is.EqualTo(20f));
        }

        [Test]
        public void Countdown_TransitionsOnlyAfterThreeSeconds()
        {
            var match = new DuelMatchSimulation(DuelRuleSet.Default);

            match.Tick(DuelCommand.None, DuelCommand.None, 2.99f);
            Assert.That(match.Snapshot.Phase, Is.EqualTo(DuelMatchPhase.Countdown));
            match.Tick(DuelCommand.None, DuelCommand.None, 0.02f);
            Assert.That(match.Snapshot.Phase, Is.EqualTo(DuelMatchPhase.Playing));
        }

        [Test]
        public void Movement_IsNormalizedAndClampedToArena()
        {
            var match = StartMatch(DuelRuleSet.Default);
            var command = new DuelCommand { Movement = new Vector2(100f, 100f), Aim = Vector2.up };

            match.Tick(command, DuelCommand.None, 10f);

            Assert.That(match.Snapshot.PlayerOne.Position.x, Is.InRange(-8f, 8f));
            Assert.That(match.Snapshot.PlayerOne.Position.y, Is.InRange(-8f, 8f));
        }

        [Test]
        public void Core_IsOwnedByOnlyOnePlayerDuringPickupRace()
        {
            var match = StartMatch(DuelRuleSet.Default);
            var towardCore = new DuelCommand { Movement = Vector2.right, Aim = Vector2.right };
            var otherTowardCore = new DuelCommand { Movement = Vector2.left, Aim = Vector2.left };

            match.Tick(towardCore, otherTowardCore, 0.65f);

            Assert.That(match.Snapshot.Core.OwnerIndex, Is.EqualTo(0).Or.EqualTo(1));
        }

        [Test]
        public void HoldingCore_ToScoreLimit_FinishesExactlyOnce()
        {
            var rules = Rules(regulation: 30f, overtime: 0f, scoreLimit: 1, hold: 0.1f);
            var match = StartMatch(rules);
            var approach = new DuelCommand { Movement = Vector2.right, Aim = Vector2.right };
            match.Tick(approach, DuelCommand.None, 0.6f);
            match.Tick(DuelCommand.None, DuelCommand.None, 0.11f);

            var result = match.Snapshot.Result;
            Assert.That(match.Snapshot.Phase, Is.EqualTo(DuelMatchPhase.Finished));
            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.WinnerIndex, Is.EqualTo(0));
            Assert.That(result.Value.Reason, Is.EqualTo(DuelTerminationReason.ScoreLimit));

            match.Tick(DuelCommand.None, DuelCommand.None, 99f);
            Assert.That(match.Snapshot.Result.Value.WinnerIndex, Is.EqualTo(0));
        }

        [Test]
        public void TiedRegulation_EntersOvertime_ThenDraws()
        {
            var match = StartMatch(Rules(regulation: 1f, overtime: 0.1f));

            match.Tick(DuelCommand.None, DuelCommand.None, 1.01f);
            Assert.That(match.Snapshot.Phase, Is.EqualTo(DuelMatchPhase.Overtime));
            match.Tick(DuelCommand.None, DuelCommand.None, 0.11f);

            Assert.That(match.Snapshot.Result.Value.Reason, Is.EqualTo(DuelTerminationReason.Draw));
            Assert.That(match.Snapshot.Result.Value.IsDraw, Is.True);
        }

        [Test]
        public void ClientReconnect_IsSingleUse_AndSecondDisconnectForfeits()
        {
            var match = StartMatch(DuelRuleSet.Default);

            Assert.That(match.DisconnectPlayer(1, false), Is.True);
            Assert.That(match.Snapshot.Phase, Is.EqualTo(DuelMatchPhase.ReconnectPause));
            Assert.That(match.ReconnectPlayer(1), Is.True);
            Assert.That(match.DisconnectPlayer(1, false), Is.True);

            Assert.That(match.Snapshot.Result.Value.WinnerIndex, Is.EqualTo(0));
            Assert.That(match.Snapshot.Result.Value.Reason, Is.EqualTo(DuelTerminationReason.ClientForfeit));
        }

        [Test]
        public void ClientReconnectTimeout_ForfeitsAfterConfiguredWindow()
        {
            var match = StartMatch(Rules(reconnect: 0.1f));
            match.DisconnectPlayer(0, false);

            match.Tick(DuelCommand.None, DuelCommand.None, 0.11f);

            Assert.That(match.Snapshot.Result.Value.WinnerIndex, Is.EqualTo(1));
            Assert.That(match.Snapshot.Result.Value.Reason, Is.EqualTo(DuelTerminationReason.ClientForfeit));
        }

        [Test]
        public void HostLoss_ProducesRatingSafeNoContest()
        {
            var match = StartMatch(DuelRuleSet.Default);

            match.DisconnectPlayer(0, true);

            Assert.That(match.Snapshot.Result.Value.WinnerIndex, Is.EqualTo(-1));
            Assert.That(match.Snapshot.Result.Value.Reason, Is.EqualTo(DuelTerminationReason.HostLost));
        }

        [Test]
        public void AbilityFeedbackSequences_IncrementOnce_WhileKnockbackAndStunStayUnchanged()
        {
            var match = StartMatch(DuelRuleSet.Default);
            var playerOneApproach = new DuelCommand { Movement = Vector2.right, Aim = Vector2.right };
            var playerTwoApproach = new DuelCommand { Movement = Vector2.left, Aim = Vector2.left };
            match.Tick(playerOneApproach, playerTwoApproach, 0.65f);
            var targetBefore = match.Snapshot.PlayerTwo.Position;

            match.Tick(
                new DuelCommand { Aim = Vector2.right, AbilityPressed = true },
                DuelCommand.None,
                0.01f);

            var snapshot = match.Snapshot;
            Assert.That(snapshot.PlayerOne.AbilitySequence, Is.EqualTo(1));
            Assert.That(snapshot.PlayerTwo.HitSequence, Is.EqualTo(1));
            Assert.That(snapshot.PlayerTwo.StunSeconds, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(Vector2.Distance(targetBefore, snapshot.PlayerTwo.Position), Is.EqualTo(1.25f).Within(0.001f));

            match.Tick(
                new DuelCommand { Aim = Vector2.right, AbilityPressed = true },
                DuelCommand.None,
                0.01f);
            Assert.That(match.Snapshot.PlayerOne.AbilitySequence, Is.EqualTo(1));
            Assert.That(match.Snapshot.PlayerTwo.HitSequence, Is.EqualTo(1));
        }

        [Test]
        public void CarrierOrbit_IsOutsidePlayerBody_AndUsesOppositePhasePerPlayer()
        {
            var carrier = new Vector3(2f, 0f, -1f);

            var playerOneCore = DuelCoreFeedback.GetCarrierOrbitPosition(carrier, 0f, 0);
            var playerTwoCore = DuelCoreFeedback.GetCarrierOrbitPosition(carrier, 0f, 1);

            Assert.That(Vector3.Distance(
                new Vector3(carrier.x, playerOneCore.y, carrier.z),
                playerOneCore), Is.EqualTo(1.15f).Within(0.001f));
            Assert.That(playerOneCore.y, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(playerTwoCore.x, Is.EqualTo(carrier.x - 1.15f).Within(0.001f));
        }

        private static DuelMatchSimulation StartMatch(DuelRuleSet rules)
        {
            var match = new DuelMatchSimulation(rules, DuelMatchMode.Ai, "test-match");
            match.Tick(DuelCommand.None, DuelCommand.None, 3.01f);
            return match;
        }

        private static DuelRuleSet Rules(
            float regulation = 180f,
            float overtime = 60f,
            int scoreLimit = 3,
            float hold = 15f,
            float reconnect = 20f)
        {
            return new DuelRuleSet(
                regulation, overtime, scoreLimit, hold, 3f, 0.75f, 3f, 1.5f,
                2.25f, 1.2f, 5f, 0.9f, 8f, reconnect);
        }
    }
}
