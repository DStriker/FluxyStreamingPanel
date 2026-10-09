using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="UserEntity"/> to the <c>users</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class UserConfiguration : IEntityTypeConfiguration<UserEntity>
    {
        /// <summary>
        /// Name of the table. Tables use the plural form of the entity name, columns use
        /// snake_case, and both live in the default schema of the database.
        /// </summary>
        public const string TableName = "users";

        /// <summary>Maximum length of a login name, also enforced by the frontend forms.</summary>
        public const int UsernameMaxLength = 20;

        /// <summary>
        /// Maximum length of an email address. 254 characters is the longest address SMTP
        /// accepts.
        /// </summary>
        public const int EmailMaxLength = 254;

        /// <summary>
        /// Maximum length of a BCrypt hash. The hash itself is 60 characters; the room above
        /// that leaves room for a stronger algorithm later, whose hash stays in the same column
        /// and needs no schema change.
        /// </summary>
        public const int PasswordHashMaxLength = 255;

        /// <summary>
        /// Maximum length of an IANA time zone identifier. The longest identifier in the
        /// tz database is well under this, so the room above is for a future rename rather
        /// than for a value that is expected to grow.
        /// </summary>
        public const int TimeZoneMaxLength = 64;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserEntity> builder)
        {
            builder.ToTable(TableName, table =>
                table.HasCheckConstraint(
                    "ck_users_registered_at_status",
                    // Guards the one inconsistency that would make a row mean two things at
                    // once: a registration timestamp on an account that is still Unregistered.
                    // The opposite is deliberately allowed, because blocking an account that
                    // never confirmed its email is a legitimate move against a spammer.
                    "registered_at IS NULL OR status <> 0"));

            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id)
                .HasColumnName("id");

            builder.Property(user => user.Username)
                .HasColumnName("username")
                .HasMaxLength(UsernameMaxLength)
                .IsRequired();

            builder.Property(user => user.Email)
                .HasColumnName("email")
                .HasMaxLength(EmailMaxLength)
                .IsRequired();

            builder.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(PasswordHashMaxLength)
                .IsRequired();

            // The level an account holds has no column of its own: it is the role of the
            // group named by user_id below, read through that join. Storing it here as well
            // would be a second copy of a fact that a group rename or a role change would
            // have to be remembered in two places.
            builder.Property(user => user.GroupId)
                .HasColumnName("group_id")
                .IsRequired();

            // Stored as smallint, which is what HasConversion<short> maps to in PostgreSQL.
            // The numeric order is not a permission order - see the enum.
            builder.Property(user => user.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(user => user.RegistrationCodeHash)
                .HasColumnName("registration_code_hash")
                .HasMaxLength(PasswordHashMaxLength);

            builder.Property(user => user.RegistrationCodeExpiresAt)
                .HasColumnName("registration_code_expires_at");

            builder.Property(user => user.RegisteredAt)
                .HasColumnName("registered_at");

            // Nullable, so the column has no HasDefaultValue either: an account that has
            // never chosen a zone simply carries NULL, and the absence of the value is the
            // state that means "read it from the browser" rather than a hole in the row.
            builder.Property(user => user.TimeZone)
                .HasColumnName("timezone")
                .HasMaxLength(TimeZoneMaxLength);

            // Two switches with no unknown state: an account either guards its sign-ins or it
            // does not, and a session is either bound to its opening address or it is not.
            builder.Property(user => user.GeoProtectionEnabled)
                .HasColumnName("geo_protection_enabled")
                .IsRequired();

            builder.Property(user => user.BindSessionToIp)
                .HasColumnName("bind_session_to_ip")
                .IsRequired();

            // Audit columns inherited from AuditableEntity. They are listed explicitly like
            // every other column, because a mapping that only names some of them would leave
            // the rest with the CLR property name.
            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // No HasDefaultValue anywhere on purpose: a value the database invents when the
            // application forgot a column hides the mistake instead of surfacing it.
            builder.HasIndex(user => user.Username)
                .IsUnique();

            builder.HasIndex(user => user.Email)
                .IsUnique();

            // An account's group is read on every authorized request and on every page of
            // the accounts list, so the join column carries its own index rather than relying
            // on the primary key to answer it.
            builder.HasIndex(user => user.GroupId);

            // RESTRICT rather than cascade: the group half of an account is not something the
            // account may lose because somebody deleted the group. The service refuses a
            // group with members first (group_in_use), so this is the line underneath that
            // one rather than a second path to the same answer.
            builder.HasOne(user => user.Group)
                .WithMany()
                .HasForeignKey(user => user.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}