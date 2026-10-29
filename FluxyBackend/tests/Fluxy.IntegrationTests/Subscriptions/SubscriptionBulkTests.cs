using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Subscriptions
{
    /// <summary>
    /// The bulk subscription endpoints: one operation over a selection, answered per row.
    /// </summary>
    /// <remarks>
    /// The rules these tests exist to pin are the ones a single row cannot show: that a plan
    /// change reports each service it cleared as a warning, that a plan change which would clear a
    /// row's only service refuses that row while the rest of the selection continues, that the
    /// field clears are idempotent, and that the owner block reuses the account block - including
    /// its refusal of the caller's own account.
    /// </remarks>
    public sealed class SubscriptionBulkTests : IntegrationTestBase
    {
        public SubscriptionBulkTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        /// <summary>How many bouquets this test has had created for it.</summary>
        private int _bouquetCount;

        [Fact]
        public async Task APlanChangeClearsAnUnsupportedServiceWithAWarning()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            var groups = await GroupIdsAsync();

            // The row starts with a playlist and a MAG.
            var id = await CreateSubscriptionAsync(Body(
                owner,
                playlistId: Playlist(),
                magMac: "AA:BB:CC:DD:EE:11",
                bouquetIds: [bouquet]));

            // The destination plan delivers a MAG and nothing else.
            var magOnly = await CreatePlanAsync(
                "MAG only",
                groups,
                [bouquet],
                supportsM3u: false,
                supportsMag: true,
                supportsEnigma2: false);

            using var response = await PostJsonAsync(
                "/admin/subscriptions/bulk",
                new { action = "change-tariff", ids = new[] { id }, tariffPlanId = magOnly },
                await GetCsrfTokenAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var item = body.RootElement.GetProperty("items")[0];

            Assert.True(item.GetProperty("ok").GetBoolean());
            Assert.Equal("subscription_updated", item.GetProperty("code").GetString());

            var warnings = item.GetProperty("warnings").EnumerateArray()
                .Select(warning => warning.GetProperty("code").GetString())
                .ToList();

            Assert.Contains("subscription_playlist_cleared", warnings);
            Assert.DoesNotContain("subscription_mag_cleared", warnings);

            // The row really lost the playlist and kept the MAG.
            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(JsonValueKind.Null, detailBody.RootElement.GetProperty("playlistId").ValueKind);
            Assert.Equal(
                "AA:BB:CC:DD:EE:11",
                detailBody.RootElement.GetProperty("magMac").GetString());
        }

        [Fact]
        public async Task APlanChangeThatWouldClearTheOnlyServiceFailsThatRowOnly()
        {
            await CreateAdminAndSignInAsync();

            var first = await CreateUserAsync(UserRole.Client, "first");
            var second = await CreateUserAsync(UserRole.Client, "second");
            var bouquet = await CreateBouquetAsync("Cinema");
            var groups = await GroupIdsAsync();

            // Only a MAG on the first row, only a playlist on the second.
            var magRow = await CreateSubscriptionAsync(Body(
                first,
                playlistId: null,
                magMac: "AA:BB:CC:DD:EE:12",
                bouquetIds: [bouquet]));
            var playlistRow = await CreateSubscriptionAsync(Body(
                second,
                playlistId: Playlist(),
                bouquetIds: [bouquet]));

            var playlistOnly = await CreatePlanAsync(
                "Playlist only",
                groups,
                [bouquet],
                supportsM3u: true,
                supportsMag: false,
                supportsEnigma2: false);

            using var response = await PostJsonAsync(
                "/admin/subscriptions/bulk",
                new
                {
                    action = "change-tariff",
                    ids = new[] { magRow, playlistRow },
                    tariffPlanId = playlistOnly
                },
                await GetCsrfTokenAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

            Assert.Equal(2, items.Count);
            // The first row's only service is a MAG the new plan cannot deliver.
            Assert.False(items[0].GetProperty("ok").GetBoolean());
            Assert.Equal("validation_failed", items[0].GetProperty("code").GetString());
            // The second row's playlist survives, so it lands.
            Assert.True(items[1].GetProperty("ok").GetBoolean());
            Assert.Equal("subscription_updated", items[1].GetProperty("code").GetString());
        }

        [Fact]
        public async Task TheFieldClearsSucceedOnAnAlreadyEmptyField()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");

            // The row keeps a MAG throughout, so no clear here would remove the last service -
            // which is the rule the refusal test covers separately.
            var id = await CreateSubscriptionAsync(Body(
                owner,
                playlistId: Playlist(),
                magMac: "AA:BB:CC:DD:EE:21",
                bouquetIds: [bouquet]));

            // The first clear empties the field and reports the same code the single row would.
            var first = await BulkAsync(new { action = "delete-playlist", ids = new[] { id } });
            Assert.Equal("subscription_updated", await FirstCodeAsync(first));

            // The second clear is a no-op that still succeeds - that is what idempotent means.
            var second = await BulkAsync(new { action = "delete-playlist", ids = new[] { id } });
            Assert.Equal("subscription_updated", await FirstCodeAsync(second));

            // The Enigma2 field was never set, so both of its clears are no-ops that succeed.
            foreach (var _ in new[] { 1, 2 })
            {
                var response = await BulkAsync(new { action = "delete-enigma2", ids = new[] { id } });
                Assert.Equal("subscription_updated", await FirstCodeAsync(response));
            }

            // Clearing the MAG would leave nothing, so it is refused rather than treated as a
            // no-op: "already empty" and "would be empty" are two different requests.
            var last = await ReadJsonAsync(await BulkAsync(new { action = "delete-mag", ids = new[] { id } }));
            var item = last.RootElement.GetProperty("items")[0];
            Assert.False(item.GetProperty("ok").GetBoolean());
            Assert.Equal("validation_failed", item.GetProperty("code").GetString());
        }

        [Fact]
        public async Task ADeleteReportsTheSingleRowCode()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            var id = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist(), bouquetIds: [bouquet]));

            Assert.Equal(
                "subscription_deleted",
                await FirstCodeAsync(await BulkAsync(new { action = "delete", ids = new[] { id } })));

            // A second delete of the same row is a per-row not-found; the rest of a selection
            // would still continue.
            using var body = await ReadJsonAsync(await BulkAsync(new { action = "delete", ids = new[] { id } }));
            Assert.Equal("subscription_not_found", body.RootElement.GetProperty("items")[0].GetProperty("code").GetString());
        }

        [Fact]
        public async Task APlanChangeWithoutADestinationRefusesTheWholeBody()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            var id = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist(), bouquetIds: [bouquet]));

            using var response = await PostJsonAsync(
                "/admin/subscriptions/bulk",
                new { action = "change-tariff", ids = new[] { id } },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ACreatorChangeReassignsTheIssuerAndRefusesAClient()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            var id = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist(), bouquetIds: [bouquet]));

            // The issuer starts as the acting administrator. A reseller is a legitimate issuer.
            var reseller = await CreateUserAsync(UserRole.Reseller, "issuer");

            var accepted = await BulkAsync(new { action = "change-creator", ids = new[] { id }, resellerId = reseller });
            Assert.Equal("subscription_updated", await FirstCodeAsync(accepted));

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(
                reseller.ToString(),
                detailBody.RootElement.GetProperty("resellerId").GetString());

            // A client may not be handed the issuing role, and that row is refused while the run
            // continues.
            var client = await CreateUserAsync(UserRole.Client, "not-an-issuer");

            using var body = await ReadJsonAsync(await BulkAsync(
                new { action = "change-creator", ids = new[] { id }, resellerId = client }));

            var item = body.RootElement.GetProperty("items")[0];
            Assert.False(item.GetProperty("ok").GetBoolean());
            Assert.Equal("validation_failed", item.GetProperty("code").GetString());
            Assert.True(item.GetProperty("errors").TryGetProperty("resellerId", out _));
        }

        [Fact]
        public async Task TheOwnerBlockIsIdempotentAndRefusesTheActingUser()
        {
            var actingId = await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            var id = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist(), bouquetIds: [bouquet]));

            // The first block lands and the subscription reads blocked afterwards.
            Assert.Equal(
                "subscription_updated",
                await FirstCodeAsync(await BulkAsync(new { action = "block-owner", ids = new[] { id } })));

            using (var list = await Client.GetAsync(
                $"/admin/subscriptions?status=blocked",
                TestContext.Current.CancellationToken))
            {
                using var listBody = await ReadJsonAsync(list);
                Assert.Equal(1, listBody.RootElement.GetProperty("total").GetInt32());
            }

            // The second block is idempotent: the answer does not depend on where it started.
            Assert.Equal(
                "subscription_updated",
                await FirstCodeAsync(await BulkAsync(new { action = "block-owner", ids = new[] { id } })));

            // A subscription whose owner is the acting account refuses that row - and the block
            // itself is what refuses it, which is why the answer names the account field.
            var actingRow = await CreateSubscriptionAsync(Body(actingId, playlistId: Playlist("a"), bouquetIds: [bouquet]));

            using var body = await ReadJsonAsync(await BulkAsync(
                new { action = "block-owner", ids = new[] { actingRow } }));

            var item = body.RootElement.GetProperty("items")[0];
            Assert.False(item.GetProperty("ok").GetBoolean());
            Assert.True(item.GetProperty("errors").TryGetProperty("userId", out _));
        }

        [Fact]
        public async Task ABulkBodyIsValidatedBeforeAnyRowIsRead()
        {
            await CreateAdminAndSignInAsync();

            foreach (var broken in new object[]
            {
                new { action = "nonsense", ids = new[] { Guid.NewGuid().ToString() } },
                new { action = "delete", ids = Array.Empty<string>() },
                new { action = "delete", ids = new[] { "not-a-uuid" } },
                new { action = "change-tariff", ids = new[] { Guid.NewGuid().ToString() }, tariffPlanId = "not-a-uuid" },
                new { action = "change-creator", ids = new[] { Guid.NewGuid().ToString() }, resellerId = "not-a-uuid" }
            })
            {
                using var response = await PostJsonAsync(
                    "/admin/subscriptions/bulk",
                    broken,
                    await GetCsrfTokenAsync());

                await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            }

            // More identifiers than one page holds.
            using var tooMany = await PostJsonAsync(
                "/admin/subscriptions/bulk",
                new
                {
                    action = "delete",
                    ids = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid().ToString()).ToArray()
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(tooMany, HttpStatusCode.BadRequest, "validation_failed");

            // CSRF precedes the body: a request with no pair is refused about the token even
            // though its body would also fail.
            using var noCsrf = await PostJsonAsync(
                "/admin/subscriptions/bulk",
                new { action = "nonsense", ids = Array.Empty<string>() });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        /// <summary>Runs one bulk operation and answers the decoded body.</summary>
        private async Task<HttpResponseMessage> BulkAsync(object body)
        {
            var response = await PostJsonAsync(
                "/admin/subscriptions/bulk",
                body,
                await GetCsrfTokenAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return response;
        }

        /// <summary>The code of the first entry of a bulk answer.</summary>
        private async Task<string?> FirstCodeAsync(HttpResponseMessage response)
        {
            using var body = await ReadJsonAsync(response);
            return body.RootElement.GetProperty("items")[0].GetProperty("code").GetString();
        }

        private async Task<string> CreateSubscriptionAsync(object body)
        {
            using var response = await PostJsonAsync(
                "/admin/subscriptions",
                body,
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "subscription_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateBouquetAsync(string name)
        {
            using var response = await PostJsonAsync(
                "/admin/bouquets",
                new { name = $"{name} {++_bouquetCount}" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "bouquet_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreatePlanAsync(
            string name,
            IReadOnlyList<string> userGroupIds,
            IReadOnlyList<string> bouquetIds,
            bool supportsM3u = true,
            bool supportsMag = true,
            bool supportsEnigma2 = true)
        {
            using var response = await PostJsonAsync(
                "/admin/tariff-plans",
                new
                {
                    name,
                    isTrial = false,
                    cost = 100,
                    durationValue = 1,
                    durationUnit = "months",
                    supportsM3u,
                    supportsMag,
                    supportsEnigma2,
                    outputMpegTs = true,
                    outputHls = true,
                    strictStbDeviceId = false,
                    allowRestream = false,
                    maxConnections = 2,
                    allowMultipleCountries = true,
                    userGroupIds,
                    bouquetIds
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "tariff_plan_created");

            return IdFromLocation(response);
        }

        private async Task<List<string>> GroupIdsAsync()
        {
            using var response = await Client.GetAsync(
                "/admin/user-groups",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            return body.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetString()!)
                .ToList();
        }

        private static string Playlist(string suffix = "f")
            => "0123456789abcdef0123456789abcde" + suffix;

        private static object Body(
            Guid owner,
            string? playlistId = "0123456789abcdef0123456789abcdef",
            string? magMac = null,
            string? enigma2Mac = null,
            IReadOnlyList<string>? bouquetIds = null)
            => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["userId"] = owner.ToString(),
                ["playlistId"] = playlistId,
                ["magMac"] = magMac,
                ["enigma2Mac"] = enigma2Mac,
                ["maxConnections"] = 3,
                ["isRestreamer"] = true,
                ["ipWhitelist"] = Array.Empty<string>(),
                ["userAgentWhitelist"] = Array.Empty<string>(),
                ["isIspLock"] = false,
                ["isCountryLock"] = false,
                ["isTrial"] = false,
                ["outputFormats"] = new[] { "mpegTs", "hls" },
                ["isStbLock"] = false,
                ["bouquetIds"] = bouquetIds
            };
    }
}
