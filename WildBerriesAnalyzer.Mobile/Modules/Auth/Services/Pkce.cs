using System.Security.Cryptography;
using System.Text;

namespace WildBerriesAnalyzer.Modules.Auth.Services
{
    public static class Pkce
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

        public static string GenerateCodeVerifier(int length = 64)
        {
            length = Math.Clamp(length, 43, 128);
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            }

            return new string(chars);
        }

        public static string GenerateState(int length = 32) => GenerateCodeVerifier(length);

        public static string CreateCodeChallengeS256(string codeVerifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        public static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }
}
