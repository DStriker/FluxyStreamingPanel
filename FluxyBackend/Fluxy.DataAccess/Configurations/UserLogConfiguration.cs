using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Users.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="UserLogEntity"/> to the <c>user_logs</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The account's key is <c>SET NULL</c>, never cascade.</b> The log is the evidence of what
    /// was attempted against an account; deleting that account may make the subject unknown, it may
    /// not erase the evidence. The description already names the account as it read at write time,
    /// so the row stays legible with a null identifier.
    /// </para>
    /// <para>
    /// <b>Three indexes are the questions the screen asks.</b> <c>(created_at, id)</c> descending is
    /// the default order and the tie-break every page turn depends on; <c>(user_id, created_at)</c>
    /// is one account's history; <c>(record_type)</c> is the filter and the second sortable column.
    /// All three are columns of this table alone, which is why exactly these two sortable fields
    /// are offered.
    /// </para>
    /// <para>
    /// No column carries <c>HasDefaultValue</c>: a value the database invented for a column the
    /// application forgot would hide the mistake instead of surfacing it - the rule every table of
    /// this solution follows.
    /// </para>
    /// </remarks>
    public sealed class UserLogConfiguration : IEntityTypeConfiguration<UserLogEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "user_logs";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserLogEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id)
                .HasColumnName("id");

            // Nullable for two reasons that are both facts about the event: the attempt may have
            // named no account at all, and the account it named may since have been deleted. The
            // foreign key is SET NULL, so the second case leaves the column empty rather than the
            // row gone.
            builder.Property(entry => entry.UserId)
                .HasColumnName("user_id");

            // smallint, like every other enum in this solution. The values are schema: they are
            // already written into rows and must not be renumbered.
            builder.Property(entry => entry.RecordType)
                .HasColumnName("record_type")
                .HasConversion<short>()
                .IsRequired();

            // Stored, not derived: the same record type ends either way, and deriving it would
            // make a failed or a successful attempt unrepresentable.
            builder.Property(entry => entry.IsSuccess)
                .HasColumnName("is_success")
                .IsRequired();

            builder.Property(entry => entry.Description)
                .HasColumnName("description")
                .IsRequired();

            // 45 characters is the longest textual IPv6 form, so the column can hold every address
            // the socket layer can hand over.
            builder.Property(entry => entry.Ip)
                .HasColumnName("ip")
                .HasMaxLength(UserLogListLimits.IpMaxLength);

            builder.Property(entry => entry.Isp)
                .HasColumnName("isp")
                .HasMaxLength(UserLogListLimits.IspMaxLength);

            // Stored upper case, two characters, like the connection log's.
            builder.Property(entry => entry.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(UserLogListLimits.CountryCodeLength);

            builder.Property(entry => entry.UserAgent)
                .HasColumnName("user_agent")
                .HasMaxLength(UserLogListLimits.UserAgentMaxLength);

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The default order, and the tie-break every page turn depends on: two entries can be
            // written in the same instant, and without the identifier the database is free to
            // answer a different order on the next read.
            builder.HasIndex(entry => new { entry.CreatedAt, entry.Id })
                .IsDescending(true, true);

            // One account's history, oldest first - the drill-down the row links to.
            builder.HasIndex(entry => new { entry.UserId, entry.CreatedAt });

            // The record-type filter, and the column the second sortable field orders by.
            builder.HasIndex(entry => entry.RecordType);

            // Evidence whose subject is gone is still evidence: deleting the account forgets who
            // the row was about, it does not delete the row.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
