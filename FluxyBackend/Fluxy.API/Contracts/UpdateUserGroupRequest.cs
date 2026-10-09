namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What the edit form sends to change a group. Every property is optional, and an absent
    /// one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// That is what makes one body serve the rename the row-level menu offers as well as the
    /// whole form, and it is also what stops a request from clearing a value simply because it
    /// forgot to include it. The one case where absence is not enough is spelled out beside
    /// <see cref="Permissions"/>: an empty list clears every grant, a missing one changes none.
    ///
    /// Two rules the service refuses rather than this body: a base group accepts
    /// <see cref="Name"/> and nothing else, and a role change re-checks every permission the
    /// group still holds. Both are refused with their own code so a client can explain the
    /// difference rather than showing "invalid input" for something the form was right to try.
    /// </remarks>
    public sealed class UpdateUserGroupRequest
    {
        /// <summary>New display name, or null to keep the current one.</summary>
        public string? Name { get; init; }

        /// <summary>New access level, as the name of the enum member, or null to keep it.</summary>
        public string? Role { get; init; }

        /// <summary>New state, as the name of the enum member, or null to keep the current one.</summary>
        public string? Status { get; init; }

        /// <summary>
        /// The whole set of permissions the group will grant, sent whole because a set of which
        /// half the caller never speaks is a set nobody can read. An empty list clears every
        /// grant; an absent one leaves them untouched.
        /// </summary>
        public IReadOnlyList<string>? Permissions { get; init; }
    }
}
