using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of a group: which role its members inherit, which state it is
    /// in, and which permissions it grants. It is mutable, has no behaviour beyond mapping,
    /// and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The permissions are deliberately not a column on this row. A set that grows as the
    /// system grows has no fixed width, and a bitmask column would trade one row per
    /// permission for a value nobody can read with SQL - <c>WHERE permissions &amp; 2 = 2</c>
    /// answers "who may edit accounts" only if the reader already knows what 2 means. They
    /// live in <see cref="UserGroupPermissionEntity"/> instead, one row per grant, which the
    /// database can index and a join can count.
    /// </remarks>
    public sealed class UserGroupEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupEntity"/> class for a group
        /// that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public UserGroupEntity()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupEntity"/> class for one of
        /// the three groups whose identifier <see cref="BaseUserGroups"/> fixes, rather than
        /// for a group about to be given a fresh one.
        /// </summary>
        /// <remarks>
        /// The two timestamps are placeholders: both are overwritten for an inserted row by
        /// <c>FluxyDbContext.ApplyAudit</c>, which is the only place audit values are ever
        /// written. The base class asks for them because its other constructor exists to
        /// rehydrate a row that is already in the database.
        /// </remarks>
        /// <param name="id">The identifier the group must carry.</param>
        public UserGroupEntity(Guid id)
            : base(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Display name, compared case sensitively. The one field every group may change -
        /// including the three the installation cannot lose.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Access level every member of this group holds.</summary>
        public UserRole Role { get; set; } = UserRole.Client;

        /// <summary>
        /// Everything the group grants, one row per grant. Read on every authorized request
        /// and never included by accident: <c>Include(u =&gt; u.Group)</c> loads the group,
        /// not its permissions, which is why every load that needs them asks for them.
        /// </summary>
        public List<UserGroupPermissionEntity> Permissions { get; set; } = [];

        /// <summary>
        /// State of the group itself. A member's own state combines with this one; see
        /// <see cref="UserStatusComposition"/>.
        /// </summary>
        public UserStatus Status { get; set; } = UserStatus.Registered;
    }
}
