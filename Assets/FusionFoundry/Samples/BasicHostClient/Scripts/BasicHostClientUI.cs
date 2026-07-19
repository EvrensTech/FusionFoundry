using System;
using System.Threading.Tasks;
using FusionFoundry.Bootstrap;
using FusionFoundry.Sessions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FusionFoundry.Samples.BasicHostClient
{
    [DisallowMultipleComponent]
    public sealed class BasicHostClientUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private FusionBootstrap bootstrap;

        [SerializeField]
        private ThirdPersonCameraRig cameraRig;

        [Header("Panels")]
        [SerializeField]
        private GameObject backdrop;

        [SerializeField]
        private GameObject mainPanel;

        [SerializeField]
        private GameObject joinPanel;

        [SerializeField]
        private GameObject connectingPanel;

        [SerializeField]
        private GameObject sessionPanel;

        [SerializeField]
        private GameObject roomCodePanel;

        [SerializeField]
        private GameObject mouseLookHint;

        [SerializeField]
        private GameObject errorPanel;

        [Header("Inputs and Labels")]
        [SerializeField]
        private InputField roomCodeInput;

        [SerializeField]
        private Text connectingText;

        [SerializeField]
        private Text sessionStatusText;

        [SerializeField]
        private Text roomCodeText;

        [SerializeField]
        private Text mouseLookHintText;

        [SerializeField]
        private Text errorText;

        [Header("Buttons")]
        [SerializeField]
        private Button createButton;

        [SerializeField]
        private Button openJoinButton;

        [SerializeField]
        private Button joinButton;

        [SerializeField]
        private Button backButton;

        [SerializeField]
        private Button leaveButton;

        [SerializeField]
        private Button dismissErrorButton;

        private bool _operationInFlight;
        private bool _isHosting;
        private bool _listenersRegistered;
        private int _lifecycleGeneration;
        private FusionSessionState _lastState = FusionSessionState.Idle;

        private void OnEnable()
        {
            _lifecycleGeneration++;
            _operationInFlight = false;

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            createButton.onClick.AddListener(HandleCreateClicked);
            openJoinButton.onClick.AddListener(HandleOpenJoinClicked);
            joinButton.onClick.AddListener(HandleJoinClicked);
            backButton.onClick.AddListener(HandleBackClicked);
            leaveButton.onClick.AddListener(HandleLeaveClicked);
            dismissErrorButton.onClick.AddListener(HideError);
            bootstrap.StateChanged += HandleStateChanged;
            _listenersRegistered = true;

            _lastState = bootstrap.State;
            HideError();
            RenderCurrentState();
        }

        private void OnDisable()
        {
            _lifecycleGeneration++;

            if (bootstrap != null)
            {
                bootstrap.StateChanged -= HandleStateChanged;
            }

            if (_listenersRegistered)
            {
                if (createButton != null)
                {
                    createButton.onClick.RemoveListener(HandleCreateClicked);
                }

                if (openJoinButton != null)
                {
                    openJoinButton.onClick.RemoveListener(HandleOpenJoinClicked);
                }

                if (joinButton != null)
                {
                    joinButton.onClick.RemoveListener(HandleJoinClicked);
                }

                if (backButton != null)
                {
                    backButton.onClick.RemoveListener(HandleBackClicked);
                }

                if (leaveButton != null)
                {
                    leaveButton.onClick.RemoveListener(HandleLeaveClicked);
                }

                if (dismissErrorButton != null)
                {
                    dismissErrorButton.onClick.RemoveListener(HideError);
                }
            }

            _listenersRegistered = false;
            _operationInFlight = false;
        }

        private void Update()
        {
            var leaveRequested =
                Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame;

            leaveRequested |=
                Gamepad.current != null &&
                Gamepad.current.startButton.wasPressedThisFrame;

            if (!_operationInFlight &&
                bootstrap != null &&
                bootstrap.State == FusionSessionState.Running &&
                leaveRequested)
            {
                HandleLeaveClicked();
            }

            if (mouseLookHint != null && mouseLookHint.activeSelf)
            {
                UpdateMouseLookHint();
            }
        }

        private async void HandleCreateClicked()
        {
            if (_operationInFlight)
            {
                return;
            }

            _isHosting = true;
            await RunStartOperationAsync(
                bootstrap.CreateSessionAsync,
                returnToJoinPanelOnFailure: false,
                "Creating room...");
        }

        private void HandleOpenJoinClicked()
        {
            if (_operationInFlight)
            {
                return;
            }

            HideError();
            ShowJoinMenu();
            roomCodeInput.Select();
            roomCodeInput.ActivateInputField();
        }

        private async void HandleJoinClicked()
        {
            if (_operationInFlight)
            {
                return;
            }

            _isHosting = false;
            var requestedCode = roomCodeInput.text;
            await RunStartOperationAsync(
                () => bootstrap.JoinSessionAsync(requestedCode),
                returnToJoinPanelOnFailure: true,
                "Joining room...");
        }

        private void HandleBackClicked()
        {
            if (_operationInFlight)
            {
                return;
            }

            HideError();
            ShowMainMenu();
        }

        private async void HandleLeaveClicked()
        {
            if (_operationInFlight || bootstrap.State != FusionSessionState.Running)
            {
                return;
            }

            _operationInFlight = true;
            SetControlsInteractable(false);
            ShowSessionHud();
            var generation = _lifecycleGeneration;

            try
            {
                await bootstrap.LeaveSessionAsync();

                if (!IsCurrentLifecycle(generation))
                {
                    return;
                }

                _isHosting = false;
                ShowMainMenu();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);

                if (IsCurrentLifecycle(generation))
                {
                    ShowMainMenu();
                    ShowError("The session could not be closed cleanly.");
                }
            }
            finally
            {
                if (IsCurrentLifecycle(generation))
                {
                    _operationInFlight = false;
                    SetControlsInteractable(true);
                }
            }
        }

        private async Task RunStartOperationAsync(
            Func<Task<FusionSessionStartResult>> operation,
            bool returnToJoinPanelOnFailure,
            string progressMessage)
        {
            _operationInFlight = true;
            HideError();
            SetControlsInteractable(false);
            ShowConnecting(progressMessage);
            var generation = _lifecycleGeneration;

            FusionSessionStartResult result;
            try
            {
                result = await operation();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                result = FusionSessionStartResult.Failed(
                    "An unexpected error occurred. Please try again.",
                    exception.GetType().Name,
                    exception.Message);
            }

            if (!IsCurrentLifecycle(generation))
            {
                return;
            }

            _operationInFlight = false;
            SetControlsInteractable(true);

            if (result.IsSuccess)
            {
                ShowSessionHud();
                return;
            }

            if (returnToJoinPanelOnFailure)
            {
                ShowJoinMenu();
            }
            else
            {
                ShowMainMenu();
            }

            ShowError(result.UserMessage);
        }

        private bool ValidateReferences()
        {
            var references = new UnityEngine.Object[]
            {
                bootstrap,
                cameraRig,
                backdrop,
                mainPanel,
                joinPanel,
                connectingPanel,
                sessionPanel,
                roomCodePanel,
                mouseLookHint,
                errorPanel,
                roomCodeInput,
                connectingText,
                sessionStatusText,
                roomCodeText,
                mouseLookHintText,
                errorText,
                createButton,
                openJoinButton,
                joinButton,
                backButton,
                leaveButton,
                dismissErrorButton
            };

            foreach (var reference in references)
            {
                if (reference == null)
                {
                    Debug.LogError(
                        "BasicHostClientUI has one or more missing Inspector references.",
                        this);
                    return false;
                }
            }

            return true;
        }

        private bool IsCurrentLifecycle(int generation)
        {
            return this != null &&
                isActiveAndEnabled &&
                generation == _lifecycleGeneration;
        }

        private void HandleStateChanged(FusionSessionState state)
        {
            var previousState = _lastState;
            _lastState = state;

            switch (state)
            {
                case FusionSessionState.Starting:
                    ShowConnecting("Connecting...");
                    break;
                case FusionSessionState.Running:
                    ShowSessionHud();
                    break;
                case FusionSessionState.Stopping:
                    ShowSessionHud();
                    break;
                case FusionSessionState.Idle:
                    if (_operationInFlight)
                    {
                        return;
                    }

                    ShowMainMenu();
                    if (previousState == FusionSessionState.Running)
                    {
                        ShowError("The session ended.");
                    }

                    break;
            }
        }

        private void RenderCurrentState()
        {
            switch (bootstrap.State)
            {
                case FusionSessionState.Starting:
                    ShowConnecting("Connecting...");
                    break;
                case FusionSessionState.Running:
                    ShowSessionHud();
                    break;
                case FusionSessionState.Stopping:
                    ShowSessionHud();
                    break;
                default:
                    ShowMainMenu();
                    break;
            }
        }

        private void ShowMainMenu()
        {
            backdrop.SetActive(true);
            mainPanel.SetActive(true);
            joinPanel.SetActive(false);
            connectingPanel.SetActive(false);
            sessionPanel.SetActive(false);
            roomCodePanel.SetActive(false);
            mouseLookHint.SetActive(false);
        }

        private void ShowJoinMenu()
        {
            backdrop.SetActive(true);
            mainPanel.SetActive(false);
            joinPanel.SetActive(true);
            connectingPanel.SetActive(false);
            sessionPanel.SetActive(false);
            roomCodePanel.SetActive(false);
            mouseLookHint.SetActive(false);
        }

        private void ShowConnecting(string message)
        {
            connectingText.text = message;
            backdrop.SetActive(true);
            mainPanel.SetActive(false);
            joinPanel.SetActive(false);
            connectingPanel.SetActive(true);
            sessionPanel.SetActive(false);
            roomCodePanel.SetActive(false);
            mouseLookHint.SetActive(false);
        }

        private void ShowSessionHud()
        {
            backdrop.SetActive(false);
            mainPanel.SetActive(false);
            joinPanel.SetActive(false);
            connectingPanel.SetActive(false);
            sessionPanel.SetActive(false);
            errorPanel.SetActive(false);

            sessionStatusText.text = _isHosting
                ? "Room created. Share the code with another player."
                : "Connected to room.";

            var showRoomCode = !string.IsNullOrEmpty(bootstrap.ActiveRoomCode);
            roomCodePanel.SetActive(showRoomCode);
            roomCodeText.text = showRoomCode ? bootstrap.ActiveRoomCode : string.Empty;
            mouseLookHint.SetActive(true);
            UpdateMouseLookHint();
        }

        private void UpdateMouseLookHint()
        {
            mouseLookHintText.text = string.Concat(
                ThirdPersonCameraRig.MouseLookToggleKeyLabel,
                "  Mouse Look: ",
                cameraRig.IsMouseLookEnabled ? "ON" : "OFF");
        }

        private void ShowError(string message)
        {
            errorText.text = message;
            errorPanel.SetActive(true);
        }

        private void HideError()
        {
            errorPanel.SetActive(false);
        }

        private void SetControlsInteractable(bool isInteractable)
        {
            createButton.interactable = isInteractable;
            openJoinButton.interactable = isInteractable;
            joinButton.interactable = isInteractable;
            backButton.interactable = isInteractable;
            leaveButton.interactable = isInteractable;
            roomCodeInput.interactable = isInteractable;
        }
    }
}
