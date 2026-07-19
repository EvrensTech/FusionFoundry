using System.Collections.Generic;
using Fusion;
using FusionFoundry.Sessions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FusionFoundry.Spawning
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkRunner))]
    public class NetworkPlayerSpawner : NetworkRunnerCallbacksBehaviour
    {
        [SerializeField]
        private NetworkObject playerPrefab;

        [SerializeField]
        private string spawnPointRootName = "SpawnPoints";

        [SerializeField]
        [Min(0.1f)]
        private float fallbackSpacing = 2.5f;

        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers =
            new Dictionary<PlayerRef, NetworkObject>();

        public NetworkObject PlayerPrefab => playerPrefab;

        public int SpawnedPlayerCount => _spawnedPlayers.Count;

        public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!CanManagePlayers(runner) || playerPrefab == null)
            {
                return;
            }

            if (_spawnedPlayers.TryGetValue(player, out var trackedObject) &&
                trackedObject != null)
            {
                return;
            }

            if (TryGetPlayerObject(runner, player, out var registeredObject) &&
                registeredObject != null)
            {
                _spawnedPlayers[player] = registeredObject;
                return;
            }

            GetSpawnPose(player, out var position, out var rotation);
            var spawnedObject = SpawnPlayer(
                runner,
                playerPrefab,
                player,
                position,
                rotation);

            if (spawnedObject == null)
            {
                Debug.LogError($"Failed to spawn a player object for {player}.", this);
                return;
            }

            SetPlayerObject(runner, player, spawnedObject);
            _spawnedPlayers[player] = spawnedObject;
        }

        public override void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!CanManagePlayers(runner))
            {
                return;
            }

            _spawnedPlayers.TryGetValue(player, out var playerObject);
            if (playerObject == null)
            {
                TryGetPlayerObject(runner, player, out playerObject);
            }

            _spawnedPlayers.Remove(player);

            if (playerObject != null)
            {
                DespawnPlayer(runner, playerObject);
            }
        }

        public override void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _spawnedPlayers.Clear();
        }

        public bool TryGetTrackedPlayerObject(
            PlayerRef player,
            out NetworkObject playerObject)
        {
            return _spawnedPlayers.TryGetValue(player, out playerObject) &&
                   playerObject != null;
        }

        protected virtual bool CanManagePlayers(NetworkRunner runner)
        {
            return runner != null && runner.IsServer;
        }

        protected virtual bool TryGetPlayerObject(
            NetworkRunner runner,
            PlayerRef player,
            out NetworkObject playerObject)
        {
            return runner.TryGetPlayerObject(player, out playerObject);
        }

        protected virtual NetworkObject SpawnPlayer(
            NetworkRunner runner,
            NetworkObject prefab,
            PlayerRef inputAuthority,
            Vector3 position,
            Quaternion rotation)
        {
            return runner.Spawn(prefab, position, rotation, inputAuthority);
        }

        protected virtual void SetPlayerObject(
            NetworkRunner runner,
            PlayerRef player,
            NetworkObject playerObject)
        {
            runner.SetPlayerObject(player, playerObject);
        }

        protected virtual void DespawnPlayer(
            NetworkRunner runner,
            NetworkObject playerObject)
        {
            runner.Despawn(playerObject);
        }

        protected virtual void GetSpawnPose(
            PlayerRef player,
            out Vector3 position,
            out Quaternion rotation)
        {
            var spawnRoot = FindSpawnPointRoot();
            if (spawnRoot != null && spawnRoot.childCount > 0)
            {
                var spawnIndex = PositiveModulo(player.AsIndex, spawnRoot.childCount);
                var spawnPoint = spawnRoot.GetChild(spawnIndex);
                position = spawnPoint.position;
                rotation = spawnPoint.rotation;
                return;
            }

            position = transform.position +
                       (Vector3.right * (player.AsIndex * fallbackSpacing));
            rotation = transform.rotation;
        }

        private Transform FindSpawnPointRoot()
        {
            if (string.IsNullOrWhiteSpace(spawnPointRootName))
            {
                return null;
            }

            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = SceneManager.GetActiveScene();
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == spawnPointRootName)
                {
                    return rootObject.transform;
                }
            }

            return null;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
