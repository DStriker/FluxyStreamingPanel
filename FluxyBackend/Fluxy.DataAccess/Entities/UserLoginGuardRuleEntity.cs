using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One entry of one account's login guard allow lists. It is mutable, has no behaviour
    /// beyond mapping, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// One row is one allowed value rather than one list, because the three lists have
    /// different limits - five networks, one country, one provider - and a row per value is
    /// what lets the database enforce "no duplicates" with a unique index instead of the
    /// application remembering to check. The switches the lists hang under live on the
    /// account row itself (<see cref="UserEntity.GeoProtectionEnabled"/>).
    /// </remarks>
    public sealed class UserLoginGuardRuleEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserLoginGuardRuleEntity"/> class for a
        /// rule that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public UserLoginGuardRuleEntity()
        {
        }

        /// <summary>Account this rule belongs to. Deleted with the account.</summary>
        public Guid UserId { get; set; }

        /// <summary>Which allow list this rule belongs to.</summary>
        public LoginGuardRuleKind Kind { get; set; }

        /// <summary>
        /// The allowed value in canonical form: a normalized address or CIDR, an upper case
        /// ISO 3166-1 alpha-2 code, or an autonomous system number as decimal text.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}
