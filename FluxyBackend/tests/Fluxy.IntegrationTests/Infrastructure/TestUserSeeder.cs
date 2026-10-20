using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// The seeder the test host uses instead of <c>DefaultUserSeeder</c>: schema first, then the
    /// three base groups, and no administrator with a random password.
    /// </summary>
    public sealed class TestUserSeeder : IUserSeeder
    {
        private readonly IDatabaseInitializer _database;
        private readonly IUserGroupRepository _groups;
        private readonly IUnitOfWork _unitOfWork;

        public TestUserSeeder(
            IDatabaseInitializer database,
            IUserGroupRepository groups,
            IUnitOfWork unitOfWork)
        {
            _database = database;
            _groups = groups;
            _unitOfWork = unitOfWork;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await _database.MigrateAsync(cancellationToken);
            await TestDataSeeder.EnsureBaseGroupsAsync(_groups, _unitOfWork, cancellationToken);
        }
    }
}
