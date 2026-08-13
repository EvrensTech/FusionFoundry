using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace DuelProtocol.Presentation
{
    /// <summary>
    /// Official Input System on-screen controls. The stick drives the existing
    /// Gamepad left-stick binding and the single action button drives buttonSouth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelMobileControls : MonoBehaviour
    {
        private static DuelMobileControls s_Instance;
        private GameObject _controlsRoot;

        public static bool IsActive => s_Instance != null &&
                                       s_Instance._controlsRoot != null &&
                                       s_Instance._controlsRoot.activeInHierarchy;

        public static void EnsureInstalled()
        {
            if (!Application.isMobilePlatform || s_Instance != null) return;

            var canvas = DuelUiFactory.CreateCanvas("Duel Mobile Controls", 90);
            s_Instance = canvas.gameObject.AddComponent<DuelMobileControls>();
            s_Instance.Build(canvas.transform);
        }

        public static void SetGameplayVisible(bool visible)
        {
            if (s_Instance?._controlsRoot != null)
            {
                s_Instance._controlsRoot.SetActive(visible);
            }
        }

        private void Build(Transform canvas)
        {
            var safeArea = DuelUiFactory.Rect("Safe Area", canvas);
            DuelUiFactory.Stretch(safeArea);
            safeArea.gameObject.AddComponent<DuelSafeArea>();

            _controlsRoot = DuelUiFactory.Rect("Gameplay Controls", safeArea).gameObject;
            DuelUiFactory.Stretch((RectTransform)_controlsRoot.transform);

            var stickBase = DuelUiFactory.Image(
                "Movement Joystick Base",
                _controlsRoot.transform,
                new Color(0.035f, 0.10f, 0.14f, 0.78f));
            DuelUiFactory.SetRect(
                stickBase.rectTransform,
                Vector2.zero,
                Vector2.zero,
                new Vector2(42f, 42f),
                new Vector2(262f, 262f));
            stickBase.raycastTarget = false;

            var stickHandle = DuelUiFactory.Image(
                "Movement Joystick Handle",
                stickBase.transform,
                new Color(0.12f, 0.88f, 1f, 0.96f));
            DuelUiFactory.SetRect(
                stickHandle.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-62f, -62f),
                new Vector2(62f, 62f));
            var stick = stickHandle.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 78f;
            stick.behaviour = OnScreenStick.Behaviour.ExactPositionWithStaticOrigin;
            stick.useIsolatedInputActions = true;

            var attackButton = DuelUiFactory.Image(
                "Attack Button",
                _controlsRoot.transform,
                new Color(0.93f, 0.18f, 0.08f, 0.92f));
            DuelUiFactory.SetRect(
                attackButton.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-218f, 54f),
                new Vector2(-54f, 218f));
            var button = attackButton.gameObject.AddComponent<OnScreenButton>();
            // buttonSouth is also the default UI Submit binding. Driving it from an
            // on-screen button can activate the selected Leave/Menu button while
            // attacking. rightShoulder is reserved as the mobile-only virtual input.
            button.controlPath = "<Gamepad>/rightShoulder";

            var label = DuelUiFactory.Text(
                "Attack Label",
                attackButton.transform,
                DuelLocalization.Text("ATAK", "ATTACK", "ANGRIFF"),
                24,
                Color.white,
                TextAnchor.MiddleCenter,
                true);
            DuelUiFactory.Stretch(label.rectTransform);
            label.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(s_Instance, this)) s_Instance = null;
        }
    }
}
