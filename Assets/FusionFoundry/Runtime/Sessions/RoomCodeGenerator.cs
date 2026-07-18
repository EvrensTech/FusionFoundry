using System.Security.Cryptography;

namespace FusionFoundry.Sessions
{
    public static class RoomCodeGenerator
    {
        public const int CodeLength = 6;

        private const string AllowedCharacters =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

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
    }
}
