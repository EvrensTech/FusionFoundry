using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuelProtocol.Content;
using DuelProtocol.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DuelFrontEndBootstrap : MonoBehaviour
    {
        public const string FrontEndScene = "DuelFrontEnd";
        public const string SoloScene = "DuelProtocol";
        public const string OnlineScene = "DuelProtocolNetwork";

        private readonly Dictionary<string, GameObject> _pages = new Dictionary<string, GameObject>();
        private RectTransform _content;
        private Text _eyebrow;
        private Text _title;
        private Text _subtitle;
        private Button _backButton;
        private Button _soloButton;
        private Button _onlineButton;
        private Button _coreArenaButton;
        private Button _wideArenaButton;
        private Text _selectionSummary;
        private Text _coreBalance;
        private Button _rewardedAdButton;
        private Text _profileSection;
        private Text _profileCallsign;
        private Text _profileRating;
        private Text _profileState;
        private Button _profileRetryButton;
        private DuelIdentitySession _identity;
        private IDuelProgressionService _progressionService;
        private DuelPlayerProfile _profile;
        private bool _profileStarted;
        private bool _profileLoading;
        private string _profileError = string.Empty;
        private string _selectedArena = "energy-core-arena";
        private bool _online;
        private string _automationScene;

        private void Awake()
        {
            _automationScene = ResolveAutomationScene();
            if (!string.IsNullOrEmpty(_automationScene)) return;
            Build();
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(_automationScene))
            {
                SceneManager.LoadScene(_automationScene);
                return;
            }
            _ = InitializeOnlineProfileAsync();
        }

        private static string ResolveAutomationScene()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (string.Equals(arg, "--duel-ai", StringComparison.OrdinalIgnoreCase))
                    return SoloScene;
                if (string.Equals(arg, "--duel-host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--duel-client", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--duel-matchmaking", StringComparison.OrdinalIgnoreCase))
                    return OnlineScene;
            }
            return string.Empty;
        }

        public void Build()
        {
            var cameraObject = new GameObject("Front End Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DuelPalette.Void;

            var canvas = DuelUiFactory.CreateCanvas("Duel Product Shell");
            var background = new GameObject("Key Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            background.transform.SetParent(canvas.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            DuelUiFactory.Stretch(bgRect);
            var bg = background.GetComponent<RawImage>();
            bg.texture = Resources.Load<Texture2D>("ProductUI/Images/duel-title-background-v1");
            bg.color = Color.white;
            var aspect = background.GetComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = 16f / 9f;

            var shade = DuelUiFactory.Image("Cinematic Shade", canvas.transform, new Color(0.01f, 0.025f, 0.045f, 0.54f));
            DuelUiFactory.Stretch(shade.rectTransform);
            var leftShade = DuelUiFactory.Image("Navigation Shade", canvas.transform, new Color(0.015f, 0.035f, 0.055f, 0.92f));
            DuelUiFactory.SetRect(leftShade.rectTransform, Vector2.zero, new Vector2(.43f, 1f), Vector2.zero, Vector2.zero);

            var safe = DuelUiFactory.Rect("Safe Area", canvas.transform);
            DuelUiFactory.Stretch(safe);
            safe.gameObject.AddComponent<DuelSafeArea>();

            var header = DuelUiFactory.Rect("Header", safe);
            DuelUiFactory.SetRect(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(72, -196), new Vector2(-72, -48));
            _eyebrow = DuelUiFactory.Text("Eyebrow", header, "EVRENS // FUSION FOUNDRY", 16, DuelPalette.Cyan, TextAnchor.UpperLeft, true);
            DuelUiFactory.SetRect(_eyebrow.rectTransform, new Vector2(0, .72f), Vector2.one, Vector2.zero, Vector2.zero);
            _title = DuelUiFactory.Text("Title", header, "DUEL PROTOCOL", 43, DuelPalette.Text, TextAnchor.MiddleLeft, true);
            DuelUiFactory.SetRect(_title.rectTransform, Vector2.zero, new Vector2(.58f, .72f), Vector2.zero, Vector2.zero);
            _subtitle = DuelUiFactory.Text("Subtitle", header, L("ENERJİ ÇEKİRDEĞİ ARENASI", "ENERGY CORE ARENA"), 15,
                DuelPalette.Muted, TextAnchor.LowerLeft);
            DuelUiFactory.SetRect(_subtitle.rectTransform, new Vector2(0, 0), new Vector2(.6f, .25f), new Vector2(3, 0), Vector2.zero);
            var build = DuelUiFactory.Text("Build", header, "BUILD 0.9 // DEVELOPMENT", 14, DuelPalette.Muted, TextAnchor.UpperRight);
            DuelUiFactory.SetRect(build.rectTransform, new Vector2(.6f, .7f), Vector2.one, Vector2.zero, Vector2.zero);

            _content = DuelUiFactory.Rect("Page Content", safe);
            DuelUiFactory.SetRect(_content, Vector2.zero, new Vector2(.46f, 1f), new Vector2(72, 56), new Vector2(-42, -222));
            _backButton = DuelUiFactory.Button("Back", safe, "‹  " + L("GERİ", "BACK"), ShowMain, false, 50);
            DuelUiFactory.SetRect((RectTransform)_backButton.transform, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(72, 56), new Vector2(236, 106));

            BuildMainPage();
            BuildPlayPage();
            BuildCollectionPage();
            BuildStorePage();
            BuildProfilePage();
            BuildSettingsPage();
            ShowMain();
        }

        private RectTransform CreatePage(string id)
        {
            var page = DuelUiFactory.Rect(id, _content);
            DuelUiFactory.Stretch(page);
            DuelUiFactory.Vertical(page, 12);
            _pages[id] = page.gameObject;
            return page;
        }

        private void BuildMainPage()
        {
            var page = CreatePage("main");
            AddSectionLabel(page, L("ANA KOMUTA", "COMMAND DECK"));
            DuelUiFactory.Button("Play", page, L("OYNA", "PLAY"), () => ShowPage("play", L("MOD SEÇİMİ", "SELECT MODE")), true, 70);
            DuelUiFactory.Button("Collection", page, L("KOLEKSİYON / SKINLER", "COLLECTION / SKINS"),
                () => ShowPage("collection", L("ROBOT KOLEKSİYONU", "ROBOT COLLECTION")));
            DuelUiFactory.Button("Store", page, L("MAĞAZA", "STORE"),
                () => ShowPage("store", L("KOZMİK VİTRİN", "COSMETIC SHOWCASE")));
            DuelUiFactory.Button("Profile", page, L("PROFİL", "PROFILE"),
                () => ShowPage("profile", L("PİLOT KAYDI", "PILOT RECORD")));
            DuelUiFactory.Button("Settings", page, L("AYARLAR", "SETTINGS"),
                () => ShowPage("settings", L("SİSTEM AYARLARI", "SYSTEM SETTINGS")));
            DuelUiFactory.Button("Quit", page, L("ÇIKIŞ", "QUIT"), Quit);
        }

        private void BuildPlayPage()
        {
            var page = CreatePage("play");
            AddSectionLabel(page, L("1 // OYUN MODU", "1 // GAME MODE"));
            var modeRow = DuelUiFactory.Rect("Mode Row", page);
            modeRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 74;
            var h = modeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12; h.childControlHeight = true; h.childControlWidth = true; h.childForceExpandWidth = true;
            _soloButton = DuelUiFactory.Button("Solo", modeRow, string.Empty, () => SelectMode(false), true, 70);
            _onlineButton = DuelUiFactory.Button("Online", modeRow, string.Empty, () => SelectMode(true), false, 70);
            AddSectionLabel(page, L("2 // ARENA", "2 // ARENA"));
            _coreArenaButton = DuelUiFactory.Button("Core Arena", page, string.Empty,
                () => SelectArena("energy-core-arena"), true);
            _wideArenaButton = DuelUiFactory.Button("Wide Arena", page, string.Empty,
                () => SelectArena("energy-core-arena-wide"));
            var rule = DuelUiFactory.Text("Rules", page,
                L("STANDART DÜELLO  //  3 SKOR  //  180 SN  //  EŞİT DONANIM",
                  "STANDARD DUEL  //  3 SCORE  //  180 SEC  //  EQUAL LOADOUT"),
                14, DuelPalette.Muted, TextAnchor.MiddleLeft);
            rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            _selectionSummary = DuelUiFactory.Text("Selection Summary", page, string.Empty, 14,
                DuelPalette.Success, TextAnchor.MiddleLeft, true);
            _selectionSummary.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            DuelUiFactory.Button("Deploy", page, L("ARENA'YA GİR", "DEPLOY TO ARENA"), Deploy, true, 72);
            UpdatePlaySelectionVisuals();
        }

        private void BuildCollectionPage()
        {
            var page = CreatePage("collection");
            AddSectionLabel(page, L("SEÇİLİ ROBOT SAVAŞTA KULLANILIR", "EQUIPPED ROBOT APPEARS IN MATCH"));
            foreach (var skin in DuelCosmeticCatalog.Skins)
            {
                var captured = skin;
                var selected = skin.Id == DuelCosmeticCatalog.EquippedId ? "  ✓" : string.Empty;
                var button = DuelUiFactory.Button("Skin " + skin.Id, page,
                    $"{skin.GetName(DuelLocalization.CurrentLanguage).ToUpperInvariant()}{selected}",
                    () => { DuelCosmeticCatalog.EquippedId = captured.Id; Rebuild(); }, skin.Id == DuelCosmeticCatalog.EquippedId);
                button.image.color = Color.Lerp(DuelPalette.PanelRaised, skin.Armor, .42f);
            }
        }

        private void BuildStorePage()
        {
            var page = CreatePage("store");
            AddSectionLabel(page, L("KOZMETİK VİTRİN // İSTEĞE BAĞLI ÖDÜLLÜ REKLAM",
                "COSMETIC SHOWCASE // OPT-IN REWARDED AD",
                "KOSMETIK-SHOP // OPTIONALE BELOHNUNGSWERBUNG"));
            var notice = DuelUiFactory.Text("Store Notice", page,
                L("Reklam yalnız sen istersen gösterilir. Tamamlanan mağaza bonusu 100 CORE verir; yarıda kapanan reklam ödül vermez.",
                  "Ads are shown only when you opt in. A completed store bonus grants 100 CORE; closing early grants no reward.",
                  "Werbung wird nur auf Wunsch gezeigt. Ein abgeschlossener Shop-Bonus gibt 100 CORE; vorzeitiges Schließen gibt keine Belohnung."),
                16, DuelPalette.Muted, TextAnchor.MiddleLeft);
            notice.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
            _coreBalance = DuelUiFactory.Text("Core Balance", page, string.Empty, 17,
                DuelPalette.Gold, TextAnchor.MiddleLeft, true);
            _coreBalance.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            UpdateCoreBalance();
            _rewardedAdButton = DuelUiFactory.Button("Rewarded Store Bonus", page,
                L("REKLAM İZLE  //  +100 CORE", "WATCH AD  //  +100 CORE", "WERBUNG ANSEHEN  //  +100 CORE"),
                WatchRewardedStoreBonus, true, 60);
            foreach (var skin in DuelCosmeticCatalog.Skins)
            {
                var captured = skin;
                var price = skin.Price == 0 ? L("VARSAYILAN", "DEFAULT") : $"{skin.Price:N0} CORE";
                DuelUiFactory.Button("Store " + skin.Id, page,
                    $"{skin.GetName(DuelLocalization.CurrentLanguage).ToUpperInvariant()}     {price}",
                    () => { DuelCosmeticCatalog.EquippedId = captured.Id; ShowToast(L("KUŞANILDI", "EQUIPPED")); });
            }
        }

        private async void WatchRewardedStoreBonus()
        {
            var ads = DuelRewardedAds.Current;
            if (!ads.IsAvailable)
            {
                ShowToast(L("REKLAM HENÜZ HAZIR DEĞİL", "AD IS NOT READY YET", "WERBUNG IST NOCH NICHT BEREIT"));
                return;
            }

            if (_rewardedAdButton != null) _rewardedAdButton.interactable = false;
            var rewarded = await ads.ShowRewardedAsync("store_bonus");
            if (this == null) return;
            if (_rewardedAdButton != null) _rewardedAdButton.interactable = true;
            if (!rewarded)
            {
                ShowToast(L("REKLAM TAMAMLANMADI // ÖDÜL YOK",
                    "AD NOT COMPLETED // NO REWARD", "WERBUNG NICHT ABGESCHLOSSEN // KEINE BELOHNUNG"));
                return;
            }

            DuelAdRewardWallet.GrantCore(100);
            UpdateCoreBalance();
            ShowToast(L("100 CORE HESABINA EKLENDİ", "100 CORE ADDED", "100 CORE HINZUGEFÜGT"));
        }

        private void UpdateCoreBalance()
        {
            if (_coreBalance == null) return;
            _coreBalance.text = $"CORE  //  {DuelAdRewardWallet.Balance:N0}";
        }

        private void BuildProfilePage()
        {
            var page = CreatePage("profile");
            _profileSection = AddSectionLabel(page, L(
                "PİLOT // UGS BAĞLANIYOR",
                "PILOT // CONNECTING TO UGS",
                "PILOT // UGS-VERBINDUNG WIRD HERGESTELLT"));
            _profileCallsign = AddStat(page, L("ÇAĞRI KODU", "CALLSIGN"), "—");
            _profileRating = AddStat(page, L("DERECE", "RATING"), "—");
            AddStat(page, L("SEZON", "SEASON"), L("SEZON ÖNCESİ", "PRE-SEASON"));
            AddStat(page, L("SEÇİLİ SKIN", "EQUIPPED SKIN"), DuelCosmeticCatalog.Equipped.GetName(DuelLocalization.CurrentLanguage));
            _profileState = DuelUiFactory.Text("Profile State", page, string.Empty,
                16, DuelPalette.Warning, TextAnchor.MiddleLeft);
            _profileState.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            _profileRetryButton = DuelUiFactory.Button(
                "Retry UGS",
                page,
                L("UGS BAĞLANTISINI YENİDEN DENE", "RETRY UGS CONNECTION", "UGS-VERBINDUNG ERNEUT VERSUCHEN"),
                () => _ = InitializeOnlineProfileAsync());
            UpdateProfilePage();
        }

        private async Task InitializeOnlineProfileAsync()
        {
            if (_profileLoading)
            {
                return;
            }

            _profileStarted = true;
            _profileLoading = true;
            _profileError = string.Empty;
            UpdateProfilePage();
            try
            {
                _identity ??= new DuelIdentitySession(new UgsDuelIdentityService());
                await _identity.InitializeAnonymousAsync("development");
                if (_identity.State != DuelIdentityState.SignedIn)
                {
                    _profileError = string.IsNullOrWhiteSpace(_identity.ErrorMessage)
                        ? _identity.ErrorCode
                        : $"{_identity.ErrorCode}: {_identity.ErrorMessage}";
                    return;
                }

                _progressionService ??= new UgsDuelBackend();
                _profile = await _progressionService.GetProfileAsync();
                if (_profile == null)
                {
                    _profileError = "GetDuelProfile returned an empty response.";
                }
            }
            catch (Exception exception)
            {
                _profileError = $"{exception.GetType().Name}: {exception.Message}";
                Debug.LogWarning($"DUEL_FRONTEND_UGS_FAILED error={_profileError}");
            }
            finally
            {
                _profileLoading = false;
                if (this != null)
                {
                    UpdateProfilePage();
                }
            }
        }

        private void UpdateProfilePage()
        {
            if (_profileState == null)
            {
                return;
            }

            if (!_profileStarted || _profileLoading)
            {
                _profileSection.text = L(
                    "PİLOT // UGS BAĞLANIYOR",
                    "PILOT // CONNECTING TO UGS",
                    "PILOT // UGS-VERBINDUNG WIRD HERGESTELLT");
                _profileState.text = L(
                    "Development ortamında anonim oturum ve profil yükleniyor...",
                    "Loading anonymous session and profile from the development environment...",
                    "Anonyme Sitzung und Profil werden aus der Entwicklungsumgebung geladen...");
                _profileState.color = DuelPalette.Warning;
                _profileRetryButton.interactable = false;
                return;
            }

            if (_profile != null && _identity?.State == DuelIdentityState.SignedIn)
            {
                var callsign = string.IsNullOrWhiteSpace(_profile.DisplayName)
                    ? _identity.PlayerId
                    : _profile.DisplayName;
                _profileSection.text = L(
                    "PİLOT // UGS DEVELOPMENT PROFİLİ",
                    "PILOT // UGS DEVELOPMENT PROFILE",
                    "PILOT // UGS-ENTWICKLUNGSPROFIL");
                _profileCallsign.text = callsign;
                var league = string.IsNullOrWhiteSpace(_profile.League)
                    ? "UNRANKED"
                    : _profile.League.ToUpperInvariant();
                _profileRating.text = $"{_profile.Rating:N0}  //  {league}";
                _profileState.text = L(
                    $"Çevrimiçi // Oyuncu {_identity.PlayerId} // {_profile.Wins}/{_profile.Losses}/{_profile.Draws} G/M/B // {_profile.Experience} XP",
                    $"Online // Player {_identity.PlayerId} // W/L/D {_profile.Wins}/{_profile.Losses}/{_profile.Draws} // {_profile.Experience} XP",
                    $"Online // Spieler {_identity.PlayerId} // S/N/U {_profile.Wins}/{_profile.Losses}/{_profile.Draws} // {_profile.Experience} EP");
                _profileState.color = DuelPalette.Success;
                _profileRetryButton.gameObject.SetActive(false);
                return;
            }

            _profileSection.text = L(
                "PİLOT // UGS BAĞLANTI HATASI",
                "PILOT // UGS CONNECTION ERROR",
                "PILOT // UGS-VERBINDUNGSFEHLER");
            _profileCallsign.text = "—";
            _profileRating.text = "—";
            _profileState.text = L(
                $"UGS bağlantısı kurulamadı: {_profileError}",
                $"UGS connection failed: {_profileError}",
                $"UGS-Verbindung fehlgeschlagen: {_profileError}");
            _profileState.color = DuelPalette.Warning;
            _profileRetryButton.gameObject.SetActive(true);
            _profileRetryButton.interactable = true;
        }

        private void BuildSettingsPage()
        {
            var page = CreatePage("settings");
            var settings = DuelPlayerPreferences.Current;
            AddSectionLabel(page, L("ERİŞİLEBİLİRLİK", "ACCESSIBILITY"));
            DuelUiFactory.Toggle("Reduced Motion", page, L("HAREKETİ AZALT", "REDUCE MOTION"), settings.ReducedMotion,
                value => SaveSetting(s => s.ReducedMotion = value));
            DuelUiFactory.Toggle("High Contrast", page, L("YÜKSEK KONTRAST", "HIGH CONTRAST"), settings.HighContrast,
                value => SaveSetting(s => s.HighContrast = value));
            DuelUiFactory.Slider("Hud Scale", page, L("UI ÖLÇEĞİ", "UI SCALE"), settings.HudScale, .8f, 1.5f,
                value => SaveSetting(s => s.HudScale = value), false);
            AddSectionLabel(page, L("SES VE DİL", "AUDIO & LANGUAGE"));
            DuelUiFactory.Slider("Master Volume", page, L("ANA SES", "MASTER VOLUME"), settings.MasterVolume, 0f, 1f,
                value => SaveSetting(s => s.MasterVolume = value));
            DuelUiFactory.Slider("Music Volume", page, L("MÜZİK", "MUSIC VOLUME"), settings.MusicVolume, 0f, 1f,
                value => SaveSetting(s => s.MusicVolume = value));
            DuelUiFactory.Slider("SFX Volume", page, L("EFEKTLER", "SFX VOLUME"), settings.SfxVolume, 0f, 1f,
                value => SaveSetting(s => s.SfxVolume = value));
            DuelUiFactory.Slider("UI Volume", page, L("ARAYÜZ SESİ", "UI VOLUME"), settings.UiVolume, 0f, 1f,
                value => SaveSetting(s => s.UiVolume = value));
            DuelUiFactory.Dropdown("Language", page, L("DİL", "LANGUAGE"),
                new[] { "English", "Türkçe", "Deutsch" },
                DuelLocalization.GetLanguageIndex(settings.Language), index =>
            {
                var language = DuelLocalization.GetLanguageAt(index);
                if (settings.Language == language) return;
                settings.Language = language;
                DuelPlayerPreferences.Save(settings);
                Rebuild();
            });
            DuelUiFactory.Button("Reset", page, L("VARSAYILANLARA DÖN", "RESTORE DEFAULTS"), () => { DuelPlayerPreferences.Reset(); Rebuild(); });
        }

        private Text AddSectionLabel(Transform parent, string value)
        {
            var label = DuelUiFactory.Text("Section", parent, value, 14, DuelPalette.Cyan, TextAnchor.MiddleLeft, true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            return label;
        }

        private Text AddStat(Transform parent, string label, string value)
        {
            var row = DuelUiFactory.Image("Stat " + label, parent, DuelPalette.PanelRaised);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            var left = DuelUiFactory.Text("Label", row.transform, label, 14, DuelPalette.Muted, TextAnchor.MiddleLeft, true);
            DuelUiFactory.SetRect(left.rectTransform, Vector2.zero, new Vector2(.45f, 1), new Vector2(20, 8), new Vector2(0, -8));
            var right = DuelUiFactory.Text("Value", row.transform, value, 18, DuelPalette.Text, TextAnchor.MiddleRight, true);
            DuelUiFactory.SetRect(right.rectTransform, new Vector2(.42f, 0), Vector2.one, new Vector2(0, 8), new Vector2(-20, -8));
            return right;
        }

        private void ShowMain()
        {
            ShowPage("main", L("ENERJİ ÇEKİRDEĞİ ARENASI", "ENERGY CORE ARENA"));
            _backButton.gameObject.SetActive(false);
        }

        private void ShowPage(string id, string subtitle)
        {
            foreach (var page in _pages) page.Value.SetActive(page.Key == id);
            _subtitle.text = subtitle;
            _backButton.gameObject.SetActive(id != "main");
            if (EventSystem.current != null && _pages.TryGetValue(id, out var activePage))
            {
                var first = activePage.GetComponentInChildren<Selectable>(false);
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        private void Deploy()
        {
            PlayerPrefs.SetString("duel.arena.selected.v1", _selectedArena);
            PlayerPrefs.Save();
            SceneManager.LoadScene(_online ? OnlineScene : SoloScene);
        }

        private void SaveSetting(Action<DuelAccessibilitySettings> change)
        {
            var settings = DuelPlayerPreferences.Current;
            change(settings);
            DuelPlayerPreferences.Save(settings);
        }

        private void SelectMode(bool online)
        {
            _online = online;
            UpdatePlaySelectionVisuals();
        }

        private void SelectArena(string arenaId)
        {
            _selectedArena = arenaId;
            UpdatePlaySelectionVisuals();
        }

        private void UpdatePlaySelectionVisuals()
        {
            if (_soloButton == null) return;
            SetSelection(_soloButton, !_online,
                L("TEK OYUNCULU\nYAPAY ZEKÂ DÜELLOSU", "SOLO\nAI DUEL"));
            SetSelection(_onlineButton, _online,
                L("ÇEVRİMİÇİ\nDERECELİ / ÖZEL", "ONLINE\nRANKED / PRIVATE"));
            SetSelection(_coreArenaButton, _selectedArena == "energy-core-arena",
                L("NEON CITADEL  •  DENGELİ  •  18×18", "NEON CITADEL  •  BALANCED  •  18×18"));
            SetSelection(_wideArenaButton, _selectedArena == "energy-core-arena-wide",
                L("ORBITAL FOUNDRY  •  GENİŞ  •  20×14", "ORBITAL FOUNDRY  •  WIDE  •  20×14"));
            if (_selectionSummary != null)
            {
                var mode = _online ? L("ÇEVRİMİÇİ", "ONLINE") : L("TEK OYUNCULU", "SOLO");
                var arena = _selectedArena == "energy-core-arena" ? "NEON CITADEL" : "ORBITAL FOUNDRY";
                _selectionSummary.text = L($"SEÇİM HAZIR  //  {mode}  //  {arena}",
                    $"SELECTION READY  //  {mode}  //  {arena}",
                    $"AUSWAHL BEREIT  //  {mode}  //  {arena}");
            }
        }

        private static void SetSelection(Button button, bool selected, string label)
        {
            if (button == null) return;
            button.image.color = selected ? new Color32(18, 137, 170, 245) : DuelPalette.PanelRaised;
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = (selected ? "✓  " : string.Empty) + label;
        }

        private void ShowToast(string message)
        {
            _subtitle.text = "✓  " + message;
            DuelUiAudio.Instance.PlayConfirm();
        }

        private void Rebuild()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private string L(string tr, string en, string de = null) => DuelLocalization.Text(tr, en, de);
    }
}
