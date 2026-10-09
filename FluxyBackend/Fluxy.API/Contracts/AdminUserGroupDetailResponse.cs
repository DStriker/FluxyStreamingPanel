namespace Fluxy.API.Contracts
{
    /// <summary>
    /// One group in full, for the edit form.
    /// </summary>
    /// <remarks>
    /// The identifier is a string rather than a value the form could submit: it is shown as
    /// static text and travels back in the <c>PATCH</c> body's path, never as a field of the
    /// body - a form that posted its own id as data would be one edit away from writing it.
    ///
    /// <see cref="Members"/> is a count of accounts that name this group, not a list of them.
    /// It is what makes the delete button able to explain itself before it is pressed rather
    /// than refusing afterwards, and the accounts that make it up belong to the page that
    /// already exists for them.
    ///
    /// The permissions are the individual keys the group grants, each spelled exactly as the
    /// permission catalog spells it, and every one of them belongs to <see cref="Role"/>: a
    /// grant that means nothing for the group's level is dropped on the way in, so this list
    /// can never carry one.
    /// </remarks>
    public sealed record AdminUserGroupDetailResponse
    {
        /// <summary>Identifier of the group, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member inherits, as the name of the enum member.</summary>
        public required string Role { get; init; }

        /// <summary>State of the group itself, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>Every permission this group grants, in the catalog's own order.</summary>
        public required IReadOnlyList<string> Permissions { get; init; }

        /// <summary>Whether this group may only be renamed.</summary>
        public required bool IsBase { get; init; }

        /// <summary>How many accounts belong to this group right now.</summary>
        public required int Members { get; init; }

        /// <summary>When the row was created.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the row was last changed.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
