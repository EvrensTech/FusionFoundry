using Fusion;
using FusionFoundry.Sessions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FusionFoundry.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunner))]
    public class FusionInputProvider : NetworkRunnerCallbacksBehaviour
    {
        [SerializeField]
        private InputActionReference moveAction;

        private bool _ownsActionEnable;

        public InputActionReference MoveAction => moveAction;

        protected virtual void OnEnable()
        {
            var action = moveAction?.action;
            if (action != null && !action.enabled)
            {
                action.Enable();
                _ownsActionEnable = true;
            }
        }

        protected virtual void OnDisable()
        {
            if (_ownsActionEnable)
            {
                moveAction?.action?.Disable();
                _ownsActionEnable = false;
            }
        }

        public override void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var movement = ReadMovement();
            var cameraForward = NormalizeDirection(ReadCameraForward());
            input.Set(new FusionInputData
            {
                Movement = cameraForward *
                    Mathf.Clamp(movement.y, -1f, 1f),
                Turn = Mathf.Clamp(movement.x, -1f, 1f)
            });
        }

        public static Vector2 NormalizeDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > Mathf.Epsilon
                ? direction.normalized
                : Vector2.up;
        }

        protected virtual Vector2 ReadMovement()
        {
            var action = moveAction?.action;
            if (!isActiveAndEnabled ||
                !Application.isFocused ||
                action == null ||
                !action.enabled)
            {
                return Vector2.zero;
            }

            try
            {
                return action.ReadValue<Vector2>();
            }
            catch (System.InvalidOperationException)
            {
                return Vector2.zero;
            }
        }

        protected virtual Vector2 ReadCameraForward()
        {
            var activeCamera = Camera.main;
            if (activeCamera == null)
            {
                return Vector2.zero;
            }

            var forward = activeCamera.transform.forward;
            return new Vector2(forward.x, forward.z);
        }
    }
}
