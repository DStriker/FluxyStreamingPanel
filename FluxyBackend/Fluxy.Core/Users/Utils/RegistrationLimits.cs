namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The lengths a set of registration values has to satisfy, in one place.
    /// </summary>
    /// <remarks>
    /// They live in Core because both sides of the wire read them: the API contracts turn them
    /// into cheap length checks that reject a malformed request before it reaches a service, and
    /// the services apply the same numbers as the authority, because a client is free to ignore
    /// whatever the server advertises. Keeping one definition stops the two from drifting into
    /// different limits.
    /// </remarks>
    public static class RegistrationLimits
    {
        /// <summary>Shortest accepted login name. The column allows 20 characters, the form wants fewer.</summary>
        public const int UsernameMinLength = 5;

        /// <summary>Longest accepted login name, matching the column width.</summary>
        public const int UsernameMaxLength = 20;

        /// <summary>Shortest accepted password.</summary>
        public const int PasswordMinLength = 8;

        /// <summary>Longest accepted password, well above the column width, which stores only a hash.</summary>
        public const int PasswordMaxLength = 100;

        /// <summary>Length of the longest address SMTP accepts, matching the column width.</summary>
        public const int EmailMaxLength = 254;
    }
}
