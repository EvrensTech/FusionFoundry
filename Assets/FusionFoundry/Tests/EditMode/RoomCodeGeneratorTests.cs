using FusionFoundry.Sessions;
using NUnit.Framework;

namespace FusionFoundry.Tests.Sessions
{
    public class RoomCodeGeneratorTests
    {
        [Test]
        public void Generate_AlwaysReturnsValidSixCharacterCode()
        {
            for (var iteration = 0; iteration < 256; iteration++)
            {
                var code = RoomCodeGenerator.Generate();

                Assert.That(code, Has.Length.EqualTo(RoomCodeGenerator.CodeLength));
                Assert.That(RoomCodeGenerator.IsValid(code), Is.True, code);
            }
        }

        [TestCase("ABCdef")]
        [TestCase("abcDEF")]
        [TestCase("012345")]
        [TestCase("O1Il0o")]
        [TestCase("89GHjk")]
        public void IsValid_AcceptsAllowedCharacters(string code)
        {
            Assert.That(RoomCodeGenerator.IsValid(code), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ABCDE")]
        [TestCase("ABCDEFG")]
        [TestCase("ABC-de")]
        [TestCase("ABC de")]
        [TestCase(" ABCde")]
        [TestCase("ABCde ")]
        [TestCase("ABCdéf")]
        public void IsValid_RejectsInvalidCode(string code)
        {
            Assert.That(RoomCodeGenerator.IsValid(code), Is.False);
        }

        [Test]
        public void SanitizeInput_PreservesCaseAndDigitsWithoutNormalizingSessionId()
        {
            Assert.That(RoomCodeGenerator.SanitizeInput("aB1-cD2 extra"), Is.EqualTo("aB1cD2"));
            Assert.That(RoomCodeGenerator.SanitizeInput("XyZ019"), Is.EqualTo("XyZ019"));
        }
    }
}
