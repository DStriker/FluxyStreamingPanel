using System.Net;
using System.Text.Json;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Subscriptions
{
    /// <summary>
    /// The admin connection log: the page, its filters, its bounds, the two permissions and the
    /// whole-table clear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules these tests exist to pin are the ones a screen cannot show from its own state:
    /// that the log holds two media and one filter names exactly one of them, that country and
    /// provider are filterable columns rather than lookups, that the owner's name and the stream's
    /// title are resolved for the page (and the title degrades to null when the row is gone), that
    /// <c>total</c> is counted under the filter, and that the clear wipes everything whatever the
    /// reader had narrowed the table to.
    /// </para>
    /// <para>
    /// <b>Rows are planted directly, because this pass has no capture path.</b> The relaying
    /// process will own the write, so the connection rows are planted with a documented SQL
    /// fixture while every row they point at (account, subscription, stream) is planted through
    /// its Core repository. The seeding helper says why.
    /// </para>
    /// </remarks>
    public sealed class ConnectionLogTests : IntegrationTestBase
    {
        public ConnectionLogTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheUntouchedPageIsNewestFirstWithTheTotalAndTheBoundsEchoed()
        {
            var (admin, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            var oldest = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1),
                EndedAt(1));
            var middle = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(2),
                EndedAt(2));
            // The newest row is still live and carries a reported process identifier, so the
            // page is pinned to answer both fields as the row holds them: an open session must
            // read as `endedAt: null` and its pid must travel rather than be dropped by the
            // projection.
            var newest = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(3),
                endedAt: null,
                broadcastPid: 4821);

            // Two audio rows the default kind must exclude.
            var audio = await CreateAudioStreamAsync("Radio");
            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.AudioStream,
                audio,
                StartedAt(9));

            using var response = await Client.GetAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var page = body.RootElement;

            Assert.Equal(3, page.GetProperty("total").GetInt32());
            Assert.Equal(1, page.GetProperty("page").GetInt32());
            Assert.Equal(20, page.GetProperty("pageSize").GetInt32());

            var items = page.GetProperty("items");
            Assert.Equal(3, items.GetArrayLength());
            Assert.Equal([newest, middle, oldest], Ids(items));

            // Every field the row draws, resolved: the owner's name and the stream's title come
            // from their own tables, not from the log row.
            var row = items[0];
            Assert.Equal(subscription, Guid.Parse(row.GetProperty("subscriptionId").GetString()!));
            Assert.Equal("owner", row.GetProperty("username").GetString());
            Assert.Equal("video", row.GetProperty("streamKind").GetString());
            Assert.Equal(stream, Guid.Parse(row.GetProperty("streamId").GetString()!));
            Assert.Equal("News", row.GetProperty("streamTitle").GetString());
            Assert.Equal("203.0.113.9", row.GetProperty("ip").GetString());
            Assert.Equal("Mozilla/5.0 (Test)", row.GetProperty("userAgent").GetString());
            Assert.Equal("DE", row.GetProperty("countryCode").GetString());
            Assert.Equal("Example ISP", row.GetProperty("isp").GetString());

            // The two fields only an open session has: no end, and the pid the broadcasting side
            // reported. Both are planted above and must survive the projection.
            Assert.Equal(JsonValueKind.Null, row.GetProperty("endedAt").ValueKind);
            Assert.Equal(4821, row.GetProperty("broadcastPid").GetInt32());

            // The page is private: no intermediary may keep it.
            Assert.True(response.Headers.CacheControl?.NoStore);

            _ = admin;
        }

        [Fact]
        public async Task TheStateFilterSeparatesLiveFromFinishedAndUnknownValuesAreRefused()
        {
            var (_, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            var live = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1));
            var finished = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(2),
                EndedAt(2));

            using var liveOnly = await GetAsync("/admin/connection-logs?state=live");
            using var liveBody = await ReadJsonAsync(liveOnly);
            Assert.Equal(1, liveBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal([live], Ids(liveBody.RootElement.GetProperty("items")));
            Assert.Equal(
                JsonValueKind.Null,
                liveBody.RootElement.GetProperty("items")[0].GetProperty("endedAt").ValueKind);

            using var all = await GetAsync("/admin/connection-logs?state=all");
            using var allBody = await ReadJsonAsync(all);
            Assert.Equal(2, allBody.RootElement.GetProperty("total").GetInt32());

            // An unknown value names nothing, and is refused rather than treated as "no filter".
            foreach (var (parameter, value) in new[]
            {
                ("state", "ended"),
                ("kind", "videostream"),
                ("sortBy", "streamTitle"),
                ("sortOrder", "downwards")
            })
            {
                using var refused = await GetAsync($"/admin/connection-logs?{parameter}={value}");
                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

                using var refusedBody = await ReadJsonAsync(refused);
                Assert.True(
                    refusedBody.RootElement.GetProperty("errors").TryGetProperty(parameter, out _),
                    $"no errors.{parameter} for {parameter}={value}: {refusedBody.RootElement}");
            }

            _ = finished;
        }

        [Fact]
        public async Task TheKindDefaultsToVideoAndTheTitleResolvesFromTheRightMedium()
        {
            var (_, subscription) = await ArrangeAsync();
            var video = await CreateVideoStreamAsync("News");
            var audio = await CreateAudioStreamAsync("Radio");

            await using var connection = await Fixture.OpenConnectionAsync();

            var videoRow = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                video,
                StartedAt(1));
            var audioRow = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.AudioStream,
                audio,
                StartedAt(2));

            // Absent kind is video.
            using var defaulted = await GetAsync("/admin/connection-logs");
            using var defaultedBody = await ReadJsonAsync(defaulted);
            Assert.Equal(1, defaultedBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(videoRow, FirstId(defaultedBody.RootElement));
            Assert.Equal(
                "News",
                defaultedBody.RootElement.GetProperty("items")[0].GetProperty("streamTitle").GetString());

            // kind=audio answers only the audio row, and its title comes from the audio table.
            using var onlyAudio = await GetAsync("/admin/connection-logs?kind=audio");
            using var onlyAudioBody = await ReadJsonAsync(onlyAudio);
            Assert.Equal(1, onlyAudioBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(audioRow, FirstId(onlyAudioBody.RootElement));
            Assert.Equal("audio", onlyAudioBody.RootElement.GetProperty("items")[0].GetProperty("streamKind").GetString());
            Assert.Equal(
                "Radio",
                onlyAudioBody.RootElement.GetProperty("items")[0].GetProperty("streamTitle").GetString());

            // A kind spelled in another case is the same request.
            using var upperCase = await GetAsync("/admin/connection-logs?kind=AUDIO");
            using var upperCaseBody = await ReadJsonAsync(upperCase);
            Assert.Equal(1, upperCaseBody.RootElement.GetProperty("total").GetInt32());

            // A stream row that is gone leaves the title null rather than failing the page.
            var orphan = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                Guid.NewGuid(),
                StartedAt(5));

            using var withOrphan = await GetAsync("/admin/connection-logs?kind=video");
            using var withOrphanBody = await ReadJsonAsync(withOrphan);
            Assert.Equal(2, withOrphanBody.RootElement.GetProperty("total").GetInt32());

            var orphanRow = withOrphanBody.RootElement.GetProperty("items")
                .EnumerateArray()
                .Single(item => Guid.Parse(item.GetProperty("id").GetString()!) == orphan);
            Assert.Equal(JsonValueKind.Null, orphanRow.GetProperty("streamTitle").ValueKind);
        }

        [Fact]
        public async Task EveryFilterNarrowsTheListAndTheTotal()
        {
            var (_, subscription) = await ArrangeAsync();
            var otherSubscription = await CreateSubscriptionAsync("second");
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            var plain = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1),
                EndedAt(1),
                ip: "198.51.100.1",
                userAgent: "AlphaBrowser/1.0",
                countryCode: "DE",
                isp: "Alpha Net");

            var foreign = await PlantAsync(
                connection,
                otherSubscription,
                ConnectionStreamKind.VideoStream,
                Guid.NewGuid(),
                StartedAt(2),
                EndedAt(2),
                ip: "198.51.100.2",
                userAgent: "BetaBrowser/2.0",
                countryCode: "FR",
                isp: "Beta Net");

            // ip, exactly.
            await AssertNarrowedAsync($"?ip=198.51.100.1", plain);
            await AssertNarrowedAsync($"?ip=198.51.100.2", foreign);

            // country: stored upper case, filtered from either case.
            await AssertNarrowedAsync("?country=DE", plain);
            await AssertNarrowedAsync("?country=de", plain);
            await AssertNarrowedAsync("?country=fr", foreign);

            // isp, exactly.
            await AssertNarrowedAsync("?isp=Alpha Net", plain);

            // userAgent, exactly - and case included, so a client that differs only by its
            // own casing is a different client rather than the same one spelled differently.
            await AssertNarrowedAsync("?userAgent=AlphaBrowser/1.0", plain);
            await AssertNarrowedAsync("?userAgent=BetaBrowser/2.0", foreign);

            using (var otherCase = await GetAsync("/admin/connection-logs?userAgent=alphabrowser/1.0"))
            using (var otherCaseBody = await ReadJsonAsync(otherCase))
            {
                Assert.Equal(0, otherCaseBody.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    0,
                    otherCaseBody.RootElement.GetProperty("items").GetArrayLength());
            }

            // A blank value is "no filter", the same as an absent one - it names no client.
            using (var blank = await GetAsync("/admin/connection-logs?userAgent="))
            using (var blankBody = await ReadJsonAsync(blank))
            {
                Assert.Equal(2, blankBody.RootElement.GetProperty("total").GetInt32());
            }

            // subscriptionId and streamId.
            await AssertNarrowedAsync($"?subscriptionId={otherSubscription}", foreign);
            await AssertNarrowedAsync($"?streamId={stream}", plain);

            // ownerId is the subscription's owner, not a column of the log.
            var owner = await OwnerOfAsync(subscription);
            var otherOwner = await OwnerOfAsync(otherSubscription);
            await AssertNarrowedAsync($"?ownerId={owner}", plain);
            await AssertNarrowedAsync($"?ownerId={otherOwner}", foreign);

            // search is a case-insensitive substring of ip, user agent or isp.
            await AssertNarrowedAsync("?search=alphabrowser", plain);
            await AssertNarrowedAsync("?search=ALPHA+N", plain);
            await AssertNarrowedAsync("?search=198.51.100.2", foreign);

            // A search that matches nothing answers 0 with an empty page, not a refusal.
            using var none = await GetAsync("/admin/connection-logs?search=nothingmatches");
            using var noneBody = await ReadJsonAsync(none);
            Assert.Equal(0, noneBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(0, noneBody.RootElement.GetProperty("items").GetArrayLength());

            // A search above the cap is refused under its own parameter.
            using var longSearch = await GetAsync(
                $"/admin/connection-logs?search={new string('a', 201)}");
            await AssertCodeAsync(longSearch, HttpStatusCode.BadRequest, "validation_failed");
            using var longSearchBody = await ReadJsonAsync(longSearch);
            Assert.True(
                longSearchBody.RootElement.GetProperty("errors").TryGetProperty("search", out _));
        }

        [Fact]
        public async Task TheBoundsAreRefusedRatherThanClamped()
        {
            await ArrangeAsync();

            using var thousand = await GetAsync("/admin/connection-logs?pageSize=1000");
            Assert.Equal(HttpStatusCode.OK, thousand.StatusCode);

            using var refusedSize = await GetAsync("/admin/connection-logs?pageSize=1001");
            await AssertCodeAsync(refusedSize, HttpStatusCode.BadRequest, "validation_failed");
            using var sizeBody = await ReadJsonAsync(refusedSize);
            Assert.True(sizeBody.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));

            using var refusedPage = await GetAsync("/admin/connection-logs?page=0");
            await AssertCodeAsync(refusedPage, HttpStatusCode.BadRequest, "validation_failed");
            using var pageBody = await ReadJsonAsync(refusedPage);
            Assert.True(pageBody.RootElement.GetProperty("errors").TryGetProperty("page", out _));

            // The page past the end answers an empty page carrying the true total, not a refusal.
            using var beyond = await GetAsync("/admin/connection-logs?page=2000000000");
            Assert.Equal(HttpStatusCode.OK, beyond.StatusCode);
        }

        [Fact]
        public async Task APageTurnSplitsTheFilteredListExactlyOnce()
        {
            var (_, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            var first = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1),
                EndedAt(1));
            var second = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(2),
                EndedAt(2));
            var third = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(3),
                EndedAt(3));

            using var pageOne = await GetAsync("/admin/connection-logs?pageSize=2&page=1");
            using var pageOneBody = await ReadJsonAsync(pageOne);
            Assert.Equal(3, pageOneBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal([third, second], Ids(pageOneBody.RootElement.GetProperty("items")));

            using var pageTwo = await GetAsync("/admin/connection-logs?pageSize=2&page=2");
            using var pageTwoBody = await ReadJsonAsync(pageTwo);
            Assert.Equal(3, pageTwoBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal([first], Ids(pageTwoBody.RootElement.GetProperty("items")));

            // Ascending is the exact reverse of the default.
            using var ascending = await GetAsync(
                "/admin/connection-logs?sortBy=startedAt&sortOrder=asc");
            using var ascendingBody = await ReadJsonAsync(ascending);
            Assert.Equal([first, second, third], Ids(ascendingBody.RootElement.GetProperty("items")));
        }

        [Fact]
        public async Task AnEndedAtOrderGroupsLiveSessionsLastInBothDirections()
        {
            var (_, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            var live = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1));
            var earlier = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(2),
                EndedAt(2));
            var later = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(3),
                EndedAt(3));

            using var descending = await GetAsync(
                "/admin/connection-logs?sortBy=endedAt&sortOrder=desc");
            using var descendingBody = await ReadJsonAsync(descending);
            Assert.Equal([later, earlier, live], Ids(descendingBody.RootElement.GetProperty("items")));

            using var ascending = await GetAsync(
                "/admin/connection-logs?sortBy=endedAt&sortOrder=asc");
            using var ascendingBody = await ReadJsonAsync(ascending);
            Assert.Equal([earlier, later, live], Ids(ascendingBody.RootElement.GetProperty("items")));
        }

        [Fact]
        public async Task ClearingWipesEverythingWhateverTheFiltersAndIsIdempotent()
        {
            var (_, subscription) = await ArrangeAsync();
            var video = await CreateVideoStreamAsync("News");
            var audio = await CreateAudioStreamAsync("Radio");

            await using var connection = await Fixture.OpenConnectionAsync();

            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                video,
                StartedAt(1),
                EndedAt(1));
            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                video,
                StartedAt(2));
            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.AudioStream,
                audio,
                StartedAt(3));
            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.AudioStream,
                audio,
                StartedAt(4),
                EndedAt(4));

            Assert.Equal(4, await TestDataSeeder.CountConnectionLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // A request without the antiforgery pair is refused before a row is read, and the
            // table is untouched.
            using var noCsrf = await Client.DeleteAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            Assert.Equal(4, await TestDataSeeder.CountConnectionLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // With the pair the whole table goes: both media, live and finished alike. The query
            // string is deliberately *filtered* - the action must ignore it and wipe everything,
            // because the button says "clear all logs", not "clear the rows on screen".
            using var cleared = await DeleteAsync(
                "/admin/connection-logs?kind=audio&state=live&search=nomatch",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "connection_logs_cleared");

            Assert.Equal(0, await TestDataSeeder.CountConnectionLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // The list agrees, and every kind and state agrees.
            using var list = await GetAsync("/admin/connection-logs?state=all");
            using var listBody = await ReadJsonAsync(list);
            Assert.Equal(0, listBody.RootElement.GetProperty("total").GetInt32());

            // A second call is success too: the state the caller asked for is the state that
            // already holds.
            using var again = await DeleteAsync(
                "/admin/connection-logs",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.OK, "connection_logs_cleared");
        }

        [Fact]
        public async Task TheLogIsRefusedToAClientAndToAnonymous()
        {
            await ArrangeAsync();

            using var anonymous = Fixture.CreateClient();
            using (var anonymousList = await anonymous.GetAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");
            }

            using (var anonymousClear = await anonymous.DeleteAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(anonymousClear, HttpStatusCode.Unauthorized, "auth_required");
            }

            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);

            // A Client is refused by the role gate ahead of the permission gate, which is the
            // envelope every admin route of this API answers - `auth_role_changed`, not
            // `permission_denied`. The permission refusal is the answer to an *administrator*
            // whose group lacks the grant, and the test below pins that one.
            using (var refusedList = await client.GetAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedList, HttpStatusCode.Forbidden, "auth_role_changed");
            }
        }

        [Fact]
        public async Task AnAdministratorWithoutTheSubscriptionGrantsIsRefusedWithPermissionDenied()
        {
            await ArrangeAsync();

            // A group of the Admin role that holds no subscription grant: the role gate lets the
            // caller into the admin area, and the permission gate is what refuses this endpoint.
            var strangers = await CreateGroupAsync(
                "connection-log-strangers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewStreams);

            using var strangerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "logstranger", groupId: strangers);
            await SignInAsync(UserRole.Admin, "logstranger", "Password1", strangerClient);

            using (var refusedList = await strangerClient.GetAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedList, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var refusedClear = await strangerClient.DeleteAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedClear, HttpStatusCode.Forbidden, "permission_denied");
            }
        }

        [Fact]
        public async Task TheReadPermissionReadsButTheEditPermissionClears()
        {
            var (_, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();
            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1),
                EndedAt(1));

            // A group holding only the read grant reads the log and may not wipe it.
            var readers = await CreateGroupAsync(
                "connection-log-readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions);

            using var readerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "logreader", groupId: readers);
            await SignInAsync(UserRole.Admin, "logreader", "Password1", readerClient);

            using (var readable = await readerClient.GetAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
            }

            using (var refusedClear = await readerClient.DeleteAsync(
                "/admin/connection-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedClear, HttpStatusCode.Forbidden, "permission_denied");
            }

            Assert.Equal(1, await TestDataSeeder.CountConnectionLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // A group holding the edit grant clears it.
            var editors = await CreateGroupAsync(
                "connection-log-editors",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions,
                UserPermission.EditSubscriptions);

            using var editorClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "logeditor", groupId: editors);
            await SignInAsync(UserRole.Admin, "logeditor", "Password1", editorClient);

            var csrf = await GetCsrfTokenAsync(editorClient);
            using var cleared = await DeleteAsync(
                "/admin/connection-logs",
                csrf,
                editorClient);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "connection_logs_cleared");

            Assert.Equal(0, await TestDataSeeder.CountConnectionLogsAsync(
                connection,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task TheSubscriptionsListCarriesTheCountOfLiveConnectionsOnly()
        {
            var (_, subscription) = await ArrangeAsync();
            var otherSubscription = await CreateSubscriptionAsync("second");
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();

            // Two rows still running under the first subscription, one finished row and one row
            // under a different subscription: only the first two may be counted, so a query that
            // counted every row, or one that ignored the subscription, is told apart from the right
            // one by a single assertion.
            var liveOne = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1));
            var liveTwo = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.AudioStream,
                stream,
                StartedAt(2));
            var finished = await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(3),
                EndedAt(3));
            var foreign = await PlantAsync(
                connection,
                otherSubscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(4));

            using var list = await GetAsync("/admin/subscriptions");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using var body = await ReadJsonAsync(list);
            var page = body.RootElement;

            // The count is a per-row fact and must not narrow the pager: both subscriptions are on
            // the page whatever their connection counts are.
            Assert.Equal(2, page.GetProperty("total").GetInt32());

            var counts = page.GetProperty("items")
                .EnumerateArray()
                .ToDictionary(
                    item => Guid.Parse(item.GetProperty("id").GetString()!),
                    item => item.GetProperty("activeConnectionCount").GetInt32());

            Assert.Equal(2, counts[subscription]);
            Assert.Equal(1, counts[otherSubscription]);

            // Ending one of the live rows moves the count, which is the half that proves the query
            // reads the current rows rather than a snapshot taken when the page first loaded.
            await using (var command = new NpgsqlCommand(
                "update connection_logs set ended_at = @ended where id = @id",
                connection))
            {
                command.Parameters.AddWithValue("ended", DateTimeOffset.UtcNow);
                command.Parameters.AddWithValue("id", liveOne);
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            using var after = await GetAsync("/admin/subscriptions");
            using var afterBody = await ReadJsonAsync(after);
            var afterCounts = afterBody.RootElement.GetProperty("items")
                .EnumerateArray()
                .ToDictionary(
                    item => Guid.Parse(item.GetProperty("id").GetString()!),
                    item => item.GetProperty("activeConnectionCount").GetInt32());

            Assert.Equal(1, afterCounts[subscription]);

            _ = liveTwo;
            _ = finished;
            _ = foreign;
        }

        [Fact]
        public async Task DeletingTheSubscriptionRemovesItsLogs()
        {            var (owner, subscription) = await ArrangeAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using var connection = await Fixture.OpenConnectionAsync();
            await PlantAsync(
                connection,
                subscription,
                ConnectionStreamKind.VideoStream,
                stream,
                StartedAt(1),
                EndedAt(1));

            using var deleted = await DeleteAsync(
                $"/admin/subscriptions/{subscription}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "subscription_deleted");

            // The cascade is the recorded consequence: the log is evidence about the subscription,
            // and the subscription it was about is gone.
            Assert.Equal(0, await TestDataSeeder.CountConnectionLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            _ = owner;
        }

        /// <summary>
        /// Creates the account, subscription and signed-in admin every test of this file starts
        /// from.
        /// </summary>
        /// <returns>The owner's identifier and the subscription's.</returns>
        private async Task<(Guid Owner, Guid Subscription)> ArrangeAsync()
        {
            var issuer = await CreateAdminAndSignInAsync();

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var owner = await CreateUserAsync(UserRole.Client, "owner");

            var subscription = await TestDataSeeder.CreateSubscriptionAsync(
                scope.ServiceProvider,
                owner,
                issuer,
                TestContext.Current.CancellationToken);

            return (owner, subscription);
        }

        /// <summary>Creates a second subscription, for a filter that must tell two of them apart.</summary>
        private async Task<Guid> CreateSubscriptionAsync(string ownerName)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var owner = await CreateUserAsync(UserRole.Client, ownerName);
            var issuer = await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                $"issuer-{ownerName}",
                "Password1",
                UserRole.Admin,
                cancellationToken: TestContext.Current.CancellationToken);

            return await TestDataSeeder.CreateSubscriptionAsync(
                scope.ServiceProvider,
                owner,
                issuer,
                TestContext.Current.CancellationToken);
        }

        /// <summary>Reads the owner of a subscription, for the owner filter.</summary>
        private async Task<Guid> OwnerOfAsync(Guid subscriptionId)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var subscriptions = scope.ServiceProvider
                .GetRequiredService<Fluxy.Core.Subscriptions.Repositories.ISubscriptionRepository>();

            var detail = await subscriptions.GetAsync(
                subscriptionId,
                TestContext.Current.CancellationToken);

            Assert.NotNull(detail);

            return detail!.UserId;
        }

        private async Task<Guid> CreateVideoStreamAsync(string name)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateVideoStreamAsync(
                scope.ServiceProvider,
                name,
                TestContext.Current.CancellationToken);
        }

        private async Task<Guid> CreateAudioStreamAsync(string name)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateAudioStreamAsync(
                scope.ServiceProvider,
                name,
                TestContext.Current.CancellationToken);
        }

        private Task<Guid> PlantAsync(
            NpgsqlConnection connection,
            Guid subscriptionId,
            ConnectionStreamKind kind,
            Guid streamId,
            DateTimeOffset startedAt,
            DateTimeOffset? endedAt = null,
            string ip = "203.0.113.9",
            string? userAgent = "Mozilla/5.0 (Test)",
            string? countryCode = "DE",
            string? isp = "Example ISP",
            int? broadcastPid = null)
            => TestDataSeeder.PlantConnectionLogAsync(
                connection,
                subscriptionId,
                kind,
                streamId,
                startedAt,
                endedAt,
                ip,
                userAgent,
                countryCode,
                isp,
                broadcastPid,
                cancellationToken: TestContext.Current.CancellationToken);

        /// <summary>Asserts that a filtered query answers exactly one row, the named one.</summary>
        private async Task AssertNarrowedAsync(string query, Guid expectedId)
        {
            using var response = await GetAsync($"/admin/connection-logs{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(expectedId, FirstId(body.RootElement));
        }

        private Task<HttpResponseMessage> GetAsync(string path)
            => Client.GetAsync(path, TestContext.Current.CancellationToken);

        /// <summary>A fixed instant, so the order is decided by what the test wrote.</summary>
        private static DateTimeOffset StartedAt(int hour)
            => new(2026, 5, 1, hour, 0, 0, TimeSpan.Zero);

        /// <summary>The end of a session that opened at <paramref name="hour"/>.</summary>
        private static DateTimeOffset EndedAt(int hour)
            => StartedAt(hour).AddMinutes(30);

        private static Guid FirstId(JsonElement page)
            => Guid.Parse(page.GetProperty("items")[0].GetProperty("id").GetString()!);

        private static IReadOnlyList<Guid> Ids(JsonElement items)
            => items
                .EnumerateArray()
                .Select(item => Guid.Parse(item.GetProperty("id").GetString()!))
                .ToList();
    }
}
