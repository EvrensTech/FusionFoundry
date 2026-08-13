using System;
using UnityEngine;

namespace DuelProtocol.Match
{
    public sealed class DuelMatchSimulation : IDuelMatchRuntime
    {
        private readonly DuelRuleSet _rules;
        private readonly DuelMatchMode _mode;
        private DuelPlayerState _playerOne;
        private DuelPlayerState _playerTwo;
        private DuelCoreState _core;
        private DuelMatchPhase _phase;
        private float _remainingSeconds;
        private float _elapsedSeconds;
        private float _countdownSeconds;
        private float _reconnectSeconds;
        private int _disconnectedPlayer = -1;
        private float _playerOneCarrySeconds;
        private float _playerTwoCarrySeconds;
        private DuelMatchResult? _result;

        public DuelMatchSimulation(
            DuelRuleSet rules,
            DuelMatchMode mode = DuelMatchMode.Ai,
            string matchId = null)
        {
            _rules = rules;
            _mode = mode;
            MatchId = string.IsNullOrWhiteSpace(matchId)
                ? Guid.NewGuid().ToString("N")
                : matchId;
            Reset();
        }

        public string MatchId { get; }

        public DuelMatchSnapshot Snapshot => CreateSnapshot();

        public void Reset()
        {
            _playerOne = CreatePlayer(new Vector2(-4f, 0f), Vector2.right);
            _playerTwo = CreatePlayer(new Vector2(4f, 0f), Vector2.left);
            _core = new DuelCoreState
            {
                Position = Vector2.zero,
                OwnerIndex = -1,
                RespawnSeconds = 0f,
                PickupLockedPlayer = -1,
                PickupLockSeconds = 0f
            };
            _phase = DuelMatchPhase.Countdown;
            _remainingSeconds = _rules.RegulationSeconds;
            _elapsedSeconds = 0f;
            _countdownSeconds = 3f;
            _reconnectSeconds = 0f;
            _disconnectedPlayer = -1;
            _playerOneCarrySeconds = 0f;
            _playerTwoCarrySeconds = 0f;
            _result = null;
        }

        public void Tick(DuelCommand playerOneCommand, DuelCommand playerTwoCommand, float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            if (_phase == DuelMatchPhase.Finished || deltaTime <= 0f)
            {
                return;
            }

            if (_phase == DuelMatchPhase.Countdown)
            {
                _countdownSeconds -= deltaTime;
                if (_countdownSeconds <= 0f)
                {
                    _phase = DuelMatchPhase.Playing;
                }
                return;
            }

            if (_phase == DuelMatchPhase.ReconnectPause)
            {
                _reconnectSeconds -= deltaTime;
                if (_reconnectSeconds <= 0f)
                {
                    Finish(
                        _disconnectedPlayer == 0 ? 1 : 0,
                        DuelTerminationReason.ClientForfeit);
                }
                return;
            }

            _elapsedSeconds += deltaTime;
            UpdateTimers(deltaTime);
            ApplyCommand(0, playerOneCommand, deltaTime);
            ApplyCommand(1, playerTwoCommand, deltaTime);
            UpdateCore(deltaTime);
            UpdateMatchClock(deltaTime);
        }

        public bool DisconnectPlayer(int playerIndex, bool isHost)
        {
            if (_phase == DuelMatchPhase.Finished || playerIndex < 0 || playerIndex > 1)
            {
                return false;
            }

            if (isHost)
            {
                Finish(-1, DuelTerminationReason.HostLost);
                return true;
            }

            var player = GetPlayer(playerIndex);
            if (player.ReconnectUsed)
            {
                Finish(playerIndex == 0 ? 1 : 0, DuelTerminationReason.ClientForfeit);
                return true;
            }

            player.Connected = false;
            player.ReconnectUsed = true;
            SetPlayer(playerIndex, player);
            _disconnectedPlayer = playerIndex;
            _reconnectSeconds = _rules.ReconnectSeconds;
            _phase = DuelMatchPhase.ReconnectPause;
            return true;
        }

        public bool ReconnectPlayer(int playerIndex)
        {
            if (_phase != DuelMatchPhase.ReconnectPause || playerIndex != _disconnectedPlayer)
            {
                return false;
            }

            var player = GetPlayer(playerIndex);
            player.Connected = true;
            SetPlayer(playerIndex, player);
            _disconnectedPlayer = -1;
            _reconnectSeconds = 0f;
            _phase = _remainingSeconds > 0f
                ? DuelMatchPhase.Playing
                : DuelMatchPhase.Overtime;
            return true;
        }

        public void Cancel()
        {
            if (_phase != DuelMatchPhase.Finished)
            {
                Finish(-1, DuelTerminationReason.Cancelled);
            }
        }

        private static DuelPlayerState CreatePlayer(Vector2 position, Vector2 facing)
        {
            return new DuelPlayerState
            {
                Position = position,
                Facing = facing,
                Score = 0,
                HoldSeconds = 0f,
                CooldownSeconds = 0f,
                StunSeconds = 0f,
                AbilitySequence = 0,
                HitSequence = 0,
                Connected = true,
                ReconnectUsed = false
            };
        }

        private void UpdateTimers(float deltaTime)
        {
            _playerOne.CooldownSeconds = Mathf.Max(0f, _playerOne.CooldownSeconds - deltaTime);
            _playerTwo.CooldownSeconds = Mathf.Max(0f, _playerTwo.CooldownSeconds - deltaTime);
            _playerOne.StunSeconds = Mathf.Max(0f, _playerOne.StunSeconds - deltaTime);
            _playerTwo.StunSeconds = Mathf.Max(0f, _playerTwo.StunSeconds - deltaTime);
            _core.RespawnSeconds = Mathf.Max(0f, _core.RespawnSeconds - deltaTime);
            _core.PickupLockSeconds = Mathf.Max(0f, _core.PickupLockSeconds - deltaTime);
            if (_core.PickupLockSeconds <= 0f)
            {
                _core.PickupLockedPlayer = -1;
            }
        }

        private void ApplyCommand(int playerIndex, DuelCommand command, float deltaTime)
        {
            var player = GetPlayer(playerIndex);
            if (!player.Connected || player.StunSeconds > 0f)
            {
                return;
            }

            var movement = Vector2.ClampMagnitude(command.Movement, 1f);
            var speed = _core.OwnerIndex == playerIndex
                ? _rules.MoveSpeed * _rules.CarrierSpeedMultiplier
                : _rules.MoveSpeed;
            player.MovementVelocity = movement * speed;
            player.Position += movement * speed * deltaTime;
            player.Position = new Vector2(
                Mathf.Clamp(player.Position.x, -_rules.ArenaHalfExtent, _rules.ArenaHalfExtent),
                Mathf.Clamp(player.Position.y, -_rules.ArenaHalfExtent, _rules.ArenaHalfExtent));

            // Attacks follow locomotion. A separate cursor/right-stick target is not
            // required; when stationary the last movement direction is retained.
            if (movement.sqrMagnitude > 0.001f)
            {
                player.Facing = movement.normalized;
            }

            SetPlayer(playerIndex, player);
            if (command.AbilityPressed)
            {
                TryUseAbility(playerIndex);
            }
        }

        private void TryUseAbility(int attackerIndex)
        {
            var attacker = GetPlayer(attackerIndex);
            if (attacker.CooldownSeconds > 0f || attacker.StunSeconds > 0f)
            {
                return;
            }

            attacker.CooldownSeconds = _rules.AbilityCooldownSeconds;
            attacker.AbilitySequence++;
            SetPlayer(attackerIndex, attacker);

            var targetIndex = attackerIndex == 0 ? 1 : 0;
            var target = GetPlayer(targetIndex);
            var offset = target.Position - attacker.Position;
            if (offset.sqrMagnitude > _rules.AbilityRange * _rules.AbilityRange)
            {
                return;
            }

            var direction = offset.sqrMagnitude > 0.001f ? offset.normalized : attacker.Facing;
            if (Vector2.Dot(attacker.Facing, direction) < 0.25f)
            {
                return;
            }

            target.StunSeconds = _rules.StunSeconds;
            target.HitSequence++;
            target.Position += direction * 1.25f;
            target.Position = new Vector2(
                Mathf.Clamp(target.Position.x, -_rules.ArenaHalfExtent, _rules.ArenaHalfExtent),
                Mathf.Clamp(target.Position.y, -_rules.ArenaHalfExtent, _rules.ArenaHalfExtent));
            SetPlayer(targetIndex, target);

            if (_core.OwnerIndex == targetIndex)
            {
                DropCore(targetIndex, target.Position);
            }
        }

        private void UpdateCore(float deltaTime)
        {
            if (_core.OwnerIndex >= 0)
            {
                var carrier = GetPlayer(_core.OwnerIndex);
                _core.Position = carrier.Position;
                carrier.HoldSeconds += deltaTime;
                if (_core.OwnerIndex == 0)
                {
                    _playerOneCarrySeconds += deltaTime;
                }
                else
                {
                    _playerTwoCarrySeconds += deltaTime;
                }
                if (carrier.HoldSeconds >= _rules.HoldSecondsPerPoint)
                {
                    carrier.Score++;
                    carrier.HoldSeconds = 0f;
                    SetPlayer(_core.OwnerIndex, carrier);
                    var scorer = _core.OwnerIndex;
                    _core.OwnerIndex = -1;
                    _core.RespawnSeconds = _rules.CoreRespawnSeconds;
                    _core.Position = Vector2.zero;
                    if (carrier.Score >= _rules.ScoreLimit)
                    {
                        Finish(
                            scorer,
                            _phase == DuelMatchPhase.Overtime
                                ? DuelTerminationReason.OvertimeScore
                                : DuelTerminationReason.ScoreLimit);
                    }
                    else if (_phase == DuelMatchPhase.Overtime)
                    {
                        Finish(scorer, DuelTerminationReason.OvertimeScore);
                    }
                }
                else
                {
                    SetPlayer(_core.OwnerIndex, carrier);
                }
                return;
            }

            if (!_core.IsAvailable)
            {
                return;
            }

            var oneDistance = Vector2.Distance(_playerOne.Position, _core.Position);
            var twoDistance = Vector2.Distance(_playerTwo.Position, _core.Position);
            var oneEligible = _core.PickupLockedPlayer != 0 && oneDistance <= _rules.PickupRange;
            var twoEligible = _core.PickupLockedPlayer != 1 && twoDistance <= _rules.PickupRange;

            if (!oneEligible && !twoEligible)
            {
                return;
            }

            var ownerIndex = oneEligible && twoEligible
                ? (oneDistance <= twoDistance ? 0 : 1)
                : (oneEligible ? 0 : 1);
            _core.OwnerIndex = ownerIndex;
            var owner = GetPlayer(ownerIndex);
            owner.HoldSeconds = 0f;
            SetPlayer(ownerIndex, owner);
        }

        private void DropCore(int ownerIndex, Vector2 position)
        {
            var owner = GetPlayer(ownerIndex);
            owner.HoldSeconds = 0f;
            SetPlayer(ownerIndex, owner);
            _core.OwnerIndex = -1;
            _core.Position = position;
            _core.PickupLockedPlayer = ownerIndex;
            _core.PickupLockSeconds = _rules.PickupLockSeconds;
        }

        private void UpdateMatchClock(float deltaTime)
        {
            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - deltaTime);
            if (_remainingSeconds > 0f)
            {
                return;
            }

            if (_playerOne.Score != _playerTwo.Score)
            {
                Finish(
                    _playerOne.Score > _playerTwo.Score ? 0 : 1,
                    DuelTerminationReason.TimeExpired);
                return;
            }

            if (_phase == DuelMatchPhase.Playing && _rules.OvertimeSeconds > 0f)
            {
                _phase = DuelMatchPhase.Overtime;
                _remainingSeconds = _rules.OvertimeSeconds;
                return;
            }

            Finish(-1, DuelTerminationReason.Draw);
        }

        private DuelPlayerState GetPlayer(int index)
        {
            return index == 0 ? _playerOne : _playerTwo;
        }

        private void SetPlayer(int index, DuelPlayerState player)
        {
            if (index == 0)
            {
                _playerOne = player;
            }
            else
            {
                _playerTwo = player;
            }
        }

        private void Finish(int winnerIndex, DuelTerminationReason reason)
        {
            _phase = DuelMatchPhase.Finished;
            _result = new DuelMatchResult
            {
                MatchId = MatchId,
                Mode = _mode,
                WinnerIndex = winnerIndex,
                PlayerOneScore = _playerOne.Score,
                PlayerTwoScore = _playerTwo.Score,
                Reason = reason,
                DurationSeconds = _elapsedSeconds,
                PlayerOneCarrySeconds = _playerOneCarrySeconds,
                PlayerTwoCarrySeconds = _playerTwoCarrySeconds
            };
        }

        private DuelMatchSnapshot CreateSnapshot()
        {
            return new DuelMatchSnapshot
            {
                MatchId = MatchId,
                Phase = _phase,
                PlayerOne = _playerOne,
                PlayerTwo = _playerTwo,
                Core = _core,
                RemainingSeconds = _phase == DuelMatchPhase.ReconnectPause
                    ? _reconnectSeconds
                    : _remainingSeconds,
                Result = _result
            };
        }
    }
}
