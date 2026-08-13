using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Services;
using UnityEngine;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DuelDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnAwake = true;
        private DuelMatchController _controller;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private bool _productHudActive;

        private void Awake()
        {
            if (buildOnAwake)
            {
                BuildDemo();
            }
        }

        public void BuildDemo()
        {
            if (_controller != null)
            {
                return;
            }

            EnsureLighting();
            var arena = CreatePrimitive("Arena", PrimitiveType.Cube, Vector3.zero, new Vector3(18f, 0.5f, 18f), new Color(0.08f, 0.12f, 0.18f));
            arena.transform.position = new Vector3(0f, -0.25f, 0f);
            DuelWorldArtDirector.EnhanceArena(arena);
            DuelWorldArtDirector.StyleBoundary(CreateBoundary(new Vector3(0f, 0.5f, 9f), new Vector3(18f, 1f, 0.4f)));
            DuelWorldArtDirector.StyleBoundary(CreateBoundary(new Vector3(0f, 0.5f, -9f), new Vector3(18f, 1f, 0.4f)));
            DuelWorldArtDirector.StyleBoundary(CreateBoundary(new Vector3(9f, 0.5f, 0f), new Vector3(0.4f, 1f, 18f)));
            DuelWorldArtDirector.StyleBoundary(CreateBoundary(new Vector3(-9f, 0.5f, 0f), new Vector3(0.4f, 1f, 18f)));

            var playerOne = CreatePrimitive("Player One", PrimitiveType.Capsule, new Vector3(-4f, 0.75f, 0f), Vector3.one, new Color(0.1f, 0.75f, 1f));
            var playerTwo = CreatePrimitive("AI Rival", PrimitiveType.Capsule, new Vector3(4f, 0.75f, 0f), Vector3.one, new Color(1f, 0.25f, 0.3f));
            var core = CreatePrimitive("Energy Core", PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.8f, new Color(1f, 0.8f, 0.1f));
            DuelWorldArtDirector.StylePlayer(playerOne, DuelPalette.Cyan, true);
            DuelWorldArtDirector.StylePlayer(playerTwo, DuelPalette.Red, false);
            DuelWorldArtDirector.StyleCore(core);
            var localInput = playerOne.AddComponent<LocalDuelCommandSource>();
            var aiInput = playerTwo.AddComponent<AiDuelCommandSource>();
            _controller = gameObject.AddComponent<DuelMatchController>();
            var settlement = gameObject.AddComponent<DuelSoloSettlementCoordinator>();
            settlement.Configure(_controller);
            _controller.Configure(playerOne.transform, playerTwo.transform, core.transform, localInput, aiInput);
            var hud = gameObject.AddComponent<DuelMatchHud>();
            hud.Configure(_controller);
            _productHudActive = true;
            EnsureCamera();
        }

        private void OnGUI()
        {
            if (_productHudActive)
            {
                return;
            }
            var snapshot = _controller?.Snapshot;
            if (snapshot == null)
            {
                return;
            }

            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            var preferences = DuelPlayerPreferences.Current;
            _titleStyle.fontSize = Mathf.RoundToInt(24f * preferences.HudScale);
            _bodyStyle.fontSize = Mathf.RoundToInt(16f * preferences.HudScale);
            _bodyStyle.normal.textColor = preferences.HighContrast ? Color.yellow : Color.white;

            var width = Mathf.Min(640f, Screen.width - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 16f, width, 238f), GUI.skin.box);
            GUILayout.Label("DUEL PROTOCOL — ENERGY CORE", _titleStyle);
            GUILayout.Label(
                $"{snapshot.PlayerOne.Score}  —  {snapshot.PlayerTwo.Score}    |    {snapshot.Phase}    |    {Mathf.CeilToInt(snapshot.RemainingSeconds)}s",
                _bodyStyle);
            var carrier = snapshot.Core.OwnerIndex < 0 ? "Core available" : $"Carrier: Player {snapshot.Core.OwnerIndex + 1}";
            GUILayout.Label(carrier, _bodyStyle);
            GUILayout.Label("WASD / Left stick: move + attack direction   •   Space / South button: pulse", _bodyStyle);
            var localInput = FindAnyObjectByType<LocalDuelCommandSource>();
            if (localInput != null && localInput.OnboardingStep != DuelOnboardingStep.Complete)
            {
                GUILayout.Label(localInput.OnboardingHint, _bodyStyle);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Reduced motion: {(preferences.ReducedMotion ? "On" : "Off")}"))
            {
                preferences.ReducedMotion = !preferences.ReducedMotion;
                DuelPlayerPreferences.Save(preferences);
            }
            if (GUILayout.Button($"High contrast: {(preferences.HighContrast ? "On" : "Off")}"))
            {
                preferences.HighContrast = !preferences.HighContrast;
                DuelPlayerPreferences.Save(preferences);
            }
            if (GUILayout.Button($"HUD: {preferences.HudScale:0.0}x"))
            {
                preferences.HudScale = preferences.HudScale >= 1.4f
                    ? 0.8f
                    : preferences.HudScale + 0.2f;
                DuelPlayerPreferences.Save(preferences);
            }
            GUILayout.EndHorizontal();
            if (snapshot.Phase == DuelMatchPhase.Finished && GUILayout.Button("Rematch"))
            {
                _controller.StartNewMatch();
            }
            GUILayout.EndArea();
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.transform.localScale = scale;
            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                             Shader.Find("Standard");
                renderer.material = new Material(shader) { color = color };
            }
            return instance;
        }

        private static GameObject CreateBoundary(Vector3 position, Vector3 scale)
        {
            return CreatePrimitive("Arena Boundary", PrimitiveType.Cube, position, scale, new Color(0.15f, 0.22f, 0.3f));
        }

        private static void EnsureLighting()
        {
            if (FindAnyObjectByType<Light>() != null)
            {
                return;
            }
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color32(196, 222, 255, 255);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            DuelWorldArtDirector.EnhanceLighting();
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f);
            DuelArenaCameraRig.Ensure(camera);
        }
    }
}
