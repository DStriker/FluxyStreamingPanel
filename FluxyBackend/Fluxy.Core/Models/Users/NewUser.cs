namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Everything an admin supplies to create an account in one request.
    /// </summary>
    /// <remarks>
    /// Shaped as one record rather than five parameters because the form posts it as one body,
    /// and shaped as required properties because the form cannot create an account without
    /// them: a username, an address, a password, a level and a state are what a row *is*.
    ///
    /// The password travels in clear text exactly once, from the form to the service that
    /// hashes it, and is never stored on this type after that.
    /// </remarks>
    public sealed record NewUser
    {
        /// <summary>Login name the account gets. Trimmed, and compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>
        /// Address the account receives mail at. Trimmed and folded to lower case before
        /// anything is compared, so two spellings of one mailbox cannot become two accounts.
        /// </summary>
        public required string Email { get; init; }

        /// <summary>Clear text password. Hashed inside the service, never returned.</summary>
        public required string Password { get; init; }

        /// <summary>Access level the account starts at.</summary>
        public required UserRole Role { get; init; }

        /// <summary>
        /// State the account starts in. Anything but <see cref="UserStatus.Unregistered"/>
        /// stamps <c>RegisteredAt</c> - the account is being handed over already activated.
        /// </summary>
        public required UserStatus Status { get; init; }

        /// <summary>
        /// IANA identifier of the display time zone, or null when the browser should decide.
        /// </summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Login guard to create the account with, or null to leave every list empty.
        /// </summary>
        public LoginGuardSettings? LoginGuard { get; init; }
    }
}
