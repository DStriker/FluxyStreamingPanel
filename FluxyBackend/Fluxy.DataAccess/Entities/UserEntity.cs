using Fluxy.Core.Models.Users;
using Fluxy.Core.Abstractions;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of a user account. It is mutable, has no behaviour beyond
    /// mapping to <see cref="User"/>, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// The row stores a group, not a level. <see cref="Role"/> is gone from the column list
    /// and survives as a read of <see cref="Group"/>'s role, which is why every load that ends
    /// in <see cref="ToModel"/> has to include it: an account without its group cannot say
    /// what level it holds or what state it is in, and the alternative - a level column kept
    /// beside the reference in step - is exactly the second copy of one fact that having a
    /// group exists to avoid.
    /// </remarks>
    public sealed class UserEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserEntity"/> class for an account that
        /// is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public UserEntity()
        {
        }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Email address of the owner. Unique, compared case sensitively.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>BCrypt hash of the password.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Group the account belongs to, and therefore where its level, its permissions and
        /// the group half of its status all come from.
        /// </summary>
        /// <remarks>
        /// Required rather than nullable: a row without a group would be an account whose
        /// level nobody can answer for. The foreign key is <c>RESTRICT</c> rather than
        /// cascade for the same reason a group with members is refused at the service - the
        /// database is the last line of a rule that says an account's group is never taken
        /// away from under it.
        /// </remarks>
        public Guid GroupId { get; set; }

        /// <summary>
        /// Navigation to that group. Null when the query did not ask for it, which is the
        /// state <see cref="ToModel"/> refuses rather than guesses its way around.
        /// </summary>
        public UserGroupEntity? Group { get; set; }

        /// <summary>
        /// Current state of <b>this row alone</b>. The state an account is read with elsewhere
        /// combines it with its group's; see <see cref="UserStatusComposition"/>.
        /// </summary>
        public UserStatus Status { get; set; } = UserStatus.Unregistered;

        /// <summary>BCrypt hash of the pending one-time registration code, if any.</summary>
        public string? RegistrationCodeHash { get; set; }

        /// <summary>Moment the pending one-time registration code expires, if any.</summary>
        public DateTimeOffset? RegistrationCodeExpiresAt { get; set; }

        /// <summary>Moment the account confirmed its email address, if it did.</summary>
        public DateTimeOffset? RegisteredAt { get; set; }

        /// <summary>
        /// IANA identifier of the chosen display time zone, or null when none is chosen.
        /// </summary>
        public string? TimeZone { get; set; }

        /// <summary>
        /// Whether a sign-in from a network that is not allowed is refused with the same
        /// answer a wrong password gets.
        /// </summary>
        public bool GeoProtectionEnabled { get; set; }

        /// <summary>
        /// Whether a refresh from an address other than the session's own ends the session.
        /// </summary>
        public bool BindSessionToIp { get; set; }

        /// <summary>
        /// Builds the business model out of this row and the group it belongs to.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The query did not include <see cref="Group"/>. Guessing a level or a status for an
        /// account whose group nobody read would be an answer made up rather than a fact, so
        /// the miss is refused loudly and the message names the fix.
        /// </exception>
        public User ToModel()
        {
            var group = Group ?? throw new InvalidOperationException(
                $"UserEntity '{Id}' was mapped without its group. Include(u => u.Group) in " +
                "the query that loads an account: an account's role, permissions and " +
                "effective status all come from its group.");

            return new User
            {
                Id = Id,
                Username = Username,
                Email = Email,
                PasswordHash = PasswordHash,
                GroupId = group.Id,
                GroupName = group.Name,
                Role = group.Role,
                Status = UserStatusComposition.Combine(Status, group.Status),
                RegistrationCodeHash = RegistrationCodeHash,
                RegistrationCodeExpiresAt = RegistrationCodeExpiresAt,
                RegisteredAt = RegisteredAt,
                TimeZone = TimeZone,
                GeoProtectionEnabled = GeoProtectionEnabled,
                BindSessionToIp = BindSessionToIp,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }
}
