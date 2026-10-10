namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// What reading one account came back with: the account itself, or the word for why there
    /// is nothing to show.
    /// </summary>
    /// <remarks>
    /// A read has two ways of coming back empty and they are not the same answer. There may be
    /// no account with that identifier, and there may be an account whose role this operator's
    /// group is not allowed to read. Both arrive here as <see cref="Detail"/> equal to null,
    /// and <see cref="Action"/> says which - the transport turns each into its own status
    /// rather than the service learning what a status is.
    ///
    /// The two-refusal shape exists because the read sits behind the same two-layer gate as
    /// every write: the coarse policy settles that this caller may read some accounts at all,
    /// and this type is where the question of *which* accounts is answered.
    /// </remarks>
    public sealed record AdminUserReadResult
    {
        /// <summary>The account, when it is there and this operator may read it.</summary>
        public AdminUserDetail? Detail { get; init; }

        /// <summary>
        /// Why <see cref="Detail"/> is null. Carries no meaning beside a non-null detail, and
        /// left at its default so the one case that has an answer cannot forget to state it.
        /// </summary>
        public AdminUserAction Action { get; init; } = AdminUserAction.NotFound;

        /// <summary>The account, read successfully.</summary>
        /// <param name="detail">What the row holds.</param>
        /// <returns>A result carrying it.</returns>
        public static AdminUserReadResult Found(AdminUserDetail detail) =>
            new() { Detail = detail };

        /// <summary>Nothing to show, for the reason <paramref name="action"/> names.</summary>
        /// <param name="action">
        /// <see cref="AdminUserAction.NotFound"/> or <see cref="AdminUserAction.PermissionDenied"/>.
        /// </param>
        /// <returns>A result carrying the refusal.</returns>
        public static AdminUserReadResult Refused(AdminUserAction action) =>
            new() { Action = action };
    }
}
