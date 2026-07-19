using UnityEngine;
using UnityEngine.InputSystem;

namespace FusionFoundry.Samples.BasicHostClient
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        private const int CollisionHitCapacity = 16;

        public const string MouseLookToggleKeyLabel = "C";

        [Header("Follow")]
        [SerializeField]
        private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [SerializeField]
        [Min(0.1f)]
        private float followDistance = 5f;

        [SerializeField]
        [Range(-80f, 80f)]
        private float startingPitch = 15f;

        [SerializeField]
        [Min(0f)]
        private float positionSmoothTime = 0.06f;

        [SerializeField]
        [Min(0f)]
        private float rotationSharpness = 20f;

        [Header("Look Input")]
        [SerializeField]
        private bool mouseLookEnabledByDefault = true;

        [SerializeField]
        [Min(0f)]
        private float mouseSensitivity = 0.12f;

        [SerializeField]
        [Min(0f)]
        private float gamepadLookSpeed = 120f;

        [SerializeField]
        private Vector2 pitchLimits = new Vector2(-20f, 65f);

        [Header("Collision")]
        [SerializeField]
        private LayerMask obstructionMask = ~0;

        [SerializeField]
        [Min(0.01f)]
        private float collisionRadius = 0.2f;

        [SerializeField]
        [Min(0f)]
        private float collisionPadding = 0.1f;

        [SerializeField]
        [Min(0.1f)]
        private float minimumDistance = 0.5f;

        [Header("Session")]
        [SerializeField]
        private bool manageCursor = true;

        private readonly RaycastHit[] _collisionHits =
            new RaycastHit[CollisionHitCapacity];

        private Transform _menuParent;
        private Vector3 _menuLocalPosition;
        private Quaternion _menuLocalRotation;
        private Vector3 _menuLocalScale;
        private UnityEngine.Object _owner;
        private Transform _target;
        private Vector3 _positionVelocity;
        private float _yaw;
        private float _pitch;
        private float _lastTargetYaw;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;
        private bool _menuPoseCaptured;
        private bool _cursorStateCaptured;
        private bool _mouseLookEnabled;

        public bool IsFollowing => _target != null;

        public Transform FollowTarget => _target;

        public UnityEngine.Object Owner => _owner;

        public bool IsMouseLookEnabled => _mouseLookEnabled;

        private void Awake()
        {
            CaptureMenuPose();
            _mouseLookEnabled = mouseLookEnabledByDefault;
        }

        private void LateUpdate()
        {
            if (_target == null || _owner == null)
            {
                if (!ReferenceEquals(_target, null) ||
                    !ReferenceEquals(_owner, null))
                {
                    ForceRelease();
                }

                return;
            }

            HandleMouseLookToggleInput();
            UpdateOrbitFromTarget();

            if (_mouseLookEnabled)
            {
                ReadLookInput(Time.unscaledDeltaTime);
            }

            ApplyCameraPose(Time.unscaledDeltaTime, snap: false);
        }

        private void OnDisable()
        {
            ForceRelease();
        }

        private void OnValidate()
        {
            followDistance = Mathf.Max(0.1f, followDistance);
            positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            collisionPadding = Mathf.Max(0f, collisionPadding);
            minimumDistance = Mathf.Clamp(
                minimumDistance,
                0.1f,
                followDistance);

            if (pitchLimits.x > pitchLimits.y)
            {
                pitchLimits = new Vector2(pitchLimits.y, pitchLimits.x);
            }

            startingPitch = Mathf.Clamp(
                startingPitch,
                pitchLimits.x,
                pitchLimits.y);
        }

        public bool Claim(UnityEngine.Object owner, Transform target)
        {
            if (owner == null || target == null)
            {
                return false;
            }

            if (!_menuPoseCaptured)
            {
                CaptureMenuPose();
            }

            if (_target == null)
            {
                CaptureCursorState();
            }

            _owner = owner;
            _target = target;
            _yaw = target.eulerAngles.y;
            _lastTargetYaw = _yaw;
            _pitch = Mathf.Clamp(
                startingPitch,
                pitchLimits.x,
                pitchLimits.y);
            _mouseLookEnabled = mouseLookEnabledByDefault;
            _positionVelocity = Vector3.zero;

            if (manageCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ApplyCameraPose(0f, snap: true);
            return true;
        }

        public bool Release(UnityEngine.Object owner)
        {
            if (_target == null && _owner == null)
            {
                return false;
            }

            if (!ReferenceEquals(_owner, owner))
            {
                return false;
            }

            ForceRelease();
            return true;
        }

        public void SnapToTarget()
        {
            if (_target != null)
            {
                UpdateOrbitFromTarget();
                ApplyCameraPose(0f, snap: true);
            }
        }

        public bool ToggleMouseLook()
        {
            return SetMouseLookEnabled(!_mouseLookEnabled);
        }

        public bool SetMouseLookEnabled(bool isEnabled)
        {
            _mouseLookEnabled = isEnabled;
            return _mouseLookEnabled;
        }

        public static void CalculateDesiredPose(
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 targetOffset,
            float yaw,
            float pitch,
            float distance,
            out Vector3 cameraPosition,
            out Quaternion cameraRotation)
        {
            var pivot = targetPosition + targetRotation * targetOffset;
            var orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            cameraPosition =
                pivot - orbitRotation * Vector3.forward * Mathf.Max(0f, distance);

            var lookDirection = pivot - cameraPosition;
            cameraRotation = lookDirection.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.LookRotation(lookDirection, Vector3.up)
                : orbitRotation;
        }

        private void CaptureMenuPose()
        {
            _menuParent = transform.parent;
            _menuLocalPosition = transform.localPosition;
            _menuLocalRotation = transform.localRotation;
            _menuLocalScale = transform.localScale;
            _menuPoseCaptured = true;
        }

        private void CaptureCursorState()
        {
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _cursorStateCaptured = true;
        }

        private void HandleMouseLookToggleInput()
        {
            if (Keyboard.current != null &&
                Keyboard.current.cKey.wasPressedThisFrame)
            {
                ToggleMouseLook();
            }
        }

        private void UpdateOrbitFromTarget()
        {
            var targetYaw = _target.eulerAngles.y;
            _yaw += Mathf.DeltaAngle(_lastTargetYaw, targetYaw);
            _lastTargetYaw = targetYaw;
        }

        private void ReadLookInput(float deltaTime)
        {
            var lookDelta = Vector2.zero;

            if (Mouse.current != null)
            {
                lookDelta += Mouse.current.delta.ReadValue() * mouseSensitivity;
            }

            if (Gamepad.current != null)
            {
                lookDelta +=
                    Gamepad.current.rightStick.ReadValue() *
                    gamepadLookSpeed *
                    Mathf.Max(0f, deltaTime);
            }

            _yaw += lookDelta.x;
            _pitch = Mathf.Clamp(
                _pitch - lookDelta.y,
                pitchLimits.x,
                pitchLimits.y);
        }

        private void ApplyCameraPose(float deltaTime, bool snap)
        {
            CalculateDesiredPose(
                _target.position,
                _target.rotation,
                targetOffset,
                _yaw,
                _pitch,
                followDistance,
                out var desiredPosition,
                out _);

            var pivot = _target.position + _target.rotation * targetOffset;
            var toCamera = desiredPosition - pivot;
            var direction = toCamera.sqrMagnitude > Mathf.Epsilon
                ? toCamera.normalized
                : Vector3.back;
            var resolvedDistance = ResolveCameraDistance(
                pivot,
                direction,
                followDistance);
            var obstructionMovedCameraInward =
                resolvedDistance < followDistance - Mathf.Epsilon &&
                Vector3.Distance(transform.position, pivot) > resolvedDistance;

            desiredPosition = pivot + direction * resolvedDistance;
            var desiredRotation = Quaternion.LookRotation(
                pivot - desiredPosition,
                Vector3.up);

            if (snap ||
                positionSmoothTime <= 0f ||
                obstructionMovedCameraInward)
            {
                transform.position = desiredPosition;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredPosition,
                    ref _positionVelocity,
                    positionSmoothTime,
                    Mathf.Infinity,
                    Mathf.Max(0f, deltaTime));
            }

            if (snap || rotationSharpness <= 0f)
            {
                transform.rotation = desiredRotation;
            }
            else
            {
                var blend = 1f - Mathf.Exp(
                    -rotationSharpness * Mathf.Max(0f, deltaTime));
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    blend);
            }
        }

        private float ResolveCameraDistance(
            Vector3 pivot,
            Vector3 direction,
            float desiredDistance)
        {
            var hitCount = Physics.SphereCastNonAlloc(
                pivot,
                collisionRadius,
                direction,
                _collisionHits,
                desiredDistance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            var nearestDistance = desiredDistance;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _collisionHits[index];
                if (hit.collider == null || IsTargetCollider(hit.collider.transform))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(
                    nearestDistance,
                    Mathf.Max(minimumDistance, hit.distance - collisionPadding));
            }

            return nearestDistance;
        }

        private bool IsTargetCollider(Transform candidate)
        {
            return _target != null &&
                (candidate == _target || candidate.IsChildOf(_target));
        }

        private void ForceRelease()
        {
            _owner = null;
            _target = null;
            _positionVelocity = Vector3.zero;
            _lastTargetYaw = 0f;
            _mouseLookEnabled = mouseLookEnabledByDefault;

            if (_menuPoseCaptured)
            {
                transform.SetParent(_menuParent, worldPositionStays: false);
                transform.localPosition = _menuLocalPosition;
                transform.localRotation = _menuLocalRotation;
                transform.localScale = _menuLocalScale;
            }

            if (manageCursor && _cursorStateCaptured)
            {
                Cursor.lockState = _previousCursorLockMode;
                Cursor.visible = _previousCursorVisible;
            }

            _cursorStateCaptured = false;
        }
    }
}
