using Microsoft.EntityFrameworkCore;

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
        }
    }
}
