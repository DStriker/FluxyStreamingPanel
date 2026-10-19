using System.Security.Cryptography;

namespace Fluxy.Application.Users.Utils
{
    /// <summary>
    /// Builds the one-time codes the three confirmation flows mail out: registration, profile
    /// changes and password resets.
    /// </summary>
    /// <remarks>
    /// One copy rather than three, because the property that matters is a property of the
    /// generator rather than of any flow: the digits come from
    /// <see cref="RandomNumberGenerator"/>, not from <see cref="Random.Shared"/> and not from a
    /// modulo of a random integer, both of which bias the result. A short code is only
    /// acceptable because the stored value is a hash and the endpoint that checks it is
    /// throttled.
    /// </remarks>
    public static class OneTimeCode
    {
        /// <summary>Digits a code is built from.</summary>
        private const string Alphabet = "0123456789";

        /// <summary>Builds a code of <paramref name="length"/> digits.</summary>
        public static string Generate(int length)
        {
            var digits = RandomNumberGenerator.GetItems(Alphabet.AsSpan(), length);

            return new string(digits);
        }
    }
}
