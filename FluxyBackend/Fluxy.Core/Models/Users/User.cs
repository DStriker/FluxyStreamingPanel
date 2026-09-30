namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Immutable business representation of a user account.
    /// </summary>
    /// <remarks>
    /// The record deliberately carries <see cref="PasswordHash"/> and
    /// <see cref="RegistrationCodeHash"/>, because they are part of the state of the account
    /// and the authentication code needs them. It is not a response contract: anything sent
    /// to a client has to be built from this model explicitly, without those two properties.
    /// </remarks>
    public sealed record User
    {
        /// <summary>Identifier of the account.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address of the owner. Unique, compared case sensitively.</summary>
        public required string Email { get; init; }

        /// <summary>BCrypt hash of the password. Never returned to a client.</summary>
        public required string PasswordHash { get; init; }

        /// <summary>Access level of the account.</summary>
        public UserRole Role { get; init; } = UserRole.Client;

        /// <summary>Current state of the account.</summary>
        public UserStatus Status { get; init; } = UserStatus.Unregistered;

        /// <summary>
        /// BCrypt hash of the one-time code sent to <see cref="Email"/> during registration,
        /// or null when no code is pending.
        /// </summary>
        public string? RegistrationCodeHash { get; init; }

        /// <summary>
        /// Moment the pending one-time code stops being accepted, or null when no code is
        /// pending. Issuing a new code replaces the previous one.
        /// </summary>
        public DateTimeOffset? RegistrationCodeExpiresAt { get; init; }

        /// <summary>
        /// Moment the account confirmed its email address, or null while it is
        /// <see cref="UserStatus.Unregistered"/>. It is a timestamp only: the state itself
        /// lives in <see cref="Status"/>, and blocking an account does not clear it.
        /// </summary>
        public DateTimeOffset? RegisteredAt { get; init; }

        /// <summary>Creation timestamp of the account.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the account.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}