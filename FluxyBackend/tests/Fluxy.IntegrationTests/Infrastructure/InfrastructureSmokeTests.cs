using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Infrastructure
{
    public sealed class InfrastructureSmokeTests : IntegrationTestBase
    {
        public InfrastructureSmokeTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task HostUsesTheContainerConnectionStrings()
        {
            var configuration = Fixture.Factory.Services.GetRequiredService<IConfiguration>();

            Assert.Equal(Fixture.PostgresConnectionString, configuration["ConnectionStrings:Postgres"]);
            Assert.Equal(Fixture.RedisConnectionString, configuration["ConnectionStrings:Redis"]);
        }

        [Fact]
        public async Task CsrfEndpointAnswersWithAToken()
        {
            var response = await Client.GetAsync("/auth/csrf", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await GetCsrfTokenAsync();

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task BaseGroupsArePresentAfterReset()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var groups = scope.ServiceProvider.GetRequiredService<IUserGroupRepository>();

            var all = await groups.GetAllAsync(TestContext.Current.CancellationToken);

            Assert.Equal(3, all.Count);
            Assert.All(BaseUserGroups.All, id => Assert.Contains(all, group => group.Id == id));
        }
    }
}
