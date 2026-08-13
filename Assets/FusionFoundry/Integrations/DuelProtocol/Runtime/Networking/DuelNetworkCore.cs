using Fusion;
using DuelProtocol.Presentation;
using UnityEngine;

namespace DuelProtocol.Networking
{
    // Host state authority is the only writer for core state.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DuelNetworkCore : NetworkBehaviour
    {
        [SerializeField] private DuelNetworkMatchState matchState;
        [SerializeField, Min(0.1f)] private float pickupRange = 1.2f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 15f;
        [SerializeField, Min(0f)] private float respawnSeconds = 3f;
        [SerializeField, Min(0f)] private float pickupLockSeconds = 0.75f;
        [SerializeField, Min(0.1f)] private float orbitRadius = 1.15f;
        [SerializeField, Min(0.1f)] private float orbitHeight = 1.25f;
        [SerializeField, Min(1f)] private float orbitDegreesPerSecond = 150f;

        [Networked] public PlayerRef Owner { get; set; }
        [Networked] public PlayerRef PickupLockedPlayer { get; set; }
        [Networked] public Vector3 AuthoritativePosition { get; set; }
        [Networked] public TickTimer HoldTimer { get; set; }
        [Networked] public TickTimer RespawnTimer { get; set; }
        [Networked] public TickTimer PickupLockTimer { get; set; }
        private DuelCoreFeedback _coreFeedback;
        private PlayerRef _timerOwner = PlayerRef.None;

        public override void Spawned()
        {
            Debug.Log(
                $"DUEL_NETWORK_WORLD_SPAWNED role={(HasStateAuthority ? "authority" : "proxy")} " +
                $"tick={Runner.Tick.Raw} object={Object.Id}");
            DuelWorldArtDirector.StyleCore(gameObject);
            _coreFeedback = GetComponent<DuelCoreFeedback>() ??
                            gameObject.AddComponent<DuelCoreFeedback>();
            _coreFeedback.SetRespawning(!RespawnTimer.ExpiredOrNotRunning(Runner), false);
            if (!HasStateAuthority) return;
            if (matchState == null) matchState = GetComponent<DuelNetworkMatchState>();
            Owner = PlayerRef.None;
            PickupLockedPlayer = PlayerRef.None;
            AuthoritativePosition = transform.position;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                transform.position = AuthoritativePosition;
                return;
            }

            if (Owner != PlayerRef.None)
            {
                matchState?.RecordCarry(Owner, Runner.DeltaTime);
                var ownerObject = Runner.GetPlayerObject(Owner);
                if (ownerObject == null)
                {
                    TryDrop(Owner, AuthoritativePosition);
                    return;
                }
                AuthoritativePosition = DuelCoreFeedback.GetCarrierOrbitPosition(
                    ownerObject.transform.position,
                    Runner.SimulationTime,
                    Owner.AsIndex - 1,
                    orbitRadius,
                    orbitHeight,
                    orbitDegreesPerSecond);
                transform.position = AuthoritativePosition;
                if (HoldTimer.Expired(Runner))
                {
                    matchState?.TryAwardPoint(GetPlayerIndex(Owner), 3);
                    Owner = PlayerRef.None;
                    RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnSeconds);
                    AuthoritativePosition = Vector3.up * 0.6f;
                    transform.position = AuthoritativePosition;
                    _coreFeedback?.SetRespawning(true, false);
                }
                return;
            }

            if (!RespawnTimer.ExpiredOrNotRunning(Runner)) return;
            if (PickupLockTimer.ExpiredOrNotRunning(Runner)) PickupLockedPlayer = PlayerRef.None;
            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (playerRef == PickupLockedPlayer) continue;
                var playerObject = Runner.GetPlayerObject(playerRef);
                if (playerObject == null ||
                    Vector3.Distance(playerObject.transform.position, AuthoritativePosition) > pickupRange) continue;
                Owner = playerRef;
                HoldTimer = TickTimer.CreateFromSeconds(Runner, holdSeconds);
                break;
            }
        }

        public override void Render()
        {
            transform.position = AuthoritativePosition;
            _coreFeedback?.SetRespawning(!RespawnTimer.ExpiredOrNotRunning(Runner));
            UpdateCarrierTimer();
        }

        private void UpdateCarrierTimer()
        {
            if (_timerOwner != PlayerRef.None && _timerOwner != Owner)
            {
                var previousPlayer = Runner.GetPlayerObject(_timerOwner);
                previousPlayer?.GetComponent<DuelNetworkPlayer>()?.SetCoreTimer(0f, false);
            }

            _timerOwner = Owner;
            if (Owner == PlayerRef.None) return;
            var remaining = HoldTimer.RemainingTime(Runner) ?? 0f;
            var ownerPlayer = Runner.GetPlayerObject(Owner);
            ownerPlayer?.GetComponent<DuelNetworkPlayer>()?.SetCoreTimer(remaining, true);
        }

        public bool TryDrop(PlayerRef owner, Vector3 position)
        {
            if (!HasStateAuthority || Owner != owner) return false;
            Owner = PlayerRef.None;
            PickupLockedPlayer = owner;
            PickupLockTimer = TickTimer.CreateFromSeconds(Runner, pickupLockSeconds);
            AuthoritativePosition = new Vector3(position.x, 0.6f, position.z);
            HoldTimer = TickTimer.None;
            return true;
        }

        private int GetPlayerIndex(PlayerRef player)
        {
            var index = 0;
            foreach (var candidate in Runner.ActivePlayers)
            {
                if (candidate == player) return index;
                index++;
            }
            return -1;
        }
    }
}
