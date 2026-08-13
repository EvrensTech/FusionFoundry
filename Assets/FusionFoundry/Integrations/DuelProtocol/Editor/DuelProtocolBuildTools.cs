using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuelProtocol.Presentation;
using DuelProtocol.Content;
using DuelProtocol.Match;
using DuelProtocol.Gameplay;
using DuelProtocol.Networking;
using Fusion;
using FusionFoundry.Bootstrap;
using FusionFoundry.Sessions;
using FusionFoundry.Spawning;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuelProtocol.Editor
{
    public static class DuelProtocolBuildTools
    {
        public const string ScenePath = "Assets/DuelProtocol/Scenes/DuelProtocol.unity";
        public const string NetworkScenePath = "Assets/DuelProtocol/Scenes/DuelProtocolNetwork.unity";
        public const string FrontEndScenePath = "Assets/DuelProtocol/Scenes/DuelFrontEnd.unity";
        private const string PlayerPrefabPath = "Assets/DuelProtocol/Prefabs/DuelNetworkPlayer.prefab";
        private const string WorldPrefabPath = "Assets/DuelProtocol/Prefabs/DuelNetworkWorld.prefab";
        private const string RunnerPrefabPath = "Assets/DuelProtocol/Prefabs/DuelNetworkRunner.prefab";
        private const string WindowsOutput = "Builds/Windows/DuelProtocol.exe";
        private const string AndroidOutput = "Builds/Android/DuelProtocol.apk";

        [MenuItem("Duel Protocol/Create or Refresh Demo Scene")]
        public static void CreateDemoScene()
        {
            Directory.CreateDirectory("Assets/DuelProtocol/Scenes");
            Directory.CreateDirectory("Assets/DuelProtocol/Config");
            EnsureConfigAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DuelProtocol";
            var bootstrap = new GameObject("Duel Protocol Bootstrap");
            bootstrap.AddComponent<DuelDemoBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            CreateNetworkContent();
            CreateFrontEndScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel Protocol demo scene written to {ScenePath}.");
        }

        [MenuItem("Duel Protocol/Create or Refresh Front End Scene")]
        public static void CreateFrontEndScene()
        {
            Directory.CreateDirectory("Assets/DuelProtocol/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DuelFrontEnd";
            new GameObject("Duel Front End Bootstrap").AddComponent<DuelFrontEndBootstrap>();
            EditorSceneManager.SaveScene(scene, FrontEndScenePath);
            var ordered = new[]
            {
                new EditorBuildSettingsScene(FrontEndScenePath, true),
                new EditorBuildSettingsScene(NetworkScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = ordered;
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel Protocol front-end scene written to {FrontEndScenePath}.");
        }

        [MenuItem("Duel Protocol/Create or Refresh Network Scene")]
        public static void CreateNetworkContent()
        {
            Directory.CreateDirectory("Assets/DuelProtocol/Prefabs");
            Directory.CreateDirectory("Assets/DuelProtocol/Scenes");
            var playerPrefab = CreatePlayerPrefab();
            var worldPrefab = CreateWorldPrefab();
            var runnerPrefab = CreateRunnerPrefab(playerPrefab, worldPrefab);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DuelProtocolNetwork";
            CreateNetworkArena();

            var bootstrapObject = new GameObject("Duel Network Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<FusionBootstrap>();
            var bootstrapSerialized = new SerializedObject(bootstrap);
            bootstrapSerialized.FindProperty("runnerPrefab").objectReferenceValue =
                runnerPrefab.GetComponent<FusionSessionController>();
            bootstrapSerialized.FindProperty("defaultMaxPlayers").intValue = 2;
            bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();

            var menu = bootstrapObject.AddComponent<DuelNetworkMenu>();
            menu.Configure(bootstrap);
            EnsureNetworkCameraAndLight();
            EditorSceneManager.SaveScene(scene, NetworkScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != NetworkScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(NetworkScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Duel Protocol network scene written to {NetworkScenePath}.");
        }

        private static void EnsureConfigAssets()
        {
            const string matchConfigPath = "Assets/DuelProtocol/Config/DuelMatchConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<DuelMatchConfig>(matchConfigPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<DuelMatchConfig>(),
                    matchConfigPath);
            }

            const string catalogPath = "Assets/DuelProtocol/Config/DuelContentCatalog.asset";
            var existingCatalog = AssetDatabase.LoadAssetAtPath<DuelContentCatalog>(catalogPath);
            if (existingCatalog == null)
            {
                var catalog = ScriptableObject.CreateInstance<DuelContentCatalog>();
                catalog.InitializeDefaults();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            else if (existingCatalog.Skins.Count < 6)
            {
                existingCatalog.InitializeDefaults();
                EditorUtility.SetDirty(existingCatalog);
            }
        }

        [MenuItem("Duel Protocol/Build/Windows x64")]
        public static void BuildWindows()
        {
            EnsureScene();
            Build(
                BuildTarget.StandaloneWindows64,
                WindowsOutput,
                BuildOptions.StrictMode | BuildOptions.CleanBuildCache);
        }

        public static void BuildWindowsIncremental()
        {
            EnsureScene();
            Build(
                BuildTarget.StandaloneWindows64,
                WindowsOutput,
                BuildOptions.StrictMode);
        }

        [MenuItem("Duel Protocol/Build/Android ARM64")]
        public static void BuildAndroid()
        {
            EnsureScene();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            Build(
                BuildTarget.Android,
                AndroidOutput,
                BuildOptions.StrictMode | BuildOptions.CleanBuildCache);
        }

        public static void BuildAndroidIncremental()
        {
            EnsureScene();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            Build(BuildTarget.Android, AndroidOutput, BuildOptions.StrictMode);
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath) || !File.Exists(NetworkScenePath) || !File.Exists(FrontEndScenePath))
            {
                CreateDemoScene();
            }
        }

        private static void Build(BuildTarget target, string output, BuildOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target))
            {
                throw new InvalidOperationException($"Could not activate build target {target}.");
            }

            // Localization collections are Addressables-backed. Building their content after the
            // target switch prevents a Windows player from packaging the last Android catalog
            // (and vice versa).
            AddressableAssetSettings.BuildPlayerContent(out var addressablesResult);
            if (!string.IsNullOrEmpty(addressablesResult.Error))
            {
                throw new InvalidOperationException(
                    $"Addressables build failed for {target}: {addressablesResult.Error}");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { FrontEndScenePath, NetworkScenePath, ScenePath },
                locationPathName = output,
                target = target,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{target} build failed: {report.summary.totalErrors} error(s).");
            }
            Debug.Log(
                $"DUEL_BUILD_OK target={target} bytes={report.summary.totalSize} " +
                $"duration={report.summary.totalTime} output={output}");
        }

        private static GameObject CreatePlayerPrefab()
        {
            var root = new GameObject("DuelNetworkPlayer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<DuelNetworkPlayer>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.75f;
            visual.GetComponent<Renderer>().sharedMaterial = CreateOrGetMaterial(
                "Assets/DuelProtocol/Config/NetworkPlayer.mat",
                new Color(0.1f, 0.75f, 1f));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateWorldPrefab()
        {
            var root = new GameObject("DuelNetworkWorld");
            root.AddComponent<NetworkObject>();
            var match = root.AddComponent<DuelNetworkMatchState>();
            var core = root.AddComponent<DuelNetworkCore>();
            var coreSerialized = new SerializedObject(core);
            coreSerialized.FindProperty("matchState").objectReferenceValue = match;
            coreSerialized.ApplyModifiedPropertiesWithoutUndo();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "EnergyCoreVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.8f;
            visual.GetComponent<Renderer>().sharedMaterial = CreateOrGetMaterial(
                "Assets/DuelProtocol/Config/EnergyCore.mat",
                new Color(1f, 0.8f, 0.1f));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, WorldPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateRunnerPrefab(GameObject playerPrefab, GameObject worldPrefab)
        {
            var root = new GameObject("DuelNetworkRunner");
            root.AddComponent<NetworkRunner>();
            root.AddComponent<FusionSessionController>();
            var playerSpawner = root.AddComponent<NetworkPlayerSpawner>();
            var localInput = root.AddComponent<LocalDuelCommandSource>();
            var inputProvider = root.AddComponent<DuelNetworkInputProvider>();
            var worldSpawner = root.AddComponent<DuelNetworkWorldSpawner>();

            var spawnerSerialized = new SerializedObject(playerSpawner);
            spawnerSerialized.FindProperty("playerPrefab").objectReferenceValue =
                playerPrefab.GetComponent<NetworkObject>();
            spawnerSerialized.FindProperty("reconnectGraceSeconds").floatValue = 20f;
            spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var inputSerialized = new SerializedObject(inputProvider);
            inputSerialized.FindProperty("commandSource").objectReferenceValue = localInput;
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();

            var worldSerialized = new SerializedObject(worldSpawner);
            worldSerialized.FindProperty("worldPrefab").objectReferenceValue =
                worldPrefab.GetComponent<NetworkObject>();
            worldSerialized.ApplyModifiedPropertiesWithoutUndo();

            var sessionSerialized = new SerializedObject(root.GetComponent<FusionSessionController>());
            sessionSerialized.FindProperty("fixedRegion").stringValue = "eu";
            sessionSerialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RunnerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateOrGetMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateNetworkArena()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena";
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(18f, 0.5f, 18f);
            floor.GetComponent<Renderer>().sharedMaterial = CreateOrGetMaterial(
                "Assets/DuelProtocol/Config/Arena.mat",
                new Color(0.08f, 0.12f, 0.18f));

            var spawnRoot = new GameObject("SpawnPoints");
            CreateSpawnPoint(spawnRoot.transform, "PlayerOne", new Vector3(-4f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            CreateSpawnPoint(spawnRoot.transform, "PlayerTwo", new Vector3(4f, 0f, 0f), Quaternion.LookRotation(Vector3.left));
        }

        private static void CreateSpawnPoint(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var spawn = new GameObject(name);
            spawn.transform.SetParent(parent);
            spawn.transform.SetPositionAndRotation(position, rotation);
        }

        private static void EnsureNetworkCameraAndLight()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f);
            DuelArenaCameraRig.Ensure(camera);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
