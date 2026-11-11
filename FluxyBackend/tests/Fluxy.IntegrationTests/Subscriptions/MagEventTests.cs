using System.Net;
using System.Text.Json;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Subscriptions
{
    /// <summary>
    /// The MAG event journal end to end: the page, every filter and refusal, the send rules for all
    /// six operations, the two permission gates, the antiforgery pair, the whole-table clear and the
    /// cascade from the subscription.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rows are planted through the Core repository.</b> The production writer is
    /// <c>IMagEventService.SendAsync</c>, whose rules the send tests exercise through the endpoint;
    /// the list tests need rows whose instants and operations they control, so they stage through
    /// <see cref="TestDataSeeder.PlantMagEventsAsync"/> and commit once.
    /// </para>
    /// <para>
    /// <b>One save stamps one instant.</b> That is what makes the same-instant tie-break test
    /// possible: rows planted in one call share <c>created_at</c>, so only the identifier can order
    /// them. A test that needs two instants advances the fixture's <c>FakeTimeProvider</c> and
    /// plants again.
    /// </para>
    /// </remarks>
    public sealed class MagEventTests : IntegrationTestBase
    {
        private const string Password = "Password1";

        public MagEventTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ThePageDefaultsToNewestFirstWithAnIdentifierTieBreak()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:01");

            await PlantAsync(Row(subscription, MagEventType.ClosePortal));
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(subscription, MagEventType.ReloadPortal));

            using var response = await GetPageAsync(string.Empty);
            response.EnsureSuccessStatusCode();

            // A page about the installation's sent events is not something a cache may keep.
            Assert.True(response.Headers.CacheControl?.NoStore);

            using (var body = await ReadJsonAsync(response))
            {
                Assert.Equal(2, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
                Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());

                var rows = Rows(body);

                Assert.Equal("reloadPortal", rows[0].GetProperty("eventType").GetString());
                Assert.Equal("closePortal", rows[1].GetProperty("eventType").GetString());

                // The device address is joined from the subscription and every identifier is text.
                Assert.Equal("AA:BB:CC:DD:EE:01", rows[0].GetProperty("magMac").GetString());
                Assert.Equal(subscription.ToString(), rows[0].GetProperty("subscriptionId").GetString());
                Assert.Equal("notConfirmed", rows[0].GetProperty("status").GetString());
                Assert.True(Guid.TryParse(rows[0].GetProperty("id").GetString(), out _));
            }

            // Same instant for every planted row: the identifier decides, descending.
            await ClearAsync();

            await PlantAsync(
                Row(subscription, MagEventType.ClosePortal),
                Row(subscription, MagEventType.ClosePortal),
                Row(subscription, MagEventType.ClosePortal));

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
            // order - so ordering the text is the database's own descending tie-break.
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
            await AssertRefusedAsync("event=play", "event");
            await AssertRefusedAsync("event=0", "event");
            await AssertRefusedAsync("event=resetStbLockk", "event");
            await AssertRefusedAsync("sortBy=message", "sortBy");
            await AssertRefusedAsync("sortOrder=up", "sortOrder");
            await AssertRefusedAsync("dateFrom=yesterday", "dateFrom");
            await AssertRefusedAsync("dateTo=05/01/2026", "dateTo");
            await AssertRefusedAsync("subscriptionId=not-a-uuid", "subscriptionId");
        }

        [Fact]
        public async Task TheSubscriptionAndEventFiltersNarrowItemsAndTotal()
        {
            await CreateAdminAndSignInAsync();
            var first = await CreateMagSubscriptionAsync("first", "AA:BB:CC:DD:EE:11");
            var second = await CreateMagSubscriptionAsync("second", "AA:BB:CC:DD:EE:22");

            var close = await PlantOneAsync(Row(first, MagEventType.ClosePortal));
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            var reboot = await PlantOneAsync(Row(first, MagEventType.RebootDevice));
            var message = await PlantOneAsync(
                Row(second, MagEventType.SendMessage, "hello", needConfirm: true));

            using (var all = await ReadPageAsync(string.Empty))
            {
                Assert.Equal(3, all.RootElement.GetProperty("total").GetInt32());
            }

            // subscriptionId narrows both the rows and the count, and every row names it.
            using (var narrowed = await ReadPageAsync($"subscriptionId={first}"))
            {
                Assert.Equal(2, narrowed.RootElement.GetProperty("total").GetInt32());
                Assert.All(
                    Rows(narrowed),
                    row => Assert.Equal(
                        first.ToString(),
                        row.GetProperty("subscriptionId").GetString()));
            }

            // event names exactly one operation, case-insensitively.
            using (var byEvent = await ReadPageAsync("event=rebootDevice"))
            {
                Assert.Equal(1, byEvent.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(reboot, FirstId(byEvent));
            }

            using (var byUpperCase = await ReadPageAsync("event=REBOOTDEVICE"))
            {
                Assert.Equal(1, byUpperCase.RootElement.GetProperty("total").GetInt32());
            }

            // A blank event is "no filter", exactly like an absent one.
            using (var blank = await ReadPageAsync("event="))
            {
                Assert.Equal(3, blank.RootElement.GetProperty("total").GetInt32());
            }

            // The filters compose into one predicate.
            using (var composed = await ReadPageAsync(
                $"subscriptionId={first}&event=closePortal"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(close, FirstId(composed));
            }

            _ = message;
        }

        [Fact]
        public async Task TheSearchCoversTheMessageAndTheDeviceMacAndKeepsTheTotalHonest()
        {
            await CreateAdminAndSignInAsync();
            var named = await CreateMagSubscriptionAsync("named", "AA:BB:CC:DD:EE:33");
            var other = await CreateMagSubscriptionAsync("other", "11:22:33:44:55:66");

            await PlantAsync(
                Row(named, MagEventType.SendMessage, "The Upstream Server Refused", needConfirm: true),
                Row(named, MagEventType.ClosePortal),
                Row(other, MagEventType.SendMessage, "a different sentence", needConfirm: true));

            // Case-insensitive substring of the stored message.
            using (var byMessage = await ReadPageAsync("search=UPSTREAM"))
            {
                Assert.Equal(1, byMessage.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "The Upstream Server Refused",
                    SingleRow(byMessage).GetProperty("message").GetString());
            }

            // The MAC of the subscription is part of the same search, through the EXISTS, so the
            // count is over mag_events alone and still describes the filtered list.
            using (var byMac = await ReadPageAsync("search=ee:33"))
            {
                Assert.Equal(2, byMac.RootElement.GetProperty("total").GetInt32());
                Assert.All(
                    Rows(byMac),
                    row => Assert.Equal(
                        named.ToString(),
                        row.GetProperty("subscriptionId").GetString()));
            }

            // A search that matches neither column is an empty page, not a refusal.
            using (var none = await ReadPageAsync("search=nothingmatches"))
            {
                Assert.Equal(0, none.RootElement.GetProperty("total").GetInt32());
                Assert.Empty(Rows(none));
            }
        }

        [Fact]
        public async Task TheDateRangeIsInclusiveOnBothEndsComposesAndIsOptional()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:44");

            var oldest = await PlantOneAsync(Row(subscription, MagEventType.ClosePortal));
            var oldestAt = Fixture.Clock.GetUtcNow();

            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            var middle = await PlantOneAsync(Row(subscription, MagEventType.ReloadPortal));
            var middleAt = Fixture.Clock.GetUtcNow();

            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            var newest = await PlantOneAsync(Row(subscription, MagEventType.RebootDevice));
            var newestAt = Fixture.Clock.GetUtcNow();

            // Absent bounds mean no bound at all.
            using (var unbounded = await ReadPageAsync(string.Empty))
            {
                Assert.Equal(3, unbounded.RootElement.GetProperty("total").GetInt32());
            }

            // Both ends inclusive: the bounds name the rows' own instants, so both endpoint rows
            // are kept when each side is the bound itself.
            using (var bounded = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(oldestAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, bounded.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    [middle, oldest],
                    Ids(bounded.RootElement.GetProperty("items")));
            }

            // Either side alone is one open-ended bound.
            using (var fromOnly = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, fromOnly.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(newest, FirstId(fromOnly));
            }

            using (var toOnly = await ReadPageAsync(
                $"dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, toOnly.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(middle, FirstId(toOnly));
            }

            // An inverted range is a valid query over an empty interval, not a refusal: 200 with an
            // empty page and a total of zero.
            using var invertedResponse = await GetPageAsync(
                $"dateFrom={Uri.EscapeDataString(newestAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(oldestAt.ToString("O"))}");

            Assert.Equal(HttpStatusCode.OK, invertedResponse.StatusCode);

            using (var inverted = await ReadJsonAsync(invertedResponse))
            {
                Assert.Equal(0, inverted.RootElement.GetProperty("total").GetInt32());
                Assert.Empty(Rows(inverted));
            }

            // The range composes with the subscription and the operation.
            using (var composed = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(middleAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}" +
                $"&subscriptionId={subscription}&event=reloadPortal"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(middle, FirstId(composed));
            }
        }

        [Fact]
        public async Task TheBoundsAreRefusedRatherThanClamped()
        {
            await CreateAdminAndSignInAsync();

            using var thousand = await GetPageAsync("pageSize=1000");
            Assert.Equal(HttpStatusCode.OK, thousand.StatusCode);

            using var refused = await GetPageAsync("pageSize=1001");
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(refused);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));
        }

        [Fact]
        public async Task ASentMessageIsRecordedOnceAndForcedToNeedConfirmation()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:55");

            using var response = await PostJsonAsync(
                "/admin/mag-events",
                new
                {
                    subscriptionId = subscription.ToString(),
                    @event = "sendMessage",
                    message = "  Hello there  ",
                    needConfirm = false,
                    rebootAfterOk = true,
                    autoHideTimeout = true,
                    additionalServicesOn = false,
                    anec = true,
                    vclub = true
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.Created, "mag_event_sent");

            var id = Guid.Parse(IdFromLocation(response));
            Assert.Equal($"/admin/mag-events/{id}", response.Headers.Location?.ToString());

            using var page = await ReadPageAsync(string.Empty);
            Assert.Equal(1, page.RootElement.GetProperty("total").GetInt32());

            var row = SingleRow(page);
            Assert.Equal(id, Guid.Parse(row.GetProperty("id").GetString()!));
            Assert.Equal("sendMessage", row.GetProperty("eventType").GetString());
            Assert.Equal("Hello there", row.GetProperty("message").GetString());
            Assert.Equal("notConfirmed", row.GetProperty("status").GetString());
            Assert.Equal("AA:BB:CC:DD:EE:55", row.GetProperty("magMac").GetString());

            // The server forced the flag and stored the rest as the body named it.
            await using var connection = await Fixture.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select need_confirm, reboot_after_ok, auto_hide_timeout, " +
                "additional_services_on, anec, vclub from mag_events where id = @id",
                connection);
            command.Parameters.AddWithValue("id", id);

            await using var reader = await command.ExecuteReaderAsync(
                TestContext.Current.CancellationToken);

            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.True(reader.GetBoolean(0));
            Assert.True(reader.GetBoolean(1));
            Assert.True(reader.GetBoolean(2));
            Assert.False(reader.GetBoolean(3));
            Assert.True(reader.GetBoolean(4));
            Assert.True(reader.GetBoolean(5));
        }

        [Fact]
        public async Task EveryOperationAppliesItsOwnMessageAndFlagRules()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:66");
            var channel = Guid.NewGuid();
            var csrf = await GetCsrfTokenAsync();

            // playChannel: a uuid is required and stored as the message; a non-uuid is refused.
            using (var played = await PostJsonAsync(
                "/admin/mag-events",
                new
                {
                    subscriptionId = subscription.ToString(),
                    @event = "playChannel",
                    message = channel.ToString()
                },
                csrf))
            {
                await AssertCodeAsync(played, HttpStatusCode.Created, "mag_event_sent");
            }

            using (var badChannel = await PostJsonAsync(
                "/admin/mag-events",
                new
                {
                    subscriptionId = subscription.ToString(),
                    @event = "playChannel",
                    message = "not-a-channel"
                },
                csrf))
            {
                await AssertCodeAsync(badChannel, HttpStatusCode.BadRequest, "validation_failed");
                using var body = await ReadJsonAsync(badChannel);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("message", out _));
            }

            // sendMessage: a blank message is refused under message.
            using (var blank = await PostJsonAsync(
                "/admin/mag-events",
                new
                {
                    subscriptionId = subscription.ToString(),
                    @event = "sendMessage",
                    message = "   "
                },
                csrf))
            {
                await AssertCodeAsync(blank, HttpStatusCode.BadRequest, "validation_failed");
                using var body = await ReadJsonAsync(blank);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("message", out _));
            }

            // sendMessage: a message longer than the column is refused, not cut.
            using (var tooLong = await PostJsonAsync(
                "/admin/mag-events",
                new
                {
                    subscriptionId = subscription.ToString(),
                    @event = "sendMessage",
                    message = new string('m', MagEventListLimits.MessageMaxLength + 1)
                },
                csrf))
            {
                await AssertCodeAsync(tooLong, HttpStatusCode.BadRequest, "validation_failed");
            }

            // closePortal, reloadPortal, rebootDevice and resetStbLock drop any message sent.
            var dropped = new[] { "closePortal", "reloadPortal", "rebootDevice", "resetStbLock" };

            foreach (var operation in dropped)
            {
                using var response = await PostJsonAsync(
                    "/admin/mag-events",
                    new
                    {
                        subscriptionId = subscription.ToString(),
                        @event = operation,
                        message = "ignored payload"
                    },
                    csrf);

                await AssertCodeAsync(response, HttpStatusCode.Created, "mag_event_sent");
            }

            using var page = await ReadPageAsync($"subscriptionId={subscription}&pageSize=10");

            // One accepted playChannel plus the four operations that drop their message: the two
            // refused sendMessage attempts left no trace at all.
            Assert.Equal(5, page.RootElement.GetProperty("total").GetInt32());

            var rows = Rows(page);

            var playedRow = rows.Single(
                row => row.GetProperty("eventType").GetString() == "playChannel");
            Assert.Equal(channel.ToString(), playedRow.GetProperty("message").GetString());
            Assert.Equal("notConfirmed", playedRow.GetProperty("status").GetString());

            foreach (var row in rows.Where(
                row => dropped.Contains(row.GetProperty("eventType").GetString())))
            {
                Assert.Equal(JsonValueKind.Null, row.GetProperty("message").ValueKind);
            }
        }

        [Fact]
        public async Task AnAbsentOrUnknownEventAndSubscriptionAreRefused()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:77");
            var csrf = await GetCsrfTokenAsync();

            using (var noEvent = await PostJsonAsync(
                "/admin/mag-events",
                new { subscriptionId = subscription.ToString(), message = "hello" },
                csrf))
            {
                await AssertCodeAsync(noEvent, HttpStatusCode.BadRequest, "validation_failed");
                using var body = await ReadJsonAsync(noEvent);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("event", out _));
            }

            using (var unknownEvent = await PostJsonAsync(
                "/admin/mag-events",
                new { subscriptionId = subscription.ToString(), @event = "destroy" },
                csrf))
            {
                await AssertCodeAsync(unknownEvent, HttpStatusCode.BadRequest, "validation_failed");
                using var body = await ReadJsonAsync(unknownEvent);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("event", out _));
            }

            using (var noSubscription = await PostJsonAsync(
                "/admin/mag-events",
                new { @event = "closePortal" },
                csrf))
            {
                await AssertCodeAsync(noSubscription, HttpStatusCode.BadRequest, "validation_failed");
                using var body = await ReadJsonAsync(noSubscription);
                Assert.True(
                    body.RootElement.GetProperty("errors").TryGetProperty("subscriptionId", out _));
            }

            using (var unknownSubscription = await PostJsonAsync(
                "/admin/mag-events",
                new { subscriptionId = Guid.NewGuid().ToString(), @event = "closePortal" },
                csrf))
            {
                await AssertCodeAsync(unknownSubscription, HttpStatusCode.BadRequest, "validation_failed");
                using var body = await ReadJsonAsync(unknownSubscription);
                Assert.True(
                    body.RootElement.GetProperty("errors").TryGetProperty("subscriptionId", out _));
            }

            await using var connection = await Fixture.OpenConnectionAsync();
            Assert.Equal(0, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ASubscriptionWithoutAMagDeviceIsRefusedWithItsOwnCode()
        {
            await CreateAdminAndSignInAsync();

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var owner = await CreateUserAsync(UserRole.Client, "no-device-owner");
            var issuer = await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                "issuer",
                Password,
                UserRole.Admin,
                cancellationToken: TestContext.Current.CancellationToken);

            var subscription = await TestDataSeeder.CreateSubscriptionAsync(
                scope.ServiceProvider,
                owner,
                issuer,
                TestContext.Current.CancellationToken);

            using var response = await PostJsonAsync(
                "/admin/mag-events",
                new { subscriptionId = subscription.ToString(), @event = "rebootDevice" },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "mag_device_missing");
        }

        [Fact]
        public async Task TheSendNeedsTheCsrfPairAndTheListNeedsTheSubscriptionGrants()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:88");

            // No antiforgery pair: the body is never read and nothing is written.
            using (var noCsrf = await PostJsonAsync(
                "/admin/mag-events",
                new { subscriptionId = subscription.ToString(), @event = "rebootDevice" }))
            {
                await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            await using var connection = await Fixture.OpenConnectionAsync();
            Assert.Equal(0, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // A group of the Admin role holding only the read grant reads but may not send or clear.
            var readers = await CreateGroupAsync(
                "mag-readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions);

            using var reader = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "magreader", groupId: readers);
            await SignInAsync(UserRole.Admin, "magreader", Password, reader);

            using (var readable = await reader.GetAsync(
                "/admin/mag-events",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
            }

            using (var refusedSend = await PostJsonAsync(
                "/admin/mag-events",
                new { subscriptionId = subscription.ToString(), @event = "rebootDevice" },
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(refusedSend, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var refusedClear = await DeleteAsync(
                "/admin/mag-events",
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(refusedClear, HttpStatusCode.Forbidden, "permission_denied");
            }

            // A client is refused by the role gate ahead of any permission.
            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "magclient", client: client);

            using (var refusedClient = await client.GetAsync(
                "/admin/mag-events",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedClient, HttpStatusCode.Forbidden, "auth_role_changed");
            }

            // Anonymous is answered as such.
            using var anonymous = Fixture.CreateClient();

            using (var anonymousList = await anonymous.GetAsync(
                "/admin/mag-events",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");
            }
        }

        [Fact]
        public async Task ClearingWipesEverythingWhateverTheFiltersAndIsIdempotent()
        {
            await CreateAdminAndSignInAsync();
            var first = await CreateMagSubscriptionAsync("first", "AA:BB:CC:DD:EE:99");
            var second = await CreateMagSubscriptionAsync("second", "AA:BB:CC:DD:EE:AA");

            await PlantAsync(
                Row(first, MagEventType.ClosePortal),
                Row(first, MagEventType.SendMessage, "first message", needConfirm: true),
                Row(second, MagEventType.RebootDevice));

            await using var connection = await Fixture.OpenConnectionAsync();

            Assert.Equal(3, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // Without the antiforgery pair the clear is refused and the table is untouched.
            using (var noCsrf = await Client.DeleteAsync(
                "/admin/mag-events",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            Assert.Equal(3, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // With the pair the whole table goes even though a filter was sent: the action names
            // the collection, never the filtered subset.
            using (var cleared = await DeleteAsync(
                $"/admin/mag-events?subscriptionId={first}&event=closePortal&search=nothing",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(cleared, HttpStatusCode.OK, "mag_events_cleared");
                using var body = await ReadJsonAsync(cleared);
                Assert.Equal(
                    "Cleared the MAG event journal: 3 row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }

            Assert.Equal(0, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // The second press is still success, with zero.
            using (var again = await DeleteAsync(
                "/admin/mag-events",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(again, HttpStatusCode.OK, "mag_events_cleared");
                using var body = await ReadJsonAsync(again);
                Assert.Equal(
                    "Cleared the MAG event journal: 0 row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }
        }

        [Fact]
        public async Task APerRowMethodAnswersMethodNotAllowed()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:BB");
            var id = await PlantOneAsync(Row(subscription, MagEventType.ClosePortal));
            var csrf = await GetCsrfTokenAsync();

            using (var patch = await PatchJsonAsync(
                $"/admin/mag-events/{id}",
                new { message = "made up" },
                csrf))
            {
                await AssertCodeAsync(patch, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var put = await PutJsonAsync(
                $"/admin/mag-events/{id}",
                new { @event = "rebootDevice" },
                csrf))
            {
                await AssertCodeAsync(put, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var delete = await DeleteAsync($"/admin/mag-events/{id}", csrf))
            {
                await AssertCodeAsync(delete, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            // The collection answers the same for methods it does not own.
            using (var collectionPut = await PutJsonAsync(
                "/admin/mag-events",
                new { @event = "rebootDevice" },
                csrf))
            {
                await AssertCodeAsync(
                    collectionPut,
                    HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed");
            }

            await using var connection = await Fixture.OpenConnectionAsync();
            Assert.Equal(1, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task APerRowMethodAnswersMethodNotAllowedForAReadOnlyAdmin()
        {
            // The 405 describes the resource, not the caller's grant: the fence is the absence of a
            // handler, so a group holding only `viewSubscriptions` must not be turned away with 403
            // before the fence is reached.
            var readers = await CreateGroupAsync(
                "mag-fence-readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSubscriptions);

            using var reader = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "magfencereader", groupId: readers);
            await SignInAsync(UserRole.Admin, "magfencereader", Password, reader);

            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:BC");
            var id = await PlantOneAsync(Row(subscription, MagEventType.ClosePortal));
            var csrf = await GetCsrfTokenAsync(reader);

            using (var patch = await PatchJsonAsync(
                $"/admin/mag-events/{id}",
                new { message = "made up" },
                csrf,
                reader))
            {
                await AssertCodeAsync(patch, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var put = await PutJsonAsync(
                $"/admin/mag-events/{id}",
                new { @event = "rebootDevice" },
                csrf,
                reader))
            {
                await AssertCodeAsync(put, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var delete = await DeleteAsync($"/admin/mag-events/{id}", csrf, reader))
            {
                await AssertCodeAsync(delete, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            await using var connection = await Fixture.OpenConnectionAsync();
            Assert.Equal(1, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeletingTheSubscriptionRemovesItsEvents()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:CC");

            await PlantAsync(
                Row(subscription, MagEventType.ClosePortal),
                Row(subscription, MagEventType.ReloadPortal));

            using var deleted = await DeleteAsync(
                $"/admin/subscriptions/{subscription}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "subscription_deleted");

            await using var connection = await Fixture.OpenConnectionAsync();
            Assert.Equal(0, await TestDataSeeder.CountMagEventsAsync(
                connection,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AConfirmedRowReadsAsConfirmed()
        {
            await CreateAdminAndSignInAsync();
            var subscription = await CreateMagSubscriptionAsync("owner", "AA:BB:CC:DD:EE:DD");
            var id = await PlantOneAsync(Row(subscription, MagEventType.RebootDevice));

            // The confirmation path belongs to the future streaming subsystem; until it exists,
            // this SQL fixture is the only way to pin that a stored 1 reads as `confirmed` rather
            // than as the send's default.
            await using var connection = await Fixture.OpenConnectionAsync();
            await using (var command = new NpgsqlCommand(
                "update mag_events set status = 1 where id = @id",
                connection))
            {
                command.Parameters.AddWithValue("id", id);
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            using var page = await ReadPageAsync(string.Empty);
            Assert.Equal("confirmed", SingleRow(page).GetProperty("status").GetString());
        }

        /// <summary>Builds a resolved row with the facts a list assertion needs.</summary>
        private static MagEventWrite Row(
            Guid subscriptionId,
            MagEventType eventType,
            string? message = null,
            bool needConfirm = false)
            => new()
            {
                SubscriptionId = subscriptionId,
                EventType = eventType,
                Message = message,
                NeedConfirm = needConfirm,
                RebootAfterOk = false,
                AutoHideTimeout = false,
                AdditionalServicesOn = true,
                Anec = false,
                Vclub = false
            };

        /// <summary>Creates an account, an issuer and a subscription carrying a MAG device.</summary>
        private async Task<Guid> CreateMagSubscriptionAsync(string ownerName, string magMac)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var owner = await CreateUserAsync(UserRole.Client, ownerName);
            var issuer = await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                $"issuer-{ownerName}",
                Password,
                UserRole.Admin,
                cancellationToken: TestContext.Current.CancellationToken);

            return await TestDataSeeder.CreateSubscriptionAsync(
                scope.ServiceProvider,
                owner,
                issuer,
                TestContext.Current.CancellationToken,
                magMac);
        }

        private async Task PlantAsync(params MagEventWrite[] rows)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            await TestDataSeeder.PlantMagEventsAsync(
                scope.ServiceProvider,
                rows,
                TestContext.Current.CancellationToken);
        }

        private async Task<Guid> PlantOneAsync(MagEventWrite row)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var ids = await TestDataSeeder.PlantMagEventsAsync(
                scope.ServiceProvider,
                [row],
                TestContext.Current.CancellationToken);

            return ids[0];
        }

        private Task<HttpResponseMessage> GetPageAsync(string query)
            => Client.GetAsync(
                query.Length == 0
                    ? "/admin/mag-events"
                    : $"/admin/mag-events?{query}",
                TestContext.Current.CancellationToken);

        private async Task<JsonDocument> ReadPageAsync(string query)
        {
            using var response = await GetPageAsync(query);
            response.EnsureSuccessStatusCode();

            return await ReadJsonAsync(response);
        }

        private async Task ClearAsync()
        {
            using var response = await DeleteAsync(
                "/admin/mag-events",
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

        private static Guid FirstId(JsonDocument document)
            => Guid.Parse(document.RootElement.GetProperty("items")[0].GetProperty("id").GetString()!);

        private static Guid[] Ids(JsonElement items)
            => items
                .EnumerateArray()
                .Select(row => Guid.Parse(row.GetProperty("id").GetString()!))
                .ToArray();
    }
}
