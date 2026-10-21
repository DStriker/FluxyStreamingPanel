using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    public sealed class VideoStreamTests : IntegrationTestBase
    {
        public VideoStreamTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task APermanentStreamKeepsItsSourcesInOrder()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Live", "VideoStream");

            using var created = await PostStreamAsync(new
            {
                name = "News HD",
                type = "permanent",
                categoryId,
                logoUrl = "https://example.test/logo.png",
                sources = new[] { "https://example.test/one", "https://example.test/two" }
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "video_stream_created");
            Assert.NotNull(created.Headers.Location);
            var streamId = IdFromLocation(created);

            using var detail = await Client.GetAsync($"/admin/video-streams/{streamId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var detailBody = await ReadJsonAsync(detail);
            var root = detailBody.RootElement;

            Assert.Equal("News HD", root.GetProperty("name").GetString());
            Assert.Equal("Permanent", root.GetProperty("type").GetString());
            Assert.Equal("Stopped", root.GetProperty("status").GetString());
            Assert.Equal("Live", root.GetProperty("categoryName").GetString());
            Assert.Equal(1, root.GetProperty("sortOrder").GetInt32());
            Assert.Equal(
                new[] { "https://example.test/one", "https://example.test/two" },
                root.GetProperty("sources").EnumerateArray().Select(value => value.GetString()).ToArray());
            Assert.Empty(root.GetProperty("files").EnumerateArray());

            var csrf = await GetCsrfTokenAsync();
            using var updated = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new
                {
                    name = "News HD Renamed",
                    sources = new[] { "https://example.test/two", "https://example.test/one" }
                },
                csrf);
            await AssertCodeAsync(updated, HttpStatusCode.OK, "video_stream_updated");

            using var reread = await Client.GetAsync($"/admin/video-streams/{streamId}", TestContext.Current.CancellationToken);
            using var rereadBody = await ReadJsonAsync(reread);
            Assert.Equal("News HD Renamed", rereadBody.RootElement.GetProperty("name").GetString());
            Assert.Equal(
                new[] { "https://example.test/two", "https://example.test/one" },
                rereadBody.RootElement.GetProperty("sources").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            csrf = await GetCsrfTokenAsync();
            using var sameName = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new { name = "News HD Renamed" },
                csrf);
            await AssertCodeAsync(sameName, HttpStatusCode.OK, "video_stream_updated");
        }

        [Fact]
        public async Task NamesAreUniqueWithinACategoryOnly()
        {
            await CreateAdminAndSignInAsync();
            var firstCategory = await CreateCategoryAsync("First", "VideoStream");
            var secondCategory = await CreateCategoryAsync("Second", "VideoStream");

            using var created = await PostStreamAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = firstCategory,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "video_stream_created");

            using var duplicate = await PostStreamAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = firstCategory,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "video_stream_already_exists");

            using var otherCategory = await PostStreamAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = secondCategory,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(otherCategory, HttpStatusCode.Created, "video_stream_created");
        }

        [Fact]
        public async Task StartAndStopChangeOnlyTheStatusAndAreIdempotent()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Live", "VideoStream");
            var streamId = await CreateStreamAsync(categoryId, "News");

            var csrf = await GetCsrfTokenAsync();
            using var started = await PostJsonAsync(
                $"/admin/video-streams/{streamId}/start",
                new { },
                csrf);
            await AssertCodeAsync(started, HttpStatusCode.OK, "video_stream_started");

            csrf = await GetCsrfTokenAsync();
            using var startedAgain = await PostJsonAsync(
                $"/admin/video-streams/{streamId}/start",
                new { },
                csrf);
            await AssertCodeAsync(startedAgain, HttpStatusCode.OK, "video_stream_started");

            using var running = await Client.GetAsync($"/admin/video-streams/{streamId}", TestContext.Current.CancellationToken);
            using var runningBody = await ReadJsonAsync(running);
            Assert.Equal("Running", runningBody.RootElement.GetProperty("status").GetString());

            csrf = await GetCsrfTokenAsync();
            using var stopped = await PostJsonAsync(
                $"/admin/video-streams/{streamId}/stop",
                new { },
                csrf);
            await AssertCodeAsync(stopped, HttpStatusCode.OK, "video_stream_stopped");

            using var stoppedAgain = await PostJsonAsync(
                $"/admin/video-streams/{streamId}/stop",
                new { },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(stoppedAgain, HttpStatusCode.OK, "video_stream_stopped");
        }

        [Fact]
        public async Task KindSpecificRulesAreEnforcedAndUnusedPayloadIsDropped()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Mixed", "VideoStream");

            using var archiveOnRedirect = await PostStreamAsync(new
            {
                name = "Redirect With Archive",
                type = "redirect",
                categoryId,
                sources = new[] { "https://example.test/source" },
                archiveDays = 7
            });
            await AssertCodeAsync(archiveOnRedirect, HttpStatusCode.BadRequest, "validation_failed");
            using var archiveBody = await ReadJsonAsync(archiveOnRedirect);
            Assert.True(archiveBody.RootElement.GetProperty("errors").TryGetProperty("archiveDays", out _));

            using var halfGuide = await PostStreamAsync(new
            {
                name = "Half Guide",
                type = "permanent",
                categoryId,
                sources = new[] { "https://example.test/source" },
                epgSourceId = Guid.NewGuid().ToString()
            });
            await AssertCodeAsync(halfGuide, HttpStatusCode.BadRequest, "validation_failed");
            using var guideBody = await ReadJsonAsync(halfGuide);
            Assert.True(guideBody.RootElement.GetProperty("errors").TryGetProperty("epgSourceId", out _));
            Assert.True(guideBody.RootElement.GetProperty("errors").TryGetProperty("epgChannelKey", out _));

            using var filePlaylist = await PostStreamAsync(new
            {
                name = "Files",
                type = "fileplaylist",
                categoryId,
                files = new[] { "first.mp4", "second.mp4" },
                sources = new[] { "https://ignored.test/source" }
            });
            await AssertCodeAsync(filePlaylist, HttpStatusCode.Created, "video_stream_created");
            var fileId = IdFromLocation(filePlaylist);

            using var files = await Client.GetAsync($"/admin/video-streams/{fileId}", TestContext.Current.CancellationToken);
            using var filesBody = await ReadJsonAsync(files);
            Assert.Equal(
                new[] { "first.mp4", "second.mp4" },
                filesBody.RootElement.GetProperty("files").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Empty(filesBody.RootElement.GetProperty("sources").EnumerateArray());

            using var archiveOnPermanent = await PostStreamAsync(new
            {
                name = "Archived",
                type = "permanent",
                categoryId,
                sources = new[] { "https://example.test/source" },
                archiveDays = 7
            });
            await AssertCodeAsync(archiveOnPermanent, HttpStatusCode.Created, "video_stream_created");

            using var emptySources = await PostStreamAsync(new
            {
                name = "No Sources",
                type = "permanent",
                categoryId,
                sources = Array.Empty<string>()
            });
            await AssertCodeAsync(emptySources, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task TheListFiltersSearchesSortsAndPagesOnTheServer()
        {
            await CreateAdminAndSignInAsync();
            var alpha = await CreateCategoryAsync("Alpha", "VideoStream");
            var beta = await CreateCategoryAsync("Beta", "VideoStream");

            _ = await CreateStreamAsync(alpha, "Alpha One");
            _ = await CreateStreamAsync(alpha, "Alpha Two");
            _ = await CreateStreamAsync(beta, "Beta One");

            using var page = await Client.GetAsync(
                "/admin/video-streams?page=1&pageSize=2&sortBy=name&sortOrder=asc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(3, pageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, pageBody.RootElement.GetProperty("items").GetArrayLength());

            using var searched = await Client.GetAsync("/admin/video-streams?search=Two", TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());

            using var filtered = await Client.GetAsync(
                $"/admin/video-streams?categoryId={alpha}&type=permanent&status=stopped",
                TestContext.Current.CancellationToken);
            using var filteredBody = await ReadJsonAsync(filtered);
            Assert.Equal(2, filteredBody.RootElement.GetProperty("total").GetInt32());

            using var invalidSort = await Client.GetAsync("/admin/video-streams?sortBy=sources", TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidSort, HttpStatusCode.BadRequest, "validation_failed");

            using var invalidFilter = await Client.GetAsync("/admin/video-streams?type=podcast", TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidFilter, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ACategoryInUseCannotBeDeletedAndAStreamCanBeDeletedOnce()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("In Use", "VideoStream");
            var streamId = await CreateStreamAsync(categoryId, "News");

            var csrf = await GetCsrfTokenAsync();
            using var categoryDelete = await DeleteAsync(
                $"/admin/content-categories/{categoryId}",
                csrf);
            await AssertCodeAsync(categoryDelete, HttpStatusCode.Conflict, "content_category_in_use");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/video-streams/{streamId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "video_stream_deleted");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/video-streams/{streamId}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "video_stream_not_found");

            csrf = await GetCsrfTokenAsync();
            using var categoryGone = await DeleteAsync(
                $"/admin/content-categories/{categoryId}",
                csrf);
            await AssertCodeAsync(categoryGone, HttpStatusCode.OK, "content_category_deleted");
        }

        [Fact]
        public async Task WritesRequireAntiforgery()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Live", "VideoStream");

            using var response = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "No Token",
                    type = "permanent",
                    categoryId,
                    sources = new[] { "https://example.test/source" }
                });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        [Fact]
        public async Task BulkTransitionsReportEachStreamAndRestartMarksItRunning()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bulk transitions", "VideoStream");
            var first = await CreateStreamAsync(categoryId, "First");
            var second = await CreateStreamAsync(categoryId, "Second");

            var csrf = await GetCsrfTokenAsync();
            using var started = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "start", ids = new[] { first, second, first } },
                csrf);
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            using var startedBody = await ReadJsonAsync(started);
            Assert.Equal("video_streams_bulk_completed", CodeOf(startedBody));
            Assert.Equal(2, startedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(0, startedBody.RootElement.GetProperty("failed").GetInt32());
            var startedItems = startedBody.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(2, startedItems.Length);
            Assert.All(
                startedItems,
                item => Assert.Equal("video_stream_started", item.GetProperty("code").GetString()));
            Assert.Equal("Running", await StatusOfAsync(first));
            Assert.Equal("Running", await StatusOfAsync(second));

            csrf = await GetCsrfTokenAsync();
            using var restarted = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "restart", ids = new[] { first, second } },
                csrf);
            Assert.Equal(HttpStatusCode.OK, restarted.StatusCode);
            using var restartedBody = await ReadJsonAsync(restarted);
            Assert.Equal(2, restartedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.All(
                restartedBody.RootElement.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("video_stream_restarted", item.GetProperty("code").GetString()));
            Assert.Equal("Running", await StatusOfAsync(first));

            csrf = await GetCsrfTokenAsync();
            using var stopped = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "stop", ids = new[] { first, second } },
                csrf);
            Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
            using var stoppedBody = await ReadJsonAsync(stopped);
            Assert.Equal(2, stoppedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.All(
                stoppedBody.RootElement.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("video_stream_stopped", item.GetProperty("code").GetString()));
            Assert.Equal("Stopped", await StatusOfAsync(first));
            Assert.Equal("Stopped", await StatusOfAsync(second));

            csrf = await GetCsrfTokenAsync();
            using var one = await PostJsonAsync(
                $"/admin/video-streams/{first}/restart",
                new { },
                csrf);
            await AssertCodeAsync(one, HttpStatusCode.OK, "video_stream_restarted");
            Assert.Equal("Running", await StatusOfAsync(first));

            csrf = await GetCsrfTokenAsync();
            using var deleted = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "delete", ids = new[] { first, second } },
                csrf);
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
            using var deletedBody = await ReadJsonAsync(deleted);
            Assert.Equal(2, deletedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.All(
                deletedBody.RootElement.GetProperty("items").EnumerateArray(),
                item => Assert.Equal("video_stream_deleted", item.GetProperty("code").GetString()));

            using var gone = await Client.GetAsync(
                $"/admin/video-streams/{first}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(gone, HttpStatusCode.NotFound, "video_stream_not_found");
        }

        [Fact]
        public async Task BulkTypeChangeClearsUnsupportedSettingsAndWarnsPerRow()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bulk types", "VideoStream");

            using var richCreated = await PostStreamAsync(new
            {
                name = "Rich",
                type = "permanent",
                categoryId,
                sources = new[] { "https://example.test/rich" },
                delayMinutes = 5,
                archiveDays = 9,
                restreamUserAgent = "agent/1",
                restreamProxy = "http://proxy.test:8080",
                restreamCookies = "a=b",
                restreamHeaders = "X-Test: 1"
            });
            await AssertCodeAsync(richCreated, HttpStatusCode.Created, "video_stream_created");
            var rich = IdFromLocation(richCreated);

            var plain = await CreateStreamAsync(categoryId, "Plain");

            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "change-type", ids = new[] { rich, plain }, type = "redirect" },
                csrf);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal("video_streams_bulk_completed", CodeOf(body));
            Assert.Equal(2, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(0, body.RootElement.GetProperty("failed").GetInt32());

            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(
                new[]
                {
                    "video_stream_delay_cleared",
                    "video_stream_archive_cleared",
                    "video_stream_restream_cleared"
                },
                items[0].GetProperty("warnings").EnumerateArray()
                    .Select(warning => warning.GetProperty("code").GetString())
                    .ToArray());

            Assert.True(
                !items[1].TryGetProperty("warnings", out var none)
                    || none.ValueKind is JsonValueKind.Null,
                "A row that cleared nothing must not carry a warning list.");

            var detail = await DetailOfAsync(rich);
            Assert.Equal("Redirect", detail.GetProperty("type").GetString());
            Assert.Equal(0, detail.GetProperty("delayMinutes").GetInt32());
            Assert.Equal(0, detail.GetProperty("archiveDays").GetInt32());
            Assert.Null(detail.GetProperty("restreamUserAgent").GetString());
            Assert.Null(detail.GetProperty("restreamProxy").GetString());
            Assert.Null(detail.GetProperty("restreamCookies").GetString());
            Assert.Null(detail.GetProperty("restreamHeaders").GetString());
        }

        [Fact]
        public async Task BulkDelayArchiveAndRestreamRefuseTheKindsThatCannotCarryThem()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Bulk settings", "VideoStream");
            var permanent = await CreateStreamAsync(categoryId, "Permanent");

            using var redirectCreated = await PostStreamAsync(new
            {
                name = "Redirect",
                type = "redirect",
                categoryId,
                sources = new[] { "https://example.test/redirect" }
            });
            await AssertCodeAsync(redirectCreated, HttpStatusCode.Created, "video_stream_created");
            var redirect = IdFromLocation(redirectCreated);

            var csrf = await GetCsrfTokenAsync();
            using var delay = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new
                {
                    action = "change-delay",
                    ids = new[] { permanent, redirect },
                    delayMinutes = 12
                },
                csrf);
            Assert.Equal(HttpStatusCode.OK, delay.StatusCode);
            using var delayBody = await ReadJsonAsync(delay);
            Assert.Equal(1, delayBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, delayBody.RootElement.GetProperty("failed").GetInt32());
            AssertBulkRow(
                delayBody.RootElement.GetProperty("items")[0],
                permanent,
                true,
                "video_stream_updated");
            AssertBulkRow(
                delayBody.RootElement.GetProperty("items")[1],
                redirect,
                false,
                "validation_failed",
                "delayMinutes");
            Assert.Equal(12, (await DetailOfAsync(permanent)).GetProperty("delayMinutes").GetInt32());

            csrf = await GetCsrfTokenAsync();
            using var archive = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new
                {
                    action = "change-archive",
                    ids = new[] { permanent, redirect },
                    archiveDays = 30
                },
                csrf);
            Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
            using var archiveBody = await ReadJsonAsync(archive);
            AssertBulkRow(
                archiveBody.RootElement.GetProperty("items")[0],
                permanent,
                true,
                "video_stream_updated");
            AssertBulkRow(
                archiveBody.RootElement.GetProperty("items")[1],
                redirect,
                false,
                "validation_failed",
                "archiveDays");
            Assert.Equal(30, (await DetailOfAsync(permanent)).GetProperty("archiveDays").GetInt32());

            csrf = await GetCsrfTokenAsync();
            using var restream = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new
                {
                    action = "change-restream",
                    ids = new[] { permanent, redirect },
                    restreamUserAgent = "agent/2",
                    restreamProxy = string.Empty,
                    restreamCookies = string.Empty,
                    restreamHeaders = string.Empty
                },
                csrf);
            Assert.Equal(HttpStatusCode.OK, restream.StatusCode);
            using var restreamBody = await ReadJsonAsync(restream);
            AssertBulkRow(
                restreamBody.RootElement.GetProperty("items")[0],
                permanent,
                true,
                "video_stream_updated");
            AssertBulkRow(
                restreamBody.RootElement.GetProperty("items")[1],
                redirect,
                false,
                "validation_failed",
                "restreamUserAgent");
            Assert.Equal(
                "agent/2",
                (await DetailOfAsync(permanent)).GetProperty("restreamUserAgent").GetString());
        }

        [Fact]
        public async Task BulkMoveRefusesOnlyTheRowWhoseNameIsTakenInTheDestination()
        {
            await CreateAdminAndSignInAsync();
            var source = await CreateCategoryAsync("Source", "VideoStream");
            var destination = await CreateCategoryAsync("Destination", "VideoStream");
            var movable = await CreateStreamAsync(source, "One");
            var colliding = await CreateStreamAsync(source, "Two");
            var taken = await CreateStreamAsync(destination, "Two");

            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new
                {
                    action = "move-category",
                    ids = new[] { movable, colliding },
                    categoryId = destination
                },
                csrf);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal("video_streams_bulk_completed", CodeOf(body));
            Assert.Equal(1, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("failed").GetInt32());
            AssertBulkRow(
                body.RootElement.GetProperty("items")[0],
                movable,
                true,
                "video_stream_updated");
            AssertBulkRow(
                body.RootElement.GetProperty("items")[1],
                colliding,
                false,
                "video_stream_already_exists");

            Assert.Equal(
                "Destination",
                (await DetailOfAsync(movable)).GetProperty("categoryName").GetString());
            Assert.Equal(
                "Source",
                (await DetailOfAsync(colliding)).GetProperty("categoryName").GetString());
            Assert.Equal(
                "Destination",
                (await DetailOfAsync(taken)).GetProperty("categoryName").GetString());
        }

        [Fact]
        public async Task BulkBodiesAreValidatedBeforeAnyRowIsTouched()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Validation", "VideoStream");
            var streamId = await CreateStreamAsync(categoryId, "Solo");

            using var noCsrf = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "start", ids = new[] { streamId } });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            await AssertBulkRefusalAsync(
                new { action = "archive", ids = new[] { streamId } },
                "action");
            await AssertBulkRefusalAsync(
                new { action = "start", ids = Array.Empty<string>() },
                "ids");
            await AssertBulkRefusalAsync(
                new { action = "change-type", ids = new[] { streamId }, type = "fileplaylist" },
                "type");
            await AssertBulkRefusalAsync(
                new { action = "change-delay", ids = new[] { streamId }, delayMinutes = -1 },
                "delayMinutes");
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

            var csrf = await GetCsrfTokenAsync();
            using var missing = await PostJsonAsync(
                "/admin/video-streams/bulk",
                new { action = "start", ids = new[] { Guid.NewGuid().ToString() } },
                csrf);
            Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
            using var missingBody = await ReadJsonAsync(missing);
            Assert.Equal(0, missingBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, missingBody.RootElement.GetProperty("failed").GetInt32());
            Assert.Equal(
                "video_stream_not_found",
                missingBody.RootElement.GetProperty("items")[0].GetProperty("code").GetString());
        }

        [Fact]
        public async Task BulkAndRestartRequireTheEditStreamsPermission()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Permission", "VideoStream");
            var streamId = await CreateStreamAsync(categoryId, "Watched");

            // An administrator whose group may watch streams but not run them: the role
            // requirement passes, so the refusal proves the permission half rather than
            // answering the wrong-level sentence a Client would get.
            var viewers = await CreateGroupAsync(
                "Stream viewers",
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

                var csrf = await GetCsrfTokenAsync(viewer);
                using var bulk = await PostJsonAsync(
                    "/admin/video-streams/bulk",
                    new { action = "start", ids = new[] { streamId } },
                    csrf,
                    viewer);
                await AssertCodeAsync(bulk, HttpStatusCode.Forbidden, "permission_denied");

                csrf = await GetCsrfTokenAsync(viewer);
                using var restart = await PostJsonAsync(
                    $"/admin/video-streams/{streamId}/restart",
                    new { },
                    csrf,
                    viewer);
                await AssertCodeAsync(restart, HttpStatusCode.Forbidden, "permission_denied");
            }
        }

        private async Task AssertBulkRefusalAsync(object body, string field)
        {
            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync("/admin/video-streams/bulk", body, csrf);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            Assert.Equal("validation_failed", CodeOf(document));
            Assert.True(
                document.RootElement.GetProperty("errors").TryGetProperty(field, out _),
                $"The refusal did not name '{field}'.");
        }

        private static void AssertBulkRow(
            JsonElement item,
            string id,
            bool ok,
            string code,
            string? errorField = null)
        {
            Assert.Equal(id, item.GetProperty("id").GetString());
            Assert.Equal(ok, item.GetProperty("ok").GetBoolean());
            Assert.Equal(code, item.GetProperty("code").GetString());

            if (errorField is not null)
            {
                Assert.True(item.GetProperty("errors").TryGetProperty(errorField, out _));
            }
        }

        private async Task<JsonElement> DetailOfAsync(string id)
        {
            using var response = await Client.GetAsync(
                $"/admin/video-streams/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);

            return body.RootElement.Clone();
        }

        private async Task<string?> StatusOfAsync(string id)
            => (await DetailOfAsync(id)).GetProperty("status").GetString();

        private async Task<string> CreateCategoryAsync(string name, string type)
        {
            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type },
                csrf);
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateStreamAsync(string categoryId, string name)
        {
            using var response = await PostStreamAsync(new
            {
                name,
                type = "permanent",
                categoryId,
                sources = new[] { "https://example.test/source" }
            });
            await AssertCodeAsync(response, HttpStatusCode.Created, "video_stream_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> PostStreamAsync(object body)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync("/admin/video-streams", body, csrf);
        }
    }
}
