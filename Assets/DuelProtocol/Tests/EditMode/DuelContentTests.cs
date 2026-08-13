using System.Threading.Tasks;
using DuelProtocol.Content;
using DuelProtocol.Services;
using NUnit.Framework;
using UnityEngine;

namespace DuelProtocol.Tests
{
    public sealed class DuelContentTests
    {
        [TearDown]
        public void TearDown()
        {
            DuelRewardedAds.Register(null);
            DuelAdRewardWallet.Reset();
        }

        [Test]
        public void MissingLocalizationKey_HasVisibleFallback()
        {
            var catalog = ScriptableObject.CreateInstance<DuelContentCatalog>();

            Assert.That(catalog.Localize("missing.key", SystemLanguage.English),
                Is.EqualTo("[missing.key]"));
            Assert.That(catalog.MissingLocalizationKeys, Contains.Item("missing.key"));

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void DataCatalog_AddsArenaSkinAbilityAndRulesWithoutMatchCodeChanges()
        {
            var catalog = ScriptableObject.CreateInstance<DuelContentCatalog>();
            catalog.InitializeDefaults();

            Assert.That(catalog.TryGetArena("energy-core-arena-wide", out var arena), Is.True);
            Assert.That(arena.Bounds, Is.EqualTo(new Vector2(20f, 14f)));
            Assert.That(catalog.TryGetSkin("default-runner", out _), Is.True);
            Assert.That(catalog.TryGetAbility("pulse", out var ability), Is.True);
            Assert.That(ability.CooldownSeconds, Is.EqualTo(3f));
            Assert.That(catalog.TryGetMatchRules("standard", out var rules), Is.True);
            Assert.That(rules.ScoreLimit, Is.EqualTo(3));
            Assert.That(
                catalog.Localize("arena.energy_core_wide", SystemLanguage.Turkish),
                Is.EqualTo("Geniş Enerji Arenası"));
            Assert.That(
                catalog.Localize("arena.energy_core_wide", SystemLanguage.German),
                Is.EqualTo("Weite Energie-Arena"));

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public async Task MissingOptionalProviders_KeepCoreFlowAvailable()
        {
            var services = new DuelOptionalServices();

            Assert.That(services.Economy.IsAvailable, Is.False);
            Assert.That(services.RewardedAds.IsAvailable, Is.False);
            Assert.That(services.Seasons.IsAvailable, Is.False);
            Assert.That(await services.Economy.GetSoftCurrencyBalanceAsync(), Is.Zero);
            Assert.That(await services.RewardedAds.ShowRewardedAsync("post-match"), Is.False);
            Assert.That(await services.Seasons.GetActiveSeasonIdAsync(), Is.Empty);
        }

        [Test]
        public async Task RewardedAdProvider_IsReplaceableAndWalletGrantsOnlyConfirmedReward()
        {
            var provider = new ConfirmedRewardedAdService();
            DuelRewardedAds.Register(provider);

            Assert.That(DuelRewardedAds.Current.IsAvailable, Is.True);
            Assert.That(await DuelRewardedAds.Current.ShowRewardedAsync("store_bonus"), Is.True);
            Assert.That(provider.LastPlacement, Is.EqualTo("store_bonus"));
            Assert.That(DuelAdRewardWallet.GrantCore(100), Is.EqualTo(100));
            Assert.That(DuelAdRewardWallet.Balance, Is.EqualTo(100));
        }

        private sealed class ConfirmedRewardedAdService : IDuelRewardedAdService
        {
            public bool IsAvailable => true;
            public string LastPlacement { get; private set; }

            public Task<bool> ShowRewardedAsync(string placementId)
            {
                LastPlacement = placementId;
                return Task.FromResult(true);
            }
        }
    }
}
