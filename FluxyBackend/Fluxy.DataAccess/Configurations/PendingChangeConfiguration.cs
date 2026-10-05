using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="PendingChangeEntity"/> to the <c>pending_changes</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class PendingChangeConfiguration : IEntityTypeConfiguration<PendingChangeEntity>
    {
        /// <summary>
        /// Name of the table. Tables use the plural form of the entity name, columns use
        /// snake_case, and both live in the default schema of the database.
        /// </summary>
        public const string TableName = "pending_changes";

        /// <summary>
        /// Maximum length of a staged username or email. 254 is the widest of the two, so one
        /// column holds either without a second mapping.
        /// </summary>
        public const int TargetValueMaxLength = 254;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PendingChangeEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(change => change.Id);
            builder.Property(change => change.Id)
                .HasColumnName("id");

            // Indexed below rather than declared as a foreign key, which is what the other two
            // tables of this project do as well: the application owns the rows, and a constraint
            // it never violates would only get in the way of a cleanup that outlives an account.
            builder.Property(change => change.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            // A staged change belongs to exactly one account and dies with it. This used to be a
            // bare indexed column on purpose, like the refresh tokens still are in prose - but an
            // account can now be deleted, and a code that outlives its account is a confirmation
            // for nobody. The foreign key says so where a comment used to.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(change => change.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Stored as smallint, like every other enum here.
            builder.Property(change => change.Kind)
                .HasColumnName("kind")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(change => change.TargetValue)
                .HasColumnName("target_value")
                .HasMaxLength(TargetValueMaxLength);

            builder.Property(change => change.NewPasswordHash)
                .HasColumnName("new_password_hash")
                .HasMaxLength(UserConfiguration.PasswordHashMaxLength);

            builder.Property(change => change.CodeHash)
                .HasColumnName("code_hash")
                .HasMaxLength(UserConfiguration.PasswordHashMaxLength)
                .IsRequired();

            builder.Property(change => change.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            // The staged guard of a ChangeLoginGuard request, as JSON. See the property: every
            // other kind leaves it null and keeps using the columns it always used.
            builder.Property(change => change.Payload)
                .HasColumnName("payload");

            // Audit columns inherited from AuditableEntity, listed explicitly like every other
            // column for the reason the other mappings give.
            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // One pending operation per account. Without it a second request would sit next to
            // the first and leave two codes that both work, one of them mailed to an address
            // that may since have been abandoned.
            builder.HasIndex(change => change.UserId)
                .IsUnique();
        }
    }
}
