using System;
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
