using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Models
{
    /// <summary>
    /// One staged change waiting for its one-time code, as the data layer reads it.
    /// </summary>
    /// <remarks>
    /// The record carries the hashes rather than clear values for the same reason the row does:
    /// the new password and the code are stored hashed, and nothing above the repository has a
    /// use for an unhashed copy. An absent property means the row does not carry it, exactly as
    /// the columns are nullable.
    /// </remarks>
    public sealed record PendingChange
    {
        /// <summary>Identifier of the staged row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account this change belongs to. One row per account.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Which operation this row is waiting to finish.</summary>
        public required PendingChangeKind Kind { get; init; }

        /// <summary>The username or email the change will write, when it is about one.</summary>
        public string? TargetValue { get; init; }

        /// <summary>BCrypt hash of the requested new password, for the password operations.</summary>
        public string? NewPasswordHash { get; init; }

        /// <summary>BCrypt hash of the one-time code that authorizes the change.</summary>
        public required string CodeHash { get; init; }

        /// <summary>Staged content that does not fit the columns, such as a login guard.</summary>
        public string? Payload { get; init; }

        /// <summary>Moment the pending code stops being accepted.</summary>
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}
