namespace Fluxy.Core.Models.Users
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
    }
}
