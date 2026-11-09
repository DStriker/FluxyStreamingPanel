namespace Fluxy.Core.Users.Commands
{
    /// <summary>
    /// The three things a visitor supplies to ask for a registration. It is the input of the
    /// registration use case and deliberately carries nothing that the database already knows,
    /// so the service is free to normalize it before anything is looked up.
    /// </summary>
    public sealed record NewRegistration
    {
        /// <summary>Login name the visitor wants, as typed.</summary>
        public required string Username { get; init; }

        /// <summary>Email address the visitor wants, as typed.</summary>
        public required string Email { get; init; }

        /// <summary>Clear text password the visitor wants. It is hashed inside the service.</summary>
        public required string Password { get; init; }

        /// <summary>Address the request came from, as the server saw it, or null.</summary>
        /// <remarks>
        /// Carried so the audit row the service writes records the network it came from. It is
        /// transport input, not a registration rule: the service never decides anything from it.
        /// </remarks>
        public string? ClientAddress { get; init; }

        /// <summary>User agent the request presented, or null. Recorded on the audit row.</summary>
        public string? UserAgent { get; init; }
    }
}
