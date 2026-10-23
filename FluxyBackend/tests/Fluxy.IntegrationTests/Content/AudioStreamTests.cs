using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    /// <summary>
    /// The audio stream resource over HTTP: the writes, the reads, the collective operations
    /// and the playlist import, exercised against the real composition root.
    /// </summary>
    /// <remarks>
    /// The shape of every test mirrors the video stream ones, because the two resources were
    /// built to answer the same questions - what differs is the payload, and the two rules
    /// that follow from it: a stream of the wrong kind of category is refused, and a bulk
    /// type change clears nothing.
    /// </remarks>
    public sealed class AudioStreamTests : IntegrationTestBase
    {
        public AudioStreamTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AnOnDemandStreamKeepsItsSourcesInOrder()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Radio", "AudioStream");

            using var created = await PostStreamAsync(new
            {
                name = "News FM",
                type = "ondemand",
                categoryId,
                logoUrl = "https://example.test/logo.png",
                sources = new[] { "https://example.test/one", "https://example.test/two" }
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "audio_stream_created");
            Assert.NotNull(created.Headers.Location);
            var streamId = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/audio-streams/{streamId}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var detailBody = await ReadJsonAsync(detail);
            var root = detailBody.RootElement;

            Assert.Equal("News FM", root.GetProperty("name").GetString());
            Assert.Equal("OnDemand", root.GetProperty("type").GetString());
            Assert.Equal("Stopped", root.GetProperty("status").GetString());
            Assert.Equal("Radio", root.GetProperty("categoryName").GetString());
            Assert.Equal(
                new[] { "https://example.test/one", "https://example.test/two" },
                root.GetProperty("sources").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            // The fields a video stream carries and an audio stream does not are absent
            // rather than empty: an audio stream has no delay, no archive and no guide.
            Assert.False(root.TryGetProperty("delayMinutes", out _));
            Assert.False(root.TryGetProperty("archiveDays", out _));
            Assert.False(root.TryGetProperty("epgSourceId", out _));

            var csrf = await GetCsrfTokenAsync();
            using var updated = await PatchJsonAsync(
                $"/admin/audio-streams/{streamId}",
                new
                {
                    name = "News FM Renamed",
                    sources = new[] { "https://example.test/two", "https://example.test/one" }
                },
                csrf);
            await AssertCodeAsync(updated, HttpStatusCode.OK, "audio_stream_updated");

            var reread = await DetailOfAsync(streamId);
            Assert.Equal("News FM Renamed", reread.GetProperty("name").GetString());
            Assert.Equal(
                new[] { "https://example.test/two", "https://example.test/one" },
                reread.GetProperty("sources").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
        }

        [Fact]
        public async Task NamesAreUniqueWithinACategoryOnly()
        {
            await CreateAdminAndSignInAsync();
            var firstCategory = await CreateCategoryAsync("First", "AudioStream");
            var secondCategory = await CreateCategoryAsync("Second", "AudioStream");

            using var created = await PostStreamAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = firstCategory,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "audio_stream_created");

            using var duplicate = await PostStreamAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = firstCategory,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "audio_stream_already_exists");

            using var otherCategory = await PostStreamAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = secondCategory,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(otherCategory, HttpStatusCode.Created, "audio_stream_created");
        }

        [Fact]
        public async Task OnlyACategoryOfAudioStreamsMayHoldAnAudioStream()
        {
            await CreateAdminAndSignInAsync();
            var videoCategory = await CreateCategoryAsync("Video", "VideoStream");

            using var response = await PostStreamAsync(new
            {
                name = "Wrong Category",
                type = "ondemand",
                categoryId = videoCategory,
                sources = new[] { "https://example.test/source" }
            });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(response);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("categoryId", out _));
        }

        [Fact]
        public async Task AStreamWithNoSourcesIsRefused()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Radio", "AudioStream");

            using var response = await PostStreamAsync(new
            {
                name = "Silent",
                type = "ondemand",
                categoryId,
                sources = Array.Empty<string>()
            });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(response);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("sources", out _));
        }

        [Fact]
        public async Task AnUnknownTypeIsRefusedRatherThanDefaulted()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Radio", "AudioStream");

            using var response = await PostStreamAsync(new
            {
                name = "Podcast",
                type = "podcast",
                categoryId,
                sources = new[] { "https://example.test/source" }
            });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(response);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("type", out _));
        }

        [Fact]
        public async Task StartAndStopChangeOnlyTheStatusAndAreIdempotent()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Radio", "AudioStream");
            var streamId = await CreateStreamAsync(categoryId, "News");

            using var started = await PostJsonAsync(
                $"/admin/audio-streams/{streamId}/start",
                new { },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(started, HttpStatusCode.OK, "audio_stream_started");

            using var startedAgain = await PostJsonAsync(
                $"/admin/audio-streams/{streamId}/start",
                new { },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(startedAgain, HttpStatusCode.OK, "audio_stream_started");

            Assert.Equal("Running", await StatusOfAsync(streamId));

            using var stopped = await PostJsonAsync(
                $"/admin/audio-streams/{streamId}/stop",
                new { },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(stopped, HttpStatusCode.OK, "audio_stream_stopped");

            using var stoppedAgain = await PostJsonAsync(
                $"/admin/audio-streams/{streamId}/stop",
                new { },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(stoppedAgain, HttpStatusCode.OK, "audio_stream_stopped");

            Assert.Equal("Stopped", await StatusOfAsync(streamId));
        }

        [Fact]
        public async Task TheListFiltersSearchesSortsAndPagesOnTheServer()
        {
            await CreateAdminAndSignInAsync();
            var alpha = await CreateCategoryAsync("Alpha", "AudioStream");
            var beta = await CreateCategoryAsync("Beta", "AudioStream");

            _ = await CreateStreamAsync(alpha, "Alpha One");
            _ = await CreateStreamAsync(alpha, "Alpha Two");
            _ = await CreateStreamAsync(beta, "Beta One");

            using var page = await Client.GetAsync(
                "/admin/audio-streams?page=1&pageSize=2&sortBy=name&sortOrder=asc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(3, pageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, pageBody.RootElement.GetProperty("items").GetArrayLength());

            using var searched = await Client.GetAsync(
                "/admin/audio-streams?search=Two",
                TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());

            using var filtered = await Client.GetAsync(
                $"/admin/audio-streams?categoryId={alpha}&type=ondemand&status=stopped",
                TestContext.Current.CancellationToken);
            using var filteredBody = await ReadJsonAsync(filtered);
            Assert.Equal(2, filteredBody.RootElement.GetProperty("total").GetInt32());

            using var invalidSort = await Client.GetAsync(
                "/admin/audio-streams?sortBy=sources",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidSort, HttpStatusCode.BadRequest, "validation_failed");

            using var invalidFilter = await Client.GetAsync(
                "/admin/audio-streams?type=podcast",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidFilter, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ACategoryHoldingAnAudioStreamCannotBeDeleted()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("In Use", "AudioStream");
            var streamId = await CreateStreamAsync(categoryId, "News");

            using var categoryDelete = await DeleteAsync(
                $"/admin/content-categories/{categoryId}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(categoryDelete, HttpStatusCode.Conflict, "content_category_in_use");

            using var deleted = await DeleteAsync(
                $"/admin/audio-streams/{streamId}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "audio_stream_deleted");

            using var again = await DeleteAsync(
                $"/admin/audio-streams/{streamId}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "audio_stream_not_found");

            using var categoryGone = await DeleteAsync(
                $"/admin/content-categories/{categoryId}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(categoryGone, HttpStatusCode.OK, "content_category_deleted");
        }

        [Fact]
        public async Task WritesRequireAntiforgery()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Radio", "AudioStream");

            using var response = await PostJsonAsync(
                "/admin/audio-streams",
                new
                {
                    name = "No Token",
                    type = "ondemand",
                    categoryId,
                    sources = new[] { "https://example.test/source" }
                });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        [Fact]
        public async Task BulkTransitionsReportEachStream()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bulk transitions", "AudioStream");
            var first = await CreateStreamAsync(categoryId, "First");
            var second = await CreateStreamAsync(categoryId, "Second");

            using var started = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new { action = "start", ids = new[] { first, second, first } },
                await GetCsrfTokenAsync());
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            using var startedBody = await ReadJsonAsync(started);
            Assert.Equal("audio_streams_bulk_completed", CodeOf(startedBody));
            Assert.Equal(2, startedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(0, startedBody.RootElement.GetProperty("failed").GetInt32());
            Assert.All(
                startedBody.RootElement.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("audio_stream_started", item.GetProperty("code").GetString()));
            Assert.Equal("Running", await StatusOfAsync(first));

            using var restarted = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new { action = "restart", ids = new[] { first, second } },
                await GetCsrfTokenAsync());
            using var restartedBody = await ReadJsonAsync(restarted);
            Assert.All(
                restartedBody.RootElement.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("audio_stream_restarted", item.GetProperty("code").GetString()));

            using var deleted = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new { action = "delete", ids = new[] { first, second } },
                await GetCsrfTokenAsync());
            using var deletedBody = await ReadJsonAsync(deleted);
            Assert.Equal(2, deletedBody.RootElement.GetProperty("succeeded").GetInt32());

            using var gone = await Client.GetAsync(
                $"/admin/audio-streams/{first}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(gone, HttpStatusCode.NotFound, "audio_stream_not_found");
        }

        [Fact]
        public async Task ABulkTypeChangeClearsNothingAndReportsNoWarning()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bulk types", "AudioStream");

            using var richCreated = await PostStreamAsync(new
            {
                name = "Rich",
                type = "ondemand",
                categoryId,
                sources = new[] { "https://example.test/rich" },
                restreamUserAgent = "agent/1",
                restreamProxy = "http://proxy.test:8080",
                restreamCookies = "a=b",
                restreamHeaders = "X-Test: 1"
            });
            await AssertCodeAsync(richCreated, HttpStatusCode.Created, "audio_stream_created");
            var rich = IdFromLocation(richCreated);

            using var response = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new { action = "change-type", ids = new[] { rich }, type = "redirect" },
                await GetCsrfTokenAsync());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal(1, body.RootElement.GetProperty("succeeded").GetInt32());

            var item = body.RootElement.GetProperty("items")[0];
            Assert.True(
                !item.TryGetProperty("warnings", out var none)
                    || none.ValueKind is JsonValueKind.Null,
                "An audio bulk run has no warning column to fill.");

            var detail = await DetailOfAsync(rich);
            Assert.Equal("Redirect", detail.GetProperty("type").GetString());
            Assert.Equal("agent/1", detail.GetProperty("restreamUserAgent").GetString());
            Assert.Equal(
                new[] { "https://example.test/rich" },
                detail.GetProperty("sources").EnumerateArray()
                    .Select(source => source.GetString()));
        }

        [Fact]
        public async Task ABulkRestreamChangeWritesAllFourSettings()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bulk restream", "AudioStream");
            var streamId = await CreateStreamAsync(categoryId, "One");

            using var response = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new
                {
                    action = "change-restream",
                    ids = new[] { streamId },
                    restreamUserAgent = "agent/2",
                    restreamProxy = string.Empty,
                    restreamCookies = string.Empty,
                    restreamHeaders = string.Empty
                },
                await GetCsrfTokenAsync());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal(1, body.RootElement.GetProperty("succeeded").GetInt32());

            var detail = await DetailOfAsync(streamId);
            Assert.Equal("agent/2", detail.GetProperty("restreamUserAgent").GetString());
            Assert.Null(detail.GetProperty("restreamProxy").GetString());
        }

        [Fact]
        public async Task ABulkMoveRefusesOnlyTheRowWhoseNameIsTakenInTheDestination()
        {
            await CreateAdminAndSignInAsync();
            var source = await CreateCategoryAsync("Source", "AudioStream");
            var destination = await CreateCategoryAsync("Destination", "AudioStream");
            var movable = await CreateStreamAsync(source, "One");
            var colliding = await CreateStreamAsync(source, "Two");
            _ = await CreateStreamAsync(destination, "Two");

            using var response = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new
                {
                    action = "move-category",
                    ids = new[] { movable, colliding },
                    categoryId = destination
                },
                await GetCsrfTokenAsync());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal(1, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("failed").GetInt32());

            Assert.Equal(
                "Destination",
                (await DetailOfAsync(movable)).GetProperty("categoryName").GetString());
            Assert.Equal(
                "Source",
                (await DetailOfAsync(colliding)).GetProperty("categoryName").GetString());
        }

        [Fact]
        public async Task ABulkBodyIsValidatedBeforeAnyRowIsTouched()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Validation", "AudioStream");
            var streamId = await CreateStreamAsync(categoryId, "Solo");

            using var noCsrf = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new { action = "start", ids = new[] { streamId } });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            await AssertBulkRefusalAsync(
                new { action = "archive", ids = new[] { streamId } },
                "action");
            await AssertBulkRefusalAsync(
                new { action = "start", ids = Array.Empty<string>() },
                "ids");
            await AssertBulkRefusalAsync(
                new { action = "change-type", ids = new[] { streamId }, type = "permanent" },
                "type");
            await AssertBulkRefusalAsync(
                new
                {
                    action = "move-category",
                    ids = new[] { streamId },
                    categoryId = "elsewhere"
                },
                "categoryId");
            await AssertBulkRefusalAsync(
                new
                {
                    action = "start",
                    ids = Enumerable.Range(0, 1001)
                        .Select(_ => Guid.NewGuid().ToString())
                        .ToArray()
                },
                "ids");

            using var missing = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                new { action = "start", ids = new[] { Guid.NewGuid().ToString() } },
                await GetCsrfTokenAsync());
            Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
            using var missingBody = await ReadJsonAsync(missing);
            Assert.Equal(0, missingBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, missingBody.RootElement.GetProperty("failed").GetInt32());
            Assert.Equal(
                "audio_stream_not_found",
                missingBody.RootElement.GetProperty("items")[0].GetProperty("code").GetString());
        }

        [Fact]
        public async Task TheAudioPermissionsAreSeparateFromTheVideoOnes()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Permission", "AudioStream");
            var streamId = await CreateStreamAsync(categoryId, "Watched");

            // An administrator whose group may watch video streams but not audio ones: the
            // role requirement passes, so the refusal proves the permission half rather than
            // answering the wrong-level sentence a Client would get.
            var viewers = await CreateGroupAsync(
                "Video stream viewers",
                UserRole.Admin,
                permissions: UserPermission.ViewStreams);

            var (viewer, _) = CreateCookieClient();
            using (viewer)
            {
                await CreateUserAsync(
                    UserRole.Admin,
                    "viewer",
                    "Password1",
                    groupId: viewers);
                await SignInAsync(UserRole.Admin, "viewer", "Password1", viewer);

                using var listed = await viewer.GetAsync(
                    "/admin/audio-streams",
                    TestContext.Current.CancellationToken);
                await AssertCodeAsync(listed, HttpStatusCode.Forbidden, "permission_denied");

                using var bulk = await PostJsonAsync(
                    "/admin/audio-streams/bulk",
                    new { action = "start", ids = new[] { streamId } },
                    await GetCsrfTokenAsync(viewer),
                    viewer);
                await AssertCodeAsync(bulk, HttpStatusCode.Forbidden, "permission_denied");
            }
        }

        [Fact]
        public async Task APlaylistImportsValidEntriesAndReportsTheRest()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Import", "AudioStream");
            await CreateStreamAsync(categoryId, "Existing");

            const string playlist =
                "#EXTM3U\n"
                + "#EXTINF:-1,Existing\n"
                + "https://example.test/existing\n"
                + "#EXTINF:-1 tvg-id=\"one\",New One\n"
                + "https://example.test/one\n"
                + "#EXTINF:-1,New One\n"
                + "https://example.test/two\n"
                + "#EXTINF:-1,Bad address\n"
                + "not a url\n";

            using var response = await ImportAsync(playlist, categoryId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("audio_streams_import_completed", CodeOf(body));
            Assert.Equal(4, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("imported").GetInt32());
            Assert.Equal(2, body.RootElement.GetProperty("skipped").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("failed").GetInt32());

            var problems = body.RootElement.GetProperty("problems").EnumerateArray().ToArray();
            Assert.Equal(3, problems.Length);
            Assert.Equal(
                new[]
                {
                    "audio_stream_already_exists",
                    "audio_stream_already_exists",
                    "validation_failed"
                },
                problems.Select(problem => problem.GetProperty("code").GetString()));

            using var page = await Client.GetAsync(
                $"/admin/audio-streams?categoryId={categoryId}&search=New%20One",
                TestContext.Current.CancellationToken);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(1, pageBody.RootElement.GetProperty("total").GetInt32());

            var imported = pageBody.RootElement.GetProperty("items").EnumerateArray().Single();
            var id = imported.GetProperty("id").GetString()!;
            var detail = await DetailOfAsync(id);
            Assert.Equal("New One", detail.GetProperty("name").GetString());
            Assert.Equal("OnDemand", detail.GetProperty("type").GetString());
            Assert.Equal("Stopped", detail.GetProperty("status").GetString());
            Assert.Equal(
                new[] { "https://example.test/one" },
                detail.GetProperty("sources").EnumerateArray()
                    .Select(source => source.GetString()));
        }

        [Fact]
        public async Task AnImportAppendsSourceToAnExistingStream()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Append", "AudioStream");
            var existingId = await CreateStreamAsync(categoryId, "Existing");

            const string playlist =
                "#EXTM3U\n"
                + "#EXTINF:-1,Existing\n"
                + "https://example.test/appended\n";

            using var response = await ImportAsync(
                playlist,
                categoryId,
                appendSourceToExisting: true);
            using var body = await ReadJsonAsync(response);
            Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(0, body.RootElement.GetProperty("imported").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("updated").GetInt32());
            Assert.Equal(0, body.RootElement.GetProperty("skipped").GetInt32());

            var detail = await DetailOfAsync(existingId);
            Assert.Equal(
                new[] { "https://example.test/source", "https://example.test/appended" },
                detail.GetProperty("sources").EnumerateArray()
                    .Select(source => source.GetString()));
        }

        [Fact]
        public async Task AnEmptyPlaylistAndAnHlsPlaylistAreRefused()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bad playlists", "AudioStream");

            using var empty = await ImportAsync(string.Empty, categoryId);
            await AssertCodeAsync(empty, HttpStatusCode.BadRequest, "audio_stream_import_empty");

            using var hls = await ImportAsync(
                "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\nhttps://example.test/a.m3u8\n",
                categoryId);
            await AssertCodeAsync(hls, HttpStatusCode.BadRequest, "audio_stream_import_hls");
        }

        [Fact]
        public async Task AMissingFileAndATypeTheImportCannotUseAreRefused()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Refused import", "AudioStream");

            using var missing = await ImportAsync(
                string.Empty,
                categoryId,
                includeFile: false);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            using var missingBody = await ReadJsonAsync(missing);
            Assert.Equal("validation_failed", CodeOf(missingBody));
            Assert.True(missingBody.RootElement.GetProperty("errors").TryGetProperty("playlist", out _));

            using var wrongType = await ImportAsync(
                "#EXTINF:-1,One\nhttps://example.test/one\n",
                categoryId,
                type: "permanent");
            Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
            using var wrongTypeBody = await ReadJsonAsync(wrongType);
            Assert.Equal("validation_failed", CodeOf(wrongTypeBody));
            Assert.True(wrongTypeBody.RootElement.GetProperty("errors").TryGetProperty("type", out _));
        }

        [Fact]
        public async Task AnImportWithoutAntiforgeryIsRefusedBeforeTheFileIsRead()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Import csrf", "AudioStream");

            using var response = await ImportAsync(
                "#EXTINF:-1,One\nhttps://example.test/one\n",
                categoryId,
                withCsrf: false);

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        private async Task AssertBulkRefusalAsync(object body, string field)
        {
            using var response = await PostJsonAsync(
                "/admin/audio-streams/bulk",
                body,
                await GetCsrfTokenAsync());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            Assert.Equal("validation_failed", CodeOf(document));
            Assert.True(
                document.RootElement.GetProperty("errors").TryGetProperty(field, out _),
                $"The refusal did not name '{field}'.");
        }

        private async Task<JsonElement> DetailOfAsync(string id)
        {
            using var response = await Client.GetAsync(
                $"/admin/audio-streams/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);

            return body.RootElement.Clone();
        }

        private async Task<string?> StatusOfAsync(string id)
            => (await DetailOfAsync(id)).GetProperty("status").GetString();

        private async Task<string> CreateCategoryAsync(string name, string type)
        {
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateStreamAsync(string categoryId, string name)
        {
            using var response = await PostStreamAsync(new
            {
                name,
                type = "ondemand",
                categoryId,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(response, HttpStatusCode.Created, "audio_stream_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> ImportAsync(
            string playlist,
            string categoryId,
            string type = "ondemand",
            string? status = null,
            bool appendSourceToExisting = false,
            bool overwriteExistingSettings = false,
            bool includeFile = true,
            bool withCsrf = true)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(type), "type");
            form.Add(new StringContent(categoryId), "categoryId");

            if (status is not null)
            {
                form.Add(new StringContent(status), "status");
            }

            if (appendSourceToExisting)
            {
                form.Add(new StringContent("true"), "appendSourceToExisting");
            }

            if (overwriteExistingSettings)
            {
                form.Add(new StringContent("true"), "overwriteExistingSettings");
            }

            if (includeFile)
            {
                var file = new ByteArrayContent(Encoding.UTF8.GetBytes(playlist));
                file.Headers.ContentType = new MediaTypeHeaderValue("audio/x-mpegurl");
                form.Add(file, "playlist", "playlist.m3u");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/admin/audio-streams/import")
            {
                Content = form
            };

            if (withCsrf)
            {
                request.Headers.Add("X-CSRF-Token", await GetCsrfTokenAsync());
            }

            return await Client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        private async Task<HttpResponseMessage> PostStreamAsync(object body)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync("/admin/audio-streams", body, csrf);
        }
    }
}
