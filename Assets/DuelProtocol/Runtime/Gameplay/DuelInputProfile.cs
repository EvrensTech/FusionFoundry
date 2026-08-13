using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace DuelProtocol.Gameplay
{
    public static class DuelInputActionFactory
    {
        private static string s_RuntimeTemplateJson;
        public const string ResourceName = "DuelInputActions";
        public const string MapName = "Duel";
        public const string MoveAction = "Move";
        public const string AimAction = "Aim";
        public const string AbilityAction = "Ability";
        public const string PointerPositionAction = "PointerPosition";

        public static InputActionAsset LoadOrCreate()
        {
            var template = Resources.Load<InputActionAsset>(ResourceName);
            return template != null ? UnityEngine.Object.Instantiate(template) : CreateRuntimeAsset();
        }

        public static InputActionAsset CreateRuntimeAsset()
        {
            if (!string.IsNullOrEmpty(s_RuntimeTemplateJson))
            {
                return InputActionAsset.FromJson(s_RuntimeTemplateJson);
            }
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = ResourceName;
            var map = new InputActionMap(MapName);

            var move = map.AddAction(MoveAction, InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick").WithProcessor("StickDeadzone");

            var aim = map.AddAction(AimAction, InputActionType.Value, expectedControlLayout: "Vector2");
            aim.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            aim.AddBinding("<Gamepad>/rightStick").WithProcessor("StickDeadzone");

            var ability = map.AddAction(AbilityAction, InputActionType.Button);
            ability.AddBinding("<Keyboard>/space");
            ability.AddBinding("<Mouse>/leftButton");
            ability.AddBinding("<Gamepad>/buttonSouth");
            ability.AddBinding("<Gamepad>/rightTrigger").WithInteraction("Press");
            ability.AddBinding("<Gamepad>/rightShoulder");

            var pointer = map.AddAction(
                PointerPositionAction,
                InputActionType.PassThrough,
                expectedControlLayout: "Vector2");
            pointer.AddBinding("<Mouse>/position");
            asset.AddActionMap(map);
            s_RuntimeTemplateJson = asset.ToJson();
            return asset;
        }
    }

    public static class DuelInputBindingStore
    {
        public const string PlayerPrefsKey = "duel.input.bindings.v1";

        [Serializable]
        private sealed class BindingOverrideProfile
        {
            public List<BindingOverrideEntry> Entries = new List<BindingOverrideEntry>();
        }

        [Serializable]
        private sealed class BindingOverrideEntry
        {
            public string Action = string.Empty;
            public int BindingIndex;
            public string Path = string.Empty;
        }

        public static void Load(InputActionAsset asset)
        {
            if (asset == null || !PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return;
            }

            try
            {
                var profile = JsonUtility.FromJson<BindingOverrideProfile>(
                    PlayerPrefs.GetString(PlayerPrefsKey));
                if (profile?.Entries == null)
                {
                    throw new InvalidOperationException("The binding profile is invalid.");
                }
                foreach (var entry in profile.Entries)
                {
                    var action = asset.FindAction(entry.Action, false);
                    if (action == null || entry.BindingIndex < 0 ||
                        entry.BindingIndex >= action.bindings.Count ||
                        string.IsNullOrWhiteSpace(entry.Path))
                    {
                        continue;
                    }
                    action.ApplyBindingOverride(entry.BindingIndex, entry.Path);
                }
            }
            catch (Exception)
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
            }
        }

        public static void Save(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }
            var profile = new BindingOverrideProfile();
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    for (var index = 0; index < action.bindings.Count; index++)
                    {
                        var binding = action.bindings[index];
                        if (string.IsNullOrWhiteSpace(binding.overridePath))
                        {
                            continue;
                        }
                        profile.Entries.Add(new BindingOverrideEntry
                        {
                            Action = action.name,
                            BindingIndex = index,
                            Path = binding.overridePath
                        });
                    }
                }
            }
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static void Reset(InputActionAsset asset)
        {
            asset?.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    public static class DuelTouchInput
    {
        public static Vector2 ReadStick(Vector2 screenPosition, Rect safeArea, bool leftSide)
        {
            if (safeArea.width <= 0f || safeArea.height <= 0f ||
                !safeArea.Contains(screenPosition))
            {
                return Vector2.zero;
            }

            var normalized = new Vector2(
                (screenPosition.x - safeArea.xMin) / safeArea.width,
                (screenPosition.y - safeArea.yMin) / safeArea.height);
            if (normalized.y > 0.62f || leftSide != (normalized.x < 0.5f))
            {
                return Vector2.zero;
            }

            var center = leftSide ? new Vector2(0.25f, 0.28f) : new Vector2(0.75f, 0.28f);
            return Vector2.ClampMagnitude((normalized - center) * 4f, 1f);
        }

        public static bool IsTouchBlockedByUi(int touchId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
        }
    }

    public static class DuelPointerAim
    {
        public static Vector2 Resolve(Camera camera, Vector2 screenPosition, Vector3 playerPosition)
        {
            if (camera == null)
            {
                return Vector2.zero;
            }

            var ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
            {
                return Vector2.zero;
            }
            var world = ray.GetPoint(distance);
            var direction = new Vector2(world.x - playerPosition.x, world.z - playerPosition.z);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        }
    }
}
