using System.Collections;
using System.Linq;
using DuelProtocol.Gameplay;
using DuelProtocol.Match;
using DuelProtocol.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DuelProtocol.Tests
{
    public sealed class DuelDemoPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            DuelPlayerPreferences.Reset();
            PlayerPrefs.DeleteKey(DuelOnboardingProgress.PlayerPrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            DuelPlayerPreferences.Reset();
            PlayerPrefs.DeleteKey(DuelOnboardingProgress.PlayerPrefsKey);
        }

        [UnityTest]
        public IEnumerator Bootstrap_CreatesPlayableArenaAndMatch()
        {
            var root = new GameObject("Duel Test Bootstrap");
            root.AddComponent<DuelDemoBootstrap>();
            yield return null;

            var controller = Object.FindAnyObjectByType<DuelMatchController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Snapshot, Is.Not.Null);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(DuelMatchPhase.Countdown));
            Assert.That(GameObject.Find("Energy Core"), Is.Not.Null);
            var arenaRenderer = GameObject.Find("Arena").GetComponent<Renderer>();
            Assert.That(arenaRenderer.sharedMaterial.shader.name,
                Does.Contain("Universal Render Pipeline/Lit"));
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.orthographic, Is.False);
            Assert.That(Camera.main.GetComponent<DuelArenaCameraRig>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DuelMatchHud>(), Is.Not.Null);
            Assert.That(GameObject.Find("Player One").transform.Find("Robot Rig"), Is.Not.Null);
            Assert.That(GameObject.Find("Energy Core").transform.Find("Core Art"), Is.Not.Null);
            Assert.That(GameObject.Find("Production Art"), Is.Not.Null);

            Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rematch_UsesCleanSimulationAndNewMatchId()
        {
            var root = new GameObject("Duel Test Bootstrap");
            root.AddComponent<DuelDemoBootstrap>();
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelMatchController>();
            var firstMatchId = controller.Snapshot.MatchId;

            controller.StartNewMatch();

            Assert.That(controller.Snapshot.MatchId, Is.Not.EqualTo(firstMatchId));
            Assert.That(controller.Snapshot.PlayerOne.Score, Is.Zero);
            Assert.That(controller.Snapshot.PlayerTwo.Score, Is.Zero);
            Assert.That(controller.Snapshot.Core.OwnerIndex, Is.EqualTo(-1));

            Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatFeedback_CreatesPooledAttackHitAndStunParticles()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Feedback Test Player";
            var feedback = player.AddComponent<DuelCombatFeedback>();
            feedback.ResetPresentation();

            feedback.Present(1, 1, true, Vector2.right);
            yield return null;

            var particles = player.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(particles.Length, Is.EqualTo(7));
            Assert.That(System.Array.Exists(particles, system => system.isPlaying), Is.True);

            feedback.SetStunned(false);
            Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoreFeedback_HidesDuringRespawn_AndReturnsWithParticleBurst()
        {
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Feedback Test Core";
            var renderer = core.GetComponent<Renderer>();
            var collider = core.GetComponent<Collider>();
            var feedback = core.AddComponent<DuelCoreFeedback>();

            feedback.SetRespawning(true, false);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);

            feedback.SetRespawning(false);
            yield return null;

            Assert.That(renderer.enabled, Is.True);
            Assert.That(collider.enabled, Is.True);
            Assert.That(System.Array.Exists(
                core.GetComponentsInChildren<ParticleSystem>(true),
                system => system.isPlaying), Is.True);

            Object.Destroy(core);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CarrierTimer_ShowsRemainingSeconds_AndCanBeHidden()
        {
            var player = new GameObject("Timer Test Player");
            var timer = player.AddComponent<DuelCarrierTimerDisplay>();

            timer.SetRemaining(12.34f, true);
            yield return null;

            Assert.That(timer.IsVisible, Is.True);
            Assert.That(timer.DisplayText, Is.EqualTo("CORE 12.3s"));

            timer.SetRemaining(0f, false);
            Assert.That(timer.IsVisible, Is.False);

            Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrontEnd_PrimaryButtonNavigatesToModeAndArenaSelection()
        {
            var root = new GameObject("Front End Test Bootstrap");
            root.AddComponent<DuelFrontEndBootstrap>();
            yield return null;

            var playButton = GameObject.Find("Play")?.GetComponent<Button>();
            Assert.That(playButton, Is.Not.Null);
            playButton.onClick.Invoke();
            yield return null;

            Assert.That(GameObject.Find("play"), Is.Not.Null);
            Assert.That(GameObject.Find("Deploy"), Is.Not.Null);
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.Not.Null);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.Not.Null);

            var shell = GameObject.Find("Duel Product Shell");
            if (shell != null) Object.Destroy(shell);
            var camera = GameObject.Find("Front End Camera");
            if (camera != null) Object.Destroy(camera);
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrontEnd_SettingsUseSlidersAndThreeLanguageDropdown()
        {
            var root = new GameObject("Front End Settings Test Bootstrap");
            root.AddComponent<DuelFrontEndBootstrap>();
            yield return null;

            GameObject.Find("Settings").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var settingsPage = GameObject.Find("settings");
            Assert.That(settingsPage, Is.Not.Null);
            Assert.That(settingsPage.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(5));
            var language = settingsPage.GetComponentInChildren<Dropdown>(true);
            Assert.That(language, Is.Not.Null);
            Assert.That(language.options.Select(option => option.text),
                Is.EqualTo(new[] { "English", "Türkçe", "Deutsch" }));

            language.Show();
            yield return null;
            var dropdownList = GameObject.Find("Dropdown List");
            Assert.That(dropdownList, Is.Not.Null, "Dil dropdown'u tıklanınca seçenek listesi açılmalı.");
            Assert.That(dropdownList.GetComponentsInChildren<Toggle>(false).Length, Is.EqualTo(3));
            Assert.That(dropdownList.GetComponent<Canvas>().sortingOrder,
                Is.GreaterThan(language.GetComponentInParent<Canvas>().sortingOrder),
                "Dropdown seçenekleri ana UI canvas'ının önünde çizilmeli.");
            language.Hide();

            var master = GameObject.Find("Master Volume").GetComponent<Slider>();
            master.value = .25f;
            Assert.That(DuelPlayerPreferences.Current.MasterVolume, Is.EqualTo(.25f).Within(.001f));

            DestroyFrontEnd(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrontEnd_ModeAndArenaButtonsExposeSelectedState()
        {
            var root = new GameObject("Front End Selection Test Bootstrap");
            root.AddComponent<DuelFrontEndBootstrap>();
            yield return null;

            GameObject.Find("Play").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Online").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Wide Arena").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.That(GameObject.Find("Online").GetComponentInChildren<Text>().text, Does.StartWith("✓"));
            Assert.That(GameObject.Find("Wide Arena").GetComponentInChildren<Text>().text, Does.StartWith("✓"));
            Assert.That(GameObject.Find("Solo").GetComponentInChildren<Text>().text, Does.Not.StartWith("✓"));
            Assert.That(GameObject.Find("Selection Summary").GetComponent<Text>().text, Does.Contain("ORBITAL FOUNDRY"));

            DestroyFrontEnd(root);
            yield return null;
        }

        private static void DestroyFrontEnd(GameObject root)
        {
            var shell = GameObject.Find("Duel Product Shell");
            if (shell != null) Object.Destroy(shell);
            var camera = GameObject.Find("Front End Camera");
            if (camera != null) Object.Destroy(camera);
            Object.Destroy(root);
        }

        private static void Cleanup()
        {
            var generatedNames = new[]
            {
                "Duel Test Bootstrap", "Arena", "Arena Boundary", "Player One", "AI Rival",
                "Energy Core", "Directional Light", "Main Camera", "Duel Match HUD",
                "Duel UI Audio", "EventSystem", "Production Art"
            };
            foreach (var instance in Object.FindObjectsByType<GameObject>())
            {
                if (System.Array.IndexOf(generatedNames, instance.name) >= 0)
                {
                    Object.Destroy(instance);
                }
            }
        }
    }
}
