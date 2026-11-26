using System.Net;
using System.Text.Json;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Subscriptions
{
    /// <summary>
    /// The connection log's new server filter and the two new fields it resolves: the identifier
    /// and the server's name.
    /// </summary>
    /// <remarks>
    /// The rules the screen cannot show from its own state: a malformed identifier is refused
    /// under its own parameter, a well-formed identifier naming nothing answers an empty page,
    /// and a row with no server survives the unfiltered list and the live count.
    /// </remarks>
    public sealed class ConnectionLogServerFilterTests : IntegrationTestBase
    {
        public ConnectionLogServerFilterTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AMalformedServerIdIsRefusedUnderItsOwnParameter()
        {
            await ArrangeAsync();

            using var response = await Client.GetAsync(
                "/admin/connection-logs?serverId=banana",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("validation_failed", CodeOf(body));

            var errors = body.RootElement.GetProperty("errors");
            Assert.True(errors.TryGetProperty("serverId", out _));
        }

        [Fact]
        public async Task AnUnknownServerIdentifierAnswersAnEmptyPage()
        {
            await ArrangeAsync();

            using var response = await Client.GetAsync(
                $"/admin/connection-logs?serverId={Guid.NewGuid()}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal(0, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(0, body.RootElement.GetProperty("items").GetArrayLength());
        }

        [Fact]
        public async Task TheNameIsResolvedAndAnUnattributedRowStaysInTheUnfilteredListAndTheLiveCount()
        {
            var (_, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");
            var server = await CreateServerAsync("Alpha");

            await using var connection = await Fixture.OpenConnectionAsync();

            var attributed = await TestDataSeeder.PlantConnectionLogAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1),
                serverId: server,
                cancellationToken: TestContext.Current.CancellationToken);

            var unattributed = await TestDataSeeder.PlantConnectionLogAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(2),
                serverId: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // The unfiltered list carries both, the attributed row names its server, and the
            // unattributed one answers null for both fields.
            using (var all = await Client.GetAsync(
                "/admin/connection-logs?state=live",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, all.StatusCode);

                using var body = await ReadJsonAsync(all);
                Assert.Equal(2, body.RootElement.GetProperty("total").GetInt32());

                var rows = body.RootElement.GetProperty("items").EnumerateArray().ToList();

                var named = rows.Single(row =>
                    Guid.Parse(row.GetProperty("id").GetString()!) == attributed);
                Assert.Equal(server, Guid.Parse(named.GetProperty("serverId").GetString()!));
                Assert.Equal("Alpha", named.GetProperty("serverName").GetString());

                var anonymous = rows.Single(row =>
                    Guid.Parse(row.GetProperty("id").GetString()!) == unattributed);
                Assert.Equal(JsonValueKind.Null, anonymous.GetProperty("serverId").ValueKind);
                Assert.Equal(JsonValueKind.Null, anonymous.GetProperty("serverName").ValueKind);
            }

            // The server filter keeps only the attributed row; the unattributed one is not a row
            // of any server.
            using (var filtered = await Client.GetAsync(
                $"/admin/connection-logs?state=live&serverId={server}",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);

                using var body = await ReadJsonAsync(filtered);
                Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());

                var row = body.RootElement.GetProperty("items")[0];
                Assert.Equal(attributed, Guid.Parse(row.GetProperty("id").GetString()!));
            }
        }

        private async Task<(Guid Owner, Guid Subscription)> ArrangeAsync()
        {
            var issuer = await CreateAdminAndSignInAsync();
            var owner = await CreateUserAsync(UserRole.Client, "owner");

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var subscription = await TestDataSeeder.CreateSubscriptionAsync(
                scope.ServiceProvider,
                owner,
                issuer,
                TestContext.Current.CancellationToken);

            return (owner, subscription);
        }

        private async Task<Guid> CreateVideoStreamAsync(string name)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateVideoStreamAsync(
                scope.ServiceProvider,
                name,
                TestContext.Current.CancellationToken);
        }

        private async Task<Guid> CreateServerAsync(string name)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateServerAsync(
                scope.ServiceProvider,
                name,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        private static DateTimeOffset StartedAt(int hour)
            => new(2026, 5, 1, hour, 0, 0, TimeSpan.Zero);
    }
}
