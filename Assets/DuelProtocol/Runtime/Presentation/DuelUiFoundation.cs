using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DuelProtocol.Presentation
{
    public static class DuelPalette
    {
        public static readonly Color Void = new Color32(5, 11, 19, 255);
        public static readonly Color Carbon = new Color32(11, 22, 34, 244);
        public static readonly Color Panel = new Color32(16, 31, 47, 238);
        public static readonly Color PanelRaised = new Color32(23, 43, 62, 248);
        public static readonly Color Line = new Color32(62, 94, 119, 180);
        public static readonly Color Text = new Color32(238, 247, 255, 255);
        public static readonly Color Muted = new Color32(147, 172, 192, 255);
        public static readonly Color Cyan = new Color32(28, 220, 255, 255);
        public static readonly Color Red = new Color32(255, 63, 86, 255);
        public static readonly Color Gold = new Color32(255, 190, 54, 255);
        public static readonly Color Success = new Color32(74, 231, 155, 255);
        public static readonly Color Warning = new Color32(255, 176, 64, 255);
        public static readonly Color Error = new Color32(255, 82, 94, 255);
    }

    public static class DuelUiAssets
    {
        private static Sprite _solid;
        private static Font _body;
        private static Font _heading;

        public static Sprite Solid
        {
            get
            {
                if (_solid != null) return _solid;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "DuelUiSolid",
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _solid = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
                _solid.name = "DuelUiSolidSprite";
                return _solid;
            }
        }

        public static Font Body => _body ??= Resources.Load<Font>("ProductUI/Fonts/Inter-Regular") ??
                                                   Resources.GetBuiltinResource<Font>("Arial.ttf");
        public static Font Heading => _heading ??= Resources.Load<Font>("ProductUI/Fonts/InterDisplay-Bold") ?? Body;
    }

    public static class DuelUiFactory
    {
        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            EnsureEventSystem();
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image Image(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = DuelUiAssets.Solid;
            image.color = color;
            return image;
        }

        public static Text Text(string name, Transform parent, string value, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool heading = false)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = heading ? DuelUiAssets.Heading : DuelUiAssets.Body;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(string name, Transform parent, string label,
            UnityEngine.Events.UnityAction onClick, bool primary = false, float height = 58f)
        {
            var image = Image(name, parent, primary ? new Color32(18, 137, 170, 245) : DuelPalette.PanelRaised);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color32(115, 232, 255, 255) : new Color32(133, 171, 198, 255);
            colors.pressedColor = primary ? new Color32(16, 103, 133, 255) : new Color32(78, 106, 128, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(65, 75, 84, 160);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(() => DuelUiAudio.Instance.PlayConfirm());
            image.gameObject.AddComponent<DuelUiButtonFeedback>();
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            var text = Text("Label", image.transform, label, 19, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            Stretch(text.rectTransform, 18, 8, 18, 8);
            return button;
        }

        public static Toggle Toggle(string name, Transform parent, string label, bool value,
            UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var root = Image(name, parent, DuelPalette.PanelRaised);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            var toggle = root.gameObject.AddComponent<Toggle>();
            var labelText = Text("Label", root.transform, label, 18, DuelPalette.Text);
            SetRect(labelText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 8), new Vector2(-86, -8));
            var track = Image("Track", root.transform, new Color32(55, 74, 89, 255));
            SetRect(track.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-70, -16), new Vector2(-18, 16));
            var mark = Image("Mark", track.transform, DuelPalette.Cyan);
            SetRect(mark.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-10, -10), new Vector2(10, 10));
            toggle.targetGraphic = track;
            toggle.graphic = mark;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(onChanged);
            toggle.onValueChanged.AddListener(_ => DuelUiAudio.Instance.PlayClick());
            root.gameObject.AddComponent<DuelUiButtonFeedback>();
            return toggle;
        }

        public static Slider Slider(string name, Transform parent, string label, float value,
            float minimum, float maximum, UnityAction<float> onChanged, bool percentage = true)
        {
            var root = Image(name, parent, DuelPalette.PanelRaised);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;

            var labelText = Text("Label", root.transform, label, 17, DuelPalette.Text, TextAnchor.MiddleLeft, true);
            SetRect(labelText.rectTransform, Vector2.zero, new Vector2(.37f, 1f), new Vector2(20, 8), new Vector2(-4, -8));

            var valueText = Text("Value", root.transform, string.Empty, 16, DuelPalette.Cyan, TextAnchor.MiddleRight, true);
            SetRect(valueText.rectTransform, new Vector2(.84f, 0), Vector2.one, Vector2.zero, new Vector2(-18, 0));

            var track = Image("Track", root.transform, new Color32(48, 69, 84, 255));
            SetRect(track.rectTransform, new Vector2(.38f, .5f), new Vector2(.83f, .5f), new Vector2(0, -5), new Vector2(0, 5));
            var fillArea = Rect("Fill Area", root.transform);
            SetRect(fillArea, new Vector2(.38f, .5f), new Vector2(.83f, .5f), new Vector2(0, -5), new Vector2(0, 5));
            var fill = Image("Fill", fillArea, DuelPalette.Cyan);
            Stretch(fill.rectTransform);
            var handleArea = Rect("Handle Slide Area", root.transform);
            SetRect(handleArea, new Vector2(.38f, .5f), new Vector2(.83f, .5f), new Vector2(-8, -13), new Vector2(8, 13));
            var handle = Image("Handle", handleArea, DuelPalette.Text);
            SetRect(handle.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(-8, -13), new Vector2(8, 13));

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(value);
            void Refresh(float current)
            {
                valueText.text = percentage
                    ? $"{Mathf.RoundToInt(current * 100f)}%"
                    : $"{current:0.0}×";
            }
            Refresh(value);
            slider.onValueChanged.AddListener(current =>
            {
                Refresh(current);
                onChanged?.Invoke(current);
            });
            root.gameObject.AddComponent<DuelUiButtonFeedback>();
            return slider;
        }

        public static Dropdown Dropdown(string name, Transform parent, string label,
            IReadOnlyList<string> options, int selectedIndex, UnityAction<int> onChanged)
        {
            var root = Image(name, parent, DuelPalette.PanelRaised);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
            var dropdown = root.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = root;

            var fieldLabel = Text("Field Label", root.transform, label, 17, DuelPalette.Text, TextAnchor.MiddleLeft, true);
            SetRect(fieldLabel.rectTransform, Vector2.zero, new Vector2(.43f, 1f), new Vector2(20, 8), new Vector2(-4, -8));
            var caption = Text("Caption", root.transform, string.Empty, 17, DuelPalette.Cyan, TextAnchor.MiddleRight, true);
            SetRect(caption.rectTransform, new Vector2(.42f, 0), new Vector2(.93f, 1f), Vector2.zero, new Vector2(-4, 0));
            var arrow = Text("Arrow", root.transform, "▼", 14, DuelPalette.Muted, TextAnchor.MiddleCenter, true);
            SetRect(arrow.rectTransform, new Vector2(.93f, 0), Vector2.one, Vector2.zero, Vector2.zero);

            var template = Image("Template", root.transform, DuelPalette.Carbon);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            // Settings is near the bottom of the panel, so the popup opens upward. Do not add a
            // Canvas here: Unity's Dropdown.SetupTemplate creates its own high-sorting popup
            // canvas and raycaster. A pre-created Canvas keeps the default sorting order and can
            // leave the options behind the product shell even though the list is active.
            SetRect(template.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 4), new Vector2(0, 190));
            var viewport = Image("Viewport", template.transform, new Color32(8, 18, 28, 255));
            Stretch(viewport.rectTransform, 4, 4, 4, 4);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var content = Rect("Content", viewport.transform);
            SetRect(content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1f);
            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var item = Image("Item", content, DuelPalette.PanelRaised);
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = item;
            var check = Image("Item Checkmark", item.transform, DuelPalette.Cyan);
            SetRect(check.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, -8), new Vector2(28, 8));
            itemToggle.graphic = check;
            var itemLabel = Text("Item Label", item.transform, "Option", 17, DuelPalette.Text, TextAnchor.MiddleLeft, true);
            Stretch(itemLabel.rectTransform, 38, 6, 12, 6);

            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            dropdown.template = template.rectTransform;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.interactable = true;
            dropdown.options.Clear();
            foreach (var option in options)
            {
                dropdown.options.Add(new Dropdown.OptionData(option));
            }
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, options.Count - 1)));
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(index =>
            {
                DuelUiAudio.Instance.PlayConfirm();
                onChanged?.Invoke(index);
            });
            template.gameObject.SetActive(false);
            root.gameObject.AddComponent<DuelUiButtonFeedback>();
            return dropdown;
        }

        public static void Stretch(RectTransform rect, float left = 0, float top = 0,
            float right = 0, float bottom = 0)
        {
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(left, bottom), new Vector2(-right, -top));
        }

        public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static VerticalLayoutGroup Vertical(Transform parent, int spacing = 12, int padding = 0)
        {
            var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Object.DontDestroyOnLoad(system);
        }
    }

    public sealed class DuelSafeArea : MonoBehaviour
    {
        private Rect _lastArea;
        private Vector2Int _lastResolution;

        private void OnEnable() => Apply();
        private void Update()
        {
            if (_lastArea != Screen.safeArea || _lastResolution.x != Screen.width || _lastResolution.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            var rect = (RectTransform)transform;
            var area = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _lastArea = area;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);
        }
    }

    public sealed class DuelUiAudio : MonoBehaviour
    {
        private static DuelUiAudio _instance;
        private AudioSource _source;
        private AudioClip _click;
        private AudioClip _confirm;
        private AudioClip _back;
        private AudioClip _error;
        private AudioSource _music;

        public static DuelUiAudio Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("Duel UI Audio");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<DuelUiAudio>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.ignoreListenerPause = true;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.ignoreListenerPause = true;
            _click = Resources.Load<AudioClip>("ProductUI/Audio/click_001");
            _confirm = Resources.Load<AudioClip>("ProductUI/Audio/confirmation_001");
            _back = Resources.Load<AudioClip>("ProductUI/Audio/back_001");
            _error = Resources.Load<AudioClip>("ProductUI/Audio/error_001");
            _music.clip = CreateAmbientLoop();
            _music.volume = DuelPlayerPreferences.Current.MasterVolume * DuelPlayerPreferences.Current.MusicVolume * .28f;
            _music.Play();
        }

        private void Update()
        {
            if (_music != null)
                _music.volume = DuelPlayerPreferences.Current.MasterVolume * DuelPlayerPreferences.Current.MusicVolume * .28f;
        }

        public void PlayClick() => Play(_click);
        public void PlayConfirm() => Play(_confirm);
        public void PlayBack() => Play(_back);
        public void PlayError() => Play(_error);

        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            var settings = DuelPlayerPreferences.Current;
            _source.PlayOneShot(clip, settings.MasterVolume * settings.UiVolume);
        }

        private static AudioClip CreateAmbientLoop()
        {
            const int rate = 22050;
            const int seconds = 8;
            var data = new float[rate * seconds];
            for (var i = 0; i < data.Length; i++)
            {
                var t = i / (float)rate;
                var fade = Mathf.Sin(Mathf.PI * i / data.Length);
                var pulse = .55f + .45f * Mathf.Sin(t * Mathf.PI * .5f);
                data[i] = (Mathf.Sin(t * Mathf.PI * 2f * 55f) * .055f +
                           Mathf.Sin(t * Mathf.PI * 2f * 82.5f) * .025f +
                           Mathf.Sin(t * Mathf.PI * 2f * 110f) * .012f) * pulse * (.8f + .2f * fade);
            }
            var clip = AudioClip.Create("Duel Ambient Protocol", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    public sealed class DuelUiButtonFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => DuelUiAudio.Instance.PlayClick();
        public void OnSelect(BaseEventData eventData) => DuelUiAudio.Instance.PlayClick();
    }
}
