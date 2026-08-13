using System.Security.Cryptography;
using System.Text;

namespace FusionFoundry.Sessions
{
    public static class RoomCodeGenerator
    {
        public const int CodeLength = 6;

        private const string AllowedCharacters =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public static string Generate()
        {
            var code = new char[CodeLength];
            var randomByte = new byte[1];
            var exclusiveUpperBound = 256 - (256 % AllowedCharacters.Length);

            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                for (var index = 0; index < code.Length; index++)
                {
                    do
                    {
                        randomNumberGenerator.GetBytes(randomByte);
                    }
                    while (randomByte[0] >= exclusiveUpperBound);

                    code[index] = AllowedCharacters[randomByte[0] % AllowedCharacters.Length];
                }
            }

            return new string(code);
        }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != CodeLength)
            {
                return false;
            }

            for (var index = 0; index < code.Length; index++)
            {
                if (AllowedCharacters.IndexOf(code[index]) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Preserves Photon session-name casing while limiting pasted or typed input to six
        /// ASCII alphanumeric characters. Invalid characters are discarded, never normalized.
        /// </summary>
        public static string SanitizeInput(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var result = new StringBuilder(CodeLength);
            foreach (var character in value)
            {
                if (!IsAllowed(character)) continue;
                result.Append(character);
                if (result.Length == CodeLength) break;
            }
            return result.ToString();
        }

        private static bool IsAllowed(char character)
        {
            return character >= 'A' && character <= 'Z' ||
                   character >= 'a' && character <= 'z' ||
                   character >= '0' && character <= '9';
        }
    }
}
