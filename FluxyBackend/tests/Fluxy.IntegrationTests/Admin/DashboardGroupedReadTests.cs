using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The four grouped reads the dashboard composes: one query each, counting distinct owners,
    /// and keeping the unattributed connection group.
    /// </summary>
    /// <remarks>
    /// These are read through the Core repository interfaces, the way every other fixture plants
    /// its data. The point of the tests is the shape the endpoint cannot show on its own: that the
    /// groups of a two-server installation add up to the total, and that an account connected
    /// through two servers is one person in the installation figure.
    /// </remarks>
    public sealed class DashboardGroupedReadTests : IntegrationTestBase
    {
        public DashboardGroupedReadTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task LiveServerCountsKeepTheUnattributedGroupAndCountDistinctOwners()
        {
            var issuer = await CreateAdminAndSignInAsync();

            var alpha = await CreateServerAsync("Alpha");
            var beta = await CreateServerAsync("Beta");

            // Two viewers. The first holds two subscriptions and uses both of them through two
            // different servers, the second holds one and is connected through Beta.
            var first = await CreateUserAsync(UserRole.Client, "first");
            var second = await CreateUserAsync(UserRole.Client, "second");

            var firstSubscription = await ArrangeSubscriptionAsync(issuer, first);
            var firstSecond = await ArrangeSubscriptionAsync(issuer, first);
            var secondSubscription = await ArrangeSubscriptionAsync(issuer, second);

            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            await PlantAsync(connection, firstSubscription, stream, alpha, StartedAt(1));
            await PlantAsync(connection, firstSecond, stream, beta, StartedAt(2));
            await PlantAsync(connection, secondSubscription, stream, beta, StartedAt(3));
            await PlantAsync(connection, firstSubscription, stream, serverId: null, StartedAt(4));

            // A finished connection is not live and must appear in no group.
            await PlantAsync(connection, firstSubscription, stream, alpha, StartedAt(5), EndedAt(5));

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var logs = scope.ServiceProvider.GetRequiredService<IConnectionLogRepository>();

            var groups = await logs.ReadLiveServerCountsAsync(TestContext.Current.CancellationToken);

            Assert.Equal(3, groups.Count);
            Assert.Equal(4, groups.Sum(group => group.ConnectionCount));

            var alphaGroup = groups.Single(group => group.ServerId == alpha);
            Assert.Equal(1, alphaGroup.ConnectionCount);
            Assert.Equal(1, alphaGroup.UserCount);

            var betaGroup = groups.Single(group => group.ServerId == beta);
            Assert.Equal(2, betaGroup.ConnectionCount);
            Assert.Equal(2, betaGroup.UserCount);

            var unattributed = groups.Single(group => group.ServerId is null);
            Assert.Equal(1, unattributed.ConnectionCount);
            Assert.Equal(1, unattributed.UserCount);

            // The installation figure counts the two viewers once each, although the first one is
            // connected through two servers.
            var liveUsers = await logs.CountLiveUsersAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, liveUsers);

            _ = first;
            _ = second;
        }

        [Fact]
        public async Task PreferredServerUsersCountDistinctOwnersAndExcludeTheNullColumn()
        {
            var issuer = await CreateAdminAndSignInAsync();
            var server = await CreateServerAsync("Alpha");

            var first = await CreateUserAsync(UserRole.Client, "first");
            await ArrangeSubscriptionAsync(issuer, first, server);
            await ArrangeSubscriptionAsync(issuer, first, server);
            await ArrangeSubscriptionAsync(issuer, "second");

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();

            var rows = await subscriptions.ReadPreferredServerUserCountsAsync(
                TestContext.Current.CancellationToken);

            var row = Assert.Single(rows);
            Assert.Equal(server, row.ServerId);
            Assert.Equal(1, row.UserCount);
        }

        [Fact]
        public async Task StreamStatusCountsGroupByTheStoredStatus()
        {
            await CreateAdminAndSignInAsync();

            await CreateVideoStreamAsync("One", VideoStreamStatus.Running);
            await CreateVideoStreamAsync("Two", VideoStreamStatus.Running);
            await CreateVideoStreamAsync("Three", VideoStreamStatus.Stopped);

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var streams = scope.ServiceProvider.GetRequiredService<IVideoStreamRepository>();

            var rows = await streams.ReadStatusCountsAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, rows.Single(row => row.Status == VideoStreamStatus.Running).Count);
            Assert.Equal(1, rows.Single(row => row.Status == VideoStreamStatus.Stopped).Count);
        }

        [Fact]
        public async Task GetAllReturnsEveryServerOrderedByNameWithTheDisabledRowIncluded()
        {
            await CreateAdminAndSignInAsync();

            await CreateServerAsync("Zulu", status: Core.Servers.Enums.ServerStatus.Disabled);
            await CreateServerAsync("Alpha");

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var servers = scope.ServiceProvider.GetRequiredService<IServerRepository>();

            var rows = await servers.GetAllAsync(TestContext.Current.CancellationToken);

            // Three rows: the two planted and the fixture's seeded main row, which sorts last
            // here only by name - the assertion is on the order, not on the count.
            Assert.Equal(3, rows.Count);
            Assert.Equal(
                rows.Select(row => row.Name).OrderBy(name => name, StringComparer.Ordinal),
                rows.Select(row => row.Name));
            Assert.Contains(rows, row => row.Status == Core.Servers.Enums.ServerStatus.Disabled);
        }

        private async Task<Guid> CreateServerAsync(
            string name,
            Core.Servers.Enums.ServerStatus status = Core.Servers.Enums.ServerStatus.Active)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateServerAsync(
                scope.ServiceProvider,
                name,
                status: status,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        private async Task<Guid> CreateVideoStreamAsync(
            string name,
            VideoStreamStatus status = VideoStreamStatus.Running)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateVideoStreamAsync(
                scope.ServiceProvider,
                name,
                TestContext.Current.CancellationToken,
                status);
        }

        private async Task<(Guid Owner, Guid Subscription)> ArrangeSubscriptionAsync(
            Guid issuer,
            string ownerName = "viewer",
            Guid? preferredServerId = null)
        {
            var owner = await CreateUserAsync(UserRole.Client, ownerName);

            return (owner, await ArrangeSubscriptionAsync(issuer, owner, preferredServerId));
        }

        private async Task<Guid> ArrangeSubscriptionAsync(
            Guid issuer,
            Guid owner,
            Guid? preferredServerId = null)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateSubscriptionAsync(
                scope.ServiceProvider,
                owner,
                issuer,
                TestContext.Current.CancellationToken,
                preferredServerId: preferredServerId);
        }

        private Task<Guid> PlantAsync(
            Npgsql.NpgsqlConnection connection,
            Guid subscriptionId,
            Guid streamId,
            Guid? serverId,
            DateTimeOffset startedAt,
            DateTimeOffset? endedAt = null)
            => TestDataSeeder.PlantConnectionLogAsync(
                connection,
                subscriptionId,
                ConnectionStreamKind.VideoStream,
                streamId,
                startedAt,
                endedAt,
                serverId: serverId,
                cancellationToken: TestContext.Current.CancellationToken);

        private static DateTimeOffset StartedAt(int hour)
            => new(2026, 5, 1, hour, 0, 0, TimeSpan.Zero);

        private static DateTimeOffset EndedAt(int hour) => StartedAt(hour).AddMinutes(30);
    }
}
