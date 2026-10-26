using Fluxy.Core.Epg.Utils;
using Fluxy.Core.Persistence.Base;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="EpgSourceEntity"/> to the <c>epg_sources</c> table. It is discovered by
    /// <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class EpgSourceConfiguration : IEntityTypeConfiguration<EpgSourceEntity>
    {
        /// <summary>Name of the table. Tables use the plural form of the entity name.</summary>
        public const string TableName = "epg_sources";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<EpgSourceEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(source => source.Id);
            builder.Property(source => source.Id).HasColumnName("id");

            builder.Property(source => source.Name)
                .HasColumnName("name")
                .HasMaxLength(EpgSourceLimits.NameMaxLength)
                .IsRequired();

            builder.Property(source => source.Url)
                .HasColumnName("url")
                .HasMaxLength(EpgSourceLimits.UrlMaxLength)
                .IsRequired();

            builder.Property(source => source.DaysKeep)
                .HasColumnName("days_keep")
                .IsRequired();

            // A null means "never refreshed", which is a fact about a source that has just
            // been added rather than a missing value.
            builder.Property(source => source.LastEpgUpdatedAt)
                .HasColumnName("last_epg_updated_at");

            // The raw guide is a text column rather than anything shaped: it is whatever the
            // source serves, it is written by the updater, and nothing else reads it.
            builder.Property(source => source.RawData)
                .HasColumnName("raw_data")
                .HasColumnType("text");

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");

            // One name per installation, compared case sensitively for the same reason
            // username and email are: PostgreSQL decides what "equal" means for a varchar from
            // the collation of the column, and the default one compares the bytes. There is no
            // second axis to scope it to - a guide source belongs to no category.
            builder.HasIndex(source => source.Name)
                .IsUnique();
        }
    }
}
