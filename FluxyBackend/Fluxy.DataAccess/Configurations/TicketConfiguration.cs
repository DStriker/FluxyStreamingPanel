using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Tickets.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="TicketEntity"/> to the <c>tickets</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The account's foreign key cascades.</b> A ticket is a conversation one account opened;
    /// deleting the account removes its conversations, and the messages' own cascade removes the
    /// text with the ticket. The table is never orphaned, and no login name is duplicated onto the
    /// row - the operator's list resolves it through a join on read.
    /// </para>
    /// <para>
    /// <b>Two indexes are the two questions the screens ask.</b> <c>(user_id, created_at)</c>
    /// descending is one account's own list, newest first; <c>(created_at, id)</c> descending is the
    /// operator's default order and the tie-break every page turn depends on.
    /// </para>
    /// <para>
    /// <b>The subject width comes from <see cref="TicketLimits"/>.</b> The service cuts a subject to
    /// the same constant, so a column width and the rule that shortens a value to fit cannot drift
    /// apart.
    /// </para>
    /// <para>
    /// No column carries <c>HasDefaultValue</c>: a value the database invented for a column the
    /// application forgot would hide the mistake instead of surfacing it - the rule every table of
    /// this solution follows.
    /// </para>
    /// </remarks>
    public sealed class TicketConfiguration : IEntityTypeConfiguration<TicketEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "tickets";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TicketEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(ticket => ticket.Id);
            builder.Property(ticket => ticket.Id)
                .HasColumnName("id");

            // A real reference: a ticket belongs to one account of this installation, so the key
            // must name an existing row and the cascade below removes the conversation with it.
            builder.Property(ticket => ticket.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(ticket => ticket.Subject)
                .HasColumnName("subject")
                .HasMaxLength(TicketLimits.SubjectMaxLength)
                .IsRequired();

            // Stored as smallint, like every other enum here. The numbers are the schema.
            builder.Property(ticket => ticket.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            builder.Property(ticket => ticket.AdminUnread)
                .HasColumnName("admin_unread")
                .IsRequired();

            builder.Property(ticket => ticket.UserUnread)
                .HasColumnName("user_unread")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // A ticket without the account that opened it is a conversation nobody can be told
            // about, so the row goes with the account.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(ticket => ticket.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One account's own list, newest first.
            builder.HasIndex(ticket => new { ticket.UserId, ticket.CreatedAt })
                .IsDescending(false, true);

            // The default order, and the tie-break every page turn depends on: two tickets can be
            // opened in the same instant, and without the identifier the database is free to
            // answer a different order on the next read.
            builder.HasIndex(ticket => new { ticket.CreatedAt, ticket.Id })
                .IsDescending(true, true);
        }
    }
}
