using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Fusion;
using FusionFoundry.Input;
using FusionFoundry.Players;
using FusionFoundry.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace FusionFoundry.Tests.G2
{
    public sealed class NetworkPlayerSpawnerPlayModeTests
    {
        private GameObject _runnerObject;
        private NetworkRunner _runner;
        private RecordingNetworkPlayerSpawner _spawner;
        private GameObject _prefabObject;
        private NetworkObject _playerPrefab;

        [SetUp]
        public void SetUp()
        {
            _runnerObject = new GameObject("G2 Test Runner");
            _runner = _runnerObject.AddComponent<NetworkRunner>();
            _spawner =
                _runnerObject.AddComponent<RecordingNetworkPlayerSpawner>();

            _prefabObject = new GameObject("G2 Test Player Prefab");
            _playerPrefab = _prefabObject.AddComponent<NetworkObject>();
            SetPlayerPrefab(_spawner, _playerPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawner != null)
            {
                _spawner.DestroySpawnedObjects();
            }

            if (_runnerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_runnerObject);
            }

            if (_prefabObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefabObject);
            }
        }

        [Test]
        public void PlayerJoined_SpawnsRegistersAndTracksInputAuthority()
        {
            var player = PlayerRef.FromIndex(2);

            _spawner.OnPlayerJoined(_runner, player);

            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(1));
            Assert.That(_spawner.SetPlayerObjectCallCount, Is.EqualTo(1));
            Assert.That(_spawner.LastSpawnPrefab, Is.SameAs(_playerPrefab));
            Assert.That(_spawner.LastSpawnInputAuthority, Is.EqualTo(player));
            Assert.That(_spawner.SpawnedPlayerCount, Is.EqualTo(1));
            Assert.That(
                _spawner.TryGetTrackedPlayerObject(
                    player,
                    out var trackedObject),
                Is.True);
            Assert.That(trackedObject, Is.SameAs(_spawner.LastSpawnedObject));
            Assert.That(
                _spawner.TryGetRegisteredObject(player, out var registeredObject),
                Is.True);
            Assert.That(registeredObject, Is.SameAs(trackedObject));
        }

        [Test]
        public void DuplicatePlayerJoined_DoesNotSpawnOrRegisterTwice()
        {
            var player = PlayerRef.FromIndex(0);

            _spawner.OnPlayerJoined(_runner, player);
            var firstObject = _spawner.LastSpawnedObject;
            _spawner.OnPlayerJoined(_runner, player);

            Assert.That(_spawner.SpawnCallCount, Is.EqualTo(1));
            Assert.That(_spawner.SetPlayerObjectCallCount, Is.EqualTo(1));
            Assert.That(_spawner.SpawnedPlayerCount, Is.EqualTo(1));
            Assert.That(
                _spawner.TryGetTrackedPlayerObject(player, out var trackedObject),
                Is.True);
            Assert.That(trackedObject, Is.SameAs(firstObject));
        }

        [Test]
        public void PlayerJoined_WithExistingRunnerRegistration_AdoptsObject()
        {
            var player = PlayerRef.FromIndex(1);
            var existingObject = _spawner.CreateSpawnedObject("Existing Player");
            _spawner.SeedRegisteredObject(player, existingObject);

            _spawner.OnPlayerJoined(_runner, player);

            Assert.That(_spawner.SpawnCallCount, Is.Zero);
            Assert.That(_spawner.SetPlayerObjectCallCount, Is.Zero);
            Assert.That(_spawner.SpawnedPlayerCount, Is.EqualTo(1));
            Assert.That(
                _spawner.TryGetTrackedPlayerObject(player, out var trackedObject),
                Is.True);
            Assert.That(trackedObject, Is.SameAs(existingObject));
        }

        [Test]
        public void PlayerLeft_DespawnsAndUntracksExactlyOnce()
        {
            var player = PlayerRef.FromIndex(3);
            _spawner.OnPlayerJoined(_runner, player);
            var spawnedObject = _spawner.LastSpawnedObject;

            _spawner.OnPlayerLeft(_runner, player);
            _spawner.OnPlayerLeft(_runner, player);

            Assert.That(_spawner.DespawnCallCount, Is.EqualTo(1));
            Assert.That(_spawner.LastDespawnedObject, Is.SameAs(spawnedObject));
            Assert.That(_spawner.SpawnedPlayerCount, Is.Zero);
            Assert.That(
                _spawner.TryGetTrackedPlayerObject(player, out _),
                Is.False);
        }

        [Test]
        public void Shutdown_ClearsAllTrackedPlayers()
        {
            var firstPlayer = PlayerRef.FromIndex(0);
            var secondPlayer = PlayerRef.FromIndex(1);
            _spawner.OnPlayerJoined(_runner, firstPlayer);
            _spawner.OnPlayerJoined(_runner, secondPlayer);

            Assert.That(_spawner.SpawnedPlayerCount, Is.EqualTo(2));

            _spawner.OnShutdown(_runner, ShutdownReason.Ok);

            Assert.That(_spawner.SpawnedPlayerCount, Is.Zero);
            Assert.That(
                _spawner.TryGetTrackedPlayerObject(firstPlayer, out _),
                Is.False);
            Assert.That(
                _spawner.TryGetTrackedPlayerObject(secondPlayer, out _),
                Is.False);
        }

        [Test]
        public void NonServerCallbacks_DoNotMutatePlayerLifecycle()
        {
            var player = PlayerRef.FromIndex(0);
            _spawner.CanManage = false;

            _spawner.OnPlayerJoined(_runner, player);
            _spawner.OnPlayerLeft(_runner, player);

            Assert.That(_spawner.SpawnCallCount, Is.Zero);
            Assert.That(_spawner.SetPlayerObjectCallCount, Is.Zero);
            Assert.That(_spawner.DespawnCallCount, Is.Zero);
            Assert.That(_spawner.SpawnedPlayerCount, Is.Zero);
        }

        private static void SetPlayerPrefab(
            NetworkPlayerSpawner spawner,
            NetworkObject playerPrefab)
        {
            var field = typeof(NetworkPlayerSpawner).GetField(
                "playerPrefab",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                "NetworkPlayerSpawner.playerPrefab test seam was not found.");
            field.SetValue(spawner, playerPrefab);
        }
    }

    public sealed class SampleNetworkPlayerMotorPlayModeTests
    {
        [TestCase(-2f, -60f)]
        [TestCase(2f, 60f)]
        public void CalculateTurnDeltaDegrees_ClampsInputAndUsesTurnSpeed(
            float turnInput,
            float expectedDelta)
        {
            var turnDelta =
                SampleNetworkPlayerMotor.CalculateTurnDeltaDegrees(
                    turnInput,
                    120f,
                    0.5f);

            Assert.That(
                turnDelta,
                Is.EqualTo(expectedDelta).Within(0.00001f));
        }

        [Test]
        public void CalculateWorldDisplacement_PreservesCameraDirection()
        {
            var displacement =
                SampleNetworkPlayerMotor.CalculateWorldDisplacement(
                    Vector2.one,
                    2f,
                    0.5f);

            var diagonal = 1f / Mathf.Sqrt(2f);
            AssertVector3(
                displacement,
                new Vector3(diagonal, 0f, diagonal));
        }

        [Test]
        public void CalculateWorldDisplacement_NegativeDirectionMovesBackward()
        {
            var displacement =
                SampleNetworkPlayerMotor.CalculateWorldDisplacement(
                    new Vector2(0f, -0.5f),
                    4f,
                    0.5f);

            AssertVector3(displacement, Vector3.back);
        }

        [Test]
        public void CalculateWorldDisplacement_ClampsMagnitude()
        {
            var displacement =
                SampleNetworkPlayerMotor.CalculateWorldDisplacement(
                    new Vector2(3f, 4f),
                    2f,
                    0.5f);

            AssertVector3(displacement, new Vector3(0.6f, 0f, 0.8f));
        }

        [Test]
        public void CalculateMotion_AppliesCameraMovementAndTurnTogether()
        {
            SampleNetworkPlayerMotor.CalculateMotion(
                Vector3.zero,
                Quaternion.identity,
                Vector2.right,
                1f,
                4f,
                120f,
                0.5f,
                out var position,
                out var rotation);

            AssertVector3(position, new Vector3(2f, 0f, 0f));
            Assert.That(
                Mathf.DeltaAngle(rotation.eulerAngles.y, 60f),
                Is.EqualTo(0f).Within(0.00001f));
        }

        [TestCase(-1f, 0.5f)]
        [TestCase(4f, -0.5f)]
        public void CalculateWorldDisplacement_NegativeRateInputsCannotReverseTime(
            float speed,
            float deltaTime)
        {
            var displacement =
                SampleNetworkPlayerMotor.CalculateWorldDisplacement(
                    Vector2.up,
                    speed,
                    deltaTime);

            AssertVector3(displacement, Vector3.zero);
        }

        [TestCase(-1f, 0.5f)]
        [TestCase(120f, -0.5f)]
        public void CalculateTurnDeltaDegrees_NegativeRateInputsCannotReverseTime(
            float turnSpeed,
            float deltaTime)
        {
            var turnDelta =
                SampleNetworkPlayerMotor.CalculateTurnDeltaDegrees(
                    1f,
                    turnSpeed,
                    deltaTime);

            Assert.That(turnDelta, Is.Zero);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.00001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.00001f));
        }
    }

    public sealed class FusionInputProviderPlayModeTests
    {
        private GameObject _runnerObject;
        private NetworkRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _runnerObject = new GameObject("G2 Input Test Runner");
            _runner = _runnerObject.AddComponent<NetworkRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_runnerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_runnerObject);
            }
        }

        [Test]
        public void MissingMoveAction_ReadsZeroMovement()
        {
            var provider =
                _runnerObject.AddComponent<InspectableFusionInputProvider>();

            Assert.That(provider.ReadMovementForTest(), Is.EqualTo(Vector2.zero));
        }

        [TestCase(1f, 1f)]
        [TestCase(-1f, -1f)]
        public void OnInput_ReadsMainCameraGroundForward(
            float forwardInput,
            float expectedWorldX)
        {
            var cameraObject = new GameObject("G2 Input Test Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(25f, 90f, 0f);
            cameraObject.AddComponent<Camera>();

            try
            {
                var provider =
                    _runnerObject.AddComponent<ForwardOnlyFusionInputProvider>();
                provider.ForwardInput = forwardInput;

                using (var buffer = new NetworkInputBuffer())
                {
                    provider.OnInput(_runner, buffer.Input);
                    var submittedInput = buffer.Input.Get<FusionInputData>();

                    Assert.That(
                        submittedInput.Movement.x,
                        Is.EqualTo(expectedWorldX).Within(0.00001f));
                    Assert.That(
                        submittedInput.Movement.y,
                        Is.EqualTo(0f).Within(0.00001f));
                    Assert.That(submittedInput.Turn, Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void OnInput_SeparatesTurnFromCameraRelativeMovement()
        {
            var provider =
                _runnerObject.AddComponent<SyntheticFusionInputProvider>();
            provider.Movement = new Vector2(3f, 4f);
            provider.CameraForward = new Vector2(3f, 4f);

            using (var buffer = new NetworkInputBuffer())
            {
                provider.OnInput(_runner, buffer.Input);
                var submittedInput = buffer.Input.Get<FusionInputData>();

                Assert.That(
                    submittedInput.Movement.x,
                    Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(
                    submittedInput.Movement.y,
                    Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(
                    submittedInput.Turn,
                    Is.EqualTo(1f).Within(0.00001f));
            }
        }
    }

    internal sealed class RecordingNetworkPlayerSpawner : NetworkPlayerSpawner
    {
        private readonly Dictionary<PlayerRef, NetworkObject> _registeredObjects =
            new Dictionary<PlayerRef, NetworkObject>();

        private readonly List<GameObject> _spawnedObjectOwners =
            new List<GameObject>();

        public bool CanManage { get; set; } = true;

        public int SpawnCallCount { get; private set; }

        public int SetPlayerObjectCallCount { get; private set; }

        public int DespawnCallCount { get; private set; }

        public NetworkObject LastSpawnPrefab { get; private set; }

        public PlayerRef LastSpawnInputAuthority { get; private set; }

        public NetworkObject LastSpawnedObject { get; private set; }

        public NetworkObject LastDespawnedObject { get; private set; }

        public NetworkObject CreateSpawnedObject(string objectName)
        {
            var owner = new GameObject(objectName);
            _spawnedObjectOwners.Add(owner);
            return owner.AddComponent<NetworkObject>();
        }

        public void SeedRegisteredObject(
            PlayerRef player,
            NetworkObject playerObject)
        {
            _registeredObjects[player] = playerObject;
        }

        public bool TryGetRegisteredObject(
            PlayerRef player,
            out NetworkObject playerObject)
        {
            return _registeredObjects.TryGetValue(player, out playerObject);
        }

        public void DestroySpawnedObjects()
        {
            foreach (var owner in _spawnedObjectOwners)
            {
                if (owner != null)
                {
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }

            _spawnedObjectOwners.Clear();
        }

        protected override bool CanManagePlayers(NetworkRunner runner)
        {
            return CanManage;
        }

        protected override bool TryGetPlayerObject(
            NetworkRunner runner,
            PlayerRef player,
            out NetworkObject playerObject)
        {
            return _registeredObjects.TryGetValue(player, out playerObject);
        }

        protected override NetworkObject SpawnPlayer(
            NetworkRunner runner,
            NetworkObject prefab,
            PlayerRef inputAuthority,
            Vector3 position,
            Quaternion rotation)
        {
            SpawnCallCount++;
            LastSpawnPrefab = prefab;
            LastSpawnInputAuthority = inputAuthority;
            LastSpawnedObject = CreateSpawnedObject(
                $"Spawned Player {inputAuthority.AsIndex}");
            LastSpawnedObject.transform.SetPositionAndRotation(position, rotation);
            return LastSpawnedObject;
        }

        protected override void SetPlayerObject(
            NetworkRunner runner,
            PlayerRef player,
            NetworkObject playerObject)
        {
            SetPlayerObjectCallCount++;
            _registeredObjects[player] = playerObject;
        }

        protected override void DespawnPlayer(
            NetworkRunner runner,
            NetworkObject playerObject)
        {
            DespawnCallCount++;
            LastDespawnedObject = playerObject;

            var playerToRemove = PlayerRef.None;
            foreach (var pair in _registeredObjects)
            {
                if (pair.Value == playerObject)
                {
                    playerToRemove = pair.Key;
                    break;
                }
            }

            if (playerToRemove != PlayerRef.None)
            {
                _registeredObjects.Remove(playerToRemove);
            }
        }

        protected override void OnDestroy()
        {
            // The test double is never registered with a running NetworkRunner.
        }
    }

    internal sealed class InspectableFusionInputProvider : FusionInputProvider
    {
        public Vector2 ReadMovementForTest()
        {
            return base.ReadMovement();
        }

        protected override void OnDestroy()
        {
            // The test double is never registered with a running NetworkRunner.
        }
    }

    internal sealed class SyntheticFusionInputProvider : FusionInputProvider
    {
        public Vector2 Movement { get; set; }

        public Vector2 CameraForward { get; set; }

        protected override Vector2 ReadMovement()
        {
            return Movement;
        }

        protected override Vector2 ReadCameraForward()
        {
            return CameraForward;
        }

        protected override void OnEnable()
        {
        }

        protected override void OnDisable()
        {
        }

        protected override void OnDestroy()
        {
            // The test double is never registered with a running NetworkRunner.
        }
    }

    internal sealed class ForwardOnlyFusionInputProvider : FusionInputProvider
    {
        public float ForwardInput { get; set; } = 1f;

        protected override Vector2 ReadMovement()
        {
            return new Vector2(0f, ForwardInput);
        }

        protected override void OnDestroy()
        {
            // The test double is never registered with a running NetworkRunner.
        }
    }

    internal sealed class NetworkInputBuffer : IDisposable
    {
        private const int WordCount = 16;

        private IntPtr _dataPointer;

        public NetworkInputBuffer()
        {
            _dataPointer = Marshal.AllocHGlobal(WordCount * sizeof(int));
            Marshal.Copy(
                new byte[WordCount * sizeof(int)],
                0,
                _dataPointer,
                WordCount * sizeof(int));

            var layoutPointer = Marshal.AllocHGlobal(
                Marshal.SizeOf<NetworkInput>());

            try
            {
                Marshal.WriteIntPtr(layoutPointer, 0, _dataPointer);
                Marshal.WriteInt32(layoutPointer, IntPtr.Size, WordCount);
                Input = Marshal.PtrToStructure<NetworkInput>(layoutPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(layoutPointer);
            }
        }

        public NetworkInput Input { get; }

        public void Dispose()
        {
            if (_dataPointer == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(_dataPointer);
            _dataPointer = IntPtr.Zero;
        }
    }
}
