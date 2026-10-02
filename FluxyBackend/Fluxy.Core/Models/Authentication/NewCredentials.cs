using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// What a sign-in form submitted, together with the area it was submitted to.
    /// </summary>
    /// <remarks>
    /// <see cref="RequiredRole"/> is part of the request rather than a property of the caller,
    /// because the endpoint is what fixes it. Each sign-in form is meant for one audience, and
    /// folding the expected role into the input is what lets a single service enforce that
    /// without the caller having to remember to check it.
    /// </remarks>
    public sealed record NewCredentials
    {
        /// <summary>Login name as typed. Trimmed, and compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>
        /// Clear text password as typed. It is verified against a hash and never stored, logged
        /// or echoed back.
        /// </summary>
        public required string Password { get; init; }

        /// <summary>
        /// Role the endpoint accepts. The account has to hold exactly this role: an operator
        /// signing in at the reseller form is refused there and has to use the operator form.
        /// </summary>
        public required UserRole RequiredRole { get; init; }

        /// <summary>
        /// Address the request came from, recorded on the session so an operator can tell
        /// where a session was opened from.
        /// </summary>
        public string? ClientAddress { get; init; }

        /// <summary>User agent the request came with, recorded on the session for the same reason.</summary>
        public string? UserAgent { get; init; }
    }
}
