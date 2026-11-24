using System.Net;
using System.Text.Json;
using Fluxy.Core.Servers.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Fluxy.IntegrationTests.Infrastructure.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The server API: the seeded main row, the wire shape of a server, the name rule, the query
    /// contract, the main-row deletion refusal, the two grants and the CSRF pair.
    /// </summary>
    /// <remarks>
    /// The rows are planted through the API rather than through the repository, because the
    /// controller is what this suite is about: a contract that refused the wrong field or
    /// answered a duplicate with the wrong status would be invisible to a repository test. The
    /// main row is the exception - it is the fixture's seeder that puts it there, and the test
    /// proves exactly that.
    /// </remarks>
    public sealed class ServersTests : IntegrationTestBase
    {
        public ServersTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AServerIsCreatedChangedAndDeleted()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostServerAsync(new
            {
                name = "  Edge one  ",
                ip = "203.0.113.7",
                port = 6200,
                clientCount = 250,
                osName = "Debian 12",
                networkInterface = "eth1",
                status = "active",
                guaranteedSpeedKbps = 12000,
                timeshiftOnly = true,
                hostname = "edge.example.test",
                vpnIp = "10.10.0.2",
                countryCodes = new[] { "de", "AT", "de" }
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "server_created");
            Assert.NotNull(created.Headers.Location);
            var id = IdFromLocation(created);
            Assert.Equal($"/admin/servers/{id}", created.Headers.Location?.ToString());

            using var detail = await Client.GetAsync(
                $"/admin/servers/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal("Edge one", root.GetProperty("name").GetString());
            Assert.Equal("203.0.113.7", root.GetProperty("ip").GetString());
            Assert.Equal(6200, root.GetProperty("port").GetInt32());
            Assert.Equal(250, root.GetProperty("clientCount").GetInt32());
            Assert.Equal("Debian 12", root.GetProperty("osName").GetString());
            Assert.Equal("eth1", root.GetProperty("networkInterface").GetString());
            Assert.Equal("Active", root.GetProperty("status").GetString());
            Assert.Equal(12000, root.GetProperty("guaranteedSpeedKbps").GetInt32());
            Assert.True(root.GetProperty("timeshiftOnly").GetBoolean());
            Assert.Equal("edge.example.test", root.GetProperty("hostname").GetString());
            Assert.Equal("10.10.0.2", root.GetProperty("vpnIp").GetString());
            Assert.Equal(0, root.GetProperty("latency").GetDouble());
            Assert.False(root.GetProperty("isMain").GetBoolean());

            var countries = root.GetProperty("countryCodes")
                .EnumerateArray()
                .Select(element => element.GetString())
                .ToArray();
            Assert.Equal(["DE", "AT"], countries);

            // The full write replaces every field, including the ones the form cleared.
            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/servers/{id}",
                Full(("name", "Renamed"), ("clientCount", 0), ("latency", 42.5),
                    ("status", "Disabled"), ("hostname", null), ("vpnIp", null)),
                csrf);
            await AssertCodeAsync(patched, HttpStatusCode.OK, "server_updated");

            using var afterPatch = await Client.GetAsync(
                $"/admin/servers/{id}",
                TestContext.Current.CancellationToken);
            using var afterPatchBody = await ReadJsonAsync(afterPatch);
            var patchedRoot = afterPatchBody.RootElement;

            Assert.Equal("Renamed", patchedRoot.GetProperty("name").GetString());
            Assert.Equal(0, patchedRoot.GetProperty("clientCount").GetInt32());
            Assert.Equal(42.5, patchedRoot.GetProperty("latency").GetDouble());
            Assert.Equal("Disabled", patchedRoot.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, patchedRoot.GetProperty("hostname").ValueKind);
            Assert.Equal(JsonValueKind.Null, patchedRoot.GetProperty("vpnIp").ValueKind);

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/servers/{id}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "server_deleted");

            using var missing = await Client.GetAsync(
                $"/admin/servers/{id}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "server_not_found");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/servers/{id}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "server_not_found");
        }

        [Fact]
        public async Task ANameIsUniqueForTheInstallation()
        {
            await CreateAdminAndSignInAsync();

            using var first = await PostServerAsync(new { name = "Duplicate" });
            await AssertCodeAsync(first, HttpStatusCode.Created, "server_created");

            using var duplicate = await PostServerAsync(new { name = "Duplicate" });
            await AssertCodeAsync(
                duplicate,
                HttpStatusCode.Conflict,
                "server_already_exists");
            using var duplicateBody = await ReadJsonAsync(duplicate);
            Assert.True(
                duplicateBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            // A change may not take a name another server holds, but keeping its own is fine.
            var id = IdFromLocation(first);

            var csrf = await GetCsrfTokenAsync();
            using var kept = await PatchJsonAsync(
                $"/admin/servers/{id}",
                Full(("name", "Duplicate")),
                csrf);
            await AssertCodeAsync(kept, HttpStatusCode.OK, "server_updated");

            using var other = await PostServerAsync(new { name = "Other" });
            var otherId = IdFromLocation(other);

            csrf = await GetCsrfTokenAsync();
            using var collision = await PatchJsonAsync(
                $"/admin/servers/{otherId}",
                Full(("name", "Duplicate")),
                csrf);
            await AssertCodeAsync(
                collision,
                HttpStatusCode.Conflict,
                "server_already_exists");
        }

        [Fact]
        public async Task EachFieldIsRefusedUnderItsOwnName()
        {
            await CreateAdminAndSignInAsync();

            await AssertRefusalAsync(
                new { name = "   " },
                "name",
                "a blank name");

            await AssertRefusalAsync(
                new { name = new string('a', ServerLimits.NameMaxLength + 1) },
                "name",
                "an oversized name");

            await AssertRefusalAsync(
                new { ip = "example.test" },
                "ip",
                "an address that is not a literal");

            await AssertRefusalAsync(
                new { vpnIp = "not-an-address" },
                "vpnIp",
                "a VPN address that is not a literal");

            await AssertRefusalAsync(
                new { port = 0 },
                "port",
                "a port below the range");

            await AssertRefusalAsync(
                new { port = 70000 },
                "port",
                "a port above the range");

            await AssertRefusalAsync(
                new { clientCount = -1 },
                "clientCount",
                "a negative client ceiling");

            await AssertRefusalAsync(
                new { clientCount = ServerLimits.ClientCountMax + 1 },
                "clientCount",
                "a client ceiling past the cap");

            await AssertRefusalAsync(
                new { status = "paused" },
                "status",
                "a status that names no member");

            await AssertRefusalAsync(
                new { guaranteedSpeedKbps = -1 },
                "guaranteedSpeedKbps",
                "a negative guaranteed speed");

            await AssertRefusalAsync(
                new { osName = "" },
                "osName",
                "a blank operating system");

            await AssertRefusalAsync(
                new { networkInterface = "" },
                "networkInterface",
                "a blank network interface");

            await AssertRefusalAsync(
                new { hostname = new string('a', ServerLimits.HostnameMaxLength + 1) },
                "hostname",
                "an oversized host name");

            await AssertRefusalAsync(
                new { countryCodes = new[] { "ZZ" } },
                "countryCodes",
                "a country that names no country");
        }

        [Fact]
        public async Task TheBoundsOfAWriteAreInclusiveAtBothEnds()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostServerAsync(new
            {
                name = "Bounds",
                port = ServerLimits.PortMax,
                clientCount = 0,
                guaranteedSpeedKbps = 0
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "server_created");

            using var detail = await Client.GetAsync(
                $"/admin/servers/{IdFromLocation(created)}",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(detail);
            Assert.Equal(0, body.RootElement.GetProperty("clientCount").GetInt32());
            Assert.Equal(0, body.RootElement.GetProperty("guaranteedSpeedKbps").GetInt32());
        }

        [Fact]
        public async Task TheListDefaultsToNameAscending()
        {
            await CreateAdminAndSignInAsync();

            await CreateServerAsync("Beta");
            await CreateServerAsync("Alpha");
            await CreateServerAsync("Gamma");

            using var page = await Client.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var body = await ReadJsonAsync(page);
            var root = body.RootElement;

            // The seeded main row is "Main server", which sorts between Gamma and nothing:
            // the three created rows plus one seeded one, ordered by name.
            Assert.Equal(4, root.GetProperty("total").GetInt32());

            var names = root.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("name").GetString())
                .ToArray();

            Assert.Equal(["Alpha", "Beta", "Gamma", "Main server"], names);
        }

        [Fact]
        public async Task SearchCoversTheNameTheHostNameAndTheAddress()
        {
            await CreateAdminAndSignInAsync();

            await CreateServerAsync(
                "Search by name",
                hostname: "analytic.example.test",
                ip: "198.51.100.1");
            await CreateServerAsync(
                "Other",
                hostname: "hidden-host.example.test",
                ip: "198.51.100.2");

            await AssertSearchFinds("Search by name", "Search by name");
            await AssertSearchFinds("hidden-host", "Other");
            await AssertSearchFinds("198.51.100.2", "Other");
        }

        [Fact]
        public async Task TheListSortsByLatencyAndCreatedAt()
        {
            await CreateAdminAndSignInAsync();

            await CreateServerAsync("Slow", latency: 80);
            await CreateServerAsync("Quick", latency: 5);
            await CreateServerAsync("Middle", latency: 40);

            var byLatency = await NamesAsync("/admin/servers?sortBy=latency&sortOrder=asc");
            var byLatencyDesc = await NamesAsync("/admin/servers?sortBy=latency&sortOrder=desc");

            var quickIndex = Array.IndexOf(byLatency, "Quick");
            var middleIndex = Array.IndexOf(byLatency, "Middle");
            var slowIndex = Array.IndexOf(byLatency, "Slow");

            Assert.True(quickIndex < middleIndex, string.Join(',', byLatency));
            Assert.True(middleIndex < slowIndex, string.Join(',', byLatency));
            Assert.True(Array.IndexOf(byLatencyDesc, "Slow") < Array.IndexOf(byLatencyDesc, "Quick"));

            // The fixture's clock is frozen, so the seeded row and the three created ones share
            // an instant; what this proves is that the column is sortable and the answer is
            // stable rather than that the values differ.
            var byCreatedAt = await NamesAsync("/admin/servers?sortBy=createdat&sortOrder=asc");
            Assert.Equal(4, byCreatedAt.Length);
        }

        [Fact]
        public async Task TheBoundsOfTheQueryAreRefusedUnderTheirParameter()
        {
            await CreateAdminAndSignInAsync();

            await AssertQueryRefusedAsync("/admin/servers?page=0", "page");
            await AssertQueryRefusedAsync("/admin/servers?pageSize=0", "pageSize");
            await AssertQueryRefusedAsync("/admin/servers?pageSize=1001", "pageSize");
            await AssertQueryRefusedAsync(
                $"/admin/servers?search={new string('a', ServerListLimits.MaxSearchLength + 1)}",
                "search");
            await AssertQueryRefusedAsync("/admin/servers?sortBy=owner", "sortBy");
            await AssertQueryRefusedAsync("/admin/servers?sortOrder=sideways", "sortOrder");
        }

        [Fact]
        public async Task TheSeededMainRowCarriesTheFakeIdentityAndIsSeededOnce()
        {
            await CreateAdminAndSignInAsync();

            using var page = await Client.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(page);
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

            Assert.Single(items);

            var main = items[0];
            Assert.Equal(MainServer.Id.ToString(), main.GetProperty("id").GetString());
            Assert.Equal(MainServer.DefaultName, main.GetProperty("name").GetString());
            Assert.True(main.GetProperty("isMain").GetBoolean());
            Assert.Equal(FakeServerIdentityProvider.Ip, main.GetProperty("ip").GetString());
            Assert.Equal(FakeServerIdentityProvider.Port, main.GetProperty("port").GetInt32());
            Assert.Equal(
                FakeServerIdentityProvider.OperatingSystem,
                main.GetProperty("osName").GetString());
            Assert.Equal(
                FakeServerIdentityProvider.NetworkInterface,
                main.GetProperty("networkInterface").GetString());
            Assert.Equal(MainServer.DefaultClientCount, main.GetProperty("clientCount").GetInt32());
            Assert.Equal(0, main.GetProperty("latency").GetDouble());
            Assert.Equal(0, main.GetProperty("guaranteedSpeedKbps").GetInt32());
            Assert.False(main.GetProperty("timeshiftOnly").GetBoolean());
            Assert.Equal(JsonValueKind.Null, main.GetProperty("hostname").ValueKind);
            Assert.Equal(JsonValueKind.Null, main.GetProperty("vpnIp").ValueKind);
            Assert.Empty(main.GetProperty("countryCodes").EnumerateArray());

            // A second run of the seeder leaves the row as it is, and adds nothing.
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            await TestDataSeeder.EnsureMainServerAsync(
                scope.ServiceProvider,
                TestContext.Current.CancellationToken);
            await TestDataSeeder.EnsureMainServerAsync(
                scope.ServiceProvider,
                TestContext.Current.CancellationToken);

            using var after = await Client.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            using var afterBody = await ReadJsonAsync(after);
            Assert.Equal(1, afterBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task TheMainRowIsEditableAndCanNotBeDeleted()
        {
            await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            using var renamed = await PatchJsonAsync(
                $"/admin/servers/{MainServer.Id}",
                Full(("name", "Main renamed"), ("port", 6001)),
                csrf);
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "server_updated");

            using var afterRename = await Client.GetAsync(
                $"/admin/servers/{MainServer.Id}",
                TestContext.Current.CancellationToken);
            using var afterRenameBody = await ReadJsonAsync(afterRename);
            Assert.Equal("Main renamed", afterRenameBody.RootElement.GetProperty("name").GetString());
            Assert.Equal(6001, afterRenameBody.RootElement.GetProperty("port").GetInt32());
            Assert.True(afterRenameBody.RootElement.GetProperty("isMain").GetBoolean());

            csrf = await GetCsrfTokenAsync();
            using var refused = await DeleteAsync($"/admin/servers/{MainServer.Id}", csrf);
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "server_immutable");

            // The refused deletion left the row exactly where it was.
            using var stillThere = await Client.GetAsync(
                $"/admin/servers/{MainServer.Id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);

            using var page = await Client.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(1, pageBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task AnUnknownIdentifierAnswersNotFoundOnBothWrites()
        {
            await CreateAdminAndSignInAsync();

            var unknown = Guid.NewGuid();

            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/servers/{unknown}",
                Full(),
                csrf);
            await AssertCodeAsync(patched, HttpStatusCode.NotFound, "server_not_found");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/servers/{unknown}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.NotFound, "server_not_found");
        }

        [Fact]
        public async Task WritesRequireCsrfAndThePermission()
        {
            await CreateAdminAndSignInAsync();

            using var noCsrf = await PostJsonAsync(
                "/admin/servers",
                new { name = "No csrf" });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            // An administrator whose group may run the streams but not read the servers.
            var streamOperators = await CreateGroupAsync(
                "Stream operators",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewStreams,
                UserPermission.EditStreams);

            using var operatorClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "serverguard", groupId: streamOperators);
            await SignInAsync(UserRole.Admin, "serverguard", "Password1", operatorClient);

            using var forbiddenRead = await operatorClient.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(forbiddenRead, HttpStatusCode.Forbidden, "permission_denied");

            var csrf = await GetCsrfTokenAsync(operatorClient);
            using var forbiddenWrite = await PostJsonAsync(
                "/admin/servers",
                new { name = "Forbidden" },
                csrf,
                operatorClient);
            await AssertCodeAsync(forbiddenWrite, HttpStatusCode.Forbidden, "permission_denied");

            // A view-only grant reads and is refused the writes.
            var readers = await CreateGroupAsync(
                "Server readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewServers);

            using var readerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "serverreader", groupId: readers);
            await SignInAsync(UserRole.Admin, "serverreader", "Password1", readerClient);

            using var allowedRead = await readerClient.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

            csrf = await GetCsrfTokenAsync(readerClient);
            using var refusedWrite = await PostJsonAsync(
                "/admin/servers",
                new { name = "Refused" },
                csrf,
                readerClient);
            await AssertCodeAsync(refusedWrite, HttpStatusCode.Forbidden, "permission_denied");

            csrf = await GetCsrfTokenAsync(readerClient);
            using var refusedDelete = await DeleteAsync(
                $"/admin/servers/{MainServer.Id}",
                csrf,
                readerClient);
            await AssertCodeAsync(refusedDelete, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task AServerStillNamedByAStreamOrSubscriptionCanNotBeDeleted()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostServerAsync(new { name = "Referenced" });
            await AssertCodeAsync(created, HttpStatusCode.Created, "server_created");
            var serverId = IdFromLocation(created);

            // A video stream names it as its transcoding server, through the stream surface.
            var categoryId = await CreateCategoryAsync();
            using var stream = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Bound",
                    type = "permanent",
                    categoryId,
                    sources = new[] { "https://example.test/bound" },
                    transcodingServerId = serverId
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(stream, HttpStatusCode.Created, "video_stream_created");

            using var refused = await DeleteAsync(
                $"/admin/servers/{serverId}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "server_in_use");

            // The row survived the refusal.
            using var stillThere = await Client.GetAsync(
                $"/admin/servers/{serverId}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
        }

        [Fact]
        public async Task TheOptionsEndpointIsTheAnyOfTheTwoFormGrants()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostServerAsync(new { name = "Zeta", timeshiftOnly = true });
            await AssertCodeAsync(created, HttpStatusCode.Created, "server_created");

            using var main = await Client.GetAsync(
                "/admin/servers",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, main.StatusCode);

            // Every row, ordered by name, each carrying the three picker facts. The seeded main
            // row sorts before "Zeta" and is included.
            using var options = await Client.GetAsync(
                "/admin/servers/options",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, options.StatusCode);

            using var body = await ReadJsonAsync(options);
            var items = body.RootElement.EnumerateArray().ToList();
            Assert.Equal(2, items.Count);

            Assert.Equal(
                new[] { MainServer.DefaultName, "Zeta" },
                items.Select(item => item.GetProperty("name").GetString()).ToArray());
            Assert.Equal(MainServer.Id.ToString(), items[0].GetProperty("id").GetString());
            Assert.False(items[0].GetProperty("timeshiftOnly").GetBoolean());
            Assert.True(items[1].GetProperty("timeshiftOnly").GetBoolean());

            // A subscriptions-only operator may fill the subscription form's picker.
            var subscriptions = await CreateGroupAsync(
                "Subscription readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions);
            using var subscriptionsClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "subreader", groupId: subscriptions);
            await SignInAsync(UserRole.Admin, "subreader", "Password1", subscriptionsClient);
            using var allowedBySubscriptions = await subscriptionsClient.GetAsync(
                "/admin/servers/options",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedBySubscriptions.StatusCode);

            // As may a streams-only operator, for the stream form.
            var streams = await CreateGroupAsync(
                "Stream readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewStreams);
            using var streamsClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "streamreader", groupId: streams);
            await SignInAsync(UserRole.Admin, "streamreader", "Password1", streamsClient);
            using var allowedByStreams = await streamsClient.GetAsync(
                "/admin/servers/options",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedByStreams.StatusCode);

            // An operator who may browse the servers but neither form is refused: `viewServers`
            // deliberately does not admit the picker.
            var serversOnly = await CreateGroupAsync(
                "Server readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewServers);
            using var serversOnlyClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "serversreader", groupId: serversOnly);
            await SignInAsync(UserRole.Admin, "serversreader", "Password1", serversOnlyClient);
            using var refused = await serversOnlyClient.GetAsync(
                "/admin/servers/options",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(refused, HttpStatusCode.Forbidden, "permission_denied");
        }

        /// <summary>Creates a category of video streams through its own surface.</summary>
        private async Task<string> CreateCategoryAsync()
        {
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name = "Referenced " + Guid.NewGuid().ToString("N")[..8], type = "VideoStream" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        /// <summary>A valid create with only the name the caller cares about.</summary>
        private static object Full(params (string Key, object? Value)[] overrides)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "Renamed",
                ["ip"] = "203.0.113.7",
                ["port"] = 6200,
                ["clientCount"] = 250,
                ["osName"] = "Debian 12",
                ["networkInterface"] = "eth1",
                ["latency"] = 0,
                ["status"] = "active",
                ["guaranteedSpeedKbps"] = 12000,
                ["timeshiftOnly"] = true,
                ["hostname"] = "edge.example.test",
                ["vpnIp"] = "10.10.0.2",
                ["countryCodes"] = new[] { "DE" }
            };

            foreach (var (key, value) in overrides)
            {
                body[key] = value;
            }

            return body;
        }

        private async Task<HttpResponseMessage> PostServerAsync(object overrides)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "Edge " + Guid.NewGuid().ToString("N")[..8],
                ["ip"] = "203.0.113.7",
                ["port"] = 6200,
                ["osName"] = "Debian 12",
                ["networkInterface"] = "eth0"
            };

            foreach (var property in overrides.GetType().GetProperties())
            {
                var value = property.GetValue(overrides);

                // A named property always replaces the default, including the nulls a test uses
                // to clear a field - that is what makes an absent-and-null distinction visible.
                body[property.Name] = value;
            }

            return await PostJsonAsync(
                "/admin/servers",
                body,
                await GetCsrfTokenAsync());
        }

        private async Task<string> CreateServerAsync(
            string name,
            string? hostname = null,
            string? ip = null,
            double? latency = null)
        {
            using var created = await PostJsonAsync(
                "/admin/servers",
                new
                {
                    name,
                    ip = ip ?? "203.0.113.7",
                    port = 6200,
                    osName = "Debian 12",
                    networkInterface = "eth0",
                    hostname
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(created, HttpStatusCode.Created, "server_created");

            var id = IdFromLocation(created);

            // Latency is deliberately not a create field: a new row starts unmeasured, and the
            // only way it acquires a value is a full write. So a fact that needs one writes it.
            if (latency is not null)
            {
                using var patched = await PatchJsonAsync(
                    $"/admin/servers/{id}",
                    Full(("name", name), ("hostname", hostname), ("ip", ip ?? "203.0.113.7"),
                        ("latency", latency.Value)),
                    await GetCsrfTokenAsync());

                await AssertCodeAsync(patched, HttpStatusCode.OK, "server_updated");
            }

            return id;
        }

        private async Task AssertRefusalAsync(object overrides, string field, string because)
        {
            using var response = await PostServerAsync(overrides);

            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"{because} answered {(int)response.StatusCode}");

            using var body = await ReadJsonAsync(response);
            Assert.Equal("validation_failed", CodeOf(body));
            Assert.True(
                body.RootElement.GetProperty("errors").TryGetProperty(field, out _),
                $"{because} was not reported under '{field}'.");
        }

        private async Task AssertQueryRefusedAsync(string path, string parameter)
        {
            using var response = await Client.GetAsync(
                path,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("validation_failed", CodeOf(body));
            Assert.True(
                body.RootElement.GetProperty("errors").TryGetProperty(parameter, out _),
                $"{path} did not report '{parameter}'.");
        }

        private async Task AssertSearchFinds(string needle, string expectedName)
        {
            var names = await NamesAsync($"/admin/servers?search={Uri.EscapeDataString(needle)}");

            Assert.Contains(expectedName, names);
        }

        private async Task<string?[]> NamesAsync(string path)
        {
            using var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);

            return body.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("name").GetString())
                .ToArray();
        }
    }
}
