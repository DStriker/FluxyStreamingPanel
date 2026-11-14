using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Tickets.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="TicketReplyEntity"/> to the <c>ticket_replies</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The author's key is <c>SET NULL</c>, never cascade.</b> A message is part of a
    /// conversation, and deleting the account that wrote it may make the author unknown but may not
    /// erase what was said. The stored side keeps the row legible with a null identifier.
    /// </para>
    /// <para>
    /// <b>The message width comes from <see cref="TicketLimits"/>.</b> The service cuts every
    /// message to the same constant, so a column width and the rule that shortens a value to fit
    /// cannot drift apart.
    /// </para>
    /// <para>
    /// <b>One index is the one question the thread asks.</b> <c>(ticket_id, created_at)</c> is the
    /// conversation, oldest first - the order the thread is drawn in and the shape both the read
    /// and the unread-mark update run on.
    /// </para>
    /// <para>
    /// No column carries <c>HasDefaultValue</c>: a value the database invented for a column the
    /// application forgot would hide the mistake instead of surfacing it - the rule every table of
    /// this solution follows.
    /// </para>
    /// </remarks>
    public sealed class TicketReplyConfiguration : IEntityTypeConfiguration<TicketReplyEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "ticket_replies";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TicketReplyEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(reply => reply.Id);
            builder.Property(reply => reply.Id)
                .HasColumnName("id");

            builder.Property(reply => reply.TicketId)
                .HasColumnName("ticket_id")
                .IsRequired();

            // Nullable for one reason that is a fact about the event: the account that wrote the
            // message may since have been deleted. The foreign key is SET NULL, so that case leaves
            // the column empty rather than the row gone.
            builder.Property(reply => reply.AuthorId)
                .HasColumnName("author_id");

            builder.Property(reply => reply.Message)
                .HasColumnName("message")
                .HasMaxLength(TicketLimits.MessageMaxLength)
                .IsRequired();

            builder.Property(reply => reply.IsAdminReply)
                .HasColumnName("is_admin_reply")
                .IsRequired();

            builder.Property(reply => reply.ReadAt)
                .HasColumnName("read_at");

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The conversation is the parent: deleting a ticket deletes its messages with it.
            builder.HasOne<TicketEntity>()
                .WithMany(ticket => ticket.Replies)
                .HasForeignKey(reply => reply.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // The author is a reference to the account, not the message's owner: the row stays when
            // the account is deleted.
            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(reply => reply.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);

            // One conversation, oldest first - the order the thread is drawn in and the shape the
            // unread-mark update and the newest-message computation run on.
            builder.HasIndex(reply => new { reply.TicketId, reply.CreatedAt });
        }
    }
}
