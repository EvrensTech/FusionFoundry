using System;
using System.Linq;
using System.Threading.Tasks;
using DuelProtocol.Gameplay;
using DuelProtocol.Presentation;
using DuelProtocol.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuelProtocol.Tests.EditMode
{
    public sealed class DuelOnlinePresentationTests
    {
        [Test]
        public async Task IdentityFailure_ProducesReadableStateWithoutEscapingException()
        {
            var identity = new FakeIdentityService { Failure = new InvalidOperationException("offline") };
            var session = new DuelIdentitySession(identity);

            await session.InitializeAnonymousAsync();

            Assert.That(session.State, Is.EqualTo(DuelIdentityState.Error));
            Assert.That(session.ErrorCode, Is.EqualTo(nameof(InvalidOperationException)));
            Assert.That(session.ErrorMessage, Is.EqualTo("offline"));
        }

        [Test]
        public async Task IdentitySuccess_ReusesAnonymousPlayerId()
        {
            var identity = new FakeIdentityService();
            var session = new DuelIdentitySession(identity);

            await session.InitializeAnonymousAsync();
            var firstId = session.PlayerId;
            await session.InitializeAnonymousAsync();

            Assert.That(session.State, Is.EqualTo(DuelIdentityState.SignedIn));
            Assert.That(session.PlayerId, Is.EqualTo(firstId));
            Assert.That(identity.AnonymousSignInCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ProfilePanel_LoadsProgressionAndLeaderboardInOfflineFallback()
        {
            var root = new GameObject("Profile Panel Test");
            try
            {
                var panel = root.AddComponent<DuelProfilePanel>();
                panel.Configure(new InMemoryDuelBackend("profile-player"), true);

                await panel.RefreshAsync();

                Assert.That(panel.State, Is.EqualTo(DuelProfilePanelState.Offline));
                Assert.That(panel.Profile.PlayerId, Is.EqualTo("profile-player"));
                Assert.That(panel.Profile.Rating, Is.EqualTo(1000));
                Assert.That(panel.Leaderboard, Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task ProfilePreferences_RoundTripBindingsAndAccessibilityThroughService()
        {
            var backend = new InMemoryDuelBackend("profile-player");
            var source = DuelInputActionFactory.CreateRuntimeAsset();
            var restored = DuelInputActionFactory.CreateRuntimeAsset();
            try
            {
                var action = source.FindAction(DuelInputActionFactory.AbilityAction, true);
                var bindingIndex = action.bindings
                    .Select((binding, index) => new { binding, index })
                    .First(item => item.binding.path == "<Keyboard>/space").index;
                action.ApplyBindingOverride(bindingIndex, "<Keyboard>/enter");
                DuelPlayerPreferences.Save(new DuelAccessibilitySettings
                {
                    ReducedMotion = true,
                    HighContrast = true,
                    HudScale = 1.4f
                });

                await DuelProfilePreferencesSync.UploadAsync(backend, source);
                DuelPlayerPreferences.Reset();
                await DuelProfilePreferencesSync.DownloadAsync(backend, restored);

                Assert.That(
                    restored.FindAction(DuelInputActionFactory.AbilityAction, true)
                        .bindings[bindingIndex].effectivePath,
                    Is.EqualTo("<Keyboard>/enter"));
                Assert.That(DuelPlayerPreferences.Current.ReducedMotion, Is.True);
                Assert.That(DuelPlayerPreferences.Current.HighContrast, Is.True);
                Assert.That(DuelPlayerPreferences.Current.HudScale, Is.EqualTo(1.4f));
            }
            finally
            {
                DuelPlayerPreferences.Reset();
                DuelInputBindingStore.Reset(source);
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        private sealed class FakeIdentityService : IDuelIdentityService
        {
            public Exception Failure;
            public int AnonymousSignInCount;
            public bool IsSignedIn { get; private set; }
            public string PlayerId { get; private set; } = string.Empty;

            public Task InitializeAsync(string environmentName)
            {
                return Failure == null ? Task.CompletedTask : Task.FromException(Failure);
            }

            public Task SignInAnonymouslyAsync()
            {
                if (IsSignedIn) return Task.CompletedTask;
                AnonymousSignInCount++;
                IsSignedIn = true;
                PlayerId = "anonymous-player";
                return Task.CompletedTask;
            }

            public Task SignInWithUsernamePasswordAsync(string username, string password)
            {
                IsSignedIn = true;
                PlayerId = username;
                return Task.CompletedTask;
            }

            public Task UpgradeAnonymousAccountAsync(string username, string password)
            {
                PlayerId = username;
                return Task.CompletedTask;
            }

            public void SignOut()
            {
                IsSignedIn = false;
            }
        }
    }
}
