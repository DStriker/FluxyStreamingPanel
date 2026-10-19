using Fluxy.Core.Abstractions;

namespace Fluxy.Core.Persistence.Interfaces
{
    /// <summary>
    /// Brings the database schema up to the version the application expects.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IUserSeeder"/> because the two are different acts: applying
    /// migrations is infrastructure, while seeding an administrator is a business decision that
    /// happens after the schema exists. The seeder calls this first; nothing else does.
    /// </remarks>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Applies every pending migration. Does nothing when the schema is current.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task MigrateAsync(CancellationToken cancellationToken = default);
    }
}
