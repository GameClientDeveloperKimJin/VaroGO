using System.Text;
using Random = System.Random;

namespace VaroGO.Net
{
    /// <summary>6자리 세션 코드. 헷갈리는 문자 0 O 1 I L 제외(S-03).</summary>
    public static class SessionCode
    {
        public const int Length = 6;
        const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        static readonly Random rng = new Random();

        public static string Generate()
        {
            var sb = new StringBuilder(Length);
            for (int i = 0; i < Length; i++) sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>공백 제거 + 대문자. 형식이 맞지 않으면 false.</summary>
        public static bool TryNormalize(string input, out string code)
        {
            code = (input ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length != Length) return false;
            foreach (char c in code)
                if (Alphabet.IndexOf(c) < 0) return false;
            return true;
        }
    }
}
