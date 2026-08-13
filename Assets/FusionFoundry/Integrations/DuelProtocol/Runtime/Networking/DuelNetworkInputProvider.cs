using System;
using DuelProtocol.Match;
using DuelProtocol.Gameplay;
using Fusion;
using FusionFoundry.Sessions;
using UnityEngine;

namespace DuelProtocol.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class DuelNetworkInputProvider : NetworkRunnerCallbacksBehaviour
    {
        [SerializeField] private MonoBehaviour commandSource;
        private IDuelCommandSource _source;
        private LocalDuelCommandSource _localSource;
        private DuelMatchSnapshot _snapshot;
        private DuelNetworkMatchState _matchState;

        public void Configure(IDuelCommandSource source, DuelMatchSnapshot snapshot)
        {
            _source = source;
            _snapshot = snapshot;
        }

        private void Awake()
        {
            _source = commandSource as IDuelCommandSource;
            _localSource = commandSource as LocalDuelCommandSource;
        }

        public void SetSnapshot(DuelMatchSnapshot snapshot) => _snapshot = snapshot;

        public override void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var command = _source?.ReadCommand(_snapshot, 0) ?? DuelCommand.None;
            AcknowledgeSessionRequests(runner);
            var localReadyRequested = _localSource != null && _localSource.ReadyRequested;
            var hasReadyTicket = DuelNetworkSessionContext.HasReadyTicket(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (localReadyRequested && !hasReadyTicket)
            {
                _ = DuelNetworkSessionContext.EnsureTicketAsync();
            }
            // Ready is a gameplay gate and must not be blocked by a slow UGS ticket
            // refresh. Ticket creation continues in parallel for result settlement.
            var readyRequested = localReadyRequested;
            var rematchRequested = _localSource != null && _localSource.RematchRequested;
            input.Set(new DuelNetworkInputData
            {
                Movement = Vector2.ClampMagnitude(command.Movement, 1f),
                Aim = command.Aim.sqrMagnitude > 0.001f ? command.Aim.normalized : Vector2.up,
                AbilityPressed = command.AbilityPressed,
                ReadyRequested = readyRequested,
                RematchRequested = rematchRequested,
                ServicePlayerId = (NetworkString<_64>)(
                    DuelNetworkSessionContext.LocalPlayerId ?? string.Empty)
            });
        }

        private void AcknowledgeSessionRequests(NetworkRunner runner)
        {
            if (_localSource == null || runner == null || runner.LocalPlayer == PlayerRef.None) return;
            if (_matchState == null) _matchState = FindAnyObjectByType<DuelNetworkMatchState>();
            if (_matchState == null) return;

            if (_localSource.ReadyRequested &&
                (_matchState.Phase != DuelMatchPhase.ReadyCheck ||
                 _matchState.IsReady(runner.LocalPlayer)))
            {
                _localSource.ClearReadyRequest();
            }
            if (_localSource.RematchRequested &&
                (_matchState.Phase != DuelMatchPhase.Finished ||
                 _matchState.IsRematchRequested(runner.LocalPlayer)))
            {
                _localSource.ClearRematchRequest();
            }
        }
    }
}
