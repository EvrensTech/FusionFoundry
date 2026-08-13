using System;
using UnityEngine;

namespace DuelProtocol.Match
{
    public enum DuelMatchPhase
    {
        WaitingForPlayers,
        ReadyCheck,
        Countdown,
        Playing,
        ReconnectPause,
        Overtime,
        Finished
    }

    public enum DuelMatchMode
    {
        Ai,
        Private,
        Unranked,
        Ranked
    }

    public enum DuelTerminationReason
    {
        None,
        ScoreLimit,
        TimeExpired,
        OvertimeScore,
        Draw,
        ClientForfeit,
        HostLost,
        Cancelled
    }

    [Serializable]
    public struct DuelCommand
    {
        public Vector2 Movement;
        public Vector2 Aim;
        public bool AbilityPressed;

        public static DuelCommand None => new DuelCommand
        {
            Movement = Vector2.zero,
            Aim = Vector2.up,
            AbilityPressed = false
        };
    }

    public interface IDuelCommandSource
    {
        DuelCommand ReadCommand(DuelMatchSnapshot snapshot, int playerIndex);
    }

    public interface IDuelMatchRules
    {
        float RegulationSeconds { get; }
        float OvertimeSeconds { get; }
        int ScoreLimit { get; }
        float HoldSecondsPerPoint { get; }
        float ReconnectSeconds { get; }
    }

    public interface IDuelMatchRuntime
    {
        string MatchId { get; }
        DuelMatchSnapshot Snapshot { get; }
        void Reset();
        void Tick(DuelCommand playerOneCommand, DuelCommand playerTwoCommand, float deltaTime);
        bool DisconnectPlayer(int playerIndex, bool isHost);
        bool ReconnectPlayer(int playerIndex);
        void Cancel();
    }

    [CreateAssetMenu(menuName = "Duel Protocol/Match Config", fileName = "DuelMatchConfig")]
    public sealed class DuelMatchConfig : ScriptableObject
    {
        [SerializeField, Min(30f)] private float regulationSeconds = 180f;
        [SerializeField, Min(0f)] private float overtimeSeconds = 60f;
        [SerializeField, Min(1)] private int scoreLimit = 3;
        [SerializeField, Min(1f)] private float holdSecondsPerPoint = 15f;
        [SerializeField, Min(0f)] private float coreRespawnSeconds = 3f;
        [SerializeField, Min(0f)] private float pickupLockSeconds = 0.75f;
        [SerializeField, Min(0f)] private float abilityCooldownSeconds = 3f;
        [SerializeField, Min(0f)] private float stunSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float abilityRange = 2.25f;
        [SerializeField, Min(0.1f)] private float pickupRange = 1.2f;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Range(0.1f, 1f)] private float carrierSpeedMultiplier = 0.9f;
        [SerializeField, Min(1f)] private float arenaHalfExtent = 8f;
        [SerializeField, Min(0f)] private float reconnectSeconds = 20f;

        public float RegulationSeconds => regulationSeconds;
        public float OvertimeSeconds => overtimeSeconds;
        public int ScoreLimit => scoreLimit;
        public float HoldSecondsPerPoint => holdSecondsPerPoint;
        public float CoreRespawnSeconds => coreRespawnSeconds;
        public float PickupLockSeconds => pickupLockSeconds;
        public float AbilityCooldownSeconds => abilityCooldownSeconds;
        public float StunSeconds => stunSeconds;
        public float AbilityRange => abilityRange;
        public float PickupRange => pickupRange;
        public float MoveSpeed => moveSpeed;
        public float CarrierSpeedMultiplier => carrierSpeedMultiplier;
        public float ArenaHalfExtent => arenaHalfExtent;
        public float ReconnectSeconds => reconnectSeconds;

        public DuelRuleSet ToRuleSet()
        {
            return new DuelRuleSet(
                regulationSeconds,
                overtimeSeconds,
                scoreLimit,
                holdSecondsPerPoint,
                coreRespawnSeconds,
                pickupLockSeconds,
                abilityCooldownSeconds,
                stunSeconds,
                abilityRange,
                pickupRange,
                moveSpeed,
                carrierSpeedMultiplier,
                arenaHalfExtent,
                reconnectSeconds);
        }
    }

    [Serializable]
    public struct DuelRuleSet : IDuelMatchRules
    {
        public DuelRuleSet(
            float regulationSeconds,
            float overtimeSeconds,
            int scoreLimit,
            float holdSecondsPerPoint,
            float coreRespawnSeconds,
            float pickupLockSeconds,
            float abilityCooldownSeconds,
            float stunSeconds,
            float abilityRange,
            float pickupRange,
            float moveSpeed,
            float carrierSpeedMultiplier,
            float arenaHalfExtent,
            float reconnectSeconds)
        {
            RegulationSeconds = Mathf.Max(1f, regulationSeconds);
            OvertimeSeconds = Mathf.Max(0f, overtimeSeconds);
            ScoreLimit = Mathf.Max(1, scoreLimit);
            HoldSecondsPerPoint = Mathf.Max(0.1f, holdSecondsPerPoint);
            CoreRespawnSeconds = Mathf.Max(0f, coreRespawnSeconds);
            PickupLockSeconds = Mathf.Max(0f, pickupLockSeconds);
            AbilityCooldownSeconds = Mathf.Max(0f, abilityCooldownSeconds);
            StunSeconds = Mathf.Max(0f, stunSeconds);
            AbilityRange = Mathf.Max(0.1f, abilityRange);
            PickupRange = Mathf.Max(0.1f, pickupRange);
            MoveSpeed = Mathf.Max(0f, moveSpeed);
            CarrierSpeedMultiplier = Mathf.Clamp(carrierSpeedMultiplier, 0.1f, 1f);
            ArenaHalfExtent = Mathf.Max(1f, arenaHalfExtent);
            ReconnectSeconds = Mathf.Max(0f, reconnectSeconds);
        }

        public float RegulationSeconds { get; }
        public float OvertimeSeconds { get; }
        public int ScoreLimit { get; }
        public float HoldSecondsPerPoint { get; }
        public float CoreRespawnSeconds { get; }
        public float PickupLockSeconds { get; }
        public float AbilityCooldownSeconds { get; }
        public float StunSeconds { get; }
        public float AbilityRange { get; }
        public float PickupRange { get; }
        public float MoveSpeed { get; }
        public float CarrierSpeedMultiplier { get; }
        public float ArenaHalfExtent { get; }
        public float ReconnectSeconds { get; }

        public static DuelRuleSet Default => new DuelRuleSet(
            180f, 60f, 3, 15f, 3f, 0.75f, 3f, 1.5f,
            2.25f, 1.2f, 5f, 0.9f, 8f, 20f);
    }

    [Serializable]
    public struct DuelPlayerState
    {
        public Vector2 Position;
        public Vector2 Facing;
        public Vector2 MovementVelocity;
        public int Score;
        public float HoldSeconds;
        public float CooldownSeconds;
        public float StunSeconds;
        public int AbilitySequence;
        public int HitSequence;
        public bool Connected;
        public bool ReconnectUsed;
    }

    [Serializable]
    public struct DuelCoreState
    {
        public Vector2 Position;
        public int OwnerIndex;
        public float RespawnSeconds;
        public int PickupLockedPlayer;
        public float PickupLockSeconds;

        public bool IsAvailable => OwnerIndex < 0 && RespawnSeconds <= 0f;
    }

    [Serializable]
    public struct DuelMatchResult
    {
        public string MatchId;
        public DuelMatchMode Mode;
        public int WinnerIndex;
        public int PlayerOneScore;
        public int PlayerTwoScore;
        public DuelTerminationReason Reason;
        public float DurationSeconds;
        public float PlayerOneCarrySeconds;
        public float PlayerTwoCarrySeconds;

        public bool IsDraw => WinnerIndex < 0;
    }

    public sealed class DuelMatchSnapshot
    {
        public string MatchId { get; internal set; }
        public DuelMatchPhase Phase { get; internal set; }
        public DuelPlayerState PlayerOne { get; internal set; }
        public DuelPlayerState PlayerTwo { get; internal set; }
        public DuelCoreState Core { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        public DuelMatchResult? Result { get; internal set; }

        public DuelPlayerState GetPlayer(int index)
        {
            return index == 0 ? PlayerOne : PlayerTwo;
        }
    }
}
