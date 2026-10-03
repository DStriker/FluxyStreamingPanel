using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of a change that has been requested and is waiting for its code
    /// to be confirmed. It has no behaviour beyond mapping, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The row holds everything needed to finish the operation without asking the visitor
    /// anything a second time: which operation it is, what it will write, and the hash of the
    /// code that authorizes it. Nothing sensitive is stored in clear text - the new password
    /// arrives already hashed, and the code is hashed the same way the registration code is.
    ///
    /// One row per account, enforced by a unique index rather than by convention, so a second
    /// request replaces the first instead of leaving two codes that would both work.
    /// </remarks>
    public sealed class PendingChangeEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PendingChangeEntity"/> class for a row
        /// that is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public PendingChangeEntity()
        {
        }

        /// <summary>Account this change belongs to.</summary>
        public Guid UserId { get; set; }

        /// <summary>Which operation this row is waiting to finish.</summary>
        public PendingChangeKind Kind { get; set; }

        /// <summary>
        /// The username or email the change will write, when that is what it is about. Null for
        /// the two password operations, which have nothing to stage but a hash.
        /// </summary>
        public string? TargetValue { get; set; }

        /// <summary>
        /// BCrypt hash of the requested new password, present only for the two password
        /// operations. Clear text never reaches this table.
        /// </summary>
        public string? NewPasswordHash { get; set; }

        /// <summary>BCrypt hash of the one-time code that authorizes the change.</summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>Moment the pending code stops being accepted.</summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
