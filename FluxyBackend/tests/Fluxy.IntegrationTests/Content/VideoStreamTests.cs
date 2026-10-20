using System.Net;
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
