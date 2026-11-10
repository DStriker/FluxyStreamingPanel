using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Requests.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="ApiRequestFailureEntity"/> to the <c>api_request_failures</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No column is a foreign key.</b> The subscription data is an opaque access parameter that
    /// may name a subscription this installation does not have, and the content snapshot may name
    /// content it never recorded - refusing those rows would delete the evidence the log exists
    /// for.
    /// </para>
    /// <para>
    /// <b>Five indexes are the questions the screen asks.</b> <c>(created_at, id)</c> descending is
    /// the default order and the tie-break every page turn depends on; <c>reason</c> is the filter
    /// and the second sortable column; <c>ip</c>, <c>country_code</c> and <c>isp</c> are the exact
    /// network filters the shared cell writes.
    /// </para>
    /// <para>
    /// <b>Every width comes from <see cref="ApiRequestFailureListLimits"/>.</b> The query parser
    /// judges an oversized filter value against the same constants, so a column width and the rule
    /// that refuses a value that cannot fit cannot drift apart.
    /// </para>
    /// <para>
    /// No column carries <c>HasDefaultValue</c>: a value the database invented for a column the
    /// application forgot would hide the mistake instead of surfacing it - the rule every table of
    /// this solution follows.
    /// </para>
    /// </remarks>
    public sealed class ApiRequestFailureConfiguration
        : IEntityTypeConfiguration<ApiRequestFailureEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "api_request_failures";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ApiRequestFailureEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(failure => failure.Id);
            builder.Property(failure => failure.Id)
                .HasColumnName("id");

            // Nullable on purpose: the request may have named no subscription data at all. It is
            // free-form text, never a foreign key - an unknown identifier is a refusal worth
            // recording, not a row the schema should reject.
            builder.Property(failure => failure.SubscriptionData)
                .HasColumnName("subscription_data")
                .HasMaxLength(ApiRequestFailureListLimits.SubscriptionDataMaxLength);

            // smallint, like every other enum in this solution. The four kinds are the
            // ContentType vocabulary the content categories already speak.
            builder.Property(failure => failure.ContentKind)
                .HasColumnName("content_kind")
                .HasConversion<short>();

            // Opaque on purpose: no foreign key. The row may name content this installation never
            // recorded, and deleting the content later must not touch the evidence.
            builder.Property(failure => failure.ContentId)
                .HasColumnName("content_id");

            // Captured at write time and never resolved on read, so the row stays legible after
            // the content it names is gone.
            builder.Property(failure => failure.ContentName)
                .HasColumnName("content_name")
                .HasMaxLength(ApiRequestFailureListLimits.ContentNameMaxLength);

            // smallint, like every other enum in this solution. The six values are schema: they
            // are already written into rows and must not be renumbered.
            builder.Property(failure => failure.Reason)
                .HasColumnName("reason")
                .HasConversion<short>()
                .IsRequired();

            // 45 characters is the longest textual IPv6 form, so the column can hold every
            // address the socket layer can hand over.
            builder.Property(failure => failure.Ip)
                .HasColumnName("ip")
                .HasMaxLength(ApiRequestFailureListLimits.IpMaxLength)
                .IsRequired();

            builder.Property(failure => failure.Isp)
                .HasColumnName("isp")
                .HasMaxLength(ApiRequestFailureListLimits.IspMaxLength);

            // Stored upper case, two characters, like the connection log's.
            builder.Property(failure => failure.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(ApiRequestFailureListLimits.CountryCodeLength);

            // The sentence a support call reads. Its width is the column's, and it is the first of
            // the three columns the search covers.
            builder.Property(failure => failure.Description)
                .HasColumnName("description")
                .HasMaxLength(ApiRequestFailureListLimits.DescriptionMaxLength)
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The default order, and the tie-break every page turn depends on: two refusals can be
            // recorded in the same instant, and without the identifier the database is free to
            // answer a different order on the next read.
            builder.HasIndex(failure => new { failure.CreatedAt, failure.Id })
                .IsDescending(true, true);

            // The reason filter, and the column the second sortable field orders by.
            builder.HasIndex(failure => failure.Reason);

            // The three exact network filters the shared cell writes. All three are nullable, and
            // PostgreSQL's b-tree indexes nulls like any other value, so a filter that names one
            // fact still costs one index read rather than a scan of a table that grows forever.
            builder.HasIndex(failure => failure.Ip);
            builder.HasIndex(failure => failure.CountryCode);
            builder.HasIndex(failure => failure.Isp);
        }
    }
}
