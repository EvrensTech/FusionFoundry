using System;
using System.Threading.Tasks;
using FusionFoundry.Sessions;
using UnityEngine;

namespace FusionFoundry.Bootstrap
{
    [DisallowMultipleComponent]
    public class FusionBootstrap : MonoBehaviour
    {
        [SerializeField]
        private FusionSessionController runnerPrefab;

        [SerializeField]
        [Min(1)]
        private int defaultMaxPlayers = 4;

        private FusionSessionController _activeController;

        public event Action<FusionSessionState> StateChanged;

        public FusionSessionState State { get; private set; } = FusionSessionState.Idle;

        public string ActiveRoomCode { get; private set; } = string.Empty;

        public async Task<FusionSessionStartResult> CreateSessionAsync()
        {
            if (!CanStartSession(out var rejection))
            {
                return rejection;
            }

            if (defaultMaxPlayers <= 0)
            {
                return FusionSessionStartResult.Failed(
                    "Maximum player count must be greater than zero.",
                    "InvalidMaxPlayers");
            }

            var roomCode = RoomCodeGenerator.Generate();
            var request = FusionSessionRequest.ForHost(roomCode, defaultMaxPlayers);
            return await StartSessionAsync(request);
        }

        public async Task<FusionSessionStartResult> JoinSessionAsync(string roomCode)
        {
            if (!CanStartSession(out var rejection))
            {
                return rejection;
            }

            var normalizedRoomCode = roomCode?.Trim();
            if (!RoomCodeGenerator.IsValid(normalizedRoomCode))
            {
                return FusionSessionStartResult.Failed(
                    "Enter a valid six-character room code.",
                    "InvalidRoomCode");
            }

            var request = FusionSessionRequest.ForClient(normalizedRoomCode);
            return await StartSessionAsync(request);
        }

        public async Task LeaveSessionAsync()
        {
            if (State != FusionSessionState.Running)
            {
                return;
            }

            SetState(FusionSessionState.Stopping);

            var controller = _activeController;
            if (controller != null)
            {
                controller.ShutdownOccurred -= HandleControllerShutdown;

                try
                {
                    await controller.ShutdownSessionAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, controller);
                }
            }

            CleanupController(controller);
            SetState(FusionSessionState.Idle);
        }

        protected virtual FusionSessionController CreateControllerInstance()
        {
            if (runnerPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(runnerPrefab);
            instance.gameObject.name = $"{runnerPrefab.gameObject.name} (Runtime)";
            return instance;
        }

        protected virtual void DestroyControllerInstance(FusionSessionController controller)
        {
            if (controller != null)
            {
                Destroy(controller.gameObject);
            }
        }

        protected virtual void OnValidate()
        {
            if (defaultMaxPlayers < 1)
            {
                defaultMaxPlayers = 1;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_activeController != null)
            {
                _activeController.ShutdownOccurred -= HandleControllerShutdown;
                DestroyControllerInstance(_activeController);
                _activeController = null;
            }
        }

        private bool CanStartSession(out FusionSessionStartResult rejection)
        {
            if (State != FusionSessionState.Idle || _activeController != null)
            {
                rejection = FusionSessionStartResult.Failed(
                    "Another session operation is already in progress.",
                    "SessionBusy");
                return false;
            }

            rejection = null;
            return true;
        }

        private async Task<FusionSessionStartResult> StartSessionAsync(
            FusionSessionRequest request)
        {
            SetState(FusionSessionState.Starting);

            FusionSessionController controller = null;
            FusionSessionStartResult result;
            try
            {
                controller = CreateControllerInstance();
                if (controller == null)
                {
                    result = FusionSessionStartResult.Failed(
                        "The network runner prefab is not configured.",
                        "MissingRunnerPrefab");
                }
                else
                {
                    _activeController = controller;
                    controller.ShutdownOccurred += HandleControllerShutdown;
                    result = await controller.StartSessionAsync(request);
                }

                if (result == null)
                {
                    result = FusionSessionStartResult.Failed(
                        "The session returned an invalid startup result.",
                        "MissingStartResult");
                }
            }
            catch (Exception exception)
            {
                result = FusionSessionStartResult.Failed(
                    "The session could not be started. Please try again.",
                    exception.GetType().Name,
                    exception.Message);
            }

            var controllerStillActive =
                controller != null &&
                ReferenceEquals(controller, _activeController) &&
                State == FusionSessionState.Starting;

            if (!result.IsSuccess || !controllerStillActive)
            {
                if (ReferenceEquals(controller, _activeController))
                {
                    CleanupController(controller);
                }

                if (State == FusionSessionState.Starting)
                {
                    SetState(FusionSessionState.Idle);
                }

                return result.IsSuccess
                    ? FusionSessionStartResult.Failed(
                        "The network runner stopped during startup.",
                        "RunnerStoppedDuringStartup")
                    : result;
            }

            ActiveRoomCode = request.SessionName;
            SetState(FusionSessionState.Running);
            return result;
        }

        private void HandleControllerShutdown(FusionSessionController controller)
        {
            if (!ReferenceEquals(controller, _activeController))
            {
                return;
            }

            CleanupController(controller);

            if (State == FusionSessionState.Running)
            {
                SetState(FusionSessionState.Idle);
            }
        }

        private void CleanupController(FusionSessionController controller)
        {
            var ownsActiveSlot = ReferenceEquals(controller, _activeController);

            if (controller != null)
            {
                controller.ShutdownOccurred -= HandleControllerShutdown;
                DestroyControllerInstance(controller);
            }

            if (ownsActiveSlot || _activeController == null)
            {
                _activeController = null;
                ActiveRoomCode = string.Empty;
            }
        }

        private void SetState(FusionSessionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;

            var handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<FusionSessionState> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(State);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
