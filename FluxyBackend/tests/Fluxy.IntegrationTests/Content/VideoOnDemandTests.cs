using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    public sealed class VideoOnDemandTests : IntegrationTestBase
    {
        public VideoOnDemandTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AVideoKeepsItsMetadataAndListsInOrder()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Films");

            using var created = await PostVideoAsync(new
            {
                name = "Dune",
                type = "relay",
                status = "active",
                categoryId,
                source = "https://example.test/dune.mp4",
                subtitleUrl = "https://example.test/dune.vtt",
                imdbId = "tt1160419",
                posterUrl = "https://example.test/dune-poster.jpg",
                backdropUrl = "https://example.test/dune-backdrop.jpg",
                plot = "A desert planet.",
                cast = new[] { "Timothée Chalamet", "Rebecca Ferguson" },
                director = "Denis Villeneuve",
                genres = new[] { "Science Fiction", "Drama" },
                releaseDate = "2021-10-22",
                rating = 8.0m,
                countryCode = "us"
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "video_on_demand_created");
            Assert.NotNull(created.Headers.Location);
            var id = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/video-on-demand/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal("Dune", root.GetProperty("name").GetString());
            Assert.Equal("Relay", root.GetProperty("type").GetString());
            Assert.Equal("Active", root.GetProperty("status").GetString());
            Assert.Equal("Films", root.GetProperty("categoryName").GetString());
            Assert.Equal("2021-10-22", root.GetProperty("releaseDate").GetString());
            Assert.Equal(8.0m, root.GetProperty("rating").GetDecimal());
            Assert.Equal("US", root.GetProperty("countryCode").GetString());
            Assert.Equal(
                new[] { "Timothée Chalamet", "Rebecca Ferguson" },
                root.GetProperty("cast").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());
            Assert.Equal(
                new[] { "Science Fiction", "Drama" },
                root.GetProperty("genres").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            var csrf = await GetCsrfTokenAsync();
            using var updated = await PatchJsonAsync(
                $"/admin/video-on-demand/{id}",
                new
                {
                    name = "Dune Part One",
                    cast = new[] { "Rebecca Ferguson", "Timothée Chalamet" },
                    rating = "",
                    releaseDate = ""
                },
                csrf);
            await AssertCodeAsync(updated, HttpStatusCode.OK, "video_on_demand_updated");

            using var reread = await Client.GetAsync(
                $"/admin/video-on-demand/{id}",
                TestContext.Current.CancellationToken);
            using var rereadBody = await ReadJsonAsync(reread);
            var rereadRoot = rereadBody.RootElement;

            Assert.Equal("Dune Part One", rereadRoot.GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, rereadRoot.GetProperty("rating").ValueKind);
            Assert.Equal(JsonValueKind.Null, rereadRoot.GetProperty("releaseDate").ValueKind);
            Assert.Equal(
                new[] { "Rebecca Ferguson", "Timothée Chalamet" },
                rereadRoot.GetProperty("cast").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            // A save that names its own name is not a conflict with itself.
            csrf = await GetCsrfTokenAsync();
            using var sameName = await PatchJsonAsync(
                $"/admin/video-on-demand/{id}",
                new { name = "Dune Part One" },
                csrf);
            await AssertCodeAsync(sameName, HttpStatusCode.OK, "video_on_demand_updated");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/video-on-demand/{id}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "video_on_demand_deleted");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/video-on-demand/{id}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "video_on_demand_not_found");
        }

        [Fact]
        public async Task NamesAreUniqueWithinACategoryOnly()
        {
            await CreateAdminAndSignInAsync();
            var first = await CreateCategoryAsync("First");
            var second = await CreateCategoryAsync("Second");

            using var created = await PostVideoAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = first,
                source = "https://example.test/shared.mp4"
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "video_on_demand_created");

            using var duplicate = await PostVideoAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = first,
                source = "https://example.test/other.mp4"
            });
            await AssertCodeAsync(
                duplicate,
                HttpStatusCode.Conflict,
                "video_on_demand_already_exists");

            using var otherCategory = await PostVideoAsync(new
            {
                name = "Shared",
                type = "redirect",
                categoryId = second,
                source = "https://example.test/shared.mp4"
            });
            await AssertCodeAsync(
                otherCategory,
                HttpStatusCode.Created,
                "video_on_demand_created");
        }

        [Fact]
        public async Task InvalidMetadataIsRefusedByField()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Films");

            using var response = await PostVideoAsync(new
            {
                name = "Bad",
                type = "relay",
                categoryId,
                source = "https://example.test/bad.mp4",
                imdbId = "not-an-imdb-id",
                rating = 11.5m,
                countryCode = "ZZ",
                releaseDate = "22.10.2021",
                cast = new[] { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen", "Twenty", "Twenty-one" }
            });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(response);
            var errors = body.RootElement.GetProperty("errors");

            Assert.True(errors.TryGetProperty("imdbId", out _));
            Assert.True(errors.TryGetProperty("rating", out _));
            Assert.True(errors.TryGetProperty("countryCode", out _));
            Assert.True(errors.TryGetProperty("releaseDate", out _));
            Assert.True(errors.TryGetProperty("cast", out _));
        }

        [Fact]
        public async Task TheListFiltersSortsAndRefusesBadParameters()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Films");
            var otherCategory = await CreateCategoryAsync("Other");

            await CreateVideoAsync(categoryId, "Alpha", new
            {
                rating = 8.0m,
                releaseDate = "2020-01-01"
            });
            await CreateVideoAsync(categoryId, "Beta", new
            {
                rating = 6.5m,
                releaseDate = "2022-01-01"
            });
            await CreateVideoAsync(categoryId, "Gamma");
            await CreateVideoAsync(otherCategory, "Delta", new { status = "active" });

            using var page = await Client.GetAsync(
                $"/admin/video-on-demand?categoryId={categoryId}&page=1&pageSize=2",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(3, pageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, pageBody.RootElement.GetProperty("items").GetArrayLength());

            using var byRating = await Client.GetAsync(
                "/admin/video-on-demand?sortBy=rating&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var ratingBody = await ReadJsonAsync(byRating);
            Assert.Equal(
                new[] { "Alpha", "Beta", "Gamma", "Delta" },
                ratingBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var ratingAscending = await Client.GetAsync(
                "/admin/video-on-demand?sortBy=rating&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var ratingAscendingBody = await ReadJsonAsync(ratingAscending);
            Assert.Equal(
                new[] { "Beta", "Alpha", "Delta", "Gamma" },
                ratingAscendingBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var byDate = await Client.GetAsync(
                "/admin/video-on-demand?sortBy=releaseDate&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var dateBody = await ReadJsonAsync(byDate);
            Assert.Equal(
                new[] { "Beta", "Alpha", "Gamma", "Delta" },
                dateBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var filtered = await Client.GetAsync(
                "/admin/video-on-demand?status=active",
                TestContext.Current.CancellationToken);
            using var filteredBody = await ReadJsonAsync(filtered);
            Assert.Equal(1, filteredBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                "Delta",
                filteredBody.RootElement.GetProperty("items")[0].GetProperty("name").GetString());

            using var unknownSort = await Client.GetAsync(
                "/admin/video-on-demand?sortBy=plot",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownSort, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownSortBody = await ReadJsonAsync(unknownSort);
            Assert.True(unknownSortBody.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));

            using var badPage = await Client.GetAsync(
                "/admin/video-on-demand?page=0",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badPage, HttpStatusCode.BadRequest, "validation_failed");

            using var badType = await Client.GetAsync(
                "/admin/video-on-demand?type=stream",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badType, HttpStatusCode.BadRequest, "validation_failed");

            // A page past the end is empty but still carries the true total.
            using var past = await Client.GetAsync(
                "/admin/video-on-demand?page=2000000000",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, past.StatusCode);
            using var pastBody = await ReadJsonAsync(past);
            Assert.Equal(0, pastBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(4, pastBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task BulkStatusMoveAndDeleteArePartialByDesign()
        {
            await CreateAdminAndSignInAsync();
            var source = await CreateCategoryAsync("Source");
            var destination = await CreateCategoryAsync("Destination");

            var one = await CreateVideoAsync(source, "One");
            var two = await CreateVideoAsync(source, "Two");
            var three = await CreateVideoAsync(source, "Three");
            var collision = await CreateVideoAsync(destination, "Two");

            var csrf = await GetCsrfTokenAsync();
            using var active = await PostJsonAsync(
                "/admin/video-on-demand/bulk",
                new { action = "change-status", ids = new[] { one, two, three }, status = "active" },
                csrf);
            await AssertBulkAsync(active, 3, 0);

            csrf = await GetCsrfTokenAsync();
            using var moved = await PostJsonAsync(
                "/admin/video-on-demand/bulk",
                new { action = "move-category", ids = new[] { one, two, three }, categoryId = destination },
                csrf);
            using var movedBody = await ReadJsonAsync(moved);
            Assert.Equal(2, movedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, movedBody.RootElement.GetProperty("failed").GetInt32());
            Assert.Equal(
                "video_on_demand_already_exists",
                movedBody.RootElement.GetProperty("items")
                    .EnumerateArray()
                    .Single(item => !item.GetProperty("ok").GetBoolean())
                    .GetProperty("code")
                    .GetString());

            csrf = await GetCsrfTokenAsync();
            using var deleted = await PostJsonAsync(
                "/admin/video-on-demand/bulk",
                new { action = "delete", ids = new[] { one, two, three } },
                csrf);
            await AssertBulkAsync(deleted, 3, 0);

            _ = collision;
        }

        [Fact]
        public async Task ThePlaylistImportCreatesSkipsAndFails()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Imported");

            using var first = await PostVideoAsync(new
            {
                name = "Already Here",
                type = "relay",
                categoryId,
                source = "https://example.test/already.mp4"
            });
            await AssertCodeAsync(first, HttpStatusCode.Created, "video_on_demand_created");

            const string Playlist = """
                #EXTM3U
                #EXTINF:-1 tvg-name="Already Here",Already Here
                https://example.test/already.mp4
                #EXTINF:-1 tvg-logo="https://example.test/poster.jpg",New Film
                https://example.test/new.mp4
                #EXTINF:-1,Not A Url
                not-a-url
                #EXTINF:-1,New Film
                https://example.test/duplicate.mp4
                """;

            using var response = await ImportAsync(Playlist, categoryId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;

            Assert.Equal("video_on_demand_import_completed", root.GetProperty("code").GetString());
            Assert.Equal(4, root.GetProperty("total").GetInt32());
            Assert.Equal(1, root.GetProperty("imported").GetInt32());
            Assert.Equal(2, root.GetProperty("skipped").GetInt32());
            Assert.Equal(1, root.GetProperty("failed").GetInt32());
            Assert.Equal(3, root.GetProperty("problems").GetArrayLength());

            using var list = await Client.GetAsync(
                $"/admin/video-on-demand?categoryId={categoryId}&search=New",
                TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);
            Assert.Equal(1, listBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                "https://example.test/poster.jpg",
                listBody.RootElement.GetProperty("items")[0].GetProperty("posterUrl").GetString());
        }

        [Fact]
        public async Task ACategoryWithVideosCannotBeDeleted()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("In Use");
            await CreateVideoAsync(categoryId, "Kept");

            var csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/content-categories/{categoryId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.Conflict, "content_category_in_use");

            using var body = await ReadJsonAsync(deleted);
            Assert.Contains("1 item", body.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task TranscodingIsAnHonestStub()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Films");
            var id = await CreateVideoAsync(categoryId, "Film");

            using var formats = await Client.GetAsync(
                "/admin/video-on-demand/transcode-formats",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, formats.StatusCode);
            using var formatsBody = await ReadJsonAsync(formats);
            Assert.False(formatsBody.RootElement.GetProperty("available").GetBoolean());
            Assert.Equal(5, formatsBody.RootElement.GetProperty("formats").GetArrayLength());

            var csrf = await GetCsrfTokenAsync();
            using var requested = await PostJsonAsync(
                $"/admin/video-on-demand/{id}/transcodes",
                new { format = "mp4" },
                csrf);
            await AssertCodeAsync(
                requested,
                HttpStatusCode.ServiceUnavailable,
                "transcode_not_configured");

            csrf = await GetCsrfTokenAsync();
            using var unknownFormat = await PostJsonAsync(
                $"/admin/video-on-demand/{id}/transcodes",
                new { format = "wav" },
                csrf);
            await AssertCodeAsync(unknownFormat, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownBody = await ReadJsonAsync(unknownFormat);
            Assert.True(unknownBody.RootElement.GetProperty("errors").TryGetProperty("format", out _));
        }

        [Fact]
        public async Task PlayRedirectsForARedirectItemAndRefusesAnUnreadableSource()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Films");

            var redirectId = await CreateVideoAsync(categoryId, "Redirected", new
            {
                type = "redirect",
                source = "https://example.test/movie.mp4"
            });

            var (client, _) = CreateCookieClient();
            await SignInAsync(UserRole.Admin, "operator", "Password1", client);

            using var redirected = await client.GetAsync(
                $"/admin/video-on-demand/{redirectId}/play",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, redirected.StatusCode);
            Assert.Equal(
                "https://example.test/movie.mp4",
                redirected.Headers.Location?.ToString());

            var unreadable = await CreateVideoAsync(categoryId, "No File", new
            {
                type = "relay",
                source = "no-such-file.mp4"
            });

            using var refused = await Client.GetAsync(
                $"/admin/video-on-demand/{unreadable}/play",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(
                refused,
                HttpStatusCode.Conflict,
                "video_on_demand_not_playable");
        }

        [Fact]
        public async Task WritesAreRefusedWithoutCsrfAndReadsNeedThePermission()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Films");

            using var noCsrf = await PostJsonAsync(
                "/admin/video-on-demand",
                new
                {
                    name = "No Token",
                    type = "relay",
                    categoryId,
                    source = "https://example.test/no-token.mp4"
                });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);

            using var forbidden = await client.GetAsync(
                "/admin/video-on-demand",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        private async Task<string> CreateCategoryAsync(string name)
        {
            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type = "videoondemand" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateVideoAsync(string categoryId, string name, object? extra = null)
        {
            var body = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["type"] = "relay",
                ["categoryId"] = categoryId,
                ["source"] = $"https://example.test/{name.ToLowerInvariant()}.mp4"
            };

            if (extra is not null)
            {
                foreach (var property in extra.GetType().GetProperties())
                {
                    body[property.Name] = property.GetValue(extra);
                }
            }

            using var response = await PostVideoAsync(body);
            await AssertCodeAsync(response, HttpStatusCode.Created, "video_on_demand_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> PostVideoAsync(object body)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync("/admin/video-on-demand", body, csrf);
        }

        private async Task AssertBulkAsync(
            HttpResponseMessage response,
            int succeeded,
            int failed)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal(
                "video_on_demand_bulk_completed",
                body.RootElement.GetProperty("code").GetString());
            Assert.Equal(succeeded, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(failed, body.RootElement.GetProperty("failed").GetInt32());
            Assert.Equal(
                succeeded + failed,
                body.RootElement.GetProperty("items").GetArrayLength());
        }

        private async Task<HttpResponseMessage> ImportAsync(
            string playlist,
            string categoryId,
            string type = "relay",
            string? status = null,
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

            if (includeFile)
            {
                var file = new ByteArrayContent(Encoding.UTF8.GetBytes(playlist));
                file.Headers.ContentType = new MediaTypeHeaderValue("audio/x-mpegurl");
                form.Add(file, "playlist", "playlist.m3u");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/admin/video-on-demand/import")
            {
                Content = form
            };

            if (withCsrf)
            {
                request.Headers.Add("X-CSRF-Token", await GetCsrfTokenAsync());
            }

            return await Client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
