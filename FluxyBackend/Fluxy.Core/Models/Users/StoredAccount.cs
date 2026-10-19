namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The smallest account shape a registration or reset flow decides from.
    /// </summary>
    /// <remarks>
    /// These flows ask about an account by a value that is not its identifier - a login name or
    /// an address - and need its identifier afterward to stage the write. The shape names only
    /// the values those decisions read, and <see cref="RowStatus"/> is the row's own state
    /// rather than the effective one, because both flows refuse an account that is not
    /// registered on its own row.
    /// </remarks>
    public sealed record StoredAccount
    {
        /// <summary>Identifier of the account.</summary>
        public required Guid Id { get; init; }

        /// <summary>Login name, compared case sensitively.</summary>
        public required string Username { get; init; }

        /// <summary>Email address the account receives mail at.</summary>
        public required string Email { get; init; }

        /// <summary>State of the account's own row, before its group is combined in.</summary>
        public required UserStatus RowStatus { get; init; }
    }
}
