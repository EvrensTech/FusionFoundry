using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FusionFoundry.Bootstrap;
using FusionFoundry.Sessions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FusionFoundry.Tests.Bootstrap
{
    public sealed class FusionBootstrapPlayModeTests
    {
        private const double TaskTimeoutSeconds = 10.0;

        private GameObject _bootstrapGameObject;
        private TestFusionBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            _bootstrapGameObject = new GameObject("Test Fusion Bootstrap");
            _bootstrap = _bootstrapGameObject.AddComponent<TestFusionBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null)
            {
                _bootstrap.DestroyRemainingControllerObjects();
            }

            if (_bootstrapGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_bootstrapGameObject);
            }
        }

        [UnityTest]
        public IEnumerator StartingSession_RejectsConcurrentSecondStart()
        {
            var pendingStart =
                new TaskCompletionSource<FusionSessionStartResult>();
            _bootstrap.EnqueueStartResult(pendingStart.Task);

            var firstStart = _bootstrap.CreateSessionAsync();

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Starting));
            Assert.That(_bootstrap.CreationCount, Is.EqualTo(1));

            var secondStart = _bootstrap.JoinSessionAsync("ABCdef");
            yield return WaitForTask(secondStart);

            Assert.That(secondStart.Result.IsSuccess, Is.False);
            Assert.That(secondStart.Result.DiagnosticCode, Is.EqualTo("SessionBusy"));
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Starting));
            Assert.That(_bootstrap.CreationCount, Is.EqualTo(1));

            pendingStart.SetResult(FusionSessionStartResult.Succeeded());
            yield return WaitForTask(firstStart);

            Assert.That(firstStart.Result.IsSuccess, Is.True);
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Running));
            Assert.That(RoomCodeGenerator.IsValid(_bootstrap.ActiveRoomCode), Is.True);

            yield return WaitForTask(_bootstrap.LeaveSessionAsync());
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupFailure_CleansControllerAndReturnsToIdle()
        {
            _bootstrap.EnqueueStartResult(
                Task.FromResult(
                    FusionSessionStartResult.Failed(
                        "Synthetic startup failure.",
                        "SyntheticFailure")));

            var start = _bootstrap.CreateSessionAsync();
            yield return WaitForTask(start);

            var failedController = _bootstrap.CreatedControllers[0];

            Assert.That(start.Result.IsSuccess, Is.False);
            Assert.That(start.Result.DiagnosticCode, Is.EqualTo("SyntheticFailure"));
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Idle));
            Assert.That(_bootstrap.ActiveRoomCode, Is.Empty);
            Assert.That(_bootstrap.CreationCount, Is.EqualTo(1));
            Assert.That(_bootstrap.DestructionCount, Is.EqualTo(1));

            yield return null;

            Assert.That(failedController == null, Is.True);
        }

        [UnityTest]
        public IEnumerator RemoteShutdownDuringStarting_WhenStartLaterSucceeds_CleansUpAndReturnsStoppedResult()
        {
            var pendingStart =
                new TaskCompletionSource<FusionSessionStartResult>();
            _bootstrap.EnqueueStartResult(pendingStart.Task);

            var start = _bootstrap.CreateSessionAsync();
            var controller = _bootstrap.CreatedControllers[0];

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Starting));

            controller.SimulateRemoteShutdown();
            pendingStart.SetResult(FusionSessionStartResult.Succeeded());
            yield return WaitForTask(start);

            Assert.That(start.Result.IsSuccess, Is.False);
            Assert.That(
                start.Result.DiagnosticCode,
                Is.EqualTo("RunnerStoppedDuringStartup"));
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Idle));
            Assert.That(_bootstrap.ActiveRoomCode, Is.Empty);
            Assert.That(_bootstrap.CreationCount, Is.EqualTo(1));
            Assert.That(_bootstrap.DestructionCount, Is.EqualTo(1));
            Assert.That(controller.ShutdownNotificationCount, Is.EqualTo(1));

            yield return null;

            Assert.That(controller == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Leave_AllowsRestartWithFreshController()
        {
            _bootstrap.EnqueueStartResult(
                Task.FromResult(FusionSessionStartResult.Succeeded()));
            _bootstrap.EnqueueStartResult(
                Task.FromResult(FusionSessionStartResult.Succeeded()));

            var firstStart = _bootstrap.CreateSessionAsync();
            yield return WaitForTask(firstStart);

            var firstController = _bootstrap.CreatedControllers[0];

            Assert.That(firstStart.Result.IsSuccess, Is.True);
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Running));

            var leave = _bootstrap.LeaveSessionAsync();
            yield return WaitForTask(leave);

            Assert.That(firstController.ShutdownCallCount, Is.EqualTo(1));
            Assert.That(firstController.ShutdownNotificationCount, Is.EqualTo(1));
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Idle));
            Assert.That(_bootstrap.ActiveRoomCode, Is.Empty);
            Assert.That(_bootstrap.DestructionCount, Is.EqualTo(1));

            var secondStart = _bootstrap.CreateSessionAsync();
            yield return WaitForTask(secondStart);

            var secondController = _bootstrap.CreatedControllers[1];

            Assert.That(secondStart.Result.IsSuccess, Is.True);
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Running));
            Assert.That(_bootstrap.CreationCount, Is.EqualTo(2));
            Assert.That(secondController, Is.Not.SameAs(firstController));

            yield return null;

            Assert.That(firstController == null, Is.True);
            Assert.That(secondController == null, Is.False);

            yield return WaitForTask(_bootstrap.LeaveSessionAsync());
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidJoin_DoesNotCreateController()
        {
            var join = _bootstrap.JoinSessionAsync("ABC0de");
            yield return WaitForTask(join);

            Assert.That(join.Result.IsSuccess, Is.False);
            Assert.That(join.Result.DiagnosticCode, Is.EqualTo("InvalidRoomCode"));
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Idle));
            Assert.That(_bootstrap.ActiveRoomCode, Is.Empty);
            Assert.That(_bootstrap.CreationCount, Is.Zero);
            Assert.That(_bootstrap.DestructionCount, Is.Zero);
            Assert.That(_bootstrap.CreatedControllers, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RemoteShutdown_CleansControllerAndReturnsToIdle()
        {
            _bootstrap.EnqueueStartResult(
                Task.FromResult(FusionSessionStartResult.Succeeded()));

            var start = _bootstrap.JoinSessionAsync("ABCdef");
            yield return WaitForTask(start);

            var controller = _bootstrap.CreatedControllers[0];

            Assert.That(start.Result.IsSuccess, Is.True);
            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Running));
            Assert.That(_bootstrap.ActiveRoomCode, Is.EqualTo("ABCdef"));

            controller.SimulateRemoteShutdown();

            Assert.That(_bootstrap.State, Is.EqualTo(FusionSessionState.Idle));
            Assert.That(_bootstrap.ActiveRoomCode, Is.Empty);
            Assert.That(_bootstrap.DestructionCount, Is.EqualTo(1));
            Assert.That(controller.ShutdownCallCount, Is.Zero);
            Assert.That(controller.ShutdownNotificationCount, Is.EqualTo(1));

            yield return null;

            Assert.That(controller == null, Is.True);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            var timeoutAt =
                Time.realtimeSinceStartupAsDouble + TaskTimeoutSeconds;

            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartupAsDouble >= timeoutAt)
                {
                    Assert.Fail(
                        $"The asynchronous operation did not complete within " +
                        $"{TaskTimeoutSeconds:0} seconds.");
                }

                yield return null;
            }

            if (task.IsCanceled)
            {
                Assert.Fail("The asynchronous operation was canceled.");
            }

            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    throw task.Exception.Flatten();
                }

                throw new InvalidOperationException(
                    "The asynchronous operation failed.");
            }
        }
    }

    internal sealed class TestFusionBootstrap : FusionBootstrap
    {
        private readonly Queue<Task<FusionSessionStartResult>> _startResults =
            new Queue<Task<FusionSessionStartResult>>();

        public List<FakeFusionSessionController> CreatedControllers { get; } =
            new List<FakeFusionSessionController>();

        public int CreationCount { get; private set; }

        public int DestructionCount { get; private set; }

        public void EnqueueStartResult(Task<FusionSessionStartResult> startResult)
        {
            _startResults.Enqueue(
                startResult ?? throw new ArgumentNullException(nameof(startResult)));
        }

        public void DestroyRemainingControllerObjects()
        {
            foreach (var controller in CreatedControllers)
            {
                if (controller != null)
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                }
            }
        }

        protected override FusionSessionController CreateControllerInstance()
        {
            CreationCount++;

            if (_startResults.Count == 0)
            {
                throw new InvalidOperationException(
                    "No fake start result was queued for this controller.");
            }

            var controllerObject = new GameObject(
                $"Fake Fusion Controller {CreationCount}");
            var controller =
                controllerObject.AddComponent<FakeFusionSessionController>();

            controller.SetStartResult(_startResults.Dequeue());
            CreatedControllers.Add(controller);
            return controller;
        }

        protected override void DestroyControllerInstance(
            FusionSessionController controller)
        {
            DestructionCount++;
            base.DestroyControllerInstance(controller);
        }
    }

    internal sealed class FakeFusionSessionController : FusionSessionController
    {
        private Task<FusionSessionStartResult> _startResult;

        public int StartCallCount { get; private set; }

        public int ShutdownCallCount { get; private set; }

        public int ShutdownNotificationCount { get; private set; }

        public FusionSessionRequest LastRequest { get; private set; }

        public void SetStartResult(Task<FusionSessionStartResult> startResult)
        {
            _startResult = startResult
                ?? throw new ArgumentNullException(nameof(startResult));
        }

        public void SimulateRemoteShutdown()
        {
            RaiseShutdownNotification();
        }

        protected override void Awake()
        {
            // The test double intentionally skips NetworkRunner initialization.
        }

        public override Task<FusionSessionStartResult> StartSessionAsync(
            FusionSessionRequest request)
        {
            StartCallCount++;
            LastRequest = request;
            return _startResult
                ?? throw new InvalidOperationException(
                    "The fake start result was not configured.");
        }

        public override Task ShutdownSessionAsync()
        {
            ShutdownCallCount++;
            RaiseShutdownNotification();
            return Task.CompletedTask;
        }

        private void RaiseShutdownNotification()
        {
            ShutdownNotificationCount++;
            NotifyShutdownOccurred();
        }
    }
}
