using System;
using DuelProtocol.Match;
using DuelProtocol.Presentation;
using UnityEngine;

namespace DuelProtocol.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DuelMatchController : MonoBehaviour
    {
        [SerializeField] private DuelMatchConfig config;
        [SerializeField] private DuelMatchMode mode = DuelMatchMode.Ai;
        [SerializeField] private Transform playerOneTransform;
        [SerializeField] private Transform playerTwoTransform;
        [SerializeField] private Transform coreTransform;
        [SerializeField] private MonoBehaviour playerOneCommandSource;
        [SerializeField] private MonoBehaviour playerTwoCommandSource;

        private DuelMatchSimulation _simulation;
        private IDuelCommandSource _playerOneSource;
        private IDuelCommandSource _playerTwoSource;
        private DuelMatchPhase _previousPhase;
        private DuelCombatFeedback _playerOneFeedback;
        private DuelCombatFeedback _playerTwoFeedback;
        private DuelCoreFeedback _coreFeedback;
        private DuelCarrierTimerDisplay _playerOneTimer;
        private DuelCarrierTimerDisplay _playerTwoTimer;
        private float _holdSecondsPerPoint;

        public event Action<DuelMatchSnapshot> SnapshotChanged;
        public event Action<DuelMatchSnapshot> MatchStarted;
        public event Action<DuelMatchResult> MatchFinished;

        public DuelMatchSnapshot Snapshot => _simulation?.Snapshot;

        private void Awake()
        {
            _playerOneSource = playerOneCommandSource as IDuelCommandSource;
            _playerTwoSource = playerTwoCommandSource as IDuelCommandSource;
            StartNewMatch();
        }

        private void Update()
        {
            if (_simulation == null)
            {
                return;
            }

            var snapshot = _simulation.Snapshot;
            var playerOne = _playerOneSource?.ReadCommand(snapshot, 0) ?? DuelCommand.None;
            var playerTwo = _playerTwoSource?.ReadCommand(snapshot, 1) ?? DuelCommand.None;
            _simulation.Tick(playerOne, playerTwo, Time.deltaTime);
            snapshot = _simulation.Snapshot;
            ApplySnapshot(snapshot);
            SnapshotChanged?.Invoke(snapshot);

            if (_previousPhase != DuelMatchPhase.Finished &&
                snapshot.Phase == DuelMatchPhase.Finished &&
                snapshot.Result.HasValue)
            {
                MatchFinished?.Invoke(snapshot.Result.Value);
            }
            _previousPhase = snapshot.Phase;
        }

        public void Configure(
            Transform playerOne,
            Transform playerTwo,
            Transform core,
            MonoBehaviour playerOneSource,
            MonoBehaviour playerTwoSource,
            DuelMatchConfig matchConfig = null,
            DuelMatchMode matchMode = DuelMatchMode.Ai)
        {
            playerOneTransform = playerOne;
            playerTwoTransform = playerTwo;
            coreTransform = core;
            playerOneCommandSource = playerOneSource;
            playerTwoCommandSource = playerTwoSource;
            config = matchConfig;
            mode = matchMode;
            _playerOneSource = playerOneSource as IDuelCommandSource;
            _playerTwoSource = playerTwoSource as IDuelCommandSource;
            StartNewMatch();
        }

        public void StartNewMatch()
        {
            var rules = config != null ? config.ToRuleSet() : DuelRuleSet.Default;
            _holdSecondsPerPoint = rules.HoldSecondsPerPoint;
            _simulation = new DuelMatchSimulation(rules, mode);
            _previousPhase = _simulation.Snapshot.Phase;
            EnsurePresentationFeedback();
            _playerOneFeedback?.ResetPresentation();
            _playerTwoFeedback?.ResetPresentation();
            _coreFeedback?.SetRespawning(false, false);
            ApplySnapshot(_simulation.Snapshot);
            MatchStarted?.Invoke(_simulation.Snapshot);
        }

        public bool DisconnectPlayer(int playerIndex, bool isHost)
        {
            return _simulation != null && _simulation.DisconnectPlayer(playerIndex, isHost);
        }

        public bool ReconnectPlayer(int playerIndex)
        {
            return _simulation != null && _simulation.ReconnectPlayer(playerIndex);
        }

        private void ApplySnapshot(DuelMatchSnapshot snapshot)
        {
            ApplyPlayer(playerOneTransform, snapshot.PlayerOne, _playerOneFeedback);
            ApplyPlayer(playerTwoTransform, snapshot.PlayerTwo, _playerTwoFeedback);
            if (coreTransform != null)
            {
                var respawning = snapshot.Core.RespawnSeconds > 0f;
                if (snapshot.Core.OwnerIndex >= 0)
                {
                    var carrier = snapshot.Core.OwnerIndex == 0 ? playerOneTransform : playerTwoTransform;
                    if (carrier != null)
                    {
                        coreTransform.position = DuelCoreFeedback.GetCarrierOrbitPosition(
                            carrier.position,
                            Time.time,
                            snapshot.Core.OwnerIndex);
                    }
                }
                else
                {
                    coreTransform.position = ToWorld(snapshot.Core.Position, 0.6f);
                }
                _coreFeedback?.SetRespawning(respawning);
            }
            _playerOneTimer?.SetRemaining(
                _holdSecondsPerPoint - snapshot.PlayerOne.HoldSeconds,
                snapshot.Core.OwnerIndex == 0);
            _playerTwoTimer?.SetRemaining(
                _holdSecondsPerPoint - snapshot.PlayerTwo.HoldSeconds,
                snapshot.Core.OwnerIndex == 1);
        }

        private static void ApplyPlayer(
            Transform target,
            DuelPlayerState player,
            DuelCombatFeedback feedback)
        {
            if (target == null)
            {
                return;
            }
            target.position = ToWorld(player.Position, 0.75f);
            if (player.Facing.sqrMagnitude > 0.001f)
            {
                target.rotation = Quaternion.LookRotation(
                    new Vector3(player.Facing.x, 0f, player.Facing.y),
                    Vector3.up);
            }
            feedback?.Present(
                player.AbilitySequence,
                player.HitSequence,
                player.StunSeconds > 0f,
                player.Facing,
                player.MovementVelocity);
        }

        private void EnsurePresentationFeedback()
        {
            if (playerOneTransform != null)
            {
                _playerOneFeedback = playerOneTransform.GetComponent<DuelCombatFeedback>() ??
                                     playerOneTransform.gameObject.AddComponent<DuelCombatFeedback>();
                _playerOneTimer = playerOneTransform.GetComponent<DuelCarrierTimerDisplay>() ??
                                  playerOneTransform.gameObject.AddComponent<DuelCarrierTimerDisplay>();
            }
            if (playerTwoTransform != null)
            {
                _playerTwoFeedback = playerTwoTransform.GetComponent<DuelCombatFeedback>() ??
                                     playerTwoTransform.gameObject.AddComponent<DuelCombatFeedback>();
                _playerTwoTimer = playerTwoTransform.GetComponent<DuelCarrierTimerDisplay>() ??
                                  playerTwoTransform.gameObject.AddComponent<DuelCarrierTimerDisplay>();
            }
            if (coreTransform != null)
            {
                _coreFeedback = coreTransform.GetComponent<DuelCoreFeedback>() ??
                                coreTransform.gameObject.AddComponent<DuelCoreFeedback>();
            }
        }

        private static Vector3 ToWorld(Vector2 value, float y)
        {
            return new Vector3(value.x, y, value.y);
        }
    }
}
