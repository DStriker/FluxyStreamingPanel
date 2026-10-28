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

    /// <summary>
    /// The bouquets an import names: the rows it creates join them, the rows it skips do not.
    /// </summary>
    /// <remarks>
    /// The field is one repeated multipart entry rather than a JSON array, so what these tests
    /// pin is that the form parser reads the repetition at all - a list sent as `bouquetIds`
    /// three times would otherwise arrive as one bouquet or as nothing, and the import would
    /// look like it had accepted a field it silently dropped.
    /// </remarks>
    public sealed class VideoStreamImportBouquetTests : IntegrationTestBase
    {
        public VideoStreamImportBouquetTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AnImportPutsEveryCreatedStreamInTheNamedBouquets()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Imported");
            var first = await CreateBouquetAsync("First");
            var second = await CreateBouquetAsync("Second");

            var playlist = "#EXTM3U\n#EXTINF:-1,Alpha\nhttps://example.test/alpha\n" +
                "#EXTINF:-1,Beta\nhttps://example.test/beta\n";

            using var imported = await ImportAsync(playlist, category, [first, second]);
            await AssertCodeAsync(imported, HttpStatusCode.OK, "video_streams_import_completed");

            // Both bouquets hold both streams: the field applies to every row the import
            // created, which is the whole reason it is offered on the form.
            foreach (var bouquetId in new[] { first, second })
            {
                using var content = await Client.GetAsync(
                    $"/admin/bouquets/{bouquetId}/content?contentType=videostream",
                    TestContext.Current.CancellationToken);

                using var body = await ReadJsonAsync(content);

                Assert.Equal(
                    new[] { "Alpha", "Beta" },
                    body.RootElement.GetProperty("members").EnumerateArray()
                        .Select(member => member.GetProperty("name").GetString())
                        .ToArray());
            }
        }

        [Fact]
        public async Task AnImportLeavesAnExistingStreamWhereItWas()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Existing first");
            var bouquet = await CreateBouquetAsync("Package");

            // A stream that already exists, and is in no bouquet.
            using var existing = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Keeper",
                    type = "permanent",
                    categoryId = category,
                    sources = new[] { "https://example.test/keeper" }
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(existing, HttpStatusCode.Created, "video_stream_created");

            // The import names the same stream, which is skipped rather than created.
            var playlist = "#EXTM3U\n#EXTINF:-1,Keeper\nhttps://example.test/keeper\n";

            using var imported = await ImportAsync(playlist, category, [bouquet]);
            await AssertCodeAsync(imported, HttpStatusCode.OK, "video_streams_import_completed");

            using var importedBody = await ReadJsonAsync(imported);
            Assert.Equal(1, importedBody.RootElement.GetProperty("skipped").GetInt32());

            // The skipped stream did not join the bouquet: an import is not an edit of a row
            // it did not create, and the count is what proves it.
            using var list = await Client.GetAsync("/admin/bouquets", TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);

            Assert.Equal(
                0,
                listBody.RootElement.GetProperty("items")[0].GetProperty("videoStreamCount").GetInt32());
        }

        [Fact]
        public async Task AnImportRefusesABouquetIdentifierThatIsNotOne()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Broken field");

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("permanent"), "type");
            form.Add(new StringContent(category), "categoryId");
            form.Add(new StringContent("not-a-uuid"), "bouquetIds");

            var file = new ByteArrayContent(
                System.Text.Encoding.UTF8.GetBytes("#EXTM3U\n#EXTINF:-1,One\nhttps://example.test/one\n"));
            form.Add(file, "playlist", "playlist.m3u");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/admin/video-streams/import")
            {
                Content = form
            };
            request.Headers.Add("X-CSRF-Token", await GetCsrfTokenAsync());

            using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(response);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("bouquetIds", out _));
        }

        private async Task<string> CreateCategoryAsync(string name)
        {
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type = "VideoStream" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateBouquetAsync(string name)
        {
            using var response = await PostJsonAsync(
                "/admin/bouquets",
                new { name },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "bouquet_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> ImportAsync(
            string playlist,
            string categoryId,
            IReadOnlyList<string> bouquetIds)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("permanent"), "type");
            form.Add(new StringContent(categoryId), "categoryId");

            // Once per bouquet, which is how a multipart form carries a list.
            foreach (var bouquetId in bouquetIds)
            {
                form.Add(new StringContent(bouquetId), "bouquetIds");
            }

            var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(playlist));
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/x-mpegurl");
            form.Add(file, "playlist", "playlist.m3u");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/admin/video-streams/import")
            {
                Content = form
            };
            request.Headers.Add("X-CSRF-Token", await GetCsrfTokenAsync());

            return await Client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
