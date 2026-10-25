using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    public sealed class SeriesTests : IntegrationTestBase
    {
        public SeriesTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ASeriesKeepsItsMetadataAndCounts()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");

            using var created = await PostSeriesAsync(new
            {
                name = "Breaking Bad",
                status = "active",
                categoryId,
                imdbId = "tt0903747",
                posterUrl = "https://example.test/poster.jpg",
                backdropUrl = "https://example.test/backdrop.jpg",
                plot = "A chemistry teacher turns to crime.",
                cast = new[] { "Bryan Cranston", "Aaron Paul" },
                director = "Vince Gilligan",
                genres = new[] { "Crime", "Drama" },
                releaseDate = "2008-01-20",
                rating = 9.5m
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "series_created");
            Assert.NotNull(created.Headers.Location);
            var id = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/series/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal("Breaking Bad", root.GetProperty("name").GetString());
            Assert.Equal("Active", root.GetProperty("status").GetString());
            Assert.Equal("Drama", root.GetProperty("categoryName").GetString());
            Assert.Equal("2008-01-20", root.GetProperty("releaseDate").GetString());
            Assert.Equal(9.5m, root.GetProperty("rating").GetDecimal());
            Assert.Equal(0, root.GetProperty("seasonsCount").GetInt32());
            Assert.Equal(0, root.GetProperty("episodesCount").GetInt32());
            Assert.Equal(
                new[] { "Bryan Cranston", "Aaron Paul" },
                root.GetProperty("cast").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            var csrf = await GetCsrfTokenAsync();
            using var updated = await PatchJsonAsync(
                $"/admin/series/{id}",
                new
                {
                    name = "Breaking Bad (2008)",
                    rating = "",
                    releaseDate = ""
                },
                csrf);
            await AssertCodeAsync(updated, HttpStatusCode.OK, "series_updated");

            using var reread = await Client.GetAsync(
                $"/admin/series/{id}",
                TestContext.Current.CancellationToken);
            using var rereadBody = await ReadJsonAsync(reread);
            Assert.Equal(JsonValueKind.Null, rereadBody.RootElement.GetProperty("rating").ValueKind);
            Assert.Equal(
                JsonValueKind.Null,
                rereadBody.RootElement.GetProperty("releaseDate").ValueKind);

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/series/{id}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "series_deleted");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/series/{id}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "series_not_found");
        }

        [Fact]
        public async Task NamesAreUniqueWithinACategoryOnly()
        {
            await CreateAdminAndSignInAsync();
            var first = await CreateCategoryAsync("First");
            var second = await CreateCategoryAsync("Second");

            await CreateSeriesAsync(first, "Shared");

            using var duplicate = await PostSeriesAsync(new
            {
                name = "Shared",
                categoryId = first
            });
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "series_already_exists");

            using var otherCategory = await PostSeriesAsync(new
            {
                name = "Shared",
                categoryId = second
            });
            await AssertCodeAsync(otherCategory, HttpStatusCode.Created, "series_created");
        }

        [Fact]
        public async Task AnEpisodeKeepsItsMetadataAndItsNumberIsUniqueInsideTheSeason()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");
            var seriesId = await CreateSeriesAsync(categoryId, "The Wire");

            using var created = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes",
                new
                {
                    seasonNumber = 1,
                    episodeNumber = 1,
                    name = "The Target",
                    source = "https://example.test/the-wire-s01e01.mp4",
                    status = "active",
                    type = "relay",
                    imdbId = "tt0749451",
                    imageUrl = "https://example.test/episode.jpg",
                    plot = "The detail opens.",
                    releaseDate = "2002-06-02",
                    rating = 8.0m
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(created, HttpStatusCode.Created, "series_episode_created");
            var episodeId = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/series/{seriesId}/episodes/{episodeId}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal(1, root.GetProperty("seasonNumber").GetInt32());
            Assert.Equal(1, root.GetProperty("episodeNumber").GetInt32());
            Assert.Equal("The Target", root.GetProperty("name").GetString());
            Assert.Equal("Active", root.GetProperty("status").GetString());
            Assert.Equal("Relay", root.GetProperty("type").GetString());
            Assert.Equal(8.0m, root.GetProperty("rating").GetDecimal());

            var csrf = await GetCsrfTokenAsync();
            using var updated = await PatchJsonAsync(
                $"/admin/series/{seriesId}/episodes/{episodeId}",
                new
                {
                    name = "The Target (Pilot)",
                    rating = "",
                    releaseDate = ""
                },
                csrf);
            await AssertCodeAsync(updated, HttpStatusCode.OK, "series_episode_updated");

            using var duplicate = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes",
                new
                {
                    seasonNumber = 1,
                    episodeNumber = 1,
                    source = "https://example.test/other.mp4",
                    type = "relay"
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(
                duplicate,
                HttpStatusCode.Conflict,
                "series_episode_already_exists");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync(
                $"/admin/series/{seriesId}/episodes/{episodeId}",
                csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "series_episode_deleted");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync(
                $"/admin/series/{seriesId}/episodes/{episodeId}",
                csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "series_episode_not_found");
        }

        [Fact]
        public async Task AddingASeasonReadsTheFolderInNaturalOrderAndRefusesAnExistingSeason()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");
            var seriesId = await CreateSeriesAsync(categoryId, "The Wire");
            var folder = CreateSeasonFolder("Episode 10.mp4", "Episode 2.mkv", "Episode 1.avi", "cover.jpg");

            try
            {
                using var created = await PostJsonAsync(
                    $"/admin/series/{seriesId}/seasons",
                    new
                    {
                        seasonNumber = 1,
                        folderPath = folder,
                        addSeasonNumberToName = true,
                        addEpisodeNumberToName = true,
                        status = "active",
                        type = "relay"
                    },
                    await GetCsrfTokenAsync());

                await AssertCodeAsync(created, HttpStatusCode.Created, "series_season_created");
                using var createdBody = await ReadJsonAsync(created);
                Assert.Equal(3, createdBody.RootElement.GetProperty("addedCount").GetInt32());
                Assert.Equal(1, createdBody.RootElement.GetProperty("firstEpisodeNumber").GetInt32());
                Assert.Equal(3, createdBody.RootElement.GetProperty("lastEpisodeNumber").GetInt32());

                using var episodes = await Client.GetAsync(
                    $"/admin/series/{seriesId}/episodes?sortBy=season&sortOrder=asc",
                    TestContext.Current.CancellationToken);
                using var episodesBody = await ReadJsonAsync(episodes);
                var rows = episodesBody.RootElement.GetProperty("items").EnumerateArray().ToArray();

                Assert.Equal(3, rows.Length);
                Assert.Equal(
                    new[] { "The Wire S01E01", "The Wire S01E02", "The Wire S01E03" },
                    rows.Select(row => row.GetProperty("name").GetString()).ToArray());
                Assert.EndsWith("Episode 1.avi", rows[0].GetProperty("source").GetString());
                Assert.EndsWith("Episode 2.mkv", rows[1].GetProperty("source").GetString());
                Assert.EndsWith("Episode 10.mp4", rows[2].GetProperty("source").GetString());
                Assert.All(rows, row =>
                {
                    Assert.Equal("Active", row.GetProperty("status").GetString());
                    Assert.Equal("Relay", row.GetProperty("type").GetString());
                });

                using var detail = await Client.GetAsync(
                    $"/admin/series/{seriesId}",
                    TestContext.Current.CancellationToken);
                using var detailBody = await ReadJsonAsync(detail);
                Assert.Equal(1, detailBody.RootElement.GetProperty("seasonsCount").GetInt32());
                Assert.Equal(3, detailBody.RootElement.GetProperty("episodesCount").GetInt32());

                using var repeated = await PostJsonAsync(
                    $"/admin/series/{seriesId}/seasons",
                    new
                    {
                        seasonNumber = 1,
                        folderPath = folder,
                        status = "inactive",
                        type = "redirect"
                    },
                    await GetCsrfTokenAsync());
                await AssertCodeAsync(
                    repeated,
                    HttpStatusCode.Conflict,
                    "series_season_already_exists");
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public async Task AddingASeasonRefusesAnEmptyOrMissingFolder()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");
            var seriesId = await CreateSeriesAsync(categoryId, "Empty");
            var folder = CreateSeasonFolder("cover.jpg", "notes.txt");

            try
            {
                using var empty = await PostJsonAsync(
                    $"/admin/series/{seriesId}/seasons",
                    new
                    {
                        seasonNumber = 1,
                        folderPath = folder,
                        type = "relay"
                    },
                    await GetCsrfTokenAsync());
                await AssertCodeAsync(empty, HttpStatusCode.BadRequest, "validation_failed");
                using var emptyBody = await ReadJsonAsync(empty);
                Assert.True(
                    emptyBody.RootElement.GetProperty("errors").TryGetProperty("folderPath", out _));

                using var missing = await PostJsonAsync(
                    $"/admin/series/{seriesId}/seasons",
                    new
                    {
                        seasonNumber = 2,
                        folderPath = Path.Combine(folder, "missing"),
                        type = "relay"
                    },
                    await GetCsrfTokenAsync());
                await AssertCodeAsync(missing, HttpStatusCode.BadRequest, "validation_failed");
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public async Task CountsFollowTheEpisodesAndTheListSortsByThem()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");

            var deep = await CreateSeriesAsync(categoryId, "Deep");
            var many = await CreateSeriesAsync(categoryId, "Many");

            await CreateEpisodeAsync(deep, 3, 1);
            await CreateEpisodeAsync(many, 1, 1);
            await CreateEpisodeAsync(many, 1, 2);

            using var deepDetail = await Client.GetAsync(
                $"/admin/series/{deep}",
                TestContext.Current.CancellationToken);
            using var deepBody = await ReadJsonAsync(deepDetail);
            Assert.Equal(3, deepBody.RootElement.GetProperty("seasonsCount").GetInt32());
            Assert.Equal(1, deepBody.RootElement.GetProperty("episodesCount").GetInt32());

            using var manyDetail = await Client.GetAsync(
                $"/admin/series/{many}",
                TestContext.Current.CancellationToken);
            using var manyBody = await ReadJsonAsync(manyDetail);
            Assert.Equal(1, manyBody.RootElement.GetProperty("seasonsCount").GetInt32());
            Assert.Equal(2, manyBody.RootElement.GetProperty("episodesCount").GetInt32());

            using var bySeasons = await Client.GetAsync(
                "/admin/series?sortBy=seasons&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var bySeasonsBody = await ReadJsonAsync(bySeasons);
            Assert.Equal(
                new[] { "Deep", "Many" },
                bySeasonsBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var byEpisodes = await Client.GetAsync(
                "/admin/series?sortBy=episodes&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var byEpisodesBody = await ReadJsonAsync(byEpisodes);
            Assert.Equal(
                new[] { "Many", "Deep" },
                byEpisodesBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var badSort = await Client.GetAsync(
                "/admin/series?sortBy=plot",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badSort, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task BulkSeriesAndEpisodesArePartialByDesign()
        {
            await CreateAdminAndSignInAsync();
            var source = await CreateCategoryAsync("Source");
            var destination = await CreateCategoryAsync("Destination");

            var one = await CreateSeriesAsync(source, "One");
            var two = await CreateSeriesAsync(source, "Two");
            var three = await CreateSeriesAsync(source, "Three");
            await CreateSeriesAsync(destination, "Two");

            var csrf = await GetCsrfTokenAsync();
            using var active = await PostJsonAsync(
                "/admin/series/bulk",
                new { action = "change-status", ids = new[] { one, two, three }, status = "active" },
                csrf);
            await AssertBulkAsync(active, "series_bulk_completed", 3, 0);

            csrf = await GetCsrfTokenAsync();
            using var moved = await PostJsonAsync(
                "/admin/series/bulk",
                new { action = "move-category", ids = new[] { one, two, three }, categoryId = destination },
                csrf);
            using var movedBody = await ReadJsonAsync(moved);
            Assert.Equal(2, movedBody.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, movedBody.RootElement.GetProperty("failed").GetInt32());
            Assert.Equal(
                "series_already_exists",
                movedBody.RootElement.GetProperty("items")
                    .EnumerateArray()
                    .Single(item => !item.GetProperty("ok").GetBoolean())
                    .GetProperty("code")
                    .GetString());

            csrf = await GetCsrfTokenAsync();
            using var deleted = await PostJsonAsync(
                "/admin/series/bulk",
                new { action = "delete", ids = new[] { one, two, three } },
                csrf);
            await AssertBulkAsync(deleted, "series_bulk_completed", 3, 0);

            var seriesId = await CreateSeriesAsync(source, "With Episodes");
            var first = await CreateEpisodeAsync(seriesId, 1, 1);
            var second = await CreateEpisodeAsync(seriesId, 1, 2);

            csrf = await GetCsrfTokenAsync();
            using var episodes = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes/bulk",
                new { action = "change-type", ids = new[] { first, second }, type = "redirect" },
                csrf);
            await AssertBulkAsync(episodes, "series_episodes_bulk_completed", 2, 0);

            csrf = await GetCsrfTokenAsync();
            using var removed = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes/bulk",
                new { action = "delete", ids = new[] { first, second } },
                csrf);
            await AssertBulkAsync(removed, "series_episodes_bulk_completed", 2, 0);
        }

        [Fact]
        public async Task ACategoryWithASeriesCannotBeDeleted()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("In Use");
            await CreateSeriesAsync(categoryId, "Kept");

            var csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/content-categories/{categoryId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.Conflict, "content_category_in_use");

            using var body = await ReadJsonAsync(deleted);
            Assert.Contains("1 item", body.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task EpisodeTranscodingIsAnHonestStub()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");
            var seriesId = await CreateSeriesAsync(categoryId, "Transcoded");
            var episodeId = await CreateEpisodeAsync(seriesId, 1, 1);

            using var formats = await Client.GetAsync(
                "/admin/series/transcode-formats",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, formats.StatusCode);
            using var formatsBody = await ReadJsonAsync(formats);
            Assert.False(formatsBody.RootElement.GetProperty("available").GetBoolean());
            Assert.Equal(5, formatsBody.RootElement.GetProperty("formats").GetArrayLength());

            var csrf = await GetCsrfTokenAsync();
            using var requested = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes/{episodeId}/transcodes",
                new { format = "mp4" },
                csrf);
            await AssertCodeAsync(
                requested,
                HttpStatusCode.ServiceUnavailable,
                "transcode_not_configured");

            csrf = await GetCsrfTokenAsync();
            using var unknownFormat = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes/{episodeId}/transcodes",
                new { format = "wav" },
                csrf);
            await AssertCodeAsync(unknownFormat, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownBody = await ReadJsonAsync(unknownFormat);
            Assert.True(unknownBody.RootElement.GetProperty("errors").TryGetProperty("format", out _));
        }

        [Fact]
        public async Task WritesAreRefusedWithoutCsrfAndReadsNeedThePermission()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Drama");

            using var noCsrf = await PostJsonAsync(
                "/admin/series",
                new { name = "No Token", categoryId });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);

            using var forbidden = await client.GetAsync(
                "/admin/series",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        private async Task<string> CreateCategoryAsync(string name)
        {
            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type = "series" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateSeriesAsync(string categoryId, string name)
        {
            using var response = await PostSeriesAsync(new
            {
                name,
                categoryId,
                source = "unused"
            });

            await AssertCodeAsync(response, HttpStatusCode.Created, "series_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> PostSeriesAsync(object body)
            => await PostJsonAsync("/admin/series", body, await GetCsrfTokenAsync());

        private async Task<string> CreateEpisodeAsync(
            string seriesId,
            int seasonNumber,
            int episodeNumber)
        {
            using var response = await PostJsonAsync(
                $"/admin/series/{seriesId}/episodes",
                new
                {
                    seasonNumber,
                    episodeNumber,
                    source = $"https://example.test/s{seasonNumber}e{episodeNumber}.mp4",
                    type = "relay"
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.Created, "series_episode_created");

            return IdFromLocation(response);
        }

        private static string CreateSeasonFolder(params string[] fileNames)
        {
            var folder = Path.Combine(Path.GetTempPath(), "fluxy-season-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);

            foreach (var fileName in fileNames)
            {
                File.WriteAllText(Path.Combine(folder, fileName), string.Empty);
            }

            return folder;
        }

        private async Task AssertBulkAsync(
            HttpResponseMessage response,
            string code,
            int succeeded,
            int failed)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
            Assert.Equal(succeeded, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(failed, body.RootElement.GetProperty("failed").GetInt32());
            Assert.Equal(
                succeeded + failed,
                body.RootElement.GetProperty("items").GetArrayLength());
        }
    }
}
