using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.ReadModels
{
    /// <summary>
    /// One group in full: everything the edit form draws.
    /// </summary>
    public sealed record UserGroupDetail
    {
        /// <summary>Identifier of the group. Shown as static text, never as a payload field.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Access level every member holds.</summary>
        public required UserRole Role { get; init; }

        /// <summary>State of the group itself.</summary>
        public required UserStatus Status { get; init; }

        /// <summary>Everything this group allows.</summary>
        public required IReadOnlySet<UserPermission> Permissions { get; init; }

        /// <summary>Whether this group may only be renamed.</summary>
        public required bool IsBase { get; init; }

        /// <summary>How many accounts currently belong to this group.</summary>
        public required int Members { get; init; }

        /// <summary>When the row was created.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the row last changed.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
