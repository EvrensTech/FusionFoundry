using System.Linq;
using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelProtocol.Tests.EditMode
{
    public sealed class DuelInputAndCameraTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(DuelInputBindingStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(DuelOnboardingProgress.PlayerPrefsKey);
            DuelPlayerPreferences.Reset();
        }

        [Test]
        public void InputActionFactory_ProvidesKeyboardMouseAndGamepadContract()
        {
            var asset = DuelInputActionFactory.CreateRuntimeAsset();
            try
            {
                var map = asset.FindActionMap(DuelInputActionFactory.MapName, true);
                var move = map.FindAction(DuelInputActionFactory.MoveAction, true);
                var aim = map.FindAction(DuelInputActionFactory.AimAction, true);
                var ability = map.FindAction(DuelInputActionFactory.AbilityAction, true);
                var pointer = map.FindAction(DuelInputActionFactory.PointerPositionAction, true);

                Assert.That(move.expectedControlType, Is.EqualTo("Vector2"));
                Assert.That(aim.expectedControlType, Is.EqualTo("Vector2"));
                Assert.That(pointer.expectedControlType, Is.EqualTo("Vector2"));
                Assert.That(move.bindings.Any(binding => binding.path == "<Gamepad>/leftStick"), Is.True);
                Assert.That(aim.bindings.Any(binding => binding.path == "<Gamepad>/rightStick"), Is.True);
                Assert.That(ability.bindings.Any(binding => binding.path == "<Keyboard>/space"), Is.True);
                Assert.That(ability.bindings.Any(binding => binding.path == "<Mouse>/leftButton"), Is.True);
                Assert.That(ability.bindings.Any(binding => binding.path == "<Gamepad>/buttonSouth"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void BindingOverrides_RoundTripThroughPlayerProfileStorage()
        {
            var source = DuelInputActionFactory.CreateRuntimeAsset();
            var restored = DuelInputActionFactory.CreateRuntimeAsset();
            try
            {
                var action = source.FindAction(DuelInputActionFactory.AbilityAction, true);
                var keyboardBinding = action.bindings
                    .Select((binding, index) => new { binding, index })
                    .First(item => item.binding.path == "<Keyboard>/space");
                action.ApplyBindingOverride(keyboardBinding.index, "<Keyboard>/enter");
                DuelInputBindingStore.Save(source);
                DuelInputBindingStore.Load(restored);

                var restoredAction = restored.FindAction(DuelInputActionFactory.AbilityAction, true);
                Assert.That(
                    restoredAction.bindings[keyboardBinding.index].effectivePath,
                    Is.EqualTo("<Keyboard>/enter"));
            }
            finally
            {
                DuelInputBindingStore.Reset(source);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void TouchSticks_RespectSafeAreaSidesAndTopUiBand()
        {
            var safeArea = new Rect(100f, 50f, 1000f, 500f);
            var left = DuelTouchInput.ReadStick(new Vector2(200f, 190f), safeArea, true);
            var wrongSide = DuelTouchInput.ReadStick(new Vector2(900f, 190f), safeArea, true);
            var uiBand = DuelTouchInput.ReadStick(new Vector2(200f, 500f), safeArea, true);

            Assert.That(left.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(left.magnitude, Is.LessThanOrEqualTo(1f));
            Assert.That(wrongSide, Is.EqualTo(Vector2.zero));
            Assert.That(uiBand, Is.EqualTo(Vector2.zero));
        }

        [TestCase(16f / 9f, 10f)]
        [TestCase(4f / 3f, 10f)]
        [TestCase(9f / 16f, 17.777f)]
        public void CameraFraming_KeepsArenaVisibleAcrossTargetRatios(float aspect, float expected)
        {
            Assert.That(
                DuelArenaCameraRig.CalculateOrthographicSize(9f, aspect, 1f),
                Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void AccessibilitySettings_AreSanitizedAndPersisted()
        {
            var settings = new DuelAccessibilitySettings
            {
                ReducedMotion = true,
                HighContrast = true,
                HudScale = 9f,
                Language = SystemLanguage.Japanese
            };
            DuelPlayerPreferences.Save(settings);
            var restored = DuelPlayerPreferences.Load();

            Assert.That(restored.ReducedMotion, Is.True);
            Assert.That(restored.HighContrast, Is.True);
            Assert.That(restored.HudScale, Is.EqualTo(1.5f));
            Assert.That(restored.Language, Is.EqualTo(SystemLanguage.English));
        }

        [Test]
        public void Localization_SupportsAndPersistsEnglishTurkishAndGermanLocales()
        {
            Assert.That(DuelLocalization.SupportedLanguages,
                Is.EqualTo(new[] { SystemLanguage.English, SystemLanguage.Turkish, SystemLanguage.German }));
            Assert.That(DuelLocalization.GetLocaleIdentifier(SystemLanguage.English).Code, Is.EqualTo("en"));
            Assert.That(DuelLocalization.GetLocaleIdentifier(SystemLanguage.Turkish).Code, Is.EqualTo("tr"));
            Assert.That(DuelLocalization.GetLocaleIdentifier(SystemLanguage.German).Code, Is.EqualTo("de"));

            var settings = new DuelAccessibilitySettings { Language = SystemLanguage.German };
            DuelPlayerPreferences.Save(settings);

            Assert.That(DuelPlayerPreferences.Load().Language, Is.EqualTo(SystemLanguage.German));
            Assert.That(DuelLocalization.Text("OYNA", "PLAY"), Is.EqualTo("SPIELEN"));
            Assert.That(DuelLocalization.GetNativeName(SystemLanguage.German), Is.EqualTo("Deutsch"));
        }

        [Test]
        public void Onboarding_AdvancesFromMovementToCapturedCore()
        {
            var progress = new DuelOnboardingProgress(false);
            var match = new DuelMatchSimulation(DuelRuleSet.Default);
            match.Tick(DuelCommand.None, DuelCommand.None, 3.1f);

            var move = DuelCommand.None;
            move.Movement = Vector2.right;
            progress.Observe(move, match.Snapshot, 0);
            Assert.That(progress.CurrentStep, Is.EqualTo(DuelOnboardingStep.AimAndPulse));

            var pulse = DuelCommand.None;
            pulse.AbilityPressed = true;
            progress.Observe(pulse, match.Snapshot, 0);
            Assert.That(progress.CurrentStep, Is.EqualTo(DuelOnboardingStep.CaptureCore));

            match.Tick(move, DuelCommand.None, 0.8f);
            Assert.That(match.Snapshot.Core.OwnerIndex, Is.EqualTo(0));
            progress.Observe(DuelCommand.None, match.Snapshot, 0);
            Assert.That(progress.CurrentStep, Is.EqualTo(DuelOnboardingStep.Complete));
        }

        [Test]
        public void PerformanceSummary_UsesNearestRankP95FrameTime()
        {
            var frames = Enumerable.Repeat(0.010f, 95)
                .Concat(Enumerable.Repeat(0.050f, 5))
                .ToArray();

            Assert.That(
                DuelPerformanceSummary.Percentile95Milliseconds(frames),
                Is.EqualTo(10f).Within(0.001f));
        }
    }
}
