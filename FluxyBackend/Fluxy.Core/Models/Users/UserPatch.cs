namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// What an admin changes about an existing account. Every property is optional, and an
    /// absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// That is what makes the same record serve both halves of the edit form and the three
    /// buttons on a table row: the block button sends <see cref="Status"/>, the form sends
    /// everything, and neither has to reconstruct the fields it is not touching - which is
    /// also what stops a request from clearing a value simply because it forgot to include it.
    ///
    /// Two values need their own rule because JSON cannot say "absent" and "null" apart once
    /// the body has been bound:
    ///
    /// - <see cref="Password"/>: null *or* an empty string means "leave the password alone".
    ///   Empty is not a password this system would accept anyway, so there is no state the
    ///   two spellings disagree about.
    /// - <see cref="TimeZone"/>: null means "leave it alone", an empty string means "clear it"
    ///   and go back to following the browser. The form always sends one of the two.
    /// </remarks>
    public sealed record UserPatch
    {
        /// <summary>New login name, or null to keep the one on the row.</summary>
        public string? Username { get; init; }

        /// <summary>New email address, or null to keep the one on the row.</summary>
        public string? Email { get; init; }

        /// <summary>New password in clear text, or null / empty to keep the current one.</summary>
        public string? Password { get; init; }

        /// <summary>New access level, or null to keep the current one.</summary>
        public UserRole? Role { get; init; }

        /// <summary>New state, or null to keep the current one. See the remarks above.</summary>
        public UserStatus? Status { get; init; }

        /// <summary>New display time zone, empty to clear it, null to keep the current one.</summary>
        public string? TimeZone { get; init; }

        /// <summary>
        /// Replacement login guard, or null to leave the switches and every list alone. The
        /// lists are replaced as a whole when they are named: a guard of which half the
        /// caller never speaks would be a guard nobody can read.
        /// </summary>
        public LoginGuardSettings? LoginGuard { get; init; }
    }
}
