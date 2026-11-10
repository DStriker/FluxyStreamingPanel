using System.Globalization;
using System.Net;
using System.Text.Json;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Requests.Commands;
using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The API request failure log end to end: the read surface, every filter and refusal, the
    /// whole-table clear, the permission gate, and the write fence - the collection answers
    /// <c>GET</c> and <c>DELETE</c> only, so <c>POST</c>/<c>PUT</c>/<c>PATCH</c> are 405
    /// <c>method_not_allowed</c> and write nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rows are planted through the Core repository.</b> This capability has no production write
    /// path, so there is no endpoint a test could call to create a row - and the persistence types
    /// are not reachable from this project. The rows therefore go through
    /// <see cref="TestDataSeeder.PlantApiRequestFailuresAsync"/>, which uses the same insert intent
    /// the Development seeder uses, and the assertions go through the API.
    /// </para>
    /// <para>
    /// <b>One save stamps one instant.</b> That is what makes the same-instant tie-break test
    /// possible: three rows planted in one call share <c>created_at</c>, so the only thing that can
    /// order them is the identifier.
    /// </para>
    /// </remarks>
    public sealed class ApiRequestFailureTests : IntegrationTestBase
    {
        private const string Password = "Password1";

        public ApiRequestFailureTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ThePageDefaultsToNewestFirstWithAnIdentifierTieBreak()
        {
            await CreateAdminAndSignInAsync();

            // Older first, then the clock moves, so the default order has something to separate.
            // The step stays inside the token validation's 30 s clock skew: the fake clock is
            // shared by the collection, and a jump a token's `nbf` cannot clear would turn every
            // later sign-in into a 401 rather than testing this page.
            await PlantAsync(Row(ApiRequestFailureReason.AuthFailed, "older refusal"));
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(ApiRequestFailureReason.UserBlocked, "newer refusal"));

            using var response = await GetPageAsync(string.Empty);
            response.EnsureSuccessStatusCode();

            // A page about the installation's refusals is not something an intermediary may keep.
            Assert.True(response.Headers.CacheControl?.NoStore);

            using var body = await ReadJsonAsync(response);

            Assert.Equal(2, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
            Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());

            var rows = Rows(body);
            Assert.Equal("newer refusal", rows[0].GetProperty("description").GetString());
            Assert.Equal("older refusal", rows[1].GetProperty("description").GetString());

            // Same instant for every planted row: the identifier decides, descending.
            await ClearAsync();

            await PlantAsync(
                Row(ApiRequestFailureReason.AuthFailed, "same instant one"),
                Row(ApiRequestFailureReason.AuthFailed, "same instant two"),
                Row(ApiRequestFailureReason.AuthFailed, "same instant three"));

            var seen = new List<Guid>();

            for (var page = 1; page <= 3; page++)
            {
                using var turn = await ReadPageAsync($"page={page}&pageSize=1");
                Assert.Equal(3, turn.RootElement.GetProperty("total").GetInt32());
                seen.AddRange(Ids(turn.RootElement.GetProperty("items")));
            }

            Assert.Equal(3, seen.Count);
            Assert.Equal(3, seen.Distinct().Count());

            // PostgreSQL compares a uuid byte-wise, which the canonical text form spells out in
            // order - so ordering the text is the database's own descending tie-break, while
            // Guid.CompareTo follows the CLR field order and would disagree on random values.
            Assert.Equal(
                seen
                    .Select(id => id.ToString())
                    .OrderByDescending(text => text, StringComparer.Ordinal)
                    .ToArray(),
                seen.Select(id => id.ToString()).ToArray());

            // A page past the end reports the true total and no rows.
            using var beyond = await ReadPageAsync("page=9&pageSize=2");
            Assert.Equal(3, beyond.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(beyond.RootElement.GetProperty("items").EnumerateArray());
        }

        [Fact]
        public async Task EveryBrokenParameterIsRefusedUnderItsOwnName()
        {
            await CreateAdminAndSignInAsync();

            await AssertRefusedAsync("page=0", "page");
            await AssertRefusedAsync("pageSize=1001", "pageSize");
            await AssertRefusedAsync($"search={new string('x', 201)}", "search");
            await AssertRefusedAsync("reasons=nope", "reasons");
            await AssertRefusedAsync($"ip={new string('1', 46)}", "ip");
            await AssertRefusedAsync("country=XX", "country");
            await AssertRefusedAsync($"isp={new string('i', 256)}", "isp");
            await AssertRefusedAsync("sortBy=description", "sortBy");
            await AssertRefusedAsync("sortOrder=upwards", "sortOrder");
            await AssertRefusedAsync("dateFrom=yesterday", "dateFrom");
            await AssertRefusedAsync("dateTo=05/01/2026", "dateTo");
        }

        [Fact]
        public async Task EveryReasonWireNameIsAcceptedInAnyCaseAndTheFilterComposes()
        {
            await CreateAdminAndSignInAsync();

            await PlantAsync(
                Row(ApiRequestFailureReason.UnknownId, "unknown identifier"),
                Row(ApiRequestFailureReason.GeoIpViolation, "geo violation"),
                Row(ApiRequestFailureReason.AuthFailed, "auth failed"),
                Row(ApiRequestFailureReason.TooManyConnections, "too many connections"),
                Row(ApiRequestFailureReason.SubscriptionExpired, "subscription expired"),
                Row(ApiRequestFailureReason.UserBlocked, "user blocked"));

            // Each wire name, spelled in a different case, selects exactly its own row.
            foreach (var (wire, description) in new[]
            {
                ("unknown_id", "unknown identifier"),
                ("GEO_IP_VIOLATION", "geo violation"),
                ("Auth_Failed", "auth failed"),
                ("too_many_connections", "too many connections"),
                ("subscription_expired", "subscription expired"),
                ("USER_BLOCKED", "user blocked")
            })
            {
                using var body = await ReadPageAsync($"reasons={Uri.EscapeDataString(wire)}");

                Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(description, SingleRow(body).GetProperty("description").GetString());
            }

            // Duplicates are deduplicated, so the same filter sent twice answers the same set.
            using var duplicated = await ReadPageAsync(
                "reasons=auth_failed&reasons=AUTH_FAILED&reasons=user_blocked");
            Assert.Equal(2, duplicated.RootElement.GetProperty("total").GetInt32());

            // The second sortable column is the reason itself; ascending follows the stored
            // smallint, which is the declaration order of the vocabulary.
            using (var byReason = await ReadPageAsync(
                "reasons=user_blocked&reasons=auth_failed&sortBy=reason&sortOrder=asc"))
            {
                var reasons = Rows(byReason)
                    .Select(row => row.GetProperty("reason").GetString())
                    .ToArray();

                Assert.Equal(["auth_failed", "user_blocked"], reasons);
            }

            // An unknown element refuses the whole filter and names the parameter, never "no filter".
            using var refused = await GetPageAsync("reasons=auth_failed&reasons=nope");
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            using (var body = await ReadJsonAsync(refused))
            {
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("reasons", out _));
            }

            // Over the shared value ceiling: the whole filter is refused, not truncated.
            var tooMany = string.Join(
                "&",
                Enumerable.Repeat("reasons=auth_failed", 101));
            await AssertRefusedAsync(tooMany, "reasons");
        }

        [Fact]
        public async Task TheIspFilterIsExactAndCasePreservedWhileBlankMeansNoFilter()
        {
            await CreateAdminAndSignInAsync();

            await PlantAsync(
                Row(ApiRequestFailureReason.AuthFailed, "provider one", isp: "SomeNet"),
                Row(ApiRequestFailureReason.AuthFailed, "provider two", isp: "somenet"));

            using (var exact = await ReadPageAsync("isp=SomeNet"))
            {
                Assert.Equal(1, exact.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("provider one", SingleRow(exact).GetProperty("description").GetString());
                Assert.Equal("SomeNet", SingleRow(exact).GetProperty("isp").GetString());
            }

            using (var otherCase = await ReadPageAsync("isp=somenet"))
            {
                Assert.Equal(1, otherCase.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("provider two", SingleRow(otherCase).GetProperty("description").GetString());
            }

            using (var blank = await ReadPageAsync("isp=&ip=&country="))
            {
                Assert.Equal(2, blank.RootElement.GetProperty("total").GetInt32());
            }

            // The reason, the exact provider and the search compose into one predicate: only the
            // row carrying all three facts is listed, and `total` counts exactly that set.
            using (var composed = await ReadPageAsync(
                "reasons=auth_failed&reasons=user_blocked&isp=SomeNet&search=provider"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("provider one", SingleRow(composed).GetProperty("description").GetString());
            }
        }

        [Fact]
        public async Task TheDateRangeIsInclusiveOnBothEndsComposesAndIsOptional()
        {
            await CreateAdminAndSignInAsync();

            // Three instants a second apart, so each bound can name one endpoint exactly. The
            // clock is the shared fake and only moves forward - the same treatment every other
            // test that needs two writes in different seconds gives it.
            await PlantAsync(Row(ApiRequestFailureReason.AuthFailed, "oldest refusal"));
            var oldestAt = Fixture.Clock.GetUtcNow();

            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(ApiRequestFailureReason.UserBlocked, "middle refusal"));
            var middleAt = Fixture.Clock.GetUtcNow();

            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(ApiRequestFailureReason.TooManyConnections, "newest refusal"));

            // Absent bounds mean no bound at all: all three rows answer.
            using (var unbounded = await ReadPageAsync(string.Empty))
            {
                Assert.Equal(3, unbounded.RootElement.GetProperty("total").GetInt32());
            }

            // Both ends inclusive: the bounds name the rows' own instants, so the middle row is
            // kept when each side is the bound itself.
            using (var bounded = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(oldestAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, bounded.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    ["middle refusal", "oldest refusal"],
                    Rows(bounded)
                        .Select(row => row.GetProperty("description").GetString())
                        .ToArray());
            }

            // Either side alone is one open-ended bound.
            using (var fromOnly = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, fromOnly.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("newest refusal", Rows(fromOnly)[0].GetProperty("description").GetString());
            }

            using (var toOnly = await ReadPageAsync(
                $"dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, toOnly.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("middle refusal", Rows(toOnly)[0].GetProperty("description").GetString());
            }

            // An inverted range is a valid query over an empty interval, not a refusal.
            using (var inverted = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(middleAt.AddSeconds(1).ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(oldestAt.ToString("O"))}"))
            {
                Assert.Equal(0, inverted.RootElement.GetProperty("total").GetInt32());
            }

            // The range composes with the other filters: only the middle row carries both facts.
            using (var composed = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(oldestAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}" +
                "&reasons=user_blocked"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("middle refusal", SingleRow(composed).GetProperty("description").GetString());
            }
        }

        [Fact]
        public async Task SearchCoversTheDescriptionTheSubscriptionDataAndTheContentName()
        {
            await CreateAdminAndSignInAsync();

            await PlantAsync(
                Row(
                    ApiRequestFailureReason.UnknownId,
                    "the description carries alpha",
                    subscriptionData: "sub-111",
                    contentName: "Channel One",
                    contentKind: ContentType.VideoStream),
                Row(
                    ApiRequestFailureReason.AuthFailed,
                    "the description carries beta",
                    subscriptionData: "sub-222",
                    contentName: "Radio Two",
                    contentKind: ContentType.AudioStream),
                Row(
                    ApiRequestFailureReason.UserBlocked,
                    "the description carries gamma",
                    subscriptionData: "sub-333",
                    contentName: "Film Three",
                    contentKind: ContentType.VideoOnDemand));

            using (var byDescription = await ReadPageAsync("search=description%20carries%20beta"))
            {
                Assert.Equal(1, byDescription.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("sub-222", SingleRow(byDescription).GetProperty("subscriptionData").GetString());
            }

            using (var bySubscription = await ReadPageAsync("search=SUB-333"))
            {
                Assert.Equal(1, bySubscription.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("Film Three", SingleRow(bySubscription).GetProperty("contentName").GetString());
            }

            using (var byContent = await ReadPageAsync("search=channel"))
            {
                Assert.Equal(1, byContent.RootElement.GetProperty("total").GetInt32());
                Assert.Equal("sub-111", SingleRow(byContent).GetProperty("subscriptionData").GetString());
            }

            // The three free-text columns composed with a reason still count exactly that set.
            using (var composed = await ReadPageAsync(
                "search=carries&reasons=unknown_id&reasons=auth_failed"))
            {
                Assert.Equal(2, composed.RootElement.GetProperty("total").GetInt32());
            }

            // The search covers exactly description, subscription_data and content_name - the
            // three stored free-text columns. A planted provider is reachable through its own
            // exact `isp` filter, but not through the search box, so this answers zero rather
            // than quietly widening what "search" means.
            using (var byIsp = await ReadPageAsync("search=Example%20ISP"))
            {
                Assert.Equal(0, byIsp.RootElement.GetProperty("total").GetInt32());
            }
        }

        [Fact]
        public async Task TheRowContractCarriesTheStoredSnapshotAndTheWireReason()
        {
            await CreateAdminAndSignInAsync();

            var contentId = Guid.NewGuid();

            await PlantAsync(new NewApiRequestFailure
            {
                SubscriptionData = "sub-abc",
                ContentKind = ContentType.Series,
                ContentId = contentId,
                ContentName = "Example Series",
                Reason = ApiRequestFailureReason.SubscriptionExpired,
                Ip = "198.51.100.7",
                Isp = "Example ISP",
                CountryCode = "DE",
                Description = "The subscription behind the request had already expired."
            });

            using var body = await ReadPageAsync(string.Empty);
            var row = SingleRow(body);

            Assert.Equal("sub-abc", row.GetProperty("subscriptionData").GetString());
            Assert.Equal("Series", row.GetProperty("contentKind").GetString());
            Assert.Equal(contentId.ToString(), row.GetProperty("contentId").GetString());
            Assert.Equal("Example Series", row.GetProperty("contentName").GetString());
            Assert.Equal("subscription_expired", row.GetProperty("reason").GetString());
            Assert.Equal("198.51.100.7", row.GetProperty("ip").GetString());
            Assert.Equal("Example ISP", row.GetProperty("isp").GetString());
            Assert.Equal("DE", row.GetProperty("countryCode").GetString());
            Assert.Equal(
                "The subscription behind the request had already expired.",
                row.GetProperty("description").GetString());

            // `createdAt` is a `DateTimeOffset` on the wire: the ISO round-trip text parses
            // back as the same instant, so a client's `new Date(...)` gets the moment rather
            // than a string it has to reformat by hand.
            var createdAtText = row.GetProperty("createdAt").GetString();

            Assert.NotNull(createdAtText);
            Assert.True(
                DateTimeOffset.TryParse(
                    createdAtText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var createdAt),
                $"createdAt did not round-trip: {createdAtText}");
            Assert.Equal(TimeSpan.Zero, createdAt.Offset);
        }

        [Fact]
        public async Task ARefusalWithNoContentAnswersTheWholeContentGroupAsNull()
        {
            await CreateAdminAndSignInAsync();

            await PlantAsync(Row(ApiRequestFailureReason.AuthFailed, "no content named"));

            using var body = await ReadPageAsync(string.Empty);
            var row = SingleRow(body);

            Assert.Equal(JsonValueKind.Null, row.GetProperty("subscriptionData").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("contentKind").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("contentId").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("contentName").ValueKind);
            Assert.Equal("auth_failed", row.GetProperty("reason").GetString());
        }

        [Fact]
        public async Task ClearingNeedsTheCsrfPairIgnoresTheFiltersAndReportsTheCount()
        {
            await CreateAdminAndSignInAsync();

            await PlantAsync(
                Row(ApiRequestFailureReason.AuthFailed, "first refusal"),
                Row(ApiRequestFailureReason.UserBlocked, "second refusal"));

            var before = await CountAsync();
            Assert.Equal(2, before);

            // No antiforgery pair: refused as such, and nothing is removed.
            using (var noCsrf = await Client.DeleteAsync(
                "/admin/api-request-failures",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            Assert.Equal(before, await CountAsync());

            // A valid pair wipes the whole table even though the screen's filters were sent.
            using (var cleared = await DeleteAsync(
                "/admin/api-request-failures?reasons=auth_failed&search=first",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(cleared, HttpStatusCode.OK, "api_request_failures_cleared");
                using var body = await ReadJsonAsync(cleared);
                Assert.Equal(
                    "Cleared the API request failure log: 2 row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }

            Assert.Equal(0, await CountAsync());

            // The second press is still success, with zero.
            using (var again = await DeleteAsync(
                "/admin/api-request-failures",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(again, HttpStatusCode.OK, "api_request_failures_cleared");
                using var body = await ReadJsonAsync(again);
                Assert.Equal(
                    "Cleared the API request failure log: 0 row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }
        }

        [Fact]
        public async Task TheEndpointsAreRefusedWithoutTheGrantAndToEveryOtherRole()
        {
            // An administrator holding only the client grants may neither read nor wipe the log.
            var readersGroup = await CreateGroupAsync(
                "Readers",
                UserRole.Admin,
                permissions: UserPermission.ViewClients);
            await CreateUserAsync(UserRole.Admin, "reader", Password, groupId: readersGroup);

            using var reader = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "reader", Password, reader);

            using (var list = await reader.GetAsync(
                "/admin/api-request-failures",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(list, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var clear = await DeleteAsync(
                "/admin/api-request-failures",
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(clear, HttpStatusCode.Forbidden, "permission_denied");
            }

            // The base Administrators group holds the grant, and its account reaches both verbs.
            await CreateAdminAndSignInAsync();

            using (var allowed = await ReadPageAsync(string.Empty))
            {
                Assert.Equal(0, allowed.RootElement.GetProperty("total").GetInt32());
            }

            using (var cleared = await DeleteAsync(
                "/admin/api-request-failures",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(cleared, HttpStatusCode.OK, "api_request_failures_cleared");
            }

            // A client and a reseller are refused by the role gate ahead of any permission.
            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);
            using (var refusedClient = await client.GetAsync(
                "/admin/api-request-failures",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedClient, HttpStatusCode.Forbidden, "auth_role_changed");
            }

            using (var refusedClientClear = await DeleteAsync(
                "/admin/api-request-failures",
                await GetCsrfTokenAsync(client),
                client))
            {
                await AssertCodeAsync(
                    refusedClientClear,
                    HttpStatusCode.Forbidden,
                    "auth_role_changed");
            }

            using var reseller = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Reseller, "seller", client: reseller);
            using (var refusedReseller = await reseller.GetAsync(
                "/admin/api-request-failures",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedReseller, HttpStatusCode.Forbidden, "auth_role_changed");
            }

            using var refusedResellerClear = await DeleteAsync(
                "/admin/api-request-failures",
                await GetCsrfTokenAsync(reseller),
                reseller);
            await AssertCodeAsync(
                refusedResellerClear,
                HttpStatusCode.Forbidden,
                "auth_role_changed");
        }

        [Fact]
        public async Task TheBaseAdministratorsGroupHoldsTheGrantAfterSeeding()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var groups = scope.ServiceProvider.GetRequiredService<IUserGroupRepository>();

            var granted = await groups.GetPermissionsAsync(
                BaseUserGroups.Administrators,
                TestContext.Current.CancellationToken);

            Assert.Contains(UserPermission.ViewApiRequestFailures, granted);
        }

        [Fact]
        public async Task EveryOtherMethodOnTheCollectionAnswersMethodNotAllowed()
        {
            await CreateAdminAndSignInAsync();

            // The write-path fence, proven at the wire rather than only by reading the
            // controller: the collection answers GET and DELETE, so every other method is a
            // 405 through the shared status-page envelope. A POST that created a row here
            // would be a capture path this capability does not have.
            var csrf = await GetCsrfTokenAsync();

            using (var post = await PostJsonAsync(
                "/admin/api-request-failures",
                new { reason = "auth_failed" },
                csrf))
            {
                await AssertCodeAsync(post, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var put = await PutJsonAsync(
                "/admin/api-request-failures",
                new { reason = "auth_failed" },
                csrf))
            {
                await AssertCodeAsync(put, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var patch = await PatchJsonAsync(
                "/admin/api-request-failures",
                new { reason = "auth_failed" },
                csrf))
            {
                await AssertCodeAsync(patch, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            // None of the three wrote anything.
            Assert.Equal(0, await CountAsync());
        }

        /// <summary>Builds one row with only the facts the assertion needs.</summary>
        private static NewApiRequestFailure Row(
            ApiRequestFailureReason reason,
            string description,
            string? subscriptionData = null,
            string? contentName = null,
            ContentType? contentKind = null,
            string? isp = "Example ISP",
            string ip = "203.0.113.9")
        {
            return new NewApiRequestFailure
            {
                SubscriptionData = subscriptionData,
                ContentKind = contentKind,
                ContentId = contentKind is null ? null : Guid.NewGuid(),
                ContentName = contentName,
                Reason = reason,
                Ip = ip,
                Isp = isp,
                CountryCode = "DE",
                Description = description
            };
        }

        private async Task PlantAsync(params NewApiRequestFailure[] rows)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            await TestDataSeeder.PlantApiRequestFailuresAsync(
                scope.ServiceProvider,
                rows,
                TestContext.Current.CancellationToken);
        }

        private Task<HttpResponseMessage> GetPageAsync(string query)
            => Client.GetAsync(
                query.Length == 0
                    ? "/admin/api-request-failures"
                    : $"/admin/api-request-failures?{query}",
                TestContext.Current.CancellationToken);

        private async Task<JsonDocument> ReadPageAsync(string query)
        {
            using var response = await GetPageAsync(query);
            response.EnsureSuccessStatusCode();

            return await ReadJsonAsync(response);
        }

        private async Task<int> CountAsync()
        {
            using var body = await ReadPageAsync(string.Empty);

            return body.RootElement.GetProperty("total").GetInt32();
        }

        private async Task ClearAsync()
        {
            using var response = await DeleteAsync(
                "/admin/api-request-failures",
                await GetCsrfTokenAsync());
            response.EnsureSuccessStatusCode();
        }

        private async Task AssertRefusedAsync(string query, string parameter)
        {
            using var response = await GetPageAsync(query);
            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(response);
            Assert.True(
                body.RootElement.GetProperty("errors").TryGetProperty(parameter, out _),
                $"no errors.{parameter}: {body.RootElement}");
        }

        private static JsonElement SingleRow(JsonDocument document)
        {
            var rows = Rows(document);

            Assert.Single(rows);

            return rows[0];
        }

        private static JsonElement[] Rows(JsonDocument document)
            => document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        private static Guid[] Ids(JsonElement items)
            => items
                .EnumerateArray()
                .Select(row => Guid.Parse(row.GetProperty("id").GetString()!))
                .ToArray();
    }
}
