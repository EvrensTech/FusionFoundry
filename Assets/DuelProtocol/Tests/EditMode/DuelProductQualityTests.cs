using System.Linq;
using DuelProtocol.Content;
using DuelProtocol.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DuelProtocol.Tests.EditMode
{
    public sealed class DuelProductQualityTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(DuelCosmeticCatalog.EquippedSkinKey);
            DuelPlayerPreferences.Reset();
        }

        [Test]
        public void CosmeticCatalog_HasSixDistinctEquippableSkins()
        {
            Assert.That(DuelCosmeticCatalog.Skins.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(DuelCosmeticCatalog.Skins.Select(skin => skin.Id).Distinct().Count(),
                Is.EqualTo(DuelCosmeticCatalog.Skins.Length));
            foreach (var skin in DuelCosmeticCatalog.Skins)
            {
                DuelCosmeticCatalog.EquippedId = skin.Id;
                Assert.That(DuelCosmeticCatalog.Equipped.Id, Is.EqualTo(skin.Id));
            }
        }

        [Test]
        public void ProductAssets_IncludeOriginalKeyArtFontsAndUiAudio()
        {
            Assert.That(Resources.Load<Texture2D>("ProductUI/Images/duel-title-background-v1"), Is.Not.Null);
            Assert.That(Resources.Load<Font>("ProductUI/Fonts/Inter-Regular"), Is.Not.Null);
            Assert.That(Resources.Load<Font>("ProductUI/Fonts/InterDisplay-Bold"), Is.Not.Null);
            Assert.That(Resources.Load<AudioClip>("ProductUI/Audio/click_001"), Is.Not.Null);
            Assert.That(Resources.Load<AudioClip>("ProductUI/Audio/confirmation_001"), Is.Not.Null);
        }

        [Test]
        public void AudioAccessibilityValues_AreClampedAndPersisted()
        {
            var settings = new DuelAccessibilitySettings
            {
                MasterVolume = 4f,
                MusicVolume = -2f,
                SfxVolume = .42f,
                UiVolume = 8f
            };
            DuelPlayerPreferences.Save(settings);
            var restored = DuelPlayerPreferences.Load();
            Assert.That(restored.MasterVolume, Is.EqualTo(1f));
            Assert.That(restored.MusicVolume, Is.EqualTo(0f));
            Assert.That(restored.SfxVolume, Is.EqualTo(.42f));
            Assert.That(restored.UiVolume, Is.EqualTo(1f));
        }

        [Test]
        public void FrontEndRoutes_AreStableBuildSceneNames()
        {
            Assert.That(DuelFrontEndBootstrap.FrontEndScene, Is.EqualTo("DuelFrontEnd"));
            Assert.That(DuelFrontEndBootstrap.SoloScene, Is.EqualTo("DuelProtocol"));
            Assert.That(DuelFrontEndBootstrap.OnlineScene, Is.EqualTo("DuelProtocolNetwork"));
        }
    }
}
