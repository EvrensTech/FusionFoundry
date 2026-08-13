using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuelProtocol.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DuelMatchHud : MonoBehaviour
    {
        private DuelMatchController _controller;
        private Text _score;
        private Text _timer;
        private Text _phase;
        private Text _objective;
        private Text _ability;
        private Text _onboarding;
        private Button _reducedMotionButton;
        private Button _rematchButton;
        private GameObject _pause;
        private GameObject _result;
        private Text _resultTitle;
        private Text _resultScore;
        private Text _settlementStatus;
        private int _lastScore;
        private bool _resultSoundPlayed;

        public void Configure(DuelMatchController controller)
        {
            _controller = controller;
            Build();
        }

        private void Update()
        {
            if (_controller?.Snapshot == null) return;
            var soloSettlement = FindAnyObjectByType<DuelSoloSettlementCoordinator>();
            if (_rematchButton != null)
            {
                _rematchButton.interactable = soloSettlement == null || !soloSettlement.IsPending;
            }
            if (_settlementStatus != null)
            {
                var settlementError = soloSettlement?.LastError;
                _settlementStatus.text = soloSettlement == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(settlementError)
                        ? $"UGS // {soloSettlement.LastStatus}"
                        : $"UGS // {soloSettlement.LastStatus} // {settlementError}";
                _settlementStatus.color = soloSettlement != null &&
                                          soloSettlement.LastStatus == "Settled"
                    ? DuelPalette.Success
                    : DuelPalette.Warning;
            }
            var snapshot = _controller.Snapshot;
            var totalScore = snapshot.PlayerOne.Score + snapshot.PlayerTwo.Score;
            if (totalScore > _lastScore) DuelGameAudio.Instance.PlayScore();
            _lastScore = totalScore;
            _score.text = $"CYAN  {snapshot.PlayerOne.Score}     —     {snapshot.PlayerTwo.Score}  CRIMSON";
            _timer.text = FormatTime(snapshot.RemainingSeconds);
            _phase.text = Phase(snapshot.Phase);
            if (snapshot.Core.OwnerIndex < 0)
                _objective.text = snapshot.Core.RespawnSeconds > 0
                    ? L($"ÇEKİRDEK {snapshot.Core.RespawnSeconds:0.0} SN İÇİNDE DÖNÜYOR", $"CORE RETURNS IN {snapshot.Core.RespawnSeconds:0.0} SEC", $"KERN KEHRT IN {snapshot.Core.RespawnSeconds:0.0} SEK ZURÜCK")
                    : L("ÇEKİRDEK BOŞTA // ELE GEÇİR", "CORE AVAILABLE // CAPTURE IT");
            else
            {
                var hold = snapshot.Core.OwnerIndex == 0 ? snapshot.PlayerOne.HoldSeconds : snapshot.PlayerTwo.HoldSeconds;
                _objective.text = L($"OYUNCU {snapshot.Core.OwnerIndex + 1} TAŞIYOR // {Mathf.Max(0, 15f - hold):0.0} SN",
                    $"PLAYER {snapshot.Core.OwnerIndex + 1} CARRYING // {Mathf.Max(0, 15f - hold):0.0} SEC",
                    $"SPIELER {snapshot.Core.OwnerIndex + 1} TRÄGT // {Mathf.Max(0, 15f - hold):0.0} SEK");
            }
            _ability.text = snapshot.PlayerOne.StunSeconds > 0
                ? L($"SİSTEM STUN // {snapshot.PlayerOne.StunSeconds:0.0} SN", $"SYSTEM STUN // {snapshot.PlayerOne.StunSeconds:0.0} SEC", $"SYSTEM BETÄUBT // {snapshot.PlayerOne.StunSeconds:0.0} SEK")
                : snapshot.PlayerOne.CooldownSeconds > 0
                    ? L($"DARBE YÜKLENİYOR // {snapshot.PlayerOne.CooldownSeconds:0.0}", $"PULSE RECHARGING // {snapshot.PlayerOne.CooldownSeconds:0.0}", $"IMPULS LÄDT // {snapshot.PlayerOne.CooldownSeconds:0.0}")
                    : L("DARBE HAZIR  [SPACE]", "PULSE READY  [SPACE]", "IMPULS BEREIT  [LEERTASTE]");
            var source = FindAnyObjectByType<LocalDuelCommandSource>();
            _onboarding.text = source != null && source.OnboardingStep != DuelOnboardingStep.Complete
                ? source.OnboardingHint : L("WASD HAREKET/YÖN  •  SPACE DARBE  •  ESC DURAKLAT", "WASD MOVE/DIRECTION  •  SPACE PULSE  •  ESC PAUSE", "WASD BEWEGEN/RICHTUNG  •  LEERTASTE IMPULS  •  ESC PAUSE");

            if (snapshot.Phase == DuelMatchPhase.Finished && !_result.activeSelf)
            {
                _result.SetActive(true);
                var won = snapshot.Result.HasValue && snapshot.Result.Value.WinnerIndex == 0;
                _resultTitle.text = won ? L("PROTOKOL TAMAMLANDI", "PROTOCOL COMPLETE") : L("SİNYAL KAYBEDİLDİ", "SIGNAL LOST");
                _resultTitle.color = won ? DuelPalette.Cyan : DuelPalette.Red;
                _resultScore.text = $"{snapshot.PlayerOne.Score}  —  {snapshot.PlayerTwo.Score}";
                Time.timeScale = 0f;
                if (!_resultSoundPlayed) { DuelGameAudio.Instance.PlayScore(); _resultSoundPlayed = true; }
            }

            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true && !_result.activeSelf)
                TogglePause();
        }

        private void Build()
        {
            var canvas = DuelUiFactory.CreateCanvas("Duel Match HUD", 30);
            var safe = DuelUiFactory.Rect("Safe Area", canvas.transform);
            DuelUiFactory.Stretch(safe);
            safe.gameObject.AddComponent<DuelSafeArea>();

            var top = DuelUiFactory.Image("Scoreboard", safe, new Color(0.025f, .065f, .095f, .92f));
            DuelUiFactory.SetRect(top.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-390, -132), new Vector2(390, -24));
            _score = DuelUiFactory.Text("Score", top.transform, string.Empty, 25, DuelPalette.Text, TextAnchor.UpperCenter, true);
            DuelUiFactory.SetRect(_score.rectTransform, new Vector2(0, .47f), new Vector2(1, 1), new Vector2(18, 0), new Vector2(-18, -8));
            _timer = DuelUiFactory.Text("Timer", top.transform, "03:00", 28, DuelPalette.Gold, TextAnchor.MiddleCenter, true);
            DuelUiFactory.SetRect(_timer.rectTransform, new Vector2(.38f, 0), new Vector2(.62f, .56f), Vector2.zero, Vector2.zero);
            _phase = DuelUiFactory.Text("Phase", top.transform, string.Empty, 12, DuelPalette.Muted, TextAnchor.MiddleLeft, true);
            DuelUiFactory.SetRect(_phase.rectTransform, new Vector2(.03f, 0), new Vector2(.38f, .48f), Vector2.zero, Vector2.zero);
            _objective = DuelUiFactory.Text("Objective", top.transform, string.Empty, 12, DuelPalette.Cyan, TextAnchor.MiddleRight, true);
            DuelUiFactory.SetRect(_objective.rectTransform, new Vector2(.62f, 0), new Vector2(.97f, .48f), Vector2.zero, Vector2.zero);

            var abilityPanel = DuelUiFactory.Image("Ability", safe, new Color(0.025f, .065f, .095f, .9f));
            DuelUiFactory.SetRect(abilityPanel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 28), new Vector2(390, 92));
            _ability = DuelUiFactory.Text("Ability State", abilityPanel.transform, string.Empty, 15, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            DuelUiFactory.Stretch(_ability.rectTransform, 12, 8, 12, 8);
            var pauseButton = DuelUiFactory.Button("Pause", safe, "Ⅱ", TogglePause, false, 54);
            DuelUiFactory.SetRect((RectTransform)pauseButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-92, -92), new Vector2(-26, -26));

            _onboarding = DuelUiFactory.Text("Controls", safe, string.Empty, 14, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            DuelUiFactory.SetRect(_onboarding.rectTransform, new Vector2(.22f, 0), new Vector2(.78f, 0), new Vector2(0, 28), new Vector2(0, 76));

            _pause = CreateModal(safe, L("SİSTEM DURAKLATILDI", "SYSTEM PAUSED"), out _);
            var pauseActions = _pause.transform.Find("Panel/Actions");
            DuelUiFactory.Button("Resume", pauseActions, L("DEVAM ET", "RESUME"), TogglePause, true);
            _reducedMotionButton = DuelUiFactory.Button("Settings", pauseActions, ReducedMotionLabel(), ToggleReducedMotion);
            DuelUiFactory.Button("Menu", pauseActions, L("ANA MENÜYE DÖN", "RETURN TO MENU"), ReturnToMenu);
            _pause.SetActive(false);

            _result = CreateModal(safe, string.Empty, out _resultTitle);
            _resultScore = DuelUiFactory.Text("Final Score", _result.transform.Find("Panel"), string.Empty, 52, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            _resultScore.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            _settlementStatus = DuelUiFactory.Text(
                "UGS Settlement",
                _result.transform.Find("Panel"),
                string.Empty,
                13,
                DuelPalette.Warning,
                TextAnchor.MiddleCenter,
                true);
            _settlementStatus.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            var resultActions = _result.transform.Find("Panel/Actions");
            _rematchButton = DuelUiFactory.Button("Rematch", resultActions, L("YENİDEN MAÇ", "REMATCH"), () => { Time.timeScale = 1f; _result.SetActive(false); _resultSoundPlayed = false; _lastScore = 0; _controller.StartNewMatch(); }, true);
            DuelUiFactory.Button("Menu", resultActions, L("ANA MENÜ", "MAIN MENU"), ReturnToMenu);
            _result.SetActive(false);
        }

        private GameObject CreateModal(Transform parent, string title, out Text titleText)
        {
            var shade = DuelUiFactory.Image("Modal", parent, new Color(0, 0, 0, .76f));
            DuelUiFactory.Stretch(shade.rectTransform);
            var panel = DuelUiFactory.Image("Panel", shade.transform, DuelPalette.Carbon);
            DuelUiFactory.SetRect(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-290, -225), new Vector2(290, 225));
            DuelUiFactory.Vertical(panel.transform, 16, 28);
            titleText = DuelUiFactory.Text("Title", panel.transform, title, 30, DuelPalette.Text, TextAnchor.MiddleCenter, true);
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            var actions = DuelUiFactory.Rect("Actions", panel.transform);
            actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 250;
            DuelUiFactory.Vertical(actions, 12);
            return shade.gameObject;
        }

        private void TogglePause()
        {
            var show = !_pause.activeSelf;
            _pause.SetActive(show);
            Time.timeScale = show ? 0f : 1f;
            DuelUiAudio.Instance.PlayBack();
        }

        private void ToggleReducedMotion()
        {
            var settings = DuelPlayerPreferences.Current;
            settings.ReducedMotion = !settings.ReducedMotion;
            DuelPlayerPreferences.Save(settings);
            if (_reducedMotionButton != null)
                _reducedMotionButton.GetComponentInChildren<Text>().text = ReducedMotionLabel();
        }

        private string ReducedMotionLabel()
        {
            var state = DuelPlayerPreferences.Current.ReducedMotion
                ? L("AÇIK", "ON", "AN")
                : L("KAPALI", "OFF", "AUS");
            return $"{L("HAREKETİ AZALT", "REDUCED MOTION")}: {state}";
        }

        private void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(DuelFrontEndBootstrap.FrontEndScene);
        }

        private string Phase(DuelMatchPhase phase) => phase switch
        {
            DuelMatchPhase.Countdown => L("BAŞLATILIYOR", "INITIALIZING"),
            DuelMatchPhase.Playing => L("DÜELLO AKTİF", "DUEL ACTIVE"),
            DuelMatchPhase.Overtime => L("UZATMA", "OVERTIME"),
            DuelMatchPhase.Finished => L("SONUÇ", "RESULT"),
            _ => phase.ToString().ToUpperInvariant()
        };

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{Mathf.FloorToInt(seconds / 60):00}:{Mathf.FloorToInt(seconds % 60):00}";
        }

        private string L(string tr, string en, string de = null) => DuelLocalization.Text(tr, en, de);
    }
}
