using Fluxy.Core.Persistence.Interfaces;
using Fluxy.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Infrastructure
{
    /// <summary>
    /// Applies the migrations through the context the application already uses.
    /// </summary>
    /// <remarks>
    /// The database is brought up to date by the same connection the repositories use, rather
    /// than by a second configuration the host would have to keep in step. This is called once
    /// at startup, before the host begins serving.
    /// </remarks>
    internal sealed class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class.
        /// </summary>
        /// <param name="context">Context whose database is migrated.</param>
        public DatabaseInitializer(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task MigrateAsync(CancellationToken cancellationToken = default)
            => _context.Database.MigrateAsync(cancellationToken);
    }
}
