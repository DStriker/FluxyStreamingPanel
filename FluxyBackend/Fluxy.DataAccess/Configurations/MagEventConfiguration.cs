using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="MagEventEntity"/> to the <c>mag_events</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The subscription's foreign key cascades.</b> An event is evidence about one
    /// subscription: deleting the subscription deletes the events it carried, because a journal
    /// line whose subject no longer exists is what the operator asked to remove.
    /// </para>
    /// <para>
    /// <b>Two indexes are the two questions the screen asks.</b> <c>(created_at, id)</c> descending
    /// is the default order and the tie-break every page turn depends on; <c>(subscription_id,
    /// created_at)</c> is "this subscription's events, oldest first", which is the drill-down and
    /// the shape the <c>subscriptionId</c> filter runs on.
    /// </para>
    /// <para>
    /// <b>No column carries <c>HasDefaultValue</c>.</b> Every flag is resolved by
    /// <c>MagEventRules</c> in the service and written explicitly, so a column the application
    /// forgot is an error rather than a value the database invented - the rule every table of this
    /// solution follows.
    /// </para>
    /// </remarks>
    public sealed class MagEventConfiguration : IEntityTypeConfiguration<MagEventEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "mag_events";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MagEventEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(magEvent => magEvent.Id);
            builder.Property(magEvent => magEvent.Id)
                .HasColumnName("id");

            // A real reference: an event is about one subscription of this installation, so the
            // key must name an existing row and the cascade below removes the journal with it.
            builder.Property(magEvent => magEvent.SubscriptionId)
                .HasColumnName("subscription_id")
                .IsRequired();

            // smallint, like every other enum in this solution. The six values are schema: they
            // are already written into rows and must not be renumbered.
            builder.Property(magEvent => magEvent.EventType)
                .HasColumnName("event_type")
                .HasConversion<short>()
                .IsRequired();

            // The payload is nullable because four operations carry none. The width is the
            // column's, and MagEventRules refuses a longer message rather than cutting it.
            builder.Property(magEvent => magEvent.Message)
                .HasColumnName("message")
                .HasMaxLength(MagEventListLimits.MessageMaxLength);

            // smallint, like every other enum here. The two values are schema.
            builder.Property(magEvent => magEvent.Status)
                .HasColumnName("status")
                .HasConversion<short>()
                .IsRequired();

            // Every flag is required and has no default: the values are resolved by
            // MagEventRules, and a row that reached the table without them is a bug the database
            // refuses to hide.
            builder.Property(magEvent => magEvent.NeedConfirm)
                .HasColumnName("need_confirm")
                .IsRequired();

            builder.Property(magEvent => magEvent.RebootAfterOk)
                .HasColumnName("reboot_after_ok")
                .IsRequired();

            builder.Property(magEvent => magEvent.AutoHideTimeout)
                .HasColumnName("auto_hide_timeout")
                .IsRequired();

            builder.Property(magEvent => magEvent.AdditionalServicesOn)
                .HasColumnName("additional_services_on")
                .IsRequired();

            builder.Property(magEvent => magEvent.Anec)
                .HasColumnName("anec")
                .IsRequired();

            builder.Property(magEvent => magEvent.Vclub)
                .HasColumnName("vclub")
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // An event without the subscription it was sent under is a row nobody can be told
            // about, so the row goes with it. The consequence is recorded here and in the entity's
            // remarks: deleting a subscription removes its events.
            builder.HasOne<SubscriptionEntity>()
                .WithMany()
                .HasForeignKey(magEvent => magEvent.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // The default order, and the tie-break every page turn depends on: two events can be
            // recorded in the same instant, and without the identifier the database is free to
            // answer a different order on the next read.
            builder.HasIndex(magEvent => new { magEvent.CreatedAt, magEvent.Id })
                .IsDescending(true, true);

            // One subscription's events, oldest first - the drill-down from a subscription, and
            // the index the `subscriptionId` filter runs on.
            builder.HasIndex(magEvent => new { magEvent.SubscriptionId, magEvent.CreatedAt });
        }
    }
}
