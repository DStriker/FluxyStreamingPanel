namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The four facts about an account that the profile page shows.
    /// </summary>
    /// <remarks>
    /// A projection of <see cref="User"/> rather than the record itself, and deliberately so:
    /// the business model carries <c>PasswordHash</c> and <c>RegistrationCodeHash</c>, and a
    /// profile endpoint that handed back the model would leak them the first time somebody
    /// pointed a JSON serializer at it. Nothing here is a secret, which is exactly why the
    /// shape is stated separately instead of relying on a serializer being configured kindly.
    ///
    /// <see cref="Permissions"/> is the one field that is not "about the account" so much as
    /// about the group it points at, and it is here rather than in an endpoint of its own
    /// because the question it answers - which buttons may this visitor see - is asked on
    /// every page they open, and asking it in two round trips would be a second round trip on
    /// every navigation. It is deliberately <i>not</i> on <c>GET /auth/me</c>, which the guest
    /// guard polls on every navigation for a yes-or-no answer.
    /// </remarks>
    public sealed record AccountProfile
    {
        /// <summary>Login name of the account, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address the account receives mail at.</summary>
        public required string Email { get; init; }

        /// <summary>Access level of the account.</summary>
        public required UserRole Role { get; init; }

        /// <summary>
        /// IANA identifier of the chosen display time zone, or null when the visitor has not
        /// chosen one. Not required, so a profile always renders even before one exists.
        /// </summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Everything the account's group allows it to do right now. Empty for a client or a
        /// reseller, which hold no permission of their own today.
        /// </summary>
        /// <remarks>
        /// Read from the row on every request rather than copied into the token, for the reason
        /// <see cref="Fluxy.Core.Models.Authentication.AccountStanding"/> gives: a grant
        /// withdrawn a second ago has to be gone now, and a copy is a statement about the moment
        /// it was taken. What a browser does with it is draw, not decide - the server refuses
        /// regardless, so a client that ignored this field entirely would see fewer buttons and
        /// no more access.
        /// </remarks>
        public IReadOnlySet<UserPermission> Permissions { get; init; } = new HashSet<UserPermission>();
    }
}
