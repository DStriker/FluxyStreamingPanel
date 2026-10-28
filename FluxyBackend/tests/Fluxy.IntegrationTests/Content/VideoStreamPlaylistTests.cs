using System.Net;
using System.Text.Json;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    /// <summary>
    /// The video-playlist stream: its ordered list of on-demand videos, and the two rules that
    /// make the kind mean something - a playlist with nothing in it is refused, and every video
    /// it names has to exist.
    /// </summary>
    /// <remarks>
    /// The list is what the kind is for. A playlist stream that held one identifier, as the
    /// first version of this domain did, could not play "one after another" at all - and the
    /// order is the only thing distinguishing a playlist from a set of videos, so the tests
    /// here are about the sequence surviving a write and a read rather than about the rows.
    /// </remarks>
    public sealed class VideoStreamPlaylistTests : IntegrationTestBase
    {
        public VideoStreamPlaylistTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task APlaylistKeepsTheOrderItWasSentIn()
        {
            await CreateAdminAndSignInAsync();

            var streamCategory = await CreateCategoryAsync("Playlists", "VideoStream");
            var videoCategory = await CreateCategoryAsync("Films", "VideoOnDemand");

            var alpha = await CreateVideoAsync(videoCategory, "Alpha");
            var beta = await CreateVideoAsync(videoCategory, "Beta");
            var gamma = await CreateVideoAsync(videoCategory, "Gamma");

            // Sent out of alphabetical order on purpose: the list is the operator's sequence,
            // not a set the server may re-sort.
            var streamId = await CreatePlaylistAsync(streamCategory, "Playlist", [gamma, alpha, beta]);

            using var detail = await Client.GetAsync(
                $"/admin/video-streams/{streamId}",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);

            Assert.Equal(
                new[] { gamma, alpha, beta },
                body.RootElement.GetProperty("vodItems").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            // A patch replaces the list wholesale, in its own order.
            using var patched = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new { vodItems = new[] { beta, gamma } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(patched, HttpStatusCode.OK, "video_stream_updated");

            using var after = await Client.GetAsync(
                $"/admin/video-streams/{streamId}",
                TestContext.Current.CancellationToken);
            using var afterBody = await ReadJsonAsync(after);

            Assert.Equal(
                new[] { beta, gamma },
                afterBody.RootElement.GetProperty("vodItems").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            // An absent list means "leave it alone", which is the patch convention everywhere
            // else on this API - and the difference from an empty one is the whole point.
            using var untouched = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new { name = "Renamed" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(untouched, HttpStatusCode.OK, "video_stream_updated");

            using var still = await Client.GetAsync(
                $"/admin/video-streams/{streamId}",
                TestContext.Current.CancellationToken);
            using var stillBody = await ReadJsonAsync(still);

            Assert.Equal(2, stillBody.RootElement.GetProperty("vodItems").GetArrayLength());
        }

        [Fact]
        public async Task APlaylistWithNothingInItIsRefused()
        {
            await CreateAdminAndSignInAsync();

            var streamCategory = await CreateCategoryAsync("Empty playlist", "VideoStream");
            var videoCategory = await CreateCategoryAsync("Empty films", "VideoOnDemand");
            var video = await CreateVideoAsync(videoCategory, "Only");

            // A create naming the kind and no videos has nothing to play.
            using var created = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Nothing",
                    type = "vodplaylist",
                    categoryId = streamCategory,
                    vodItems = Array.Empty<string>()
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(created, HttpStatusCode.BadRequest, "validation_failed");
            using var createdBody = await ReadJsonAsync(created);
            Assert.True(createdBody.RootElement.GetProperty("errors").TryGetProperty("vodItems", out _));

            // The same rule applies to a patch that empties a list the stream already had: an
            // edit that changed the kind without choosing a video is the same request shape.
            var streamId = await CreatePlaylistAsync(streamCategory, "Full", [video]);

            using var emptied = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new { vodItems = Array.Empty<string>() },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(emptied, HttpStatusCode.BadRequest, "validation_failed");
            using var emptiedBody = await ReadJsonAsync(emptied);
            Assert.True(emptiedBody.RootElement.GetProperty("errors").TryGetProperty("vodItems", out _));
        }

        [Fact]
        public async Task APlaylistNamingAVideoThatDoesNotExistIsRefused()
        {
            await CreateAdminAndSignInAsync();

            var streamCategory = await CreateCategoryAsync("Guarded playlist", "VideoStream");

            using var created = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Dangling",
                    type = "vodplaylist",
                    categoryId = streamCategory,
                    vodItems = new[] { Guid.NewGuid().ToString() }
                },
                await GetCsrfTokenAsync());

            // Refused with the field named rather than failing at the write: the membership
            // rows carry a real foreign key, and a provider's exception is worth less to the
            // operator than a sentence under the input.
            await AssertCodeAsync(created, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(created);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("vodItems", out _));
        }

        [Fact]
        public async Task DeletingAVideoTakesItOutOfEveryPlaylist()
        {
            await CreateAdminAndSignInAsync();

            var streamCategory = await CreateCategoryAsync("Cascade playlist", "VideoStream");
            var videoCategory = await CreateCategoryAsync("Cascade films", "VideoOnDemand");

            var doomed = await CreateVideoAsync(videoCategory, "Doomed");
            var keeper = await CreateVideoAsync(videoCategory, "Keeper");

            var streamId = await CreatePlaylistAsync(streamCategory, "Playlist", [doomed, keeper]);

            using var deleted = await DeleteAsync(
                $"/admin/video-on-demand/{doomed}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "video_on_demand_deleted");

            // The foreign key cascades, so the playlist loses its first entry and keeps the
            // second - a step that resolved to nothing would be worse than a shorter playlist.
            using var detail = await Client.GetAsync(
                $"/admin/video-streams/{streamId}",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(detail);

            Assert.Equal(
                new[] { keeper },
                body.RootElement.GetProperty("vodItems").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
        }

        private async Task<string> CreateCategoryAsync(string name, string type)
        {
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateVideoAsync(string categoryId, string name)
        {
            using var response = await PostJsonAsync(
                "/admin/video-on-demand",
                new
                {
                    name,
                    type = "relay",
                    categoryId,
                    source = "https://example.test/video"
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "video_on_demand_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreatePlaylistAsync(
            string categoryId,
            string name,
            IReadOnlyList<string> vodItems)
        {
            using var response = await PostJsonAsync(
                "/admin/video-streams",
                new { name, type = "vodplaylist", categoryId, vodItems },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "video_stream_created");

            return IdFromLocation(response);
        }
    }
}
