using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="CreditLogEntity"/> to the <c>credit_logs</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two foreign keys with two different answers to deletion.</b> The reseller's key
    /// cascades: a ledger line whose subject is gone is a line nobody can be told about. The
    /// issuer's key is <c>SET NULL</c>: credit that was issued is a fact about the reseller, and
    /// deleting the administrator who issued it must not erase the reseller's history - it only
    /// makes the issuer unknown.
    /// </para>
    /// <para>
    /// <b>The two indexes are the questions the screen asks.</b> <c>(created_at DESC, id DESC)</c>
    /// is the default order and the tie-break every page turn depends on;
    /// <c>(reseller_id, created_at)</c> is one reseller's history, oldest first. Both are columns
    /// of this table alone, which is why the two sortable fields are the only ones offered.
    /// </para>
    /// </remarks>
    public sealed class CreditLogConfiguration : IEntityTypeConfiguration<CreditLogEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "credit_logs";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<CreditLogEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id)
                .HasColumnName("id");

            builder.Property(entry => entry.ResellerId)
                .HasColumnName("reseller_id")
                .IsRequired();

            // Set on every row the admin panel writes - create and update, either sign. Null only
            // on a reseller-spent row, a write path that does not exist yet.
            builder.Property(entry => entry.IssuedById)
                .HasColumnName("issued_by_id");

            // Signed: positive is issued, negative is spent. A check constraint is deliberately
            // absent - the sign is the meaning, and a column that could only be non-negative
            // could not express spending at all.
            builder.Property(entry => entry.Amount)
                .HasColumnName("amount")
                .IsRequired();

            builder.Property(entry => entry.Description)
                .HasColumnName("description")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The default order, and the tie-break every page turn depends on: two entries can be
            // written in the same instant, and without the identifier the database is free to
            // answer a different order on the next read.
            builder.HasIndex(entry => new { entry.CreatedAt, entry.Id })
                .IsDescending(true, true);

            // One reseller's history, oldest first - the drill-down the row links to.
            builder.HasIndex(entry => new { entry.ResellerId, entry.CreatedAt });

            // A ledger line whose subject is gone is a line nobody can be told about, so the row
            // goes with the account.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(entry => entry.ResellerId)
                .OnDelete(DeleteBehavior.Cascade);

            // The issuer is history, not the subject: deleting the administrator leaves the
            // credit and the reseller, and only forgets who issued it.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(entry => entry.IssuedById)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
