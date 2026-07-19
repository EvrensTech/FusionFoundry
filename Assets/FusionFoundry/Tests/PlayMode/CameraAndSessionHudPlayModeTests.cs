using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using FusionFoundry.Bootstrap;
using FusionFoundry.Samples.BasicHostClient;
using FusionFoundry.Sessions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FusionFoundry.Tests.Presentation
{
    public sealed class ThirdPersonCameraRigPlayModeTests
    {
        private GameObject _environment;
        private GameObject _cameraObject;
        private GameObject _targetObject;
        private GameObject _ownerObject;
        private ThirdPersonCameraRig _rig;
        private Vector3 _menuLocalPosition;
        private Quaternion _menuLocalRotation;

        [SetUp]
        public void SetUp()
        {
            _environment = new GameObject("Camera Test Environment");
            _cameraObject = new GameObject("Camera Test Camera");
            _cameraObject.transform.SetParent(
                _environment.transform,
                worldPositionStays: false);
            _menuLocalPosition = new Vector3(0f, 5.5f, -10f);
            _menuLocalRotation = Quaternion.Euler(18f, 0f, 0f);
            _cameraObject.transform.localPosition = _menuLocalPosition;
            _cameraObject.transform.localRotation = _menuLocalRotation;
            _cameraObject.AddComponent<Camera>();
            _rig = _cameraObject.AddComponent<ThirdPersonCameraRig>();
            SetPrivateField(_rig, "manageCursor", false);
            SetPrivateField(_rig, "obstructionMask", (LayerMask)0);

            _targetObject = new GameObject("Camera Test Target");
            _targetObject.transform.SetPositionAndRotation(
                new Vector3(2f, 0f, 3f),
                Quaternion.Euler(0f, 90f, 0f));
            _ownerObject = new GameObject("Camera Test Owner");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ownerObject);
            Object.DestroyImmediate(_targetObject);
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_environment);
        }

        [Test]
        public void Claim_SnapsBehindTargetAndReleaseRestoresMenuPose()
        {
            Assert.That(
                _rig.Claim(_ownerObject, _targetObject.transform),
                Is.True);

            ThirdPersonCameraRig.CalculateDesiredPose(
                _targetObject.transform.position,
                _targetObject.transform.rotation,
                new Vector3(0f, 1.5f, 0f),
                90f,
                15f,
                5f,
                out var expectedPosition,
                out var expectedRotation);

            AssertVector3(_cameraObject.transform.position, expectedPosition);
            AssertQuaternion(_cameraObject.transform.rotation, expectedRotation);
            Assert.That(_rig.FollowTarget, Is.SameAs(_targetObject.transform));

            Assert.That(_rig.Release(_ownerObject), Is.True);
            Assert.That(_cameraObject.transform.parent, Is.SameAs(_environment.transform));
            AssertVector3(_cameraObject.transform.localPosition, _menuLocalPosition);
            AssertQuaternion(_cameraObject.transform.localRotation, _menuLocalRotation);
            Assert.That(_rig.IsFollowing, Is.False);
        }

        [Test]
        public void Release_FromPreviousOwner_DoesNotClearNewTarget()
        {
            var secondOwner = new GameObject("Second Camera Owner");
            var secondTarget = new GameObject("Second Camera Target");

            try
            {
                _rig.Claim(_ownerObject, _targetObject.transform);
                _rig.Claim(secondOwner, secondTarget.transform);

                Assert.That(_rig.Release(_ownerObject), Is.False);
                Assert.That(_rig.Owner, Is.SameAs(secondOwner));
                Assert.That(_rig.FollowTarget, Is.SameAs(secondTarget.transform));
            }
            finally
            {
                Object.DestroyImmediate(secondTarget);
                Object.DestroyImmediate(secondOwner);
            }
        }

        [Test]
        public void SnapToTarget_TracksTargetMovement()
        {
            _rig.Claim(_ownerObject, _targetObject.transform);
            var initialPosition = _cameraObject.transform.position;

            _targetObject.transform.position += new Vector3(4f, 0f, -2f);
            _rig.SnapToTarget();

            AssertVector3(
                _cameraObject.transform.position - initialPosition,
                new Vector3(4f, 0f, -2f));
        }

        [Test]
        public void MouseLookToggle_PreservesOrbitAndTracksTargetMovement()
        {
            _rig.Claim(_ownerObject, _targetObject.transform);

            SetPrivateField(_rig, "_yaw", 137f);
            SetPrivateField(_rig, "_pitch", 32f);
            _rig.SnapToTarget();

            var orbitPosition = _cameraObject.transform.position;
            var orbitRotation = _cameraObject.transform.rotation;

            Assert.That(_rig.IsMouseLookEnabled, Is.True);
            Assert.That(_rig.ToggleMouseLook(), Is.False);
            AssertVector3(_cameraObject.transform.position, orbitPosition);
            AssertQuaternion(_cameraObject.transform.rotation, orbitRotation);

            var targetDelta = new Vector3(4f, 0f, -2f);
            _targetObject.transform.position += targetDelta;
            _rig.SnapToTarget();

            ThirdPersonCameraRig.CalculateDesiredPose(
                _targetObject.transform.position,
                _targetObject.transform.rotation,
                new Vector3(0f, 1.5f, 0f),
                137f,
                32f,
                5f,
                out var expectedPosition,
                out var expectedRotation);

            AssertVector3(_cameraObject.transform.position, expectedPosition);
            AssertQuaternion(_cameraObject.transform.rotation, expectedRotation);
            AssertVector3(
                _cameraObject.transform.position - orbitPosition,
                targetDelta);
            Assert.That(_rig.FollowTarget, Is.SameAs(_targetObject.transform));

            Assert.That(_rig.ToggleMouseLook(), Is.True);
            AssertVector3(_cameraObject.transform.position, expectedPosition);
            AssertQuaternion(_cameraObject.transform.rotation, expectedRotation);
        }

        private static void SetPrivateField(
            object instance,
            string fieldName,
            object value)
        {
            var field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(instance, value);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }

        private static void AssertQuaternion(
            Quaternion actual,
            Quaternion expected)
        {
            Assert.That(
                Quaternion.Angle(actual, expected),
                Is.LessThan(0.001f));
        }
    }

    public sealed class LocalPlayerCameraBinderPlayModeTests
    {
        private GameObject _cameraObject;
        private ThirdPersonCameraRig _rig;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("Binder Test Camera");
            _cameraObject.AddComponent<Camera>();
            _rig = _cameraObject.AddComponent<ThirdPersonCameraRig>();
            SetPrivateField(_rig, "manageCursor", false);
            SetPrivateField(_rig, "obstructionMask", (LayerMask)0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void LocalAuthority_ClaimsCameraWhileRemoteAuthorityDoesNot()
        {
            var remoteObject = new GameObject("Remote Player");
            var localObject = new GameObject("Local Player");

            try
            {
                var remote = remoteObject.AddComponent<TestCameraBinder>();
                remote.Rig = _rig;
                remote.HasAuthority = false;
                remote.RefreshBinding();

                Assert.That(_rig.IsFollowing, Is.False);

                var local = localObject.AddComponent<TestCameraBinder>();
                local.Rig = _rig;
                local.HasAuthority = true;
                local.RefreshBinding();

                Assert.That(local.IsCameraBound, Is.True);
                Assert.That(_rig.FollowTarget, Is.SameAs(localObject.transform));
            }
            finally
            {
                Object.DestroyImmediate(localObject);
                Object.DestroyImmediate(remoteObject);
            }
        }

        [Test]
        public void AuthorityLoss_ReleasesCameraAndRestoresMenuPose()
        {
            var playerObject = new GameObject("Local Player");
            var menuPosition = _cameraObject.transform.position;

            try
            {
                var binder = playerObject.AddComponent<TestCameraBinder>();
                binder.Rig = _rig;
                binder.HasAuthority = true;
                binder.RefreshBinding();
                Assert.That(binder.IsCameraBound, Is.True);

                binder.HasAuthority = false;
                binder.RefreshBinding();

                Assert.That(_rig.IsFollowing, Is.False);
                Assert.That(_cameraObject.transform.position, Is.EqualTo(menuPosition));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        private static void SetPrivateField(
            object instance,
            string fieldName,
            object value)
        {
            var field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(instance, value);
        }
    }

    public sealed class BasicHostClientHudPlayModeTests
    {
        private GameObject _bootstrapObject;
        private HudTestBootstrap _bootstrap;
        private GameObject _cameraObject;
        private ThirdPersonCameraRig _cameraRig;
        private GameObject _uiRoot;
        private BasicHostClientUI _ui;
        private GameObject _backdrop;
        private GameObject _mainPanel;
        private GameObject _joinPanel;
        private GameObject _connectingPanel;
        private GameObject _sessionPanel;
        private GameObject _roomCodePanel;
        private GameObject _mouseLookHint;
        private GameObject _errorPanel;
        private InputField _roomCodeInput;
        private Text _roomCodeText;
        private Text _mouseLookHintText;
        private Button _createButton;
        private Button _joinButton;
        private Button _leaveButton;

        [SetUp]
        public void SetUp()
        {
            _bootstrapObject = new GameObject("HUD Test Bootstrap");
            _bootstrap = _bootstrapObject.AddComponent<HudTestBootstrap>();

            _cameraObject = new GameObject("HUD Test Camera");
            _cameraObject.AddComponent<Camera>();
            _cameraRig = _cameraObject.AddComponent<ThirdPersonCameraRig>();

            _uiRoot = new GameObject("HUD Test UI");
            _uiRoot.SetActive(false);

            _backdrop = CreatePanel("Backdrop");
            _mainPanel = CreatePanel("Main Panel");
            _joinPanel = CreatePanel("Join Panel");
            _connectingPanel = CreatePanel("Connecting Panel");
            _sessionPanel = CreatePanel("Session Panel");
            _roomCodePanel = CreatePanel("Room Code Panel");
            _mouseLookHint = CreatePanel("Mouse Look Hint");
            _errorPanel = CreatePanel("Error Panel");

            _roomCodeInput = CreateComponent<InputField>("Room Code Input");
            _roomCodeText = CreateComponent<Text>("Room Code Text");
            _mouseLookHintText = CreateComponent<Text>("Mouse Look Hint Text");
            _createButton = CreateComponent<Button>("Create Button");
            _joinButton = CreateComponent<Button>("Join Button");
            _leaveButton = CreateComponent<Button>("Leave Button");

            _ui = _uiRoot.AddComponent<BasicHostClientUI>();
            SetUiField("bootstrap", _bootstrap);
            SetUiField("cameraRig", _cameraRig);
            SetUiField("backdrop", _backdrop);
            SetUiField("mainPanel", _mainPanel);
            SetUiField("joinPanel", _joinPanel);
            SetUiField("connectingPanel", _connectingPanel);
            SetUiField("sessionPanel", _sessionPanel);
            SetUiField("roomCodePanel", _roomCodePanel);
            SetUiField("mouseLookHint", _mouseLookHint);
            SetUiField("errorPanel", _errorPanel);
            SetUiField("roomCodeInput", _roomCodeInput);
            SetUiField("connectingText", CreateComponent<Text>("Connecting Text"));
            SetUiField("sessionStatusText", CreateComponent<Text>("Session Text"));
            SetUiField("roomCodeText", _roomCodeText);
            SetUiField("mouseLookHintText", _mouseLookHintText);
            SetUiField("errorText", CreateComponent<Text>("Error Text"));
            SetUiField("createButton", _createButton);
            SetUiField("openJoinButton", CreateComponent<Button>("Open Join Button"));
            SetUiField("joinButton", _joinButton);
            SetUiField("backButton", CreateComponent<Button>("Back Button"));
            SetUiField("leaveButton", _leaveButton);
            SetUiField(
                "dismissErrorButton",
                CreateComponent<Button>("Dismiss Error Button"));

            _uiRoot.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null &&
                _bootstrap.State == FusionSessionState.Running)
            {
                _bootstrap.Controller?.CompleteShutdown();
            }

            Object.DestroyImmediate(_uiRoot);
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_bootstrapObject);
            _bootstrap?.DestroyControllerObject();
        }

        [UnityTest]
        public IEnumerator HostRunning_ShowsOnlyRoomCodeHud()
        {
            _createButton.onClick.Invoke();
            yield return null;

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Running));
            AssertSessionHud();
            Assert.That(_roomCodeText.text, Has.Length.EqualTo(6));
            Assert.That(_mouseLookHintText.text, Is.EqualTo("C  Mouse Look: ON"));

            _cameraRig.SetMouseLookEnabled(false);
            yield return null;

            Assert.That(_mouseLookHintText.text, Is.EqualTo("C  Mouse Look: OFF"));
        }

        [UnityTest]
        public IEnumerator ClientRunning_ShowsTheJoinedRoomCode()
        {
            _roomCodeInput.text = "ABCDEF";
            _joinButton.onClick.Invoke();
            yield return null;

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Running));
            AssertSessionHud();
            Assert.That(_roomCodeText.text, Is.EqualTo("ABCDEF"));
        }

        [UnityTest]
        public IEnumerator Stopping_KeepsRoomCodeUntilIdleRestoresMenu()
        {
            _createButton.onClick.Invoke();
            yield return null;
            var activeRoomCode = _roomCodeText.text;

            _leaveButton.onClick.Invoke();

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Stopping));
            AssertSessionHud();
            Assert.That(_roomCodeText.text, Is.EqualTo(activeRoomCode));

            _bootstrap.Controller.CompleteShutdown();
            yield return null;

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Idle));
            Assert.That(_backdrop.activeSelf, Is.True);
            Assert.That(_mainPanel.activeSelf, Is.True);
            Assert.That(_roomCodePanel.activeSelf, Is.False);
            Assert.That(_mouseLookHint.activeSelf, Is.False);
        }

        private void AssertSessionHud()
        {
            Assert.That(_backdrop.activeSelf, Is.False);
            Assert.That(_mainPanel.activeSelf, Is.False);
            Assert.That(_joinPanel.activeSelf, Is.False);
            Assert.That(_connectingPanel.activeSelf, Is.False);
            Assert.That(_sessionPanel.activeSelf, Is.False);
            Assert.That(_errorPanel.activeSelf, Is.False);
            Assert.That(_roomCodePanel.activeSelf, Is.True);
            Assert.That(_mouseLookHint.activeSelf, Is.True);
        }

        private GameObject CreatePanel(string panelName)
        {
            var panel = new GameObject(panelName);
            panel.transform.SetParent(_uiRoot.transform);
            return panel;
        }

        private T CreateComponent<T>(string objectName)
            where T : Component
        {
            var owner = new GameObject(objectName);
            owner.transform.SetParent(_uiRoot.transform);
            return owner.AddComponent<T>();
        }

        private void SetUiField(string fieldName, object value)
        {
            var field = typeof(BasicHostClientUI).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_ui, value);
        }
    }

    internal sealed class TestCameraBinder : LocalPlayerCameraBinder
    {
        public bool HasAuthority { get; set; }

        public ThirdPersonCameraRig Rig { get; set; }

        protected override bool HasLocalInputAuthority()
        {
            return HasAuthority;
        }

        protected override ThirdPersonCameraRig ResolveCameraRig()
        {
            return Rig;
        }
    }

    internal sealed class HudTestBootstrap : FusionBootstrap
    {
        public PendingShutdownSessionController Controller { get; private set; }

        public void DestroyControllerObject()
        {
            if (Controller != null)
            {
                Object.DestroyImmediate(Controller.gameObject);
                Controller = null;
            }
        }

        protected override FusionSessionController CreateControllerInstance()
        {
            var controllerObject = new GameObject("HUD Test Controller");
            Controller =
                controllerObject.AddComponent<PendingShutdownSessionController>();
            return Controller;
        }

        protected override void DestroyControllerInstance(
            FusionSessionController controller)
        {
            if (controller != null)
            {
                Object.DestroyImmediate(controller.gameObject);
            }

            if (ReferenceEquals(Controller, controller))
            {
                Controller = null;
            }
        }
    }

    internal sealed class PendingShutdownSessionController :
        FusionSessionController
    {
        private readonly TaskCompletionSource<bool> _shutdownCompletion =
            new TaskCompletionSource<bool>();

        protected override void Awake()
        {
            // The presentation test does not start a real NetworkRunner.
        }

        public override Task<FusionSessionStartResult> StartSessionAsync(
            FusionSessionRequest request)
        {
            return Task.FromResult(FusionSessionStartResult.Succeeded());
        }

        public override async Task ShutdownSessionAsync()
        {
            await _shutdownCompletion.Task;
        }

        public void CompleteShutdown()
        {
            _shutdownCompletion.TrySetResult(true);
        }
    }
}
