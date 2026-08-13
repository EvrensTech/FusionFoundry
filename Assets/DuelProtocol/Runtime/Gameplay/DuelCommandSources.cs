using DuelProtocol.Match;
using DuelProtocol.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelProtocol.Gameplay
{
    public sealed class LocalDuelCommandSource : MonoBehaviour, IDuelCommandSource
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputAction _moveAction;
        private InputAction _abilityAction;
        private bool _ownsInputActions;
        private DuelOnboardingProgress _onboarding;

        public bool ReadyRequested { get; private set; }
        public bool RematchRequested { get; private set; }
        public InputActionAsset InputActions => inputActions;
        public DuelOnboardingStep OnboardingStep =>
            _onboarding?.CurrentStep ?? DuelOnboardingStep.Move;
        public string OnboardingHint => _onboarding?.Hint ?? string.Empty;

        private void Awake()
        {
            DuelMobileControls.EnsureInstalled();
            if (inputActions == null)
            {
                inputActions = DuelInputActionFactory.LoadOrCreate();
                _ownsInputActions = true;
            }
            BindActions();
            DuelInputBindingStore.Load(inputActions);
            _onboarding = new DuelOnboardingProgress();
        }

        private void OnEnable()
        {
            inputActions?.Enable();
        }

        private void OnDisable()
        {
            inputActions?.Disable();
        }

        private void OnDestroy()
        {
            if (!_ownsInputActions || inputActions == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(inputActions);
            }
            else
            {
                DestroyImmediate(inputActions);
            }
        }

        public void RequestReady()
        {
            ReadyRequested = true;
        }

        public void RequestRematch()
        {
            RematchRequested = true;
        }

        public void ClearReadyRequest()
        {
            ReadyRequested = false;
        }

        public void ClearRematchRequest()
        {
            RematchRequested = false;
        }

        public void ResetSessionRequests()
        {
            ReadyRequested = false;
            RematchRequested = false;
        }

        public bool ApplyBindingOverride(string actionName, int bindingIndex, string overridePath)
        {
            var action = inputActions?.FindAction(actionName, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count ||
                string.IsNullOrWhiteSpace(overridePath))
            {
                return false;
            }
            action.ApplyBindingOverride(bindingIndex, overridePath);
            DuelInputBindingStore.Save(inputActions);
            return true;
        }

        public void ResetBindingOverrides()
        {
            DuelInputBindingStore.Reset(inputActions);
        }

        public void ResetOnboarding()
        {
            _onboarding?.Reset();
        }

        public DuelCommand ReadCommand(DuelMatchSnapshot snapshot, int playerIndex)
        {
            var movement = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var ability = _abilityAction?.WasPressedThisFrame() ?? false;
            if (!DuelMobileControls.IsActive)
            {
                ReadLegacyTouches(ref movement, ref ability);
            }
            movement = Vector2.ClampMagnitude(movement, 1f);
            var aim = movement.sqrMagnitude > 0.01f
                ? movement.normalized
                : (snapshot != null ? snapshot.GetPlayer(playerIndex).Facing : Vector2.up);

            var command = new DuelCommand
            {
                Movement = movement,
                Aim = aim.normalized,
                AbilityPressed = ability
            };
            _onboarding?.Observe(command, snapshot, playerIndex);
            return command;
        }

        private static void ReadLegacyTouches(
            ref Vector2 movement,
            ref bool ability)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
            }

            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                var touchId = touch.touchId.ReadValue();
                if (DuelTouchInput.IsTouchBlockedByUi(touchId))
                {
                    continue;
                }

                var position = touch.position.ReadValue();
                var value = DuelTouchInput.ReadStick(position, Screen.safeArea, true);
                if (value.sqrMagnitude > movement.sqrMagnitude)
                {
                    movement = value;
                }
            }
        }

        private void BindActions()
        {
            var map = inputActions?.FindActionMap(DuelInputActionFactory.MapName, false);
            _moveAction = map?.FindAction(DuelInputActionFactory.MoveAction, false);
            _abilityAction = map?.FindAction(DuelInputActionFactory.AbilityAction, false);
        }
    }

    public sealed class AiDuelCommandSource : MonoBehaviour, IDuelCommandSource
    {
        [SerializeField, Range(0f, 1f)] private float aggression = 0.65f;
        private float _abilityDecisionCooldown;

        public DuelCommand ReadCommand(DuelMatchSnapshot snapshot, int playerIndex)
        {
            var self = snapshot.GetPlayer(playerIndex);
            var opponent = snapshot.GetPlayer(playerIndex == 0 ? 1 : 0);
            var ownsCore = snapshot.Core.OwnerIndex == playerIndex;
            var opponentOwnsCore = snapshot.Core.OwnerIndex >= 0 && !ownsCore;
            var target = ownsCore
                ? GetDefensiveTarget(self.Position, opponent.Position)
                : (opponentOwnsCore ? opponent.Position : snapshot.Core.Position);
            var movement = target - self.Position;
            if (movement.sqrMagnitude > 0.05f)
            {
                movement.Normalize();
            }

            var aimVector = opponent.Position - self.Position;
            var distance = aimVector.magnitude;
            if (distance > 0.01f)
            {
                aimVector /= distance;
            }
            else
            {
                aimVector = self.Facing;
            }

            _abilityDecisionCooldown = Mathf.Max(0f, _abilityDecisionCooldown - Time.deltaTime);
            var useAbility = distance <= 2.1f && self.CooldownSeconds <= 0f &&
                             _abilityDecisionCooldown <= 0f &&
                             (opponentOwnsCore || Random.value <= aggression);
            if (useAbility)
            {
                _abilityDecisionCooldown = 0.25f;
            }

            return new DuelCommand
            {
                Movement = movement,
                Aim = aimVector,
                AbilityPressed = useAbility
            };
        }

        private static Vector2 GetDefensiveTarget(Vector2 self, Vector2 opponent)
        {
            var away = self - opponent;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector2.up;
            }
            return self + away.normalized * 3f;
        }
    }
}
