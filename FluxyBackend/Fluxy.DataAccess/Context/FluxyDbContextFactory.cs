using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fluxy.DataAccess.Context
{
    /// <summary>
    /// Builds a <see cref="FluxyDbContext"/> for the <c>dotnet ef</c> tooling.
    /// </summary>
    /// <remarks>
    /// Without this factory the tooling would have to start the whole application to reach the
    /// context. The connection string is only needed to pick the provider - no connection is
    /// opened while a migration is scaffolded or applied - so the fallback below never has to
    /// be valid credentials. Set the <c>ConnectionStrings__Postgres</c> environment variable to
    /// use the real one when a migration has to talk to the database, for example with
    /// <c>dotnet ef database update</c>.
    /// </remarks>
    public sealed class FluxyDbContextFactory : IDesignTimeDbContextFactory<FluxyDbContext>
    {
        /// <summary>Configuration key the connection string is read from, in environment variable form.</summary>
        public const string ConnectionStringVariable = "ConnectionStrings__Postgres";

        private const string DesignTimeConnectionString =
            "Host=localhost;Port=5432;Database=fluxy;Username=fluxy;Password=design-time-placeholder";

        /// <inheritdoc />
        public FluxyDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

            var options = new DbContextOptionsBuilder<FluxyDbContext>()
                .UseNpgsql(
                    string.IsNullOrWhiteSpace(connectionString)
                        ? DesignTimeConnectionString
                        : connectionString)
                .Options;

            return new FluxyDbContext(options);
        }
    }
}