using Fluxy.Core.Models.Users;
using Fluxy.Core.Abstractions;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of a user account. It is mutable, has no behaviour beyond
    /// mapping to and from <see cref="User"/>, and knows nothing about HTTP.
    /// </summary>
    public sealed class UserEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserEntity"/> class for an account that
        /// is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public UserEntity()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserEntity"/> class from an existing
        /// row. Only <see cref="FromModel(User)"/> uses it, which keeps the audit values of the
        /// account it was built from.
        /// </summary>
        /// <param name="id">Primary key of the stored row.</param>
        /// <param name="createdAt">Creation timestamp of the stored row.</param>
        /// <param name="updatedAt">Last modification timestamp of the stored row.</param>
        private UserEntity(Guid id, DateTimeOffset createdAt, DateTimeOffset updatedAt)
            : base(id, createdAt, updatedAt)
        {
        }

        /// <summary>Login name, at most 20 characters. Unique, compared case sensitively.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Email address of the owner. Unique, compared case sensitively.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>BCrypt hash of the password.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Access level of the account.</summary>
        public UserRole Role { get; set; } = UserRole.Client;

        /// <summary>Current state of the account.</summary>
        public UserStatus Status { get; set; } = UserStatus.Unregistered;

        /// <summary>BCrypt hash of the pending one-time registration code, if any.</summary>
        public string? RegistrationCodeHash { get; set; }

        /// <summary>Moment the pending one-time registration code expires, if any.</summary>
        public DateTimeOffset? RegistrationCodeExpiresAt { get; set; }

        /// <summary>Moment the account confirmed its email address, if it did.</summary>
        public DateTimeOffset? RegisteredAt { get; set; }

        /// <summary>
        /// Builds the business model out of this row.
        /// </summary>
        public User ToModel()
        {
            return new User
            {
                Id = Id,
                Username = Username,
                Email = Email,
                PasswordHash = PasswordHash,
                Role = Role,
                Status = Status,
                RegistrationCodeHash = RegistrationCodeHash,
                RegistrationCodeExpiresAt = RegistrationCodeExpiresAt,
                RegisteredAt = RegisteredAt,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }

        /// <summary>
        /// Builds an entity out of a business model, keeping the identifier and both audit
        /// timestamps, so that re-saving the result does not look like a new row.
        /// </summary>
        /// <param name="model">Business model to map.</param>
        public static UserEntity FromModel(User model)
        {
            return new UserEntity(model.Id, model.CreatedAt, model.UpdatedAt)
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = model.PasswordHash,
                Role = model.Role,
                Status = model.Status,
                RegistrationCodeHash = model.RegistrationCodeHash,
                RegistrationCodeExpiresAt = model.RegistrationCodeExpiresAt,
                RegisteredAt = model.RegisteredAt
            };
        }
    }
}