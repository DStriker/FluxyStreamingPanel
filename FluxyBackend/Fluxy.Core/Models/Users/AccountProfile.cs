namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The three facts about an account that the profile page shows.
    /// </summary>
    /// <remarks>
    /// A projection of <see cref="User"/> rather than the record itself, and deliberately so:
    /// the business model carries <c>PasswordHash</c> and <c>RegistrationCodeHash</c>, and a
    /// profile endpoint that handed back the model would leak them the first time somebody
    /// pointed a JSON serializer at it. Nothing here is a secret, which is exactly why the
    /// shape is stated separately instead of relying on a serializer being configured kindly.
    /// </remarks>
    public sealed record AccountProfile
    {
        /// <summary>Login name of the account, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address the account receives mail at.</summary>
        public required string Email { get; init; }

        /// <summary>Access level of the account.</summary>
        public required UserRole Role { get; init; }
    }
}
