namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Which operation an admin asked to run over a set of groups.
    /// </summary>
    /// <remarks>
    /// Deliberately a different type from <see cref="BulkUserAction"/> rather than the same
    /// one, because only two of its five members would apply and the other three would be
    /// names an account operation answers with and a group cannot. Two enums make "this
    /// action was sent to the wrong endpoint" a compile error instead of a runtime surprise.
    ///
    /// Block and Unblock are members here even though the single-group path spells them
    /// differently: a group has no dedicated status endpoint, it has a <c>PATCH</c> that sets
    /// the state, and the row button sends <c>Blocked</c> or <c>Registered</c>. This enum
    /// names what the operator meant; the service translates it into that patch, so the
    /// wording a button carries never has to know which field carries it.
    /// </remarks>
    public enum BulkUserGroupAction
    {
        /// <summary>Put every named group into Blocked, withdrawing the standing of its members.</summary>
        Block,

        /// <summary>Take every named group out of Blocked, back to Registered.</summary>
        Unblock,

        /// <summary>Delete every named group and the permission rows it holds.</summary>
        Delete
    }
}
