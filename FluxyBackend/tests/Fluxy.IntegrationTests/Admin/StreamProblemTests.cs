using System.Net;
using System.Text.Json;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The stream problem journal end to end: the read surface, every filter and refusal, the
    /// whole-table clear, the split permission gate, the write fence, the stream-name join and the
    /// record path the future producer will call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rows are planted through the Core repository.</b> The journal has no HTTP write path by
    /// design, so there is no endpoint a test could call to create a row. Rows therefore go
    /// through <see cref="TestDataSeeder.PlantStreamProblemsAsync"/>, which stages through
    /// <see cref="IStreamProblemRepository"/> and commits once - the same insert intent the
    /// Development seeder uses. The assertions go through the API.
    /// </para>
    /// <para>
    /// <b>One save stamps one instant.</b> That is what makes the same-instant tie-break test
    /// possible: rows planted in one call share <c>created_at</c>, so only the identifier can
    /// order them. A test that needs two instants advances the fixture's <c>FakeTimeProvider</c>
    /// and plants again.
    /// </para>
    /// <para>
    /// <b>The stream's name is asserted, not taken for granted.</b> The name is resolved by a join
    /// on <c>video_streams</c> rather than stored, so a read path that forgot the join would answer
    /// null and the column would be empty; the list tests check the name the planted stream
    /// carries.
    /// </para>
    /// </remarks>
    public sealed class StreamProblemTests : IntegrationTestBase
    {
        private const string Password = "Password1";

        public StreamProblemTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ThePageDefaultsToNewestFirstWithAnIdentifierTieBreak()
        {
            await CreateAdminAndSignInAsync();
            var stream = await CreateVideoStreamAsync("News");

            // Older first, then the clock moves one second, so the default order has two distinct
            // instants to separate. The step stays inside the access token's clock skew: the fake
            // clock is shared by the collection and a jump a token's `nbf` cannot clear would turn
            // a later request into a 401 rather than testing this page.
            await PlantAsync(Row(stream, "older problem"));
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(stream, "newer problem"));

            using var response = await GetPageAsync(string.Empty);
            response.EnsureSuccessStatusCode();

            // A page about the installation's stream failures is not something a cache may keep.
            Assert.True(response.Headers.CacheControl?.NoStore);

            using (var body = await ReadJsonAsync(response))
            {
                Assert.Equal(2, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
                Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());

                var rows = Rows(body);

                Assert.Equal("newer problem", rows[0].GetProperty("errorMessage").GetString());
                Assert.Equal("older problem", rows[1].GetProperty("errorMessage").GetString());

                // The join filled the name, and the two identifiers are text on the wire.
                Assert.Equal("News", rows[0].GetProperty("streamName").GetString());
                Assert.Equal(stream.ToString(), rows[0].GetProperty("streamId").GetString());
                Assert.True(Guid.TryParse(rows[0].GetProperty("id").GetString(), out _));
            }

            // Same instant for every planted row: the identifier decides, descending.
            await ClearAsync();

            await PlantAsync(
                Row(stream, "same instant one"),
                Row(stream, "same instant two"),
                Row(stream, "same instant three"));

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
            await AssertRefusedAsync("sortBy=foo", "sortBy");
            await AssertRefusedAsync("sortOrder=up", "sortOrder");
            await AssertRefusedAsync("dateFrom=yesterday", "dateFrom");
            await AssertRefusedAsync("dateTo=05/01/2026", "dateTo");
            await AssertRefusedAsync("streamId=not-a-uuid", "streamId");
        }

        [Fact]
        public async Task TheStreamFilterNarrowsItemsAndTotalAndComposes()
        {
            await CreateAdminAndSignInAsync();
            var firstStream = await CreateVideoStreamAsync("First");
            var secondStream = await CreateVideoStreamAsync("Second");

            await PlantAsync(Row(firstStream, "refused the connection"));
            var earliest = Fixture.Clock.GetUtcNow();
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(firstStream, "the source timed out"));
            await PlantAsync(Row(secondStream, "refused the connection"));

            // Unfiltered: everything the two streams produced.
            using (var all = await ReadPageAsync(string.Empty))
            {
                Assert.Equal(3, all.RootElement.GetProperty("total").GetInt32());
            }

            // One stream narrows both the rows and the count, and every row names that stream.
            using (var narrowed = await ReadPageAsync($"streamId={firstStream}"))
            {
                Assert.Equal(2, narrowed.RootElement.GetProperty("total").GetInt32());
                Assert.All(
                    Rows(narrowed),
                    row => Assert.Equal(
                        firstStream.ToString(),
                        row.GetProperty("streamId").GetString()));
                Assert.All(
                    Rows(narrowed),
                    row => Assert.Equal("First", row.GetProperty("streamName").GetString()));
            }

            // The stream, the range and the message compose into one predicate: only the row
            // carrying all three facts is listed, and `total` counts exactly that set.
            using (var composed = await ReadPageAsync(
                $"streamId={firstStream}" +
                $"&dateFrom={Uri.EscapeDataString(earliest.ToString("O"))}" +
                "&search=refused"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "refused the connection",
                    SingleRow(composed).GetProperty("errorMessage").GetString());
            }
        }

        [Fact]
        public async Task TheDateRangeIsInclusiveOnBothEndsComposesAndIsOptional()
        {
            await CreateAdminAndSignInAsync();
            var stream = await CreateVideoStreamAsync("News");

            // Three instants a second apart, so each bound can name one endpoint exactly and be
            // checked against the row written on that very instant.
            await PlantAsync(Row(stream, "oldest problem"));
            var oldestAt = Fixture.Clock.GetUtcNow();

            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(stream, "middle problem"));
            var middleAt = Fixture.Clock.GetUtcNow();

            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantAsync(Row(stream, "newest problem"));
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
                    ["middle problem", "oldest problem"],
                    Rows(bounded)
                        .Select(row => row.GetProperty("errorMessage").GetString())
                        .ToArray());
            }

            // Either side alone is one open-ended bound.
            using (var fromOnly = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, fromOnly.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "newest problem",
                    Rows(fromOnly)[0].GetProperty("errorMessage").GetString());
            }

            using (var toOnly = await ReadPageAsync(
                $"dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}"))
            {
                Assert.Equal(2, toOnly.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "middle problem",
                    Rows(toOnly)[0].GetProperty("errorMessage").GetString());
            }

            // A bound naming the oldest instant alone keeps it: `>=` and not `>`.
            using (var atOldest = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(oldestAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(oldestAt.ToString("O"))}"))
            {
                Assert.Equal(1, atOldest.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "oldest problem",
                    SingleRow(atOldest).GetProperty("errorMessage").GetString());
            }

            // An inverted range is a valid query over an empty interval, not a refusal.
            using (var inverted = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(newestAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(oldestAt.ToString("O"))}"))
            {
                Assert.Equal(0, inverted.RootElement.GetProperty("total").GetInt32());
            }

            // The range composes with the stream and the message: only the middle row carries all
            // three facts.
            using (var composed = await ReadPageAsync(
                $"dateFrom={Uri.EscapeDataString(middleAt.ToString("O"))}" +
                $"&dateTo={Uri.EscapeDataString(middleAt.ToString("O"))}" +
                $"&streamId={stream}&search=middle"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "middle problem",
                    SingleRow(composed).GetProperty("errorMessage").GetString());
            }
        }

        [Fact]
        public async Task PageTurnsAreStableAndDisjoint()
        {
            await CreateAdminAndSignInAsync();
            var stream = await CreateVideoStreamAsync("News");

            // Five rows in one save share one instant, so every page turn is decided by the
            // identifier tie-break alone - the case a missing `id` in the order cannot survive.
            var planted = new[]
            {
                Row(stream, "row one"),
                Row(stream, "row two"),
                Row(stream, "row three"),
                Row(stream, "row four"),
                Row(stream, "row five")
            };

            await PlantAsync(planted);

            var seen = new List<Guid>();

            for (var page = 1; page <= 3; page++)
            {
                using var turn = await ReadPageAsync($"page={page}&pageSize=2");

                // The count describes the list, not the page, and every page reports the same one.
                Assert.Equal(5, turn.RootElement.GetProperty("total").GetInt32());
                seen.AddRange(Ids(turn.RootElement.GetProperty("items")));
            }

            // Five rows over three pages, no repeat and no gap, and the order is the one the
            // default claims.
            Assert.Equal(5, seen.Count);
            Assert.Equal(5, seen.Distinct().Count());
            Assert.Equal(
                seen
                    .Select(id => id.ToString())
                    .OrderByDescending(text => text, StringComparer.Ordinal)
                    .ToArray(),
                seen.Select(id => id.ToString()).ToArray());
        }

        [Fact]
        public async Task SearchIsCaseInsensitiveOverTheMessageOnly()
        {
            await CreateAdminAndSignInAsync();
            var namedStream = await CreateVideoStreamAsync("Distinctive Channel Name");

            await PlantAsync(
                Row(namedStream, "The Upstream Server Refused The Connection"),
                Row(namedStream, "a different sentence entirely"));

            // Case-insensitive against the stored message, on the database's own comparison.
            using (var upper = await ReadPageAsync("search=UPSTREAM"))
            {
                Assert.Equal(1, upper.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "The Upstream Server Refused The Connection",
                    SingleRow(upper).GetProperty("errorMessage").GetString());
            }

            using (var lower = await ReadPageAsync("search=upstream"))
            {
                Assert.Equal(1, lower.RootElement.GetProperty("total").GetInt32());
            }

            // The search covers the message and nothing else: the stream's name is resolved by a
            // join and is deliberately not part of the stored predicate, so a name that the
            // messages never mention answers zero rather than quietly widening what search means.
            using (var byStreamName = await ReadPageAsync("search=Distinctive"))
            {
                Assert.Equal(0, byStreamName.RootElement.GetProperty("total").GetInt32());
            }
        }

        [Fact]
        public async Task TheEndpointsAreRefusedWithoutTheStreamGrantsAndToEveryOtherRole()
        {
            // An administrator holding only the client grants may neither read nor wipe the
            // journal: the endpoints sit on the streams grants, not on the role alone.
            var clientReaders = await CreateGroupAsync(
                "Client readers",
                UserRole.Admin,
                permissions: UserPermission.ViewClients);
            await CreateUserAsync(UserRole.Admin, "reader", Password, groupId: clientReaders);

            using (var reader = Fixture.CreateClient())
            {
                await SignInAsync(UserRole.Admin, "reader", Password, reader);

                using (var list = await reader.GetAsync(
                    "/admin/stream-problems",
                    TestContext.Current.CancellationToken))
                {
                    await AssertCodeAsync(list, HttpStatusCode.Forbidden, "permission_denied");
                }

                using (var clear = await DeleteAsync(
                    "/admin/stream-problems",
                    await GetCsrfTokenAsync(reader),
                    reader))
                {
                    await AssertCodeAsync(clear, HttpStatusCode.Forbidden, "permission_denied");
                }
            }

            // The read grant reaches the list but not the wipe: this is the split the two grants
            // exist for, and a read-only group must not be able to destroy the journal.
            var watchers = await CreateGroupAsync(
                "Stream watchers",
                UserRole.Admin,
                permissions: UserPermission.ViewStreams);
            await CreateUserAsync(UserRole.Admin, "watcher", Password, groupId: watchers);

            using (var watcher = Fixture.CreateClient())
            {
                await SignInAsync(UserRole.Admin, "watcher", Password, watcher);

                using (var readable = await watcher.GetAsync(
                    "/admin/stream-problems",
                    TestContext.Current.CancellationToken))
                {
                    Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
                }

                using (var refusedClear = await DeleteAsync(
                    "/admin/stream-problems",
                    await GetCsrfTokenAsync(watcher),
                    watcher))
                {
                    await AssertCodeAsync(
                        refusedClear,
                        HttpStatusCode.Forbidden,
                        "permission_denied");
                }
            }

            // A client and a reseller are refused by the role gate ahead of any permission.
            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);

            using (var refusedClient = await client.GetAsync(
                "/admin/stream-problems",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedClient, HttpStatusCode.Forbidden, "auth_role_changed");
            }

            using (var refusedClientClear = await DeleteAsync(
                "/admin/stream-problems",
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
                "/admin/stream-problems",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(
                    refusedReseller,
                    HttpStatusCode.Forbidden,
                    "auth_role_changed");
            }

            using (var refusedResellerClear = await DeleteAsync(
                "/admin/stream-problems",
                await GetCsrfTokenAsync(reseller),
                reseller))
            {
                await AssertCodeAsync(
                    refusedResellerClear,
                    HttpStatusCode.Forbidden,
                    "auth_role_changed");
            }
        }

        [Fact]
        public async Task TheBaseAdministratorsGroupReadsAndClearsAfterSeeding()
        {
            await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
            {
                var groups = scope.ServiceProvider
                    .GetRequiredService<IUserGroupRepository>();

                var granted = await groups.GetPermissionsAsync(
                    BaseUserGroups.Administrators,
                    TestContext.Current.CancellationToken);

                // The journal rides the streams grants; the base group holds both, which is what
                // makes the read and the clear reachable without a new permission.
                Assert.Contains(UserPermission.ViewStreams, granted);
                Assert.Contains(UserPermission.EditStreams, granted);
            }

            await CreateAdminAndSignInAsync();
            var stream = await CreateVideoStreamAsync("News");

            await PlantAsync(Row(stream, "a problem to read"));

            using (var list = await ReadPageAsync(string.Empty))
            {
                Assert.Equal(1, list.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "News",
                    SingleRow(list).GetProperty("streamName").GetString());
            }

            using var cleared = await DeleteAsync(
                "/admin/stream-problems",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "stream_problems_cleared");
        }

        [Fact]
        public async Task ClearingNeedsTheCsrfPairIgnoresTheFiltersAndReportsTheCount()
        {
            await CreateAdminAndSignInAsync();
            var stream = await CreateVideoStreamAsync("News");

            await PlantAsync(
                Row(stream, "first problem"),
                Row(stream, "second problem"));

            Assert.Equal(2, await CountAsync());

            // No antiforgery pair: refused as such, and nothing is removed.
            using (var noCsrf = await Client.DeleteAsync(
                "/admin/stream-problems",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            Assert.Equal(2, await CountAsync());

            // A valid pair wipes the whole table even though the screen's filters were sent: the
            // delete names the collection, never the filtered subset.
            using (var cleared = await DeleteAsync(
                $"/admin/stream-problems?streamId={stream}&search=first",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(cleared, HttpStatusCode.OK, "stream_problems_cleared");
                using var body = await ReadJsonAsync(cleared);
                Assert.Equal(
                    "Cleared the stream problem journal: 2 row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }

            Assert.Equal(0, await CountAsync());

            // The second press is still success, with zero.
            using (var again = await DeleteAsync(
                "/admin/stream-problems",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(again, HttpStatusCode.OK, "stream_problems_cleared");
                using var body = await ReadJsonAsync(again);
                Assert.Equal(
                    "Cleared the stream problem journal: 0 row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }
        }

        [Fact]
        public async Task EveryOtherMethodOnTheCollectionAnswersMethodNotAllowed()
        {
            await CreateAdminAndSignInAsync();

            // The write fence, proven at the wire rather than only by reading the controller: the
            // collection answers GET and DELETE, so every other method is a 405 through the shared
            // status-page envelope. A POST that recorded a problem here would be an operator
            // writing evidence by hand.
            var csrf = await GetCsrfTokenAsync();

            using (var post = await PostJsonAsync(
                "/admin/stream-problems",
                new { streamId = Guid.NewGuid(), errorMessage = "made up" },
                csrf))
            {
                await AssertCodeAsync(post, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var put = await PutJsonAsync(
                "/admin/stream-problems",
                new { streamId = Guid.NewGuid(), errorMessage = "made up" },
                csrf))
            {
                await AssertCodeAsync(put, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using (var patch = await PatchJsonAsync(
                "/admin/stream-problems",
                new { errorMessage = "made up" },
                csrf))
            {
                await AssertCodeAsync(patch, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            // None of the three wrote anything.
            Assert.Equal(0, await CountAsync());
        }

        [Fact]
        public async Task DeletingTheStreamRemovesItsProblems()
        {
            await CreateAdminAndSignInAsync();
            var deletedStream = await CreateVideoStreamAsync("Leaving");
            var keptStream = await CreateVideoStreamAsync("Staying");

            await PlantAsync(
                Row(deletedStream, "about the leaving stream"),
                Row(deletedStream, "also about the leaving stream"),
                Row(keptStream, "about the staying stream"));

            using var removed = await DeleteAsync(
                $"/admin/video-streams/{deletedStream}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(removed, HttpStatusCode.OK, "video_stream_deleted");

            // The cascade is the recorded consequence: the journal is evidence about the stream,
            // and the stream it was about is gone - while the other stream's rows stay.
            using var body = await ReadPageAsync(string.Empty);
            Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                keptStream.ToString(),
                SingleRow(body).GetProperty("streamId").GetString());
            Assert.Equal("Staying", SingleRow(body).GetProperty("streamName").GetString());

            // Filtering by the deleted stream is a valid empty query, not a refusal.
            using var gone = await ReadPageAsync($"streamId={deletedStream}");
            Assert.Equal(0, gone.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task TheRecordPathTrimsCutsAndStampsOneRow()
        {
            await CreateAdminAndSignInAsync();
            var stream = await CreateVideoStreamAsync("News");

            await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
            {
                var problems = scope.ServiceProvider.GetRequiredService<IStreamProblemService>();

                // The producer's message is trimmed at its edges and cut to the column rather
                // than refused: losing the whole record over its length would lose the fact that
                // the stream failed at all.
                await problems.RecordAsync(
                    new NewStreamProblem
                    {
                        StreamId = stream,
                        ErrorMessage = "  " + new string(
                            'm',
                            StreamProblemListLimits.ErrorMessageMaxLength + 20) + "  "
                    },
                    TestContext.Current.CancellationToken);

                // A blank message is refused at the boundary instead of stored as an empty row.
                await Assert.ThrowsAsync<ArgumentException>(
                    () => problems.RecordAsync(
                        new NewStreamProblem { StreamId = stream, ErrorMessage = "   " },
                        TestContext.Current.CancellationToken));
            }

            using var body = await ReadPageAsync(string.Empty);

            // Exactly one row: the refused record left no trace.
            Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());

            var row = SingleRow(body);
            Assert.Equal(
                new string('m', StreamProblemListLimits.ErrorMessageMaxLength),
                row.GetProperty("errorMessage").GetString());
            Assert.Equal("News", row.GetProperty("streamName").GetString());

            // `created_at` and `updated_at` are the context's to stamp, and a fresh row is not
            // born with a newer update: both name the same instant.
            var id = Guid.Parse(row.GetProperty("id").GetString()!);

            await using var connection = await Fixture.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select created_at = updated_at from stream_problems where id = @id",
                connection);
            command.Parameters.AddWithValue("id", id);

            Assert.True(Convert.ToBoolean(await command.ExecuteScalarAsync(
                TestContext.Current.CancellationToken)));

            // The stored instant is the host's clock, which this suite drives. Postgres stores
            // `timestamptz` and answers it in the session's time zone, so the read names UTC
            // explicitly and both sides are compared as instants.
            await using var stamped = new NpgsqlCommand(
                "select created_at at time zone 'UTC' from stream_problems where id = @id",
                connection);
            stamped.Parameters.AddWithValue("id", id);

            var createdAt = DateTime.SpecifyKind(
                (DateTime)(await stamped.ExecuteScalarAsync(
                    TestContext.Current.CancellationToken))!,
                DateTimeKind.Utc);

            Assert.Equal(
                Fixture.Clock.GetUtcNow().UtcDateTime,
                createdAt,
                TimeSpan.FromMilliseconds(1));
        }

        /// <summary>Builds one row with the facts an assertion needs.</summary>
        private static NewStreamProblem Row(Guid streamId, string message)
            => new()
            {
                StreamId = streamId,
                ErrorMessage = message
            };

        /// <summary>Creates a video stream of the installation's own, with its category.</summary>
        private async Task<Guid> CreateVideoStreamAsync(string name)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateVideoStreamAsync(
                scope.ServiceProvider,
                name,
                TestContext.Current.CancellationToken);
        }

        private async Task PlantAsync(params NewStreamProblem[] rows)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            await TestDataSeeder.PlantStreamProblemsAsync(
                scope.ServiceProvider,
                rows,
                TestContext.Current.CancellationToken);
        }

        private Task<HttpResponseMessage> GetPageAsync(string query)
            => Client.GetAsync(
                query.Length == 0
                    ? "/admin/stream-problems"
                    : $"/admin/stream-problems?{query}",
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
                "/admin/stream-problems",
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
