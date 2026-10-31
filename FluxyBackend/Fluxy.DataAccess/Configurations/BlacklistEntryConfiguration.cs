using Fluxy.Core.Blacklists.Utils;
using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="BlacklistEntryEntity"/> to the <c>blacklist_entries</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class BlacklistEntryConfiguration : IEntityTypeConfiguration<BlacklistEntryEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "blacklist_entries";

        /// <summary>
        /// Name of the unique index over <c>(type, value)</c>. Written out because the service
        /// matches a refused write against this exact name, and a generated one would make the
        /// match depend on the property names rather than on the column list.
        /// </summary>
        public const string TypeValueIndexName = "ux_blacklist_entries_type_value";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<BlacklistEntryEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id).HasColumnName("id");

            // Stored as smallint, like every other enum here. The three members are kinds of
            // list rather than a scale, so the numeric order means nothing.
            builder.Property(entry => entry.Type)
                .HasColumnName("type")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(entry => entry.Value)
                .HasColumnName("value")
                .HasMaxLength(BlacklistEntryLimits.ValueMaxLength)
                .IsRequired();

            // The counter is a plain integer with no database default, per the project rule:
            // a value the database invented when the application forgot a column would hide
            // the mistake. The service writes zero on every create.
            builder.Property(entry => entry.BlockedAttempts)
                .HasColumnName("blocked_attempts")
                .IsRequired();

            builder.Property(entry => entry.Note)
                .HasColumnName("note")
                .HasMaxLength(BlacklistEntryLimits.NoteMaxLength);

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // One value per list. Scoped to the type because an address blacklisted as an IP
            // and the same text blacklisted as a user-agent are two unrelated rules, and
            // forbidding one because the other exists would be a rule about nothing. Case
            // sensitive for the same reason username and email are: PostgreSQL decides what
            // "equal" means for a varchar from the column's collation, and the default one
            // compares the bytes.
            builder.HasIndex(entry => new { entry.Type, entry.Value })
                .IsUnique()
                .HasDatabaseName(TypeValueIndexName);

            // The read every screen performs: one list ordered by the added date. The type
            // leads so the index can answer it as a range scan, and the date follows in the
            // direction the default order runs.
            builder.HasIndex(entry => new { entry.Type, entry.CreatedAt });
        }
    }
}
