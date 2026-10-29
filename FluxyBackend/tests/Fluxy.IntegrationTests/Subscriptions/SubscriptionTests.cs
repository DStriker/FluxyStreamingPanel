using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Subscriptions
{
    /// <summary>
    /// The subscription endpoints: the rows an operator issues, their services and access
    /// parameters, the computed status and the paged list.
    /// </summary>
    /// <remarks>
    /// The rules these tests exist to pin are the ones no screen can show from its own state: that
    /// a plan is the only source of truth for the fields it owns, that clearing it restores the
    /// row's own values, that a plan outside the owner's groups is refused, that a save leaving no
    /// service is refused, that the playlist id and the two MACs are unique, and that the page
    /// really is searched, filtered and ordered on the server.
    /// </remarks>
    public sealed class SubscriptionTests : IntegrationTestBase
    {
        public SubscriptionTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        /// <summary>How many bouquets this test has had created for it.</summary>
        private int _bouquetCount;

        [Fact]
        public async Task ASubscriptionRoundTripsWithoutAPlan()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");

            using var created = await PostSubscriptionAsync(
                Body(owner, bouquetIds: [bouquet], playlistId: Playlist()));
            await AssertCodeAsync(created, HttpStatusCode.Created, "subscription_created");

            var id = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

            using var body = await ReadJsonAsync(detail);
            var subscription = body.RootElement;

            Assert.Equal("subscriber", subscription.GetProperty("username").GetString());
            Assert.Equal("active", subscription.GetProperty("status").GetString());
            Assert.Equal("0123456789abcdef0123456789abcdef", subscription.GetProperty("playlistId").GetString());
            Assert.Equal(3, subscription.GetProperty("maxConnections").GetInt32());
            Assert.True(subscription.GetProperty("isRestreamer").GetBoolean());
            Assert.Equal(["mpegTs", "hls"], Names(subscription.GetProperty("outputFormats")));
            Assert.Equal([bouquet], Ids(subscription.GetProperty("bouquetIds")));
            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("derived").ValueKind);

            // The list feeds the whole screen, including the effective service support.
            using var list = await Client.GetAsync(
                "/admin/subscriptions",
                TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);
            var row = listBody.RootElement.GetProperty("items")[0];

            Assert.Equal("active", row.GetProperty("status").GetString());
            Assert.True(row.GetProperty("supportsPlaylist").GetBoolean());
            Assert.True(row.GetProperty("supportsMag").GetBoolean());
            Assert.True(row.GetProperty("supportsEnigma2").GetBoolean());

            // A patch changes only what it names.
            using var patched = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { maxConnections = 9 },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(patched, HttpStatusCode.OK, "subscription_updated");

            using var after = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var afterBody = await ReadJsonAsync(after);
            Assert.Equal(9, afterBody.RootElement.GetProperty("maxConnections").GetInt32());
            Assert.Equal(
                "0123456789abcdef0123456789abcdef",
                afterBody.RootElement.GetProperty("playlistId").GetString());

            // The two deletes: the first lands, the second reports that there is nothing there.
            using var deleted = await DeleteAsync(
                $"/admin/subscriptions/{id}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "subscription_deleted");

            using var again = await DeleteAsync(
                $"/admin/subscriptions/{id}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "subscription_not_found");
        }

        [Fact]
        public async Task APlanIsTheSourceOfTruthForTheFieldsItOwns()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var groups = await GroupIdsAsync();
            var bouquet = await CreateBouquetAsync("Cinema");
            // A monthly plan that allows two connections, restreaming and HLS only.
            var plan = await CreatePlanAsync(
                "Monthly",
                groups,
                [bouquet],
                supportsM3u: true,
                supportsMag: false,
                supportsEnigma2: false,
                outputMpegTs: false,
                outputHls: true,
                allowRestream: true,
                maxConnections: 2);

            // The body names values the plan does not state. They must be ignored.
            using var created = await PostSubscriptionAsync(Body(
                owner,
                planId: plan,
                playlistId: Playlist(),
                maxConnections: 99,
                outputFormats: ["mpegTs"],
                isRestreamer: false));
            await AssertCodeAsync(created, HttpStatusCode.Created, "subscription_created");

            var id = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(detail);
            var subscription = body.RootElement;

            Assert.Equal(2, subscription.GetProperty("maxConnections").GetInt32());
            Assert.True(subscription.GetProperty("isRestreamer").GetBoolean());
            Assert.Equal(["hls"], Names(subscription.GetProperty("outputFormats")));

            // The plan's expiry: one month from the subscription's own creation.
            var createdAt = subscription.GetProperty("createdAt").GetDateTimeOffset();
            var expireAt = subscription.GetProperty("expireAt").GetDateTimeOffset();
            Assert.Equal(createdAt.AddMonths(1), expireAt, TimeSpan.FromSeconds(1));

            // The derived block carries what the plan decides, which is what the form draws.
            var derived = subscription.GetProperty("derived");
            Assert.Equal(2, derived.GetProperty("maxConnections").GetInt32());
            Assert.False(derived.GetProperty("supportsMag").GetBoolean());
            Assert.Equal([bouquet], Ids(derived.GetProperty("bouquetIds")));

            // The list shows the effective support, not the row's dormant values.
            using var list = await Client.GetAsync(
                "/admin/subscriptions",
                TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);
            var row = listBody.RootElement.GetProperty("items")[0];
            Assert.True(row.GetProperty("supportsPlaylist").GetBoolean());
            Assert.False(row.GetProperty("supportsMag").GetBoolean());
            Assert.False(row.GetProperty("supportsEnigma2").GetBoolean());
        }

        [Fact]
        public async Task ClearingThePlanReactivatesTheSubscriptionsOwnValues()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var groups = await GroupIdsAsync();
            var bouquet = await CreateBouquetAsync("Cinema");
            var plan = await CreatePlanAsync("Monthly", groups, [bouquet], maxConnections: 2);

            var id = await CreateSubscriptionAsync(Body(
                owner,
                planId: plan,
                playlistId: Playlist(),
                maxConnections: 8));

            using var cleared = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { clearTariffPlan = true, maxConnections = 5 },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "subscription_updated");

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(detail);
            var subscription = body.RootElement;

            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("tariffPlanId").ValueKind);
            Assert.Equal(5, subscription.GetProperty("maxConnections").GetInt32());
            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("derived").ValueKind);
        }

        [Fact]
        public async Task ClearingThePlanAndTheCeilingWithTheBodysOwnFlagsTakesEffect()
        {
            // The body the edit form actually sends once its two clear flags are derived from the
            // values rather than from state kept beside them: a null identifier *and* the flag.
            // Without the flag the transport reads a null as "keep", so this test is the one that
            // would have caught a form reporting "saved" while the old plan stayed attached.
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var groups = await GroupIdsAsync();
            var bouquet = await CreateBouquetAsync("Cinema");
            var plan = await CreatePlanAsync("Monthly", groups, [bouquet], maxConnections: 2);

            var id = await CreateSubscriptionAsync(Body(
                owner,
                planId: plan,
                playlistId: Playlist(),
                maxConnections: 8));

            using var cleared = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { tariffPlanId = (string?)null, clearTariffPlan = true, maxConnections = (int?)null, clearMaxConnections = true },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "subscription_updated");

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(detail);
            var subscription = body.RootElement;

            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("tariffPlanId").ValueKind);
            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("maxConnections").ValueKind);
            Assert.Equal(JsonValueKind.Null, subscription.GetProperty("derived").ValueKind);
        }

        [Fact]
        public async Task ClearingOnlyTheCeilingKeepsThePlanAttached()
        {
            // The two flags are independent: clearing the ceiling must not clear the plan, and a
            // null ceiling alone (no flag) would be read as "keep".
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var groups = await GroupIdsAsync();
            var bouquet = await CreateBouquetAsync("Cinema");
            var plan = await CreatePlanAsync("Monthly", groups, [bouquet], maxConnections: 2);

            var id = await CreateSubscriptionAsync(Body(owner, planId: plan, playlistId: Playlist()));

            using var cleared = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { maxConnections = (int?)null, clearMaxConnections = true },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "subscription_updated");

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(detail);
            var subscription = body.RootElement;

            // The plan is still attached, and its ceiling is still what the row reports.
            Assert.Equal(plan, subscription.GetProperty("tariffPlanId").GetString());
            Assert.Equal(2, subscription.GetProperty("maxConnections").GetInt32());
        }

        [Fact]
        public async Task APlanOutsideTheOwnersGroupsIsRefused()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            // A plan offered only to the Administrators base group. The client owner is not in it.
            var adminGroup = FluxyApiFactory.BaseGroup(UserRole.Admin).ToString();
            var plan = await CreatePlanAsync("Admins only", [adminGroup], [bouquet]);

            using var refused = await PostSubscriptionAsync(Body(
                owner,
                planId: plan,
                playlistId: Playlist()));
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(refused);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("tariffPlanId", out _));
        }

        [Fact]
        public async Task ASaveThatLeavesNoServiceIsRefused()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");

            // No service at all on a create.
            using var noService = await PostSubscriptionAsync(Body(
                owner,
                playlistId: null,
                magMac: null,
                enigma2Mac: null,
                bouquetIds: [bouquet]));
            await AssertCodeAsync(noService, HttpStatusCode.BadRequest, "validation_failed");

            // A plan that supports nothing would clear the only service the body names. The plan
            // itself refuses to be created with no delivery method, so the plan that clears is the
            // one that supports *one* kind and the body names only the other.
            var groups = await GroupIdsAsync();
            var playlistOnly = await CreatePlanAsync(
                "Playlist only",
                groups,
                [bouquet],
                supportsM3u: true,
                supportsMag: false,
                supportsEnigma2: false);

            using var cleared = await PostSubscriptionAsync(Body(
                owner,
                planId: playlistOnly,
                playlistId: null,
                magMac: "AA:BB:CC:DD:EE:0A",
                bouquetIds: [bouquet]));
            await AssertCodeAsync(cleared, HttpStatusCode.BadRequest, "validation_failed");

            // Removing the only service from an existing subscription is refused too.
            var id = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist(), bouquetIds: [bouquet]));

            using var clearedField = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { playlistId = string.Empty },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(clearedField, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ASaveWithoutAPlanNeedsAtLeastOneBouquet()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");

            using var refused = await PostSubscriptionAsync(Body(
                owner,
                playlistId: Playlist(),
                bouquetIds: []));
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(refused);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("bouquetIds", out _));

            // The *absent* list is the default: every bouquet the installation has.
            var bouquet = await CreateBouquetAsync("Cinema");

            using var created = await PostSubscriptionAsync(Body(
                owner,
                playlistId: Playlist(),
                bouquetIds: null));
            await AssertCodeAsync(created, HttpStatusCode.Created, "subscription_created");

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{IdFromLocation(created)}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal([bouquet], Ids(detailBody.RootElement.GetProperty("bouquetIds")));
        }

        [Fact]
        public async Task ThePlaylistIdAndBothMacsAreUnique()
        {
            await CreateAdminAndSignInAsync();

            var first = await CreateUserAsync(UserRole.Client, "first");
            var second = await CreateUserAsync(UserRole.Client, "second");
            var bouquet = await CreateBouquetAsync("Cinema");

            var playlist = Playlist();
            _ = await CreateSubscriptionAsync(Body(first, playlistId: playlist, magMac: "AA:BB:CC:DD:EE:01"));

            // The same playlist id on another subscription.
            using var duplicatePlaylist = await PostSubscriptionAsync(Body(
                second,
                playlistId: playlist,
                bouquetIds: [bouquet]));
            await AssertCodeAsync(duplicatePlaylist, HttpStatusCode.Conflict, "subscription_already_exists");
            using (var body = await ReadJsonAsync(duplicatePlaylist))
            {
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("playlistId", out _));
            }

            // The same MAG MAC, typed in another case with the other separator.
            using var duplicateMag = await PostSubscriptionAsync(Body(
                second,
                playlistId: Playlist("b"),
                magMac: "aa-bb-cc-dd-ee-01",
                bouquetIds: [bouquet]));
            await AssertCodeAsync(duplicateMag, HttpStatusCode.Conflict, "subscription_already_exists");
            using (var body = await ReadJsonAsync(duplicateMag))
            {
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("magMac", out _));
            }

            // A MAC in another case is stored in canonical form.
            using var canonical = await PostSubscriptionAsync(Body(
                second,
                playlistId: Playlist("c"),
                magMac: "aa-bb-cc-dd-ee-ff",
                bouquetIds: [bouquet]));
            await AssertCodeAsync(canonical, HttpStatusCode.Created, "subscription_created");

            using var detail = await Client.GetAsync(
                $"/admin/subscriptions/{IdFromLocation(canonical)}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(
                "AA:BB:CC:DD:EE:FF",
                detailBody.RootElement.GetProperty("magMac").GetString());
        }

        [Fact]
        public async Task ThePlaylistIdGeneratorMintsAnUnusedIdentifier()
        {
            await CreateAdminAndSignInAsync();

            using var response = await PostJsonAsync(
                "/admin/subscriptions/playlist-id",
                new { },
                await GetCsrfTokenAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var playlistId = body.RootElement.GetProperty("playlistId").GetString();

            Assert.NotNull(playlistId);
            Assert.Equal(32, playlistId!.Length);
            Assert.Matches("^[0-9a-f]{32}$", playlistId);

            // Nothing was written: the table is still empty.
            using var list = await Client.GetAsync(
                "/admin/subscriptions",
                TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);
            Assert.Equal(0, listBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task TheComputedStatusPrecedenceIsWhatTheFilterReturns()
        {
            await CreateAdminAndSignInAsync();

            var bouquets = await CreateBouquetAsync("Cinema");

            // Active: no expiry, no trial, permitted owner.
            var active = await CreateUserAsync(UserRole.Client, "active");
            var activeId = await CreateSubscriptionAsync(Body(active, playlistId: Playlist("a"), bouquetIds: [bouquets]));

            // Expired: an expiry in the past.
            var expired = await CreateUserAsync(UserRole.Client, "expired");
            var expiredId = await CreateSubscriptionAsync(Body(
                expired,
                playlistId: Playlist("b"),
                expireAt: "2020-01-01",
                bouquetIds: [bouquets]));

            // Blocked: an unexpired subscription whose owner is blocked.
            var blocked = await CreateUserAsync(UserRole.Client, "blocked");
            var blockedId = await CreateSubscriptionAsync(Body(
                blocked,
                playlistId: Playlist("c"),
                bouquetIds: [bouquets]));
            using (var blockResponse = await PostJsonAsync(
                $"/admin/users/{blocked}/block",
                new { },
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(blockResponse, HttpStatusCode.OK, "user_blocked");
            }

            // Trial: an unexpired trial of a permitted owner.
            var trial = await CreateUserAsync(UserRole.Client, "trial");
            var trialId = await CreateSubscriptionAsync(Body(
                trial,
                playlistId: Playlist("d"),
                isTrial: true,
                bouquetIds: [bouquets]));

            using var expiredPage = await Client.GetAsync(
                "/admin/subscriptions?status=expired",
                TestContext.Current.CancellationToken);
            using var expiredBody = await ReadJsonAsync(expiredPage);
            Assert.Equal(1, expiredBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(expiredId, FirstId(expiredBody.RootElement));

            using var blockedPage = await Client.GetAsync(
                "/admin/subscriptions?status=blocked",
                TestContext.Current.CancellationToken);
            using var blockedBody = await ReadJsonAsync(blockedPage);
            Assert.Equal(1, blockedBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(blockedId, FirstId(blockedBody.RootElement));

            using var trialPage = await Client.GetAsync(
                "/admin/subscriptions?status=trial",
                TestContext.Current.CancellationToken);
            using var trialBody = await ReadJsonAsync(trialPage);
            Assert.Equal(1, trialBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(trialId, FirstId(trialBody.RootElement));

            using var activePage = await Client.GetAsync(
                "/admin/subscriptions?status=active",
                TestContext.Current.CancellationToken);
            using var activeBody = await ReadJsonAsync(activePage);
            Assert.Equal(1, activeBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(activeId, FirstId(activeBody.RootElement));

            // The tags the list draws agree with the filter that produced them.
            Assert.Equal("expired", StatusOf(expiredBody.RootElement, expiredId));
            Assert.Equal("blocked", StatusOf(blockedBody.RootElement, blockedId));
            Assert.Equal("trial", StatusOf(trialBody.RootElement, trialId));
        }

        [Fact]
        public async Task TheTableIsPagedSearchedAndSortedOnTheServer()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");

            var first = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist("a"), bouquetIds: [bouquet]));

            // The audit clock is the host's `TimeProvider` (the design derives an expiry from the
            // very stamp the row is written with), and the test host freezes it - so two creates a
            // moment apart would share a `created_at` and the order would fall to the identifier
            // tie-break. Advancing it keeps this test about the *order* rather than about the tie.
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));

            var second = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist("b"), magMac: "AA:BB:CC:DD:EE:02", bouquetIds: [bouquet]));

            // The owner filter narrows before the count.
            using var byOwner = await Client.GetAsync(
                $"/admin/subscriptions?ownerId={owner}",
                TestContext.Current.CancellationToken);
            using var byOwnerBody = await ReadJsonAsync(byOwner);
            Assert.Equal(2, byOwnerBody.RootElement.GetProperty("total").GetInt32());

            // Search over the MAC a support call quotes.
            using var searched = await Client.GetAsync(
                "/admin/subscriptions?search=aa-bb-cc-dd-ee-02",
                TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(second, FirstId(searchedBody.RootElement));

            // Search over the playlist id as well.
            using var byPlaylist = await Client.GetAsync(
                $"/admin/subscriptions?search={Playlist("a")}",
                TestContext.Current.CancellationToken);
            using var byPlaylistBody = await ReadJsonAsync(byPlaylist);
            Assert.Equal(1, byPlaylistBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(first, FirstId(byPlaylistBody.RootElement));

            // Paging: two rows, one page of one each.
            using var firstPage = await Client.GetAsync(
                "/admin/subscriptions?page=1&pageSize=1&sortBy=createdAt&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var firstPageBody = await ReadJsonAsync(firstPage);
            Assert.Equal(2, firstPageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(1, firstPageBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(first, FirstId(firstPageBody.RootElement));

            // The bounds are refused rather than clamped, and a value that names nothing is
            // refused rather than treated as "no filter".
            foreach (var broken in new[]
            {
                "page=0",
                "pageSize=1001",
                $"search={new string('a', 201)}",
                "sortBy=duration",
                "sortOrder=upwards",
                "status=whatever",
                "ownerId=not-a-uuid",
                "creatorId=not-a-uuid"
            })
            {
                using var refused = await Client.GetAsync(
                    $"/admin/subscriptions?{broken}",
                    TestContext.Current.CancellationToken);

                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }
        }

        [Fact]
        public async Task SubscriptionWritesRequireAntiforgeryAndThePair()
        {
            using var anonymous = Fixture.CreateClient();
            using var anonymousList = await anonymous.GetAsync(
                "/admin/subscriptions",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAdminAndSignInAsync();
            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");

            // CSRF precedes validation: this body would also fail the service minimum, and the
            // answer is about the token rather than about the body.
            using var noCsrf = await PostJsonAsync("/admin/subscriptions", Body(
                owner,
                playlistId: null,
                bouquetIds: [bouquet]));
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            var id = await CreateSubscriptionAsync(Body(owner, playlistId: Playlist(), bouquetIds: [bouquet]));

            // A group holding only the read grant may read the table and one subscription, and
            // may change nothing - including the generator, which mints something about to be
            // stored.
            var readerClient = Fixture.CreateClient();
            var readerGroup = await CreateGroupAsync(
                "subscription-readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions);
            _ = await CreateUserAsync(UserRole.Admin, "subscriber-reader", groupId: readerGroup);
            await SignInAsync(UserRole.Admin, "subscriber-reader", "Password1", readerClient);

            using var readable = await readerClient.GetAsync(
                "/admin/subscriptions",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

            using var readableDetail = await readerClient.GetAsync(
                $"/admin/subscriptions/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readableDetail.StatusCode);

            using var refusedWrite = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { maxConnections = 1 },
                await GetCsrfTokenAsync(readerClient),
                readerClient);
            Assert.Equal(HttpStatusCode.Forbidden, refusedWrite.StatusCode);

            using var refusedGenerator = await PostJsonAsync(
                "/admin/subscriptions/playlist-id",
                new { },
                await GetCsrfTokenAsync(readerClient),
                readerClient);
            Assert.Equal(HttpStatusCode.Forbidden, refusedGenerator.StatusCode);

            // A group holding the view and edit pair may write.
            var editorClient = Fixture.CreateClient();
            var editorGroup = await CreateGroupAsync(
                "subscription-editors",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions,
                UserPermission.EditSubscriptions);
            _ = await CreateUserAsync(UserRole.Admin, "subscriber-editor", groupId: editorGroup);
            await SignInAsync(UserRole.Admin, "subscriber-editor", "Password1", editorClient);

            using var allowed = await PatchJsonAsync(
                $"/admin/subscriptions/{id}",
                new { maxConnections = 1 },
                await GetCsrfTokenAsync(editorClient),
                editorClient);
            await AssertCodeAsync(allowed, HttpStatusCode.OK, "subscription_updated");
        }

        [Fact]
        public async Task DeletingAPlanStillSoldIsRefused()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "subscriber");
            var groups = await GroupIdsAsync();
            var bouquet = await CreateBouquetAsync("Cinema");
            var plan = await CreatePlanAsync("Monthly", groups, [bouquet]);

            _ = await CreateSubscriptionAsync(Body(owner, planId: plan, playlistId: Playlist()));

            using var refused = await DeleteAsync(
                $"/admin/tariff-plans/{plan}",
                await GetCsrfTokenAsync());

            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "tariff_plan_in_use");

            // The plan is still there, which is what the refusal promised.
            using var stillThere = await Client.GetAsync(
                $"/admin/tariff-plans/{plan}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
        }

        /// <summary>
        /// A subscription body a test would post, with the values it overrides.
        /// </summary>
        /// <remarks>
        /// An absent bouquet list is left as JSON <c>null</c> here, because that is the body that
        /// asks for "every bouquet" - the server's default. A refusal test that forgot to name one
        /// would otherwise assert the wrong refusal.
        /// </remarks>
        private static object Body(
            Guid owner,
            string? playlistId = "0123456789abcdef0123456789abcdef",            string? magMac = null,
            string? enigma2Mac = null,
            string? planId = null,
            int? maxConnections = 3,
            bool isRestreamer = true,
            IReadOnlyList<string>? outputFormats = null,
            string? expireAt = null,
            bool isTrial = false,
            IReadOnlyList<string>? bouquetIds = null)
            => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["userId"] = owner.ToString(),
                ["playlistId"] = playlistId,
                ["magMac"] = magMac,
                ["enigma2Mac"] = enigma2Mac,
                ["tariffPlanId"] = planId,
                ["maxConnections"] = maxConnections,
                ["isRestreamer"] = isRestreamer,
                ["ipWhitelist"] = Array.Empty<string>(),
                ["userAgentWhitelist"] = Array.Empty<string>(),
                ["isIspLock"] = false,
                ["isCountryLock"] = false,
                ["isTrial"] = isTrial,
                ["outputFormats"] = outputFormats ?? ["mpegTs", "hls"],
                ["isStbLock"] = false,
                ["expireAt"] = expireAt,
                ["bouquetIds"] = bouquetIds
            };

        /// <summary>
        /// Posts a subscription body and, when the server refused it, fails with the body so the
        /// assertion names the reason rather than only the status.
        /// </summary>
        /// <remarks>
        /// Only used where the test expects a create: a test that asserts a refusal posts through
        /// <see cref="PostJsonAsync"/> directly, so its own assertion is the one that reports.
        /// </remarks>
        private async Task<HttpResponseMessage> PostSubscriptionAsync(object body)
            => await PostJsonAsync("/admin/subscriptions", body, await GetCsrfTokenAsync());

        private async Task<string> CreateSubscriptionAsync(object body)
        {
            using var response = await PostSubscriptionAsync(body);
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
            bool supportsEnigma2 = true,
            bool outputMpegTs = true,
            bool outputHls = true,
            bool allowRestream = false,
            int maxConnections = 2)
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
                    outputMpegTs,
                    outputHls,
                    strictStbDeviceId = false,
                    allowRestream,
                    maxConnections,
                    allowMultipleCountries = true,
                    userGroupIds,
                    bouquetIds
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "tariff_plan_created");

            return IdFromLocation(response);
        }

        /// <summary>The identifiers of the three base groups every installation has.</summary>
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

        /// <summary>A 32-hex playlist id, deterministic so a search can find it.</summary>
        /// <remarks>
        /// The base is 31 characters, and the one-character suffix makes the 32 a playlist id has -
        /// so two calls with different suffixes are two different ids and both have the shape the
        /// server accepts.
        /// </remarks>
        private static string Playlist(string suffix = "f")
            => "0123456789abcdef0123456789abcde" + suffix;

        private static string[] Names(JsonElement items)
            => items.EnumerateArray().Select(item => item.GetString()!).ToArray();

        private static string?[] Ids(JsonElement items)
            => items.EnumerateArray().Select(item => item.GetString()).ToArray();

        private static string? FirstId(JsonElement page)
            => page.GetProperty("items")[0].GetProperty("id").GetString();

        private static string? StatusOf(JsonElement page, string id)
            => page.GetProperty("items").EnumerateArray()
                .First(item => item.GetProperty("id").GetString() == id)
                .GetProperty("status").GetString();
    }
}
