using UnityEngine;
using UnityEngine.UI;

namespace FusionFoundry.Samples.BasicHostClient
{
    /// <summary>
    /// Applies the Evrens visual identity without coupling the reusable session UI
    /// logic to presentation details. The scene remains easy to inspect and edit,
    /// while builds always receive the same brand treatment.
    /// </summary>
    internal static class EvrensBrandTheme
    {
        private static readonly Color Canvas = Hex("071314");
        private static readonly Color Surface = Hex("102A2C");
        private static readonly Color SurfaceRaised = Hex("17383A");
        private static readonly Color Brand = Hex("275457");
        private static readonly Color BrandHover = Hex("397276");
        private static readonly Color BrandPressed = Hex("1D4244");
        private static readonly Color TextPrimary = Hex("F7FAF9");
        private static readonly Color TextMuted = Hex("A9C1C0");
        private static readonly Color InputText = Hex("102526");
        private static readonly Color Danger = Hex("7B343B");

        public static void Apply(Transform root)
        {
            if (FindDeepChild(root, "EvrensBrandMark") != null)
            {
                return;
            }

            StyleImages(root);
            StyleText(root);
            StyleButtons(root);
            StyleInput(root);
            AddMainLogo(root);
            AddCornerMark(root);
        }

        private static void StyleImages(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                switch (image.gameObject.name)
                {
                    case "Backdrop": image.color = Canvas; break;
                    case "MainPanel":
                    case "JoinPanel": image.color = WithAlpha(Surface, 0.98f); break;
                    case "ConnectingPanel":
                    case "SessionPanel":
                    case "RoomCodePanel": image.color = WithAlpha(SurfaceRaised, 0.98f); break;
                    case "ErrorPanel": image.color = WithAlpha(Danger, 0.99f); break;
                }
            }
        }

        private static void StyleText(Transform root)
        {
            foreach (var label in root.GetComponentsInChildren<Text>(true))
            {
                label.color = TextPrimary;

                var name = label.gameObject.name;
                if (name == "Subtitle" || name == "Instructions" ||
                    name == "Hint" || name == "SessionStatusText" ||
                    name == "RoomCodeLabel")
                {
                    label.color = TextMuted;
                }

                if (name == "Title")
                {
                    label.fontStyle = FontStyle.Bold;
                }
            }
        }

        private static void StyleButtons(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var isPrimary = button.gameObject.name == "CreateButton" ||
                    button.gameObject.name == "JoinButton";
                var isDanger = button.gameObject.name == "LeaveButton";

                var normal = isDanger ? Danger : isPrimary ? Brand : SurfaceRaised;
                var colors = button.colors;
                colors.normalColor = normal;
                colors.highlightedColor = isDanger ? Hex("98505A") : BrandHover;
                colors.pressedColor = isDanger ? Hex("60272D") : BrandPressed;
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = WithAlpha(normal, 0.42f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.12f;
                button.colors = colors;
            }
        }

        private static void StyleInput(Transform root)
        {
            var input = root.GetComponentInChildren<InputField>(true);
            if (input == null)
            {
                return;
            }

            if (input.targetGraphic is Image background)
            {
                background.color = TextPrimary;
            }

            input.textComponent.color = InputText;
            input.caretColor = Brand;
            input.selectionColor = WithAlpha(BrandHover, 0.45f);
            if (input.placeholder is Text placeholder)
            {
                placeholder.color = WithAlpha(Brand, 0.62f);
            }
        }

        private static void AddMainLogo(Transform root)
        {
            var mainPanel = FindDeepChild(root, "MainPanel") as RectTransform;
            var texture = Resources.Load<Texture2D>("Brand/evrens-logo");
            if (mainPanel == null || texture == null)
            {
                return;
            }

            var logo = CreateRawImage("EvrensLogo", mainPanel, texture);
            logo.uvRect = new Rect(0.31f, 0.398f, 0.376f, 0.182f);
            logo.color = Color.white;
            SetRect(logo.rectTransform, new Vector2(0f, 185f), new Vector2(250f, 68f));

            var title = FindDeepChild(mainPanel, "Title") as RectTransform;
            if (title != null)
            {
                title.anchoredPosition = new Vector2(0f, 112f);
                var text = title.GetComponent<Text>();
                text.text = "FusionFoundry";
                text.fontSize = 38;
            }

            mainPanel.sizeDelta = new Vector2(620f, 520f);
        }

        private static void AddCornerMark(Transform root)
        {
            var texture = Resources.Load<Texture2D>("Brand/evrens-icon");
            if (texture == null)
            {
                return;
            }

            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                return;
            }

            var mark = CreateRawImage("EvrensBrandMark", canvas.transform, texture);
            mark.uvRect = new Rect(0.201f, 0.146f, 0.616f, 0.709f);
            mark.color = new Color(1f, 1f, 1f, 0.9f);
            mark.raycastTarget = false;
            var rect = mark.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -28f);
            rect.sizeDelta = new Vector2(54f, 62f);
            rect.SetAsLastSibling();
        }

        private static RawImage CreateRawImage(string name, Transform parent, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var result = FindDeepChild(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
