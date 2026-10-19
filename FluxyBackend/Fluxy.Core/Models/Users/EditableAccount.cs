namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// An account read for a change, with both halves of its status kept apart.
    /// </summary>
    /// <remarks>
    /// <see cref="Account"/> carries the effective status, because that is what every read of
    /// an account answers with. A change, however, sometimes has to test the row's own status:
    /// registration confirms an <see cref="UserStatus.Unregistered"/> row, and a password reset
    /// refuses anything that is not registered on its own row. Keeping the two values beside
    /// each other lets the service ask the question the flow has always asked without the
    /// repository hiding which one it returned.
    /// </remarks>
    public sealed record EditableAccount
    {
        /// <summary>The account as every other read returns it, with its effective status.</summary>
        public required User Account { get; init; }

        /// <summary>State of the account's own row, before the group's state is combined in.</summary>
        public required UserStatus RowStatus { get; init; }

        /// <summary>State of the account's group, before it is combined with the row's.</summary>
        public required UserStatus GroupStatus { get; init; }
    }
}
