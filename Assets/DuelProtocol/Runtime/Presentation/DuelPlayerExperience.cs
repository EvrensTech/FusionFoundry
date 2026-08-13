using System;
using System.Threading.Tasks;
using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelProtocol.Presentation
{
    [Serializable]
    public sealed class DuelAccessibilitySettings
    {
        public bool ReducedMotion;
        public bool HighContrast;
        [Range(0.8f, 1.5f)] public float HudScale = 1f;
        [Range(0f, 1f)] public float MasterVolume = 0.9f;
        [Range(0f, 1f)] public float MusicVolume = 0.65f;
        [Range(0f, 1f)] public float SfxVolume = 0.85f;
        [Range(0f, 1f)] public float UiVolume = 0.8f;
        public SystemLanguage Language = SystemLanguage.English;

        public void Sanitize()
        {
            HudScale = Mathf.Clamp(HudScale, 0.8f, 1.5f);
            MasterVolume = Mathf.Clamp01(MasterVolume);
            MusicVolume = Mathf.Clamp01(MusicVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
            UiVolume = Mathf.Clamp01(UiVolume);
            if (Language != SystemLanguage.Turkish &&
                Language != SystemLanguage.English &&
                Language != SystemLanguage.German)
            {
                Language = SystemLanguage.English;
            }
        }
    }

    public static class DuelPlayerPreferences
    {
        public const string PlayerPrefsKey = "duel.accessibility.v1";
        private static DuelAccessibilitySettings s_Current;

        public static DuelAccessibilitySettings Current => s_Current ??= Load();

        public static DuelAccessibilitySettings Load()
        {
            DuelAccessibilitySettings settings = null;
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    settings = JsonUtility.FromJson<DuelAccessibilitySettings>(
                        PlayerPrefs.GetString(PlayerPrefsKey));
                }
                catch (ArgumentException)
                {
                    PlayerPrefs.DeleteKey(PlayerPrefsKey);
                }
            }
            settings ??= new DuelAccessibilitySettings();
            settings.Sanitize();
            DuelLocalization.SyncUnityLocale(settings.Language);
            return settings;
        }

        public static void Save(DuelAccessibilitySettings settings)
        {
            s_Current = settings ?? new DuelAccessibilitySettings();
            s_Current.Sanitize();
            DuelLocalization.SyncUnityLocale(s_Current.Language);
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(s_Current));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            s_Current = new DuelAccessibilitySettings();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    public static class DuelProfilePreferencesSync
    {
        public static DuelPlayerPreferencesData Capture(InputActionAsset inputActions)
        {
            var local = DuelPlayerPreferences.Current;
            return new DuelPlayerPreferencesData
            {
                InputBindingsJson = inputActions?.SaveBindingOverridesAsJson() ?? string.Empty,
                ReducedMotion = local.ReducedMotion,
                HighContrast = local.HighContrast,
                HudScale = local.HudScale
            };
        }

        public static void Apply(
            DuelPlayerPreferencesData cloud,
            InputActionAsset inputActions)
        {
            if (cloud == null)
            {
                return;
            }
            if (inputActions != null)
            {
                inputActions.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(cloud.InputBindingsJson))
                {
                    try
                    {
                        inputActions.LoadBindingOverridesFromJson(cloud.InputBindingsJson);
                    }
                    catch (Exception)
                    {
                        inputActions.RemoveAllBindingOverrides();
                    }
                }
                DuelInputBindingStore.Save(inputActions);
            }
            DuelPlayerPreferences.Save(new DuelAccessibilitySettings
            {
                ReducedMotion = cloud.ReducedMotion,
                HighContrast = cloud.HighContrast,
                HudScale = cloud.HudScale,
                MasterVolume = DuelPlayerPreferences.Current.MasterVolume,
                MusicVolume = DuelPlayerPreferences.Current.MusicVolume,
                SfxVolume = DuelPlayerPreferences.Current.SfxVolume,
                UiVolume = DuelPlayerPreferences.Current.UiVolume,
                Language = DuelPlayerPreferences.Current.Language
            });
        }

        public static async Task DownloadAsync(
            IDuelPlayerPreferencesService service,
            InputActionAsset inputActions)
        {
            if (service == null) return;
            Apply(await service.GetPreferencesAsync(), inputActions);
        }

        public static Task UploadAsync(
            IDuelPlayerPreferencesService service,
            InputActionAsset inputActions)
        {
            return service?.SavePreferencesAsync(Capture(inputActions)) ?? Task.CompletedTask;
        }
    }

    public enum DuelOnboardingStep
    {
        Move,
        AimAndPulse,
        CaptureCore,
        Complete
    }

    public sealed class DuelOnboardingProgress
    {
        public const string PlayerPrefsKey = "duel.onboarding.step.v1";
        private readonly bool _persist;

        public DuelOnboardingProgress(bool persist = true)
        {
            _persist = persist;
            CurrentStep = persist
                ? (DuelOnboardingStep)Mathf.Clamp(
                    PlayerPrefs.GetInt(PlayerPrefsKey, 0),
                    0,
                    (int)DuelOnboardingStep.Complete)
                : DuelOnboardingStep.Move;
        }

        public DuelOnboardingStep CurrentStep { get; private set; }

        public string Hint => CurrentStep switch
        {
            DuelOnboardingStep.Move => DuelLocalization.Text(
                "HAREKET: WASD, joystick veya sol analog",
                "MOVE: WASD, joystick or left stick",
                "BEWEGEN: WASD, Joystick oder linker Stick"),
            DuelOnboardingStep.AimAndPulse => DuelLocalization.Text(
                "HAREKET YÖNÜNE DARBE: Space, gamepad tuşu veya ATAK",
                "PULSE IN MOVE DIRECTION: Space, gamepad button or ATTACK",
                "IMPULS IN BEWEGUNGSRICHTUNG: Leertaste, Gamepad oder ANGRIFF"),
            DuelOnboardingStep.CaptureCore => DuelLocalization.Text(
                "ELE GEÇİR: çekirdeği al ve 15 saniye tut",
                "CAPTURE: take the core and hold it for 15 seconds"),
            _ => string.Empty
        };

        public void Observe(DuelCommand command, DuelMatchSnapshot snapshot, int playerIndex)
        {
            if (CurrentStep == DuelOnboardingStep.Move && command.Movement.sqrMagnitude > 0.04f)
            {
                Advance(DuelOnboardingStep.AimAndPulse);
            }
            if (CurrentStep == DuelOnboardingStep.AimAndPulse && command.AbilityPressed)
            {
                Advance(DuelOnboardingStep.CaptureCore);
            }
            if (CurrentStep == DuelOnboardingStep.CaptureCore && snapshot != null)
            {
                var player = snapshot.GetPlayer(playerIndex);
                if (snapshot.Core.OwnerIndex == playerIndex || player.Score > 0)
                {
                    Advance(DuelOnboardingStep.Complete);
                }
            }
        }

        public void Reset()
        {
            CurrentStep = DuelOnboardingStep.Move;
            if (_persist)
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
                PlayerPrefs.Save();
            }
        }

        private void Advance(DuelOnboardingStep step)
        {
            if (step <= CurrentStep)
            {
                return;
            }
            CurrentStep = step;
            if (_persist)
            {
                PlayerPrefs.SetInt(PlayerPrefsKey, (int)CurrentStep);
                PlayerPrefs.Save();
            }
        }
    }
}
