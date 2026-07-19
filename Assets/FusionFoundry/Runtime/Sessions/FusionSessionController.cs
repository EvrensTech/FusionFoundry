using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace FusionFoundry.Sessions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunner))]
    public class FusionSessionController : NetworkRunnerCallbacksBehaviour
    {
        private NetworkRunner _runner;
        private NetworkSceneManagerDefault _sceneManager;
        private bool _startAttempted;
        private bool _shutdownNotified;

        public event Action<FusionSessionController> ShutdownOccurred;

        public NetworkRunner Runner => _runner;

        public bool IsRunning => _runner != null && _runner.IsRunning;

        protected virtual void Awake()
        {
            _runner = GetComponent<NetworkRunner>();
            _sceneManager = GetComponent<NetworkSceneManagerDefault>();

            if (_sceneManager == null)
            {
                _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }
        }

        protected override void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
            }

            base.OnDestroy();
        }

        public virtual async Task<FusionSessionStartResult> StartSessionAsync(
            FusionSessionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_startAttempted)
            {
                return FusionSessionStartResult.Failed(
                    "This network runner has already been used.",
                    "RunnerAlreadyUsed");
            }

            _startAttempted = true;
            _shutdownNotified = false;

            var startGameArgs = new StartGameArgs
            {
                GameMode = request.Mode,
                SessionName = request.SessionName,
                SceneManager = _sceneManager
            };

            if (request.Mode == GameMode.Host)
            {
                startGameArgs.PlayerCount = request.MaxPlayers;
                startGameArgs.IsOpen = true;
                startGameArgs.IsVisible = false;
            }
            else if (request.Mode == GameMode.Client)
            {
                startGameArgs.EnableClientSessionCreation = false;
            }
            else
            {
                return FusionSessionStartResult.Failed(
                    "The requested session mode is not supported.",
                    "UnsupportedGameMode",
                    request.Mode.ToString());
            }

            try
            {
                var startResult = await _runner.StartGame(startGameArgs);

                if (startResult.Ok)
                {
                    return FusionSessionStartResult.Succeeded();
                }

                var diagnosticCode = startResult.ShutdownReason.ToString();
                var diagnosticMessage = startResult.ErrorMessage;
                await TryShutdownAfterFailureAsync(startResult.ShutdownReason);

                return FusionSessionStartResult.Failed(
                    GetUserMessage(startResult.ShutdownReason),
                    diagnosticCode,
                    diagnosticMessage);
            }
            catch (Exception exception)
            {
                await TryShutdownAfterFailureAsync(ShutdownReason.Error);

                return FusionSessionStartResult.Failed(
                    "The session could not be started. Please try again.",
                    exception.GetType().Name,
                    exception.Message);
            }
        }

        public virtual async Task ShutdownSessionAsync()
        {
            if (_runner == null || _runner.IsShutdown)
            {
                NotifyShutdownOccurred();
                return;
            }

            await _runner.Shutdown(
                destroyGameObject: false,
                shutdownReason: ShutdownReason.Ok);
        }

        protected void NotifyShutdownOccurred()
        {
            if (_shutdownNotified)
            {
                return;
            }

            _shutdownNotified = true;

            var handlers = ShutdownOccurred;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<FusionSessionController> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private async Task TryShutdownAfterFailureAsync(ShutdownReason reason)
        {
            if (_runner == null || _runner.IsShutdown)
            {
                return;
            }

            try
            {
                await _runner.Shutdown(
                    destroyGameObject: false,
                    shutdownReason: reason);
            }
            catch (Exception cleanupException)
            {
                Debug.LogException(cleanupException, this);
            }
        }

        private static string GetUserMessage(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound:
                    return "Room not found. Check the code and try again.";
                case ShutdownReason.GameIsFull:
                    return "The room is full.";
                case ShutdownReason.GameClosed:
                    return "The room is no longer accepting players.";
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                    return "Authentication failed. Check the Photon configuration.";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                    return "The connection timed out. Please try again.";
                default:
                    return "The session could not be started. Please try again.";
            }
        }

        public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            NotifyShutdownOccurred();
        }
    }
}
