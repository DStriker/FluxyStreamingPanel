namespace Fluxy.Core.Authentication.Utils
{
    /// <summary>
    /// Names of the claims this project writes into and reads out of an access token.
    /// </summary>
    /// <remarks>
    /// They are constants in one place rather than string literals scattered over the two halves
    /// of the code, because a claim written under one spelling and read under another is not a
    /// token that fails - it is a token whose payload silently comes back empty, and the
    /// authorization decision that depends on it is made against a default rather than against
    /// what was signed.
    ///
    /// The names are the registered JWT ones where the specification has them, which is what
    /// keeps a token readable by anything else that understands JWT. Nothing here is a
    /// secret: the claims are only as trustworthy as the signature over them, and the signature
    /// is what is verified.
    /// </remarks>
    public static class TokenClaimTypes
    {
        /// <summary>
        /// Identifier of the account. The standard <c>sub</c>, and the only claim authorization
        /// treats as an identity on its own.
        /// </summary>
        public const string Subject = "sub";

        /// <summary>Login name of the account, for display.</summary>
        public const string Name = "name";

        /// <summary>
        /// Role of the account, carried as the numeric value of <see cref="Fluxy.Core.Users.Enums.UserRole"/>.
        /// </summary>
        public const string Role = "role";

        /// <summary>
        /// Session the token belongs to, as a GUID. It is what lets a sign-out find the whole
        /// refresh chain a token came from, and it is the value a withdrawal is remembered by:
        /// one session identifier covers every access token the session ever held, including
        /// those minted by earlier rotations.
        /// </summary>
        public const string Session = "sid";

        /// <summary>
        /// Identifier of the token itself, the standard <c>jti</c>. It identifies the token
        /// rather than the session and is not what revocation is checked against - the session
        /// identifier is, so that ending a session does not have to name every token.
        /// </summary>
        public const string TokenId = "jti";

        /// <summary>Moment the token was issued, the standard <c>iat</c>.</summary>
        public const string IssuedAt = "iat";

        /// <summary>
        /// Moment the token stops being accepted, the standard <c>exp</c>.
        /// </summary>
        public const string ExpiresAt = "exp";
    }
}
