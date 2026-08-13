using Fusion;
using DuelProtocol.Presentation;
using UnityEngine;

namespace DuelProtocol.Networking
{
    // Networked properties are woven in both Editor and player builds.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class DuelNetworkPlayer : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float arenaHalfExtent = 8f;
        [SerializeField, Min(0f)] private float stunSeconds = 1.5f;

        [Networked] public Vector2 Facing { get; set; }
        [Networked] public Vector2 MovementVelocity { get; set; }
        [Networked] public Vector2 AttackInheritedVelocity { get; set; }
        [Networked] public TickTimer AbilityCooldown { get; set; }
        [Networked] public TickTimer StunTimer { get; set; }
        [Networked] public int AbilitySequence { get; set; }
        [Networked] public int HitSequence { get; set; }
        [Networked] public NetworkString<_64> ServicePlayerId { get; set; }
        private DuelCombatFeedback _combatFeedback;
        private DuelCarrierTimerDisplay _carrierTimer;

        public float AbilityCooldownRemaining =>
            Runner == null ? 0f : Mathf.Max(0f, AbilityCooldown.RemainingTime(Runner) ?? 0f);

        public override void Spawned()
        {
            if (HasStateAuthority && Facing.sqrMagnitude < 0.001f) Facing = Vector2.up;
            var cyanTeam = Object.InputAuthority.AsIndex % 2 != 0;
            DuelWorldArtDirector.StylePlayer(
                gameObject,
                cyanTeam ? DuelPalette.Cyan : DuelPalette.Red,
                Object.HasInputAuthority);
            _combatFeedback = GetComponent<DuelCombatFeedback>() ??
                              gameObject.AddComponent<DuelCombatFeedback>();
            _carrierTimer = GetComponent<DuelCarrierTimerDisplay>() ??
                            gameObject.AddComponent<DuelCarrierTimerDisplay>();
            _combatFeedback.ResetPresentation(AbilitySequence, HitSequence);
            _carrierTimer.SetRemaining(0f, false);
        }

        public override void Render()
        {
            _combatFeedback?.Present(
                AbilitySequence,
                HitSequence,
                !StunTimer.ExpiredOrNotRunning(Runner),
                Facing,
                AttackInheritedVelocity);
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out DuelNetworkInputData input)) return;

            var matchState = FindAnyObjectByType<DuelNetworkMatchState>();

            if (HasStateAuthority)
            {
                if (string.IsNullOrWhiteSpace(ServicePlayerId.ToString()) &&
                    !string.IsNullOrWhiteSpace(input.ServicePlayerId.ToString()))
                {
                    ServicePlayerId = input.ServicePlayerId;
                    Debug.Log(
                        $"DUEL_NETWORK_IDENTITY_REGISTERED player={Object.InputAuthority} " +
                        $"servicePlayerId={ServicePlayerId}");
                }
                if (input.ReadyRequested) matchState?.ApplyReadyFromInput(Object.InputAuthority);
                if (input.RematchRequested) matchState?.ApplyRematchFromInput(Object.InputAuthority);
            }

            if (matchState == null ||
                (matchState.Phase != DuelProtocol.Match.DuelMatchPhase.Playing &&
                 matchState.Phase != DuelProtocol.Match.DuelMatchPhase.Overtime))
            {
                MovementVelocity = Vector2.zero;
                return;
            }
            if (!StunTimer.ExpiredOrNotRunning(Runner))
            {
                MovementVelocity = Vector2.zero;
                return;
            }

            var movement = Vector2.ClampMagnitude(input.Movement, 1f);
            MovementVelocity = movement * moveSpeed;
            var position = transform.position + new Vector3(movement.x, 0f, movement.y) * moveSpeed * Runner.DeltaTime;
            position.x = Mathf.Clamp(position.x, -arenaHalfExtent, arenaHalfExtent);
            position.z = Mathf.Clamp(position.z, -arenaHalfExtent, arenaHalfExtent);
            transform.position = position;

            if (movement.sqrMagnitude > 0.001f)
            {
                Facing = movement.normalized;
                transform.rotation = Quaternion.LookRotation(new Vector3(Facing.x, 0f, Facing.y), Vector3.up);
            }

            if (input.AbilityPressed && AbilityCooldown.ExpiredOrNotRunning(Runner))
            {
                AbilityCooldown = TickTimer.CreateFromSeconds(Runner, 3f);
                AttackInheritedVelocity = MovementVelocity;
                AbilitySequence++;
                if (HasStateAuthority) ApplyAbility();
            }
        }

        public void ApplyStun(float seconds)
        {
            if (HasStateAuthority) StunTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, seconds));
        }

        public void SetCoreTimer(float remainingSeconds, bool visible)
        {
            _carrierTimer?.SetRemaining(remainingSeconds, visible);
        }

        private void ApplyAbility()
        {
            DuelNetworkPlayer bestTarget = null;
            var bestDistance = float.MaxValue;
            foreach (var playerRef in Runner.ActivePlayers)
            {
                var playerObject = Runner.GetPlayerObject(playerRef);
                if (playerObject == null || playerObject == Object) continue;
                var candidate = playerObject.GetComponent<DuelNetworkPlayer>();
                if (candidate == null) continue;
                var offset = candidate.transform.position - transform.position;
                var planar = new Vector2(offset.x, offset.z);
                var distance = planar.magnitude;
                if (distance <= 0.001f || distance > 2.25f || distance >= bestDistance ||
                    Vector2.Dot(Facing, planar.normalized) < 0.25f) continue;
                bestTarget = candidate;
                bestDistance = distance;
            }

            if (bestTarget == null) return;
            bestTarget.HitSequence++;
            bestTarget.ApplyStun(stunSeconds);
            var push = bestTarget.transform.position - transform.position;
            push.y = 0f;
            if (push.sqrMagnitude > 0.001f) bestTarget.transform.position += push.normalized * 1.25f;
            FindAnyObjectByType<DuelNetworkCore>()?.TryDrop(
                bestTarget.Object.InputAuthority,
                bestTarget.transform.position);
        }
    }
}
