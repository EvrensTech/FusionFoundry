using Fusion;
using FusionFoundry.Input;
using UnityEngine;

namespace FusionFoundry.Players
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class SampleNetworkPlayerMotor : NetworkBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float movementSpeed = 4f;

        [SerializeField]
        [Min(0f)]
        private float turnSpeedDegrees = 120f;

        public float MovementSpeed => movementSpeed;

        public float TurnSpeedDegrees => turnSpeedDegrees;

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out FusionInputData input))
            {
                return;
            }

            CalculateMotion(
                transform.position,
                transform.rotation,
                input.Movement,
                input.Turn,
                movementSpeed,
                turnSpeedDegrees,
                Runner.DeltaTime,
                out var position,
                out var rotation);

            transform.SetPositionAndRotation(
                position,
                rotation);
        }

        public static void CalculateMotion(
            Vector3 currentPosition,
            Quaternion currentRotation,
            Vector2 worldMovement,
            float turnInput,
            float speed,
            float turnSpeed,
            float deltaTime,
            out Vector3 position,
            out Quaternion rotation)
        {
            rotation = currentRotation * Quaternion.Euler(
                0f,
                CalculateTurnDeltaDegrees(
                    turnInput,
                    turnSpeed,
                    deltaTime),
                0f);
            position = currentPosition + CalculateWorldDisplacement(
                worldMovement,
                speed,
                deltaTime);
        }

        public static float CalculateTurnDeltaDegrees(
            float turnInput,
            float turnSpeed,
            float deltaTime)
        {
            return Mathf.Clamp(turnInput, -1f, 1f) *
                Mathf.Max(0f, turnSpeed) *
                Mathf.Max(0f, deltaTime);
        }

        public static Vector3 CalculateWorldDisplacement(
            Vector2 worldMovement,
            float speed,
            float deltaTime)
        {
            var movement = Vector2.ClampMagnitude(worldMovement, 1f);
            return new Vector3(movement.x, 0f, movement.y) *
                Mathf.Max(0f, speed) *
                Mathf.Max(0f, deltaTime);
        }
    }
}
