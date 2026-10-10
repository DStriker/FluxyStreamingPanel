namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Which operation an admin asked to run over a set of accounts.
    /// </summary>
    /// <remarks>
    /// The members name the acts the per-account endpoints already perform rather than a
    /// second vocabulary for them: a client that knows <c>POST /admin/users/{id}/block</c>
    /// knows this too, and the service that carries them out is the same one. Adding an
    /// operation here therefore means adding it to <c>UserAdminService</c>'s switch as well -
    /// the switch is exhaustive and an unhandled member is a compile error, not a silent
    /// no-op.
    ///
    /// Values are fixed and must not be renumbered. The member is what a request spells and
    /// what a log line reads; the number is what a column would store, and a bulk operation
    /// is never stored.
    /// </remarks>
    public enum BulkUserAction
    {
        /// <summary>Put every named account into Blocked and end the sessions it holds.</summary>
        Block,

        /// <summary>
        /// Take every named account out of Blocked, back to the state it was blocked from.
        /// </summary>
        Unblock,

        /// <summary>
        /// Delete every named account, along with everything that references it.
        /// </summary>
        Delete,

        /// <summary>
        /// Move every named account out of Unregistered and into Registered, stamping the
        /// moment it happened.
        /// </summary>
        ConfirmRegistration,

        /// <summary>
        /// Move every named account into the group the operation carries, changing its level,
        /// its permissions and what it is allowed to do at all.
        /// </summary>
        AssignGroup
    }
}
