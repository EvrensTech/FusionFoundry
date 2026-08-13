using DuelProtocol.Match;
using DuelProtocol.Networking;
using FusionFoundry.Sessions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DuelNetworkUi : MonoBehaviour
    {
        private DuelNetworkMenu _menu;
        private GameObject _lobby;
        private GameObject _search;
        private GameObject _matchHud;
        private GameObject _result;
        private Text _status;
        private Text _identity;
        private Text _searchText;
        private Text _room;
        private Text _score;
        private Text _timer;
        private Text _phase;
        private Text _cooldown;
        private Text _readyState;
        private InputField _roomInput;
        private Button _ready;
        private Button _rematch;
        private Button _leave;
        private GameObject _readyGate;

        public void Configure(DuelNetworkMenu menu)
        {
            _menu = menu;
            Build();
        }

        private void Update()
        {
            if (_menu == null) return;
            var running = _menu.SessionState == FusionSessionState.Running;
            _lobby.SetActive(!running && !_menu.Searching);
            _search.SetActive(!running && _menu.Searching);
            _matchHud.SetActive(running);
            _status.text = string.IsNullOrWhiteSpace(_menu.SettlementText)
                ? _menu.StatusText
                : $"{_menu.StatusText}\n{_menu.SettlementText}";
            _identity.text = $"{L("KİMLİK", "IDENTITY")}  {_menu.IdentityText.ToUpperInvariant()}" +
                             (string.IsNullOrEmpty(_menu.IdentityError) ? string.Empty : $"  //  {_menu.IdentityError}");
            _searchText.text = L(
                $"RAKİP ARANIYOR\nREYTİNG {_menu.SearchRating}  •  ±{_menu.SearchBand}\n{_menu.SearchElapsed:0} / 60 SN",
                $"SEARCHING FOR OPPONENT\nRATING {_menu.SearchRating}  •  ±{_menu.SearchBand}\n{_menu.SearchElapsed:0} / 60 SEC",
                $"GEGNER WIRD GESUCHT\nWERTUNG {_menu.SearchRating}  •  ±{_menu.SearchBand}\n{_menu.SearchElapsed:0} / 60 SEK");
            _room.text = string.IsNullOrWhiteSpace(_menu.ActiveRoomCode)
                ? L("GÜVENLİ BAĞLANTI", "SECURE CONNECTION")
                : $"{L("ODA", "ROOM")}  {_menu.ActiveRoomCode}";
            EnsureSelection(running ? _matchHud : _menu.Searching ? _search : _lobby);

            var state = FindAnyObjectByType<DuelNetworkMatchState>();
            if (state == null || !running)
            {
                _result.SetActive(false);
                _readyGate.SetActive(false);
                DuelMobileControls.SetGameplayVisible(false);
                return;
            }
            _score.text = $"CYAN  {state.PlayerOneScore}     —     {state.PlayerTwoScore}  CRIMSON";
            var seconds = state.MatchTimer.RemainingTime(state.Runner) ?? 0f;
            _timer.text = state.Phase == DuelMatchPhase.ReadyCheck
                ? "--:--"
                : $"{Mathf.FloorToInt(seconds / 60):00}:{Mathf.FloorToInt(seconds % 60):00}";
            _phase.text = Phase(state.Phase);
            UpdateReadyGate(state);
            UpdateAbilityCooldown(state);
            _rematch.gameObject.SetActive(state.Phase == DuelMatchPhase.Finished);
            _result.SetActive(state.Phase == DuelMatchPhase.Finished);
            DuelMobileControls.SetGameplayVisible(
                state.Phase == DuelMatchPhase.Playing || state.Phase == DuelMatchPhase.Overtime);
        }

        private void UpdateReadyGate(DuelNetworkMatchState state)
        {
            var visible = state.Phase == DuelMatchPhase.ReadyCheck;
            _readyGate.SetActive(visible);
            if (!visible) return;

            var localPlayer = state.Runner.LocalPlayer;
            var localReady = state.IsReady(localPlayer);
            var localIndex = localPlayer.AsIndex - 1;
            var opponentReady = localIndex == 0
                ? (bool)state.PlayerTwoReady
                : (bool)state.PlayerOneReady;
            _ready.interactable = !localReady;
            _ready.GetComponentInChildren<Text>().text = localReady
                ? L("HAZIRSIN", "YOU ARE READY", "DU BIST BEREIT")
                : L("HAZIR", "READY", "BEREIT");
            _readyState.text = localReady
                ? opponentReady
                    ? L("İKİ OYUNCU HAZIR // MAÇ BAŞLIYOR", "BOTH PLAYERS READY // STARTING", "BEIDE BEREIT // START")
                    : L("RAKİBİN HAZIR OLMASI BEKLENİYOR", "WAITING FOR OPPONENT", "WARTE AUF DEN GEGNER")
                : opponentReady
                    ? L("RAKİP HAZIR // SENİ BEKLİYOR", "OPPONENT READY // WAITING FOR YOU", "GEGNER BEREIT // WARTET AUF DICH")
                    : L("İKİ OYUNCU DA HAZIR VERMELİ", "BOTH PLAYERS MUST READY UP", "BEIDE SPIELER MÜSSEN BEREIT SEIN");
        }

        private void UpdateAbilityCooldown(DuelNetworkMatchState state)
        {
            var localObject = state.Runner.GetPlayerObject(state.Runner.LocalPlayer);
            var player = localObject != null ? localObject.GetComponent<DuelNetworkPlayer>() : null;
            var remaining = player != null ? player.AbilityCooldownRemaining : 0f;
            _cooldown.text = remaining > 0.05f
                ? L($"SALDIRI // {remaining:0.0} SN", $"ATTACK // {remaining:0.0} SEC", $"ANGRIFF // {remaining:0.0} SEK")
                : L("SALDIRI // HAZIR", "ATTACK // READY", "ANGRIFF // BEREIT");
            _cooldown.color = remaining > 0.05f ? DuelPalette.Warning : DuelPalette.Cyan;
        }

        private static void EnsureSelection(GameObject scope)
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null || scope == null) return;
            var first = scope.GetComponentInChildren<Selectable>(false);
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private void Build()
        {
            var canvas = DuelUiFactory.CreateCanvas("Duel Network Product UI", 35);
            var shade = DuelUiFactory.Image("Readability Shade", canvas.transform, new Color(.01f, .025f, .04f, .36f));
            DuelUiFactory.Stretch(shade.rectTransform);
            var safe = DuelUiFactory.Rect("Safe Area", canvas.transform);
            DuelUiFactory.Stretch(safe);
            safe.gameObject.AddComponent<DuelSafeArea>();

            var header = DuelUiFactory.Image("Network Header", safe, new Color(.025f, .065f, .095f, .94f));
            DuelUiFactory.SetRect(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -116), new Vector2(-26, -24));
            var brand = DuelUiFactory.Text("Brand", header.transform, "DUEL PROTOCOL  //  NETWORK", 25, DuelPalette.Text, TextAnchor.MiddleLeft, true);
            DuelUiFactory.SetRect(brand.rectTransform, Vector2.zero, new Vector2(.46f, 1), new Vector2(24, 8), Vector2.zero);
            _status = DuelUiFactory.Text("Status", header.transform, string.Empty, 14, DuelPalette.Muted, TextAnchor.UpperRight);
            DuelUiFactory.SetRect(_status.rectTransform, new Vector2(.43f, .44f), Vector2.one, Vector2.zero, new Vector2(-24, -10));
            _identity = DuelUiFactory.Text("Identity", header.transform, string.Empty, 12, DuelPalette.Cyan, TextAnchor.LowerRight, true);
            DuelUiFactory.SetRect(_identity.rectTransform, new Vector2(.43f, 0), new Vector2(1, .48f), Vector2.zero, new Vector2(-24, 8));

            _lobby = CreatePanel(safe, "Lobby", 590, 610);
            var lobbyContent = _lobby.transform.Find("Content");
            AddTitle(lobbyContent, L("ARENA AĞ GEÇİDİ", "ARENA NETWORK GATEWAY"),
                L("Hızlı eşleş veya altı karakterli özel oda kodu kullan.", "Match quickly or use a six-character private room code."));
            DuelUiFactory.Button("Unranked", lobbyContent, L("DERECESİZ RAKİP BUL", "FIND UNRANKED OPPONENT"), _menu.UiFindUnranked, true, 66);
            DuelUiFactory.Button("Ranked", lobbyContent, L("DERECELİ RAKİP BUL", "FIND RANKED OPPONENT"), _menu.UiFindRanked, false, 66);
            DuelUiFactory.Button("Create", lobbyContent, L("ÖZEL ODA OLUŞTUR", "CREATE PRIVATE ROOM"), _menu.UiCreateRoom);
            _roomInput = CreateInput(lobbyContent, L("ODA KODU", "ROOM CODE"));
            DuelUiFactory.Button("Join", lobbyContent, L("KODLA KATIL", "JOIN WITH CODE"), () => _menu.UiJoinRoom(_roomInput.text), true);
            DuelUiFactory.Button("Back", lobbyContent, L("ANA MENÜYE DÖN", "RETURN TO MAIN MENU"), ReturnToMenu);

            _search = CreatePanel(safe, "Matchmaking", 540, 430);
            var searchContent = _search.transform.Find("Content");
            _searchText = DuelUiFactory.Text("Search State", searchContent, string.Empty, 25, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            _searchText.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
            DuelUiFactory.Button("Cancel", searchContent, L("ARAMAYI İPTAL ET", "CANCEL SEARCH"), _menu.UiCancelMatchmaking);

            _matchHud = DuelUiFactory.Rect("Match HUD", safe).gameObject;
            DuelUiFactory.Stretch((RectTransform)_matchHud.transform);
            var scoreboard = DuelUiFactory.Image("Scoreboard", _matchHud.transform, new Color(.025f, .065f, .095f, .94f));
            DuelUiFactory.SetRect(scoreboard.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-390, -132), new Vector2(390, -24));
            _score = DuelUiFactory.Text("Score", scoreboard.transform, string.Empty, 25, DuelPalette.Text, TextAnchor.UpperCenter, true);
            DuelUiFactory.SetRect(_score.rectTransform, new Vector2(0, .46f), Vector2.one, new Vector2(15, 0), new Vector2(-15, -8));
            _timer = DuelUiFactory.Text("Timer", scoreboard.transform, string.Empty, 28, DuelPalette.Gold, TextAnchor.LowerCenter, true);
            DuelUiFactory.SetRect(_timer.rectTransform, new Vector2(.4f, 0), new Vector2(.6f, .52f), Vector2.zero, Vector2.zero);
            _phase = DuelUiFactory.Text("Phase", scoreboard.transform, string.Empty, 13, DuelPalette.Cyan, TextAnchor.LowerLeft, true);
            DuelUiFactory.SetRect(_phase.rectTransform, new Vector2(.03f, 0), new Vector2(.4f, .48f), Vector2.zero, Vector2.zero);
            _room = DuelUiFactory.Text("Room", scoreboard.transform, string.Empty, 13, DuelPalette.Muted, TextAnchor.LowerRight, true);
            DuelUiFactory.SetRect(_room.rectTransform, new Vector2(.6f, 0), new Vector2(.97f, .48f), Vector2.zero, Vector2.zero);
            _cooldown = DuelUiFactory.Text("Attack Cooldown", _matchHud.transform, string.Empty, 17, DuelPalette.Cyan, TextAnchor.MiddleCenter, true);
            DuelUiFactory.SetRect(_cooldown.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-180, 102), new Vector2(180, 146));

            _readyGate = DuelUiFactory.Image("Ready Gate", _matchHud.transform, new Color(.015f, .055f, .08f, .96f)).gameObject;
            DuelUiFactory.SetRect((RectTransform)_readyGate.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-300, -170), new Vector2(300, 170));
            DuelUiFactory.Vertical(_readyGate.transform, 18, 28);
            var readyTitle = DuelUiFactory.Text("Ready Title", _readyGate.transform,
                L("DÜELLO HAZIRLIK KAPISI", "DUEL READY GATE", "DUELL-BEREITSCHAFT"),
                30, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            readyTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 82;
            _readyState = DuelUiFactory.Text("Ready State", _readyGate.transform, string.Empty,
                17, DuelPalette.Gold, TextAnchor.MiddleCenter, true);
            _readyState.gameObject.AddComponent<LayoutElement>().preferredHeight = 78;
            _ready = DuelUiFactory.Button("Ready", _readyGate.transform,
                L("HAZIR", "READY", "BEREIT"), _menu.UiReady, true, 68);

            var actionBar = DuelUiFactory.Rect("Actions", _matchHud.transform);
            DuelUiFactory.SetRect(actionBar, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-265, 26), new Vector2(265, 94));
            var actions = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            actions.spacing = 12; actions.childControlWidth = true; actions.childForceExpandWidth = true; actions.childControlHeight = true;
            _rematch = DuelUiFactory.Button("Rematch", actionBar, L("YENİDEN MAÇ", "REMATCH"), _menu.UiRematch, true, 64);
            _leave = DuelUiFactory.Button("Leave", actionBar, L("AYRIL", "LEAVE"), _menu.UiLeaveRoom, false, 64);

            _result = DuelUiFactory.Image("Result Glow", _matchHud.transform, new Color(.02f, .08f, .11f, .72f)).gameObject;
            DuelUiFactory.SetRect((RectTransform)_result.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-260, -60), new Vector2(260, 60));
            var resultText = DuelUiFactory.Text("Result Text", _result.transform, L("DÜELLO TAMAMLANDI", "DUEL COMPLETE"), 28, DuelPalette.Gold, TextAnchor.MiddleCenter, true);
            DuelUiFactory.Stretch(resultText.rectTransform);
            _result.SetActive(false);
            _readyGate.SetActive(false);
        }

        private static GameObject CreatePanel(Transform parent, string name, float width, float height)
        {
            var panel = DuelUiFactory.Image(name, parent, DuelPalette.Carbon);
            DuelUiFactory.SetRect(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-width / 2, -height / 2), new Vector2(width / 2, height / 2));
            var content = DuelUiFactory.Rect("Content", panel.transform);
            DuelUiFactory.Stretch(content, 34, 34, 34, 34);
            DuelUiFactory.Vertical(content, 12);
            return panel.gameObject;
        }

        private void AddTitle(Transform parent, string title, string subtitle)
        {
            var heading = DuelUiFactory.Text("Title", parent, title, 30, DuelPalette.Text, TextAnchor.MiddleLeft, true);
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            var supporting = DuelUiFactory.Text("Subtitle", parent, subtitle, 15, DuelPalette.Muted, TextAnchor.MiddleLeft);
            supporting.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        }

        private InputField CreateInput(Transform parent, string placeholder)
        {
            var panel = DuelUiFactory.Image("Room Code Input", parent, new Color32(7, 17, 27, 255));
            panel.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            var input = panel.gameObject.AddComponent<InputField>();
            var text = DuelUiFactory.Text("Text", panel.transform, string.Empty, 22, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            DuelUiFactory.Stretch(text.rectTransform, 18, 8, 18, 8);
            var hint = DuelUiFactory.Text("Placeholder", panel.transform, placeholder, 17, DuelPalette.Muted, TextAnchor.MiddleCenter, true);
            DuelUiFactory.Stretch(hint.rectTransform, 18, 8, 18, 8);
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = RoomCodeGenerator.CodeLength;
            input.contentType = InputField.ContentType.Alphanumeric;
            input.onValueChanged.AddListener(value =>
            {
                var sanitized = RoomCodeGenerator.SanitizeInput(value);
                if (!string.Equals(value, sanitized, System.StringComparison.Ordinal))
                    input.SetTextWithoutNotify(sanitized);
            });
            return input;
        }

        private void ReturnToMenu() => SceneManager.LoadScene(DuelFrontEndBootstrap.FrontEndScene);

        private string Phase(DuelMatchPhase phase) => phase switch
        {
            DuelMatchPhase.ReadyCheck => L("HAZIR KONTROLÜ", "READY CHECK"),
            DuelMatchPhase.Countdown => L("BAŞLATILIYOR", "INITIALIZING"),
            DuelMatchPhase.Playing => L("DÜELLO AKTİF", "DUEL ACTIVE"),
            DuelMatchPhase.ReconnectPause => L("BAĞLANTI BEKLENİYOR", "WAITING FOR CONNECTION"),
            DuelMatchPhase.Overtime => L("UZATMA", "OVERTIME"),
            DuelMatchPhase.Finished => L("SONUÇ", "RESULT"),
            _ => phase.ToString().ToUpperInvariant()
        };

        private string L(string tr, string en, string de = null) => DuelLocalization.Text(tr, en, de);
    }
}
