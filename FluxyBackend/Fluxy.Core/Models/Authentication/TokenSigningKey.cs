using System.Text;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// Turns the configured secret into the key an access token is signed and verified with.
    /// </summary>
    /// <remarks>
    /// It lives in the model layer and is called from both halves that need it - the service
    /// that mints a token and the scheme that validates one - because those two have to agree
    /// on the key exactly. A second copy of the parsing would be a place for them to drift, and
    /// the drift would not look like a configuration error: it would look like a stream of
    /// 401s on every request, indistinguishable from a clock problem or from an expired token.
    ///
    /// It throws rather than substituting a default. A key that is missing is not a weaker
    /// installation, it is a broken one, and an installation that signs tokens nobody can
    /// verify - or that falls back to some guessable value - is worse than one that refuses to
    /// start.
    /// </remarks>
    public static class TokenSigningKey
    {
        /// <summary>
        /// Shortest key HMAC-SHA256 can be used with safely.
        /// </summary>
        /// <remarks>
        /// 32 bytes is the size of the hash itself and the shortest key that is not weaker than
        /// it. The framework will happily sign with anything, including a one-character key, so
        /// the floor is this class's job to enforce rather than something a caller can forget.
        /// </remarks>
        public const int MinimumByteCount = 32;

        /// <summary>
        /// Builds signing material from a configured secret.
        /// </summary>
        /// <param name="configuredSecret">
        /// The secret as configured. Read as hexadecimal when it is hexadecimal, and as UTF-8
        /// bytes otherwise.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// The secret is missing, or is shorter than <see cref="MinimumByteCount"/> once decoded.
        /// </exception>
        /// <returns>The bytes to sign with.</returns>
        /// <remarks>
        /// Hexadecimal is recognised because that is what the secrets script generates, and
        /// reading 32 hex characters as hex is 16 bytes of entropy while reading the same text
        /// as UTF-8 is 32 bytes of low-entropy ASCII. A secret that is not hex is still usable -
        /// someone may have supplied arbitrary text deliberately - so this falls back instead of
        /// refusing, and the length check below is what actually guards it.
        /// </remarks>
        public static byte[] Materialize(string? configuredSecret)
        {
            if (string.IsNullOrWhiteSpace(configuredSecret))
            {
                throw new InvalidOperationException(
                    "The token signing key is not configured, so no token can be signed or " +
                    "verified. Provide it as JWT_SIGNING_KEY in the compose .env file " +
                    "(scripts\\init-secrets.ps1 generates it), or through user-secrets.");
            }

            var material = FromHex(configuredSecret) ?? Encoding.UTF8.GetBytes(configuredSecret);

            if (material.Length < MinimumByteCount)
            {
                throw new InvalidOperationException(
                    $"The configured token signing key is {material.Length} bytes long. " +
                    $"HMAC-SHA256 needs at least {MinimumByteCount} for the key to be " +
                    "unguessable, and a shorter one lets a token be forged by anyone willing " +
                    "to try a few obvious values. Regenerate it with " +
                    "scripts\\init-secrets.ps1 -Force.");
            }

            return material;
        }

        /// <summary>
        /// Decodes a run of hexadecimal pairs, or answers null when the text is not hexadecimal.
        /// </summary>
        /// <remarks>
        /// An odd number of characters cannot be hexadecimal, and anything that is not a hex
        /// digit makes the decoder reject the whole string. Both are ordinary states rather than
        /// failures, and both are answered by "not hex" so the caller can use its fallback.
        /// </remarks>
        private static byte[]? FromHex(string value)
        {
            if (value.Length % 2 != 0)
            {
                return null;
            }

            try
            {
                return Convert.FromHexString(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}