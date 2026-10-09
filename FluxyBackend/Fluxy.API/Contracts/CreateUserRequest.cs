namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What the create form sends to make an account in one request.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose, and the reason is the order of the checks: an attribute
    /// is validated by the framework *before* the action runs, so a body that failed one would
    /// be answered without ever reaching the antiforgery check. Every field here is checked in
    /// the action and then in the service, in the order the rest of this API checks things -
    /// CSRF first, rules second, database last - and the field names land in <c>errors</c>
    /// under the same camelCase spelling the body used.
    ///
    /// The password travels in clear text exactly once, from this body to the service that
    /// hashes it, and is never part of a response.
    /// </remarks>
    public sealed class CreateUserRequest
    {
        /// <summary>Login name the account gets. Trimmed, and compared case sensitively.</summary>
        public string? Username { get; init; }

        /// <summary>
        /// Address the account receives mail at. Trimmed and folded to lower case before
        /// anything is compared, so two spellings of one mailbox cannot become two accounts.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>Clear text password. Hashed inside the service, never returned.</summary>
        public string? Password { get; init; }

        /// <summary>
        /// Group the account joins, as a uuid. Required: an account with no group would be one
        /// whose level nobody could answer for, because the level is the group's role rather
        /// than a column on the account.
        /// </summary>
        public string? GroupId { get; init; }

        /// <summary>
        /// State the account starts in, as the name of the enum member. Required for the same
        /// reason - <c>Unregistered</c> and <c>Registered</c> are not the same account.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// IANA identifier of the display time zone, or null when the browser should decide.
        /// </summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Login guard to create the account with, or null to leave every list empty.
        /// </summary>
        public PatchLoginGuardRequest? LoginGuard { get; init; }
    }
}
