using Fluxy.Core.Abstractions;
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
    public class FluxyDbContext : DbContext
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
        /// Issued refresh tokens. One row per token ever issued, kept after it is spent so that
        /// presenting it twice is recognisable as such.
        /// </summary>
        public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

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