using System.Net;
using System.Text.Json;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Utils;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Fluxy.IntegrationTests.Infrastructure.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The dashboard endpoint: its gate, its wire shape, the per-server attribution, the
    /// unavailable rows, the cache and the degraded redis configuration.
    /// </summary>
    /// <remarks>
    /// The probe is the suite's fake, so "did not answer" is something a test declares rather than
    /// something it waits for. The rows the dashboard reads are planted through the Core
    /// repositories and the documented connection-log SQL fixture, the same way the log suite does
    /// its own.
    /// <para>
    /// <b>The fake replaces the whole unkeyed <c>IServerStatsProbe</c> chain, so every row -
    /// the main one included - is the fake's answer in these tests.</b> The decorator and the
    /// local sampler are exercised by the one test that asks for a factory built with
    /// <c>keepLocalMainProbe</c>, where the keyed remote stays the fake and the unkeyed
    /// registration stays the real decorator. That split is deliberate: it keeps every existing
    /// test about composition and cache behaviour, and confines the local reading to the test that
    /// is about it.
    /// </para>    /// </remarks>
    public sealed class DashboardTests : IntegrationTestBase
    {
        public DashboardTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AnAdminHoldingAllThreeGrantsReadsOneRowPerServerAndNoStore()
        {
            await CreateAdminAndSignInAsync();

            var server = await CreateServerAsync("Edge one");
            Fixture.Factory.StatsProbe.SetResult(server, new ServerStatsSample
            {
                CpuPercent = 12.5,
                RamPercent = 44.25,
                DiskPercent = 61,
                UptimeSeconds = 864000,
                NetworkInMbps = 120.5,
                NetworkOutMbps = 90.25
            });

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;

            Assert.True(root.TryGetProperty("kpis", out _));
            Assert.True(root.TryGetProperty("serversUnavailable", out _));
            Assert.True(root.TryGetProperty("generatedAt", out _));

            var servers = root.GetProperty("servers");
            Assert.Equal(2, servers.GetArrayLength());

            var row = servers
                .EnumerateArray()
                .Single(entry => entry.GetProperty("name").GetString() == "Edge one");

            Assert.Equal(server, Guid.Parse(row.GetProperty("id").GetString()!));
            Assert.Equal("Active", row.GetProperty("status").GetString());
            Assert.True(row.GetProperty("available").GetBoolean());
            Assert.Equal(12.5, row.GetProperty("cpuPercent").GetDouble());
            Assert.Equal(44.25, row.GetProperty("ramPercent").GetDouble());
            Assert.Equal(61, row.GetProperty("diskPercent").GetDouble());
            Assert.Equal(864000, row.GetProperty("uptimeSeconds").GetInt64());
            Assert.Equal(120.5, row.GetProperty("networkInMbps").GetDouble());
            Assert.Equal(90.25, row.GetProperty("networkOutMbps").GetDouble());
        }

        [Fact]
        public async Task AnAdminMissingOneOfTheThreeGrantsIsRefused()
        {
            // Subscriptions and streams but deliberately no `viewServers`.
            var group = await CreateGroupAsync(
                "Dashboard without servers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions,
                UserPermission.ViewStreams);

            using var client = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "dashguard", groupId: group);
            await SignInAsync(UserRole.Admin, "dashguard", "Password1", client);

            using var response = await client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task AnAnonymousCallerIsSentToSignIn()
        {
            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ASignedInClientIsRefusedByTheRoleGate()
        {
            await CreateUserAsync(UserRole.Client, "visitor");
            await SignInAsync(UserRole.Client, "visitor", "Password1");

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);

            // The class-level Admin policy refuses a client session. The envelope names the role
            // change rather than the permission, because the caller never reaches a permission
            // handler at all - the existing pipeline's answer, pinned rather than restated.
            await AssertCodeAsync(response, HttpStatusCode.Forbidden, "auth_role_changed");
        }

        [Fact]
        public async Task TwoFailingProbesAreCountedAndTheirNetworkRatesExcluded()
        {
            await CreateAdminAndSignInAsync();

            // Every server answers except the two planted ones, which are left without a declared
            // sample - the fake answers that as "did not answer".
            Fixture.Factory.StatsProbe.Result = new ServerStatsSample
            {
                CpuPercent = 10,
                RamPercent = 20,
                DiskPercent = 30,
                UptimeSeconds = 40,
                NetworkInMbps = 50,
                NetworkOutMbps = 60
            };

            var first = await CreateServerAsync("Edge one");
            var second = await CreateServerAsync("Edge two");

            Fixture.Factory.StatsProbe.SetResult(first, null);
            Fixture.Factory.StatsProbe.SetResult(second, null);

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;

            // Three rows, two unavailable: the main row answered with the default sample, and the
            // two planted rows did not. The fake replaces the whole probe chain, so the main row is
            // its answer too - the local decorator is not in this factory.
            Assert.Equal(3, root.GetProperty("servers").GetArrayLength());
            Assert.Equal(2, root.GetProperty("serversUnavailable").GetInt32());

            // Exactly one server's rates reach the total, not two and not three.
            Assert.Equal(50, root.GetProperty("kpis").GetProperty("networkInMbps").GetDouble());
            Assert.Equal(60, root.GetProperty("kpis").GetProperty("networkOutMbps").GetDouble());
        }

        [Fact]
        public async Task TheSecondImmediateRequestPerformsNoExtraProbes()
        {
            await CreateAdminAndSignInAsync();

            using var first = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var afterFirst = Fixture.Factory.StatsProbe.CallCount;
            Assert.True(afterFirst > 0);

            using var second = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);

            Assert.Equal(afterFirst, Fixture.Factory.StatsProbe.CallCount);
        }

        [Fact]
        public async Task PerServerCountsAttributeByTheRealServerIdAndLeaveTheNullGroupOut()
        {
            var issuer = await CreateAdminAndSignInAsync();

            var alpha = await CreateServerAsync("Alpha");
            var beta = await CreateServerAsync("Beta");

            var (userId, subscription) = await ArrangeSubscriptionAsync(issuer);
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            // Two live connections for one account on Alpha: two connections, one viewer.
            await PlantAsync(connection, subscription, stream, alpha, startedAt: StartedAt(1));
            await PlantAsync(connection, subscription, stream, alpha, startedAt: StartedAt(2));

            // One live connection on Beta by a second account.
            var (_, otherSubscription) = await ArrangeSubscriptionAsync(issuer, ownerName: "second");
            await PlantAsync(connection, otherSubscription, stream, beta, startedAt: StartedAt(3));

            // One live connection nobody attributed: it counts in the installation total and in
            // no server row.
            await PlantAsync(connection, subscription, stream, serverId: null, startedAt: StartedAt(4));

            // A finished connection must not count at all.
            await PlantAsync(connection, subscription, stream, alpha, startedAt: StartedAt(5), endedAt: EndedAt(5));

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;

            var servers = root.GetProperty("servers").EnumerateArray().ToList();

            var alphaRow = servers.Single(row => row.GetProperty("id").GetString() == alpha.ToString());
            Assert.Equal(2, alphaRow.GetProperty("activeConnections").GetInt32());
            Assert.Equal(1, alphaRow.GetProperty("usersWithActiveConnections").GetInt32());

            var betaRow = servers.Single(row => row.GetProperty("id").GetString() == beta.ToString());
            Assert.Equal(1, betaRow.GetProperty("activeConnections").GetInt32());
            Assert.Equal(1, betaRow.GetProperty("usersWithActiveConnections").GetInt32());

            var mainRow = servers.Single(row => row.GetProperty("name").GetString() != "Alpha"
                && row.GetProperty("name").GetString() != "Beta");
            Assert.Equal(0, mainRow.GetProperty("activeConnections").GetInt32());
            Assert.Equal(0, mainRow.GetProperty("usersWithActiveConnections").GetInt32());

            // Four live connections, three distinct accounts: Alpha's owner, Beta's owner and the
            // unattributed one's owner (the same as Alpha's, so actually three distinct).
            Assert.Equal(4, root.GetProperty("kpis").GetProperty("activeConnections").GetInt32());
            Assert.Equal(2, root.GetProperty("kpis").GetProperty("usersWithActiveConnections").GetInt32());

            _ = userId;
        }

        [Fact]
        public async Task PreferredServerUsersAreDistinctAccountsAndUnnamedSubscriptionsCountNowhere()
        {
            var issuer = await CreateAdminAndSignInAsync();
            var server = await CreateServerAsync("Alpha");

            // Two single-subscription accounts.
            await ArrangeSubscriptionAsync(issuer, "viewer", preferredServerId: server);
            await ArrangeSubscriptionAsync(issuer, "second", preferredServerId: server);

            // A third account with two subscriptions on the same server counts once.
            var third = await CreateUserAsync(UserRole.Client, "third");
            await ArrangeSubscriptionAsync(issuer, third, preferredServerId: server);
            await ArrangeSubscriptionAsync(issuer, third, preferredServerId: server);

            // A subscription naming no server must not appear anywhere.
            await ArrangeSubscriptionAsync(issuer, "unplaced");

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);

            var row = body.RootElement.GetProperty("servers").EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == server.ToString());

            Assert.Equal(3, row.GetProperty("preferredServerUsers").GetInt32());
        }

        [Fact]
        public async Task TheStreamFiguresCountRunningAndStoppedByTheirStoredStatus()
        {
            await CreateAdminAndSignInAsync();

            await CreateVideoStreamAsync("One", VideoStreamStatus.Running);
            await CreateVideoStreamAsync("Two", VideoStreamStatus.Running);
            await CreateVideoStreamAsync("Three", VideoStreamStatus.Stopped);

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var kpis = body.RootElement.GetProperty("kpis");

            Assert.Equal(2, kpis.GetProperty("streamsRunning").GetInt32());
            Assert.Equal(1, kpis.GetProperty("streamsStopped").GetInt32());
        }

        [Fact]
        public async Task ADisabledServerIsAnsweredAsARow()
        {
            await CreateAdminAndSignInAsync();
            var disabled = await CreateServerAsync("Retired edge", status: Core.Servers.Enums.ServerStatus.Disabled);

            Fixture.Factory.StatsProbe.SetResult(disabled, new ServerStatsSample
            {
                CpuPercent = 1,
                RamPercent = 2,
                DiskPercent = 3,
                UptimeSeconds = 4,
                NetworkInMbps = 5,
                NetworkOutMbps = 6
            });

            using var response = await Client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);

            var row = body.RootElement.GetProperty("servers").EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == disabled.ToString());

            Assert.Equal("Disabled", row.GetProperty("status").GetString());
            Assert.True(row.GetProperty("available").GetBoolean());
        }

        /// <summary>
        /// A factory pointed at no redis at all: the dashboard still answers 200, built directly.
        /// </summary>
        [Fact]
        public async Task AFactoryWithoutRedisStillAnswers()
        {
            using var factory = new FluxyApiFactory(
                Fixture.PostgresConnectionString,
                redisConnectionString: string.Empty,
                Fixture.Email,
                Fixture.Recaptcha,
                Fixture.GeoIp,
                Fixture.Clock);

            using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            // The dashboard needs a signed-in admin, so the caller is planted through the factory's
            // own provider rather than the fixture's.
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await TestDataSeeder.CreateUserAsync(
                    scope.ServiceProvider,
                    "redisless",
                    "Password1",
                    UserRole.Admin,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            string? csrf;

            using (var csrfResponse = await client.GetAsync(
                "/auth/csrf",
                TestContext.Current.CancellationToken))
            {
                csrfResponse.EnsureSuccessStatusCode();

                using var csrfBody = JsonDocument.Parse(
                    await csrfResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

                csrf = csrfBody.RootElement.GetProperty("token").GetString();
            }

            // Sign in through the same client: the factory's host is separate from the fixture's,
            // so its cookies come from its own login.
            using (var login = await PostJsonAsync(
                "/auth/admin-login",
                new { username = "redisless", password = "Password1" },
                csrf,
                client))
            {
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            }

            using var response = await client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// The main row is measured in-process from the very first request, and a non-main row the
        /// remote cannot reach is still unavailable with zeros.
        /// </summary>
        /// <remarks>
        /// The factory keeps the real decorator and the real sampler (over a deterministic reader)
        /// but replaces the keyed remote with the suite's fake. That is the only way to prove the
        /// split: the main row carries the local figures even though the fake would have answered
        /// something else, and the planted row carries the fake's declared silence. Nothing primes
        /// the sampler here: the cold start must answer on its own, which is the property the first
        /// request after a restart depends on.
        /// </remarks>
        [Fact]
        public async Task TheMainRowIsMeasuredLocallyFromTheFirstRequestWhileAnUnreachableServerStaysUnavailable()
        {
            using var factory = new FluxyApiFactory(
                Fixture.PostgresConnectionString,
                Fixture.RedisConnectionString,
                Fixture.Email,
                Fixture.Recaptcha,
                Fixture.GeoIp,
                Fixture.Clock,
                keepLocalMainProbe: true);

            using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await TestDataSeeder.CreateUserAsync(
                    scope.ServiceProvider,
                    "localprobe",
                    "Password1",
                    UserRole.Admin,
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            // A non-main row whose remote answer is declared silence.
            Guid unreachable;

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                unreachable = await TestDataSeeder.CreateServerAsync(
                    scope.ServiceProvider,
                    "Unreachable edge",
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            // If the main row were delegated, this sample would be what it showed.
            factory.StatsProbe.Result = new ServerStatsSample
            {
                CpuPercent = 77,
                RamPercent = 88,
                DiskPercent = 99,
                UptimeSeconds = 111,
                NetworkInMbps = 222,
                NetworkOutMbps = 333
            };

            factory.StatsProbe.SetResult(unreachable, null);

            var csrf = await GetCsrfTokenAsync(client);

            using (var login = await PostJsonAsync(
                "/auth/admin-login",
                new { username = "localprobe", password = "Password1" },
                csrf,
                client))
            {
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            }

            using var response = await client.GetAsync(
                "/admin/dashboard",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var servers = body.RootElement.GetProperty("servers").EnumerateArray().ToList();

            // The main row: the sampler's figures on the very first request, not the fake's.
            var main = servers.Single(row => row.GetProperty("id").GetString() == MainServer.Id.ToString());
            Assert.True(main.GetProperty("available").GetBoolean());
            Assert.Equal(10, main.GetProperty("ramPercent").GetDouble());
            Assert.Equal(50, main.GetProperty("diskPercent").GetDouble());
            Assert.Equal(900, main.GetProperty("uptimeSeconds").GetInt64());

            // One 125 kB step per reading over the cold 250 ms window is 4 Mbps.
            Assert.Equal(4, main.GetProperty("networkInMbps").GetDouble(), precision: 6);
            Assert.Equal(4, main.GetProperty("networkOutMbps").GetDouble(), precision: 6);
            Assert.InRange(main.GetProperty("cpuPercent").GetDouble(), 0, 100);

            // The non-main row the remote could not reach: unavailable with zeros, exactly as
            // before this change.
            var missing = servers.Single(row => row.GetProperty("id").GetString() == unreachable.ToString());
            Assert.False(missing.GetProperty("available").GetBoolean());
            Assert.Equal(0, missing.GetProperty("cpuPercent").GetDouble());
            Assert.Equal(0, missing.GetProperty("ramPercent").GetDouble());
            Assert.Equal(0, missing.GetProperty("diskPercent").GetDouble());
            Assert.Equal(0, missing.GetProperty("uptimeSeconds").GetInt64());
            Assert.Equal(0, missing.GetProperty("networkInMbps").GetDouble());
            Assert.Equal(0, missing.GetProperty("networkOutMbps").GetDouble());
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
