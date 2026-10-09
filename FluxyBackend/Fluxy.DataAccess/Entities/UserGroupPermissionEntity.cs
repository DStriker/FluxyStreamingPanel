using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One permission one group grants. It is mutable, has no behaviour beyond mapping, and
    /// knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// One row is one grant rather than one set, for the reason a login guard rule is a row
    /// rather than a list: the database can enforce "no duplicates" with a unique index
    /// instead of the application remembering to check, and a permission can be counted,
    /// searched and listed with plain SQL. The set a group grants is the set of rows it has.
    /// </remarks>
    public sealed class UserGroupPermissionEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupPermissionEntity"/> class for
        /// a grant that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public UserGroupPermissionEntity()
        {
        }

        /// <summary>Group this grant belongs to. Deleted with the group.</summary>
        public Guid UserGroupId { get; set; }

        /// <summary>
        /// What the group allows. Stored as the numeric value of the flag, so adding a member
        /// to the enum adds a value nobody has used yet and renumbering the existing ones
        /// would silently reinterpret rows already written.
        /// </summary>
        public UserPermission Permission { get; set; } = UserPermission.None;
    }
}
