using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Users.Models;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Context
{
    /// <summary>
    /// Entity Framework Core context for the Fluxy PostgreSQL database.
    /// The provider and the connection string are supplied by
    /// <c>ServiceCollectionExtensions.AddDataAccess</c>, so this class only
    /// describes the shape of the database and stays provider-agnostic.
    /// </summary>
    internal sealed class FluxyDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluxyDbContext"/> class.
        /// </summary>
        /// <param name="options">Options carrying the Npgsql provider and the connection string.</param>
        public FluxyDbContext(DbContextOptions<FluxyDbContext> options)
            : base(options)
        {
        }

        /// <summary>User accounts.</summary>
        public DbSet<UserEntity> Users => Set<UserEntity>();

        /// <summary>
        /// Groups of accounts: the place where the level a member holds and the permissions it
        /// is granted are stored together.
        /// </summary>
        public DbSet<UserGroupEntity> UserGroups => Set<UserGroupEntity>();

        /// <summary>
        /// One row per permission one group grants. The set a group holds is the set of rows
        /// it has.
        /// </summary>
        public DbSet<UserGroupPermissionEntity> GroupPermissions => Set<UserGroupPermissionEntity>();

        /// <summary>
        /// Issued refresh tokens. One row per token ever issued, kept after it is spent so that
        /// presenting it twice is recognisable as such.
        /// </summary>
        public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

        /// <summary>
        /// Changes that were requested and are waiting for their code to be confirmed: a
        /// profile change of an account that is signed in, or a password reset asked for from
        /// the public form.
        /// </summary>
        public DbSet<PendingChangeEntity> PendingChanges => Set<PendingChangeEntity>();

        /// <summary>
        /// Entries of the login guard allow lists, one row per allowed value.
        /// </summary>
        public DbSet<UserLoginGuardRuleEntity> LoginGuardRules => Set<UserLoginGuardRuleEntity>();

        /// <summary>
        /// Categories of content, one row per section an operator can file a piece of content
        /// under. The kind of content is a column of the row, so one table holds the
        /// categories of all four kinds.
        /// </summary>
        public DbSet<ContentCategoryEntity> ContentCategories => Set<ContentCategoryEntity>();

        /// <summary>
        /// Configured streams. Each names the category it is filed under and the kind of
        /// stream it is, and carries its two lists as rows of their own.
        /// </summary>
        public DbSet<VideoStreamEntity> VideoStreams => Set<VideoStreamEntity>();

        /// <summary>
        /// One address a stream relays from, in the order the stream uses them. The set of
        /// rows a stream has is its whole failover chain.
        /// </summary>
        public DbSet<VideoStreamSourceEntity> VideoStreamSources => Set<VideoStreamSourceEntity>();

        /// <summary>One file a stream plays, in the order it plays them.</summary>
        public DbSet<VideoStreamFileEntity> VideoStreamFiles => Set<VideoStreamFileEntity>();

        /// <summary>
        /// Configured audio streams. Each names the category it is filed under and the kind of
        /// stream it is, and carries its source list as rows of their own.
        /// </summary>
        public DbSet<AudioStreamEntity> AudioStreams => Set<AudioStreamEntity>();

        /// <summary>
        /// One address an audio stream relays from, in the order the stream uses them. The set
        /// of rows a stream has is its whole failover chain.
        /// </summary>
        public DbSet<AudioStreamSourceEntity> AudioStreamSources => Set<AudioStreamSourceEntity>();

        /// <summary>
        /// Videos available on demand. Each names the category it is filed under, the address
        /// it is played from, and the metadata the catalogue shows around it.
        /// </summary>
        public DbSet<VodItemEntity> VodItems => Set<VodItemEntity>();

        /// <summary>
        /// Series. Each names the category it is filed under and carries the metadata the
        /// catalogue shows around it; its episodes are rows of their own.
        /// </summary>
        public DbSet<SeriesEntity> Series => Set<SeriesEntity>();

        /// <summary>
        /// Episodes of a series, one row per season-and-number pair.
        /// </summary>
        public DbSet<EpisodeEntity> Episodes => Set<EpisodeEntity>();

        /// <summary>
        /// Programme guide sources: where a schedule is fetched from and how much of it is
        /// kept. A source carries the raw file it last received; the programmes parsed out of
        /// it are rows of their own.
        /// </summary>
        public DbSet<EpgSourceEntity> EpgSources => Set<EpgSourceEntity>();

        /// <summary>
        /// One programme of one guide source, written and deleted by the updater alone.
        /// </summary>
        public DbSet<EpgDataEntity> EpgData => Set<EpgDataEntity>();

        /// <summary>
        /// Bouquets: named packages of content. What each one holds is not on its row - the
        /// four membership sets below are.
        /// </summary>
        public DbSet<BouquetEntity> Bouquets => Set<BouquetEntity>();

        /// <summary>
        /// One video stream a bouquet holds, in the position the bouquet holds it in. The set of
        /// rows one bouquet has is its video list, in order.
        /// </summary>
        public DbSet<BouquetVideoStreamEntity> BouquetVideoStreams => Set<BouquetVideoStreamEntity>();

        /// <summary>
        /// One audio stream a bouquet holds, in the position the bouquet holds it in. Ordered
        /// apart from the video list, which is why it is its own table.
        /// </summary>
        public DbSet<BouquetAudioStreamEntity> BouquetAudioStreams => Set<BouquetAudioStreamEntity>();

        /// <summary>
        /// One on-demand video a bouquet holds, in the position the bouquet holds it in. Ordered
        /// apart from the live kinds, which is why it is its own table.
        /// </summary>
        public DbSet<BouquetVodItemEntity> BouquetVodItems => Set<BouquetVodItemEntity>();

        /// <summary>
        /// One series a bouquet holds, in the position the bouquet holds it in. Ordered apart
        /// from the other three kinds, which is why it is its own table.
        /// </summary>
        public DbSet<BouquetSeriesEntity> BouquetSeries => Set<BouquetSeriesEntity>();

        /// <inheritdoc />
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyAudit();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        /// <inheritdoc />
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ApplyAudit();

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Builds the model from the table configurations that live in this assembly.
        /// <see cref="DbContext.OnModelCreating(DbContextOptionsBuilder)"/> discovers every
        /// <see cref="IEntityTypeConfiguration{TEntity}"/> in the <c>Configurations</c> folder,
        /// so adding a new mapping class is enough to have it applied - no registration here.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(FluxyDbContext).Assembly);

            ApplyAuditableEntityConvention(modelBuilder);
        }

        /// <summary>
        /// Stamps <see cref="AuditableEntity.CreatedAt"/> and
        /// <see cref="AuditableEntity.UpdatedAt"/> on everything that is about to be written.
        /// Both take the same instant, so a row that is inserted and never touched again has
        /// two equal timestamps instead of an <c>UpdatedAt</c> that is a few ticks newer.
        /// </summary>
        private void ApplyAudit()
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Property(nameof(AuditableEntity.CreatedAt)).CurrentValue = now;
                        entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
                        break;

                    case EntityState.Modified:
                        entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
                        break;
                }
            }
        }

        /// <summary>
        /// Enforces the two project rules for entities that do not derive from
        /// <see cref="AuditableEntity"/>: a single column <see cref="Guid"/> primary key, and
        /// a non nullable creation and modification timestamp.
        /// </summary>
        /// <remarks>
        /// The rule is checked instead of documented, so a new entity cannot quietly break the
        /// convention that every other table follows - the model would fail to build with a
        /// message naming the type. The columns themselves still come from the base class;
        /// this method only validates the shape.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A mapped entity does not follow the rules.
        /// </exception>
        private static void ApplyAuditableEntityConvention(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Owned types have no key of their own, and a shared CLR type is a side table
                // of a dictionary rather than a real entity of this project.
                if (entityType.IsOwned() || entityType.HasSharedClrType)
                {
                    continue;
                }

                var key = entityType.FindPrimaryKey();
                if (key is null
                    || key.Properties.Count != 1
                    || key.Properties[0].ClrType != typeof(Guid))
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityType.ClrType.Name}' does not have a single column Guid " +
                        "primary key. Every entity of this project must derive from " +
                        "AuditableEntity, which supplies the identifier.");
                }

                // The key is generated by the base class constructor, so the database must not
                // try to produce a value of its own for it.
                key.Properties[0].ValueGenerated = ValueGenerated.Never;

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType != typeof(DateTimeOffset)
                        || property.Name is not (nameof(AuditableEntity.CreatedAt)
                            or nameof(AuditableEntity.UpdatedAt)))
                    {
                        continue;
                    }

                    if (property.IsNullable)
                    {
                        throw new InvalidOperationException(
                            $"Audit property '{entityType.ClrType.Name}.{property.Name}' is " +
                            "nullable. CreatedAt and UpdatedAt describe a stored row and must " +
                            "always have a value.");
                    }

                    // Assigned by ApplyAudit on every save, never by the database.
                    property.ValueGenerated = ValueGenerated.Never;
                }
            }
        }
    }
}