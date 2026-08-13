using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using FusionFoundry.Sessions;
using NUnit.Framework;

namespace FusionFoundry.Tests.Sessions
{
    public class FusionSessionRequestTests
    {
        [Test]
        public void ForHost_CreatesHostRequest()
        {
            var request = FusionSessionRequest.ForHost("AbC234", 4);

            Assert.That(request.Mode, Is.EqualTo(GameMode.Host));
            Assert.That(request.SessionName, Is.EqualTo("AbC234"));
            Assert.That(request.MaxPlayers, Is.EqualTo(4));
            Assert.That(request.IsVisible, Is.False);
            Assert.That(request.IsOpen, Is.True);
            Assert.That(request.ReserveReconnectSlot, Is.True);
        }

        [Test]
        public void ForMatchmakingHost_PublishesDefensivelyCopiedProperties()
        {
            var properties = new Dictionary<string, SessionProperty>
            {
                ["mode"] = 2,
                ["build"] = "release-1"
            };

            var request = FusionSessionRequest.ForMatchmakingHost("AbC234", 2, properties);
            properties["mode"] = 3;
            var returned = (Dictionary<string, SessionProperty>)request.SessionProperties;
            returned["mode"] = 4;

            Assert.That(request.Mode, Is.EqualTo(GameMode.Host));
            Assert.That(request.IsVisible, Is.True);
            Assert.That(request.IsOpen, Is.True);
            Assert.That(request.ReserveReconnectSlot, Is.False);
            Assert.That((int)request.SessionProperties["mode"], Is.EqualTo(2));
        }

        [Test]
        public void ForClient_CreatesClientRequestWithoutMaxPlayers()
        {
            var request = FusionSessionRequest.ForClient("AbC234");

            Assert.That(request.Mode, Is.EqualTo(GameMode.Client));
            Assert.That(request.SessionName, Is.EqualTo("AbC234"));
            Assert.That(request.MaxPlayers, Is.Null);
        }

        [Test]
        public void ForClient_WithReconnectCredentials_PreservesIdentityAndDefensivelyCopiesToken()
        {
            var token = new byte[16];
            token[0] = 42;

            var request = FusionSessionRequest.ForClient("AbC234", 123456L, token);
            token[0] = 7;
            var returnedToken = request.ConnectionToken;
            returnedToken[0] = 9;

            Assert.That(request.PlayerUniqueId, Is.EqualTo(123456L));
            Assert.That(request.ConnectionToken[0], Is.EqualTo(42));
        }

        [TestCase(0L)]
        public void ForClient_RejectsMissingReconnectIdentity(long playerUniqueId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => FusionSessionRequest.ForClient("AbC234", playerUniqueId, new byte[16]));
        }

        [Test]
        public void ForClient_RejectsShortReconnectToken()
        {
            Assert.Throws<ArgumentException>(
                () => FusionSessionRequest.ForClient("AbC234", 123L, new byte[15]));
        }

        [Test]
        public void Factories_PreserveSessionNameCase()
        {
            var hostRequest = FusionSessionRequest.ForHost("AbCdEf", 2);
            var clientRequest = FusionSessionRequest.ForClient("aBcDeF");

            Assert.That(hostRequest.SessionName, Is.EqualTo("AbCdEf"));
            Assert.That(clientRequest.SessionName, Is.EqualTo("aBcDeF"));
            Assert.That(hostRequest.SessionName, Is.Not.EqualTo(clientRequest.SessionName));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ForHost_RejectsMissingSessionName(string sessionName)
        {
            Assert.Throws<ArgumentException>(
                () => FusionSessionRequest.ForHost(sessionName, 4));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ForClient_RejectsMissingSessionName(string sessionName)
        {
            Assert.Throws<ArgumentException>(
                () => FusionSessionRequest.ForClient(sessionName));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ForHost_RejectsNonPositiveMaxPlayers(int maxPlayers)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => FusionSessionRequest.ForHost("AbC234", maxPlayers));
        }

        [Test]
        public void Contract_HasNoPublicSettersOrConstructors()
        {
            var publicProperties = typeof(FusionSessionRequest).GetProperties();

            Assert.That(publicProperties, Is.Not.Empty);
            Assert.That(publicProperties.All(property => !property.CanWrite), Is.True);
            Assert.That(typeof(FusionSessionRequest).GetConstructors(), Is.Empty);
        }
    }
}
