using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    public sealed class ContentCategoryTests : IntegrationTestBase
    {
        public ContentCategoryTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task CategoriesAreScopedToTheirTypeAndJoinTheEndOfTheirList()
        {
            await CreateAdminAndSignInAsync();

            using var missingType = await Client.GetAsync("/admin/content-categories", TestContext.Current.CancellationToken);
            await AssertCodeAsync(missingType, HttpStatusCode.BadRequest, "validation_failed");
            using var missingBody = await ReadJsonAsync(missingType);
            Assert.True(missingBody.RootElement.GetProperty("errors").TryGetProperty("type", out _));

            var newsVideo = await CreateCategoryAsync("News", "VideoStream");
            _ = await CreateCategoryAsync("News", "Series");
            var sportsVideo = await CreateCategoryAsync("Sports", "VideoStream");

            using var videoList = await Client.GetAsync("/admin/content-categories?type=VideoStream", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, videoList.StatusCode);
            using var videoBody = await ReadJsonAsync(videoList);
            var videoItems = videoBody.RootElement.GetProperty("items").EnumerateArray().ToArray();

            Assert.Equal(2, videoItems.Length);
            Assert.Equal("News", videoItems[0].GetProperty("name").GetString());
            Assert.Equal(1, videoItems[0].GetProperty("sortOrder").GetInt32());
            Assert.Equal("Sports", videoItems[1].GetProperty("name").GetString());
            Assert.Equal(2, videoItems[1].GetProperty("sortOrder").GetInt32());
            Assert.Equal(newsVideo, videoItems[0].GetProperty("id").GetString());
            Assert.Equal(sportsVideo, videoItems[1].GetProperty("id").GetString());

            using var seriesList = await Client.GetAsync("/admin/content-categories?type=series", TestContext.Current.CancellationToken);
            using var seriesBody = await ReadJsonAsync(seriesList);
            Assert.Single(seriesBody.RootElement.GetProperty("items").EnumerateArray());

            using var unknownType = await Client.GetAsync("/admin/content-categories?type=podcast", TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownType, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ANameIsUniqueWithinItsTypeOnly()
        {
            await CreateAdminAndSignInAsync();
            await CreateCategoryAsync("News", "VideoStream");

            using var duplicate = await PostCategoryAsync("News", "VideoStream");
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "content_category_already_exists");
            using var duplicateBody = await ReadJsonAsync(duplicate);
            Assert.True(duplicateBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var sameNameOtherType = await PostCategoryAsync("News", "Series");
            await AssertCodeAsync(sameNameOtherType, HttpStatusCode.Created, "content_category_created");

            using var blank = await PostCategoryAsync("   ", "VideoStream");
            await AssertCodeAsync(blank, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ACategoryCanBeRenamedAndDeletedAndItsGapIsKept()
        {
            await CreateAdminAndSignInAsync();
            var first = await CreateCategoryAsync("First", "VideoStream");
            var second = await CreateCategoryAsync("Second", "VideoStream");
            _ = await CreateCategoryAsync("Third", "VideoStream");

            var csrf = await GetCsrfTokenAsync();
            using var renamed = await PatchJsonAsync(
                $"/admin/content-categories/{first}",
                new { name = "First Renamed" },
                csrf);
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "content_category_updated");

            csrf = await GetCsrfTokenAsync();
            using var duplicateRename = await PatchJsonAsync(
                $"/admin/content-categories/{first}",
                new { name = "Second" },
                csrf);
            await AssertCodeAsync(duplicateRename, HttpStatusCode.Conflict, "content_category_already_exists");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/content-categories/{second}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "content_category_deleted");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/content-categories/{second}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "content_category_not_found");

            using var list = await Client.GetAsync("/admin/content-categories?type=VideoStream", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            var positions = body.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("sortOrder").GetInt32())
                .ToArray();

            Assert.Equal(new[] { 1, 3 }, positions);
        }

        [Fact]
        public async Task ReorderWritesTheWholeListInOneRequest()
        {
            await CreateAdminAndSignInAsync();
            var first = await CreateCategoryAsync("First", "VideoStream");
            var second = await CreateCategoryAsync("Second", "VideoStream");
            var third = await CreateCategoryAsync("Third", "VideoStream");

            var csrf = await GetCsrfTokenAsync();
            using var reordered = await PutJsonAsync(
                "/admin/content-categories/order",
                new
                {
                    contentType = "VideoStream",
                    orderedIds = new[] { third, second, first }
                },
                csrf);
            await AssertCodeAsync(reordered, HttpStatusCode.OK, "content_categories_reordered");

            using var list = await Client.GetAsync("/admin/content-categories?type=videostream", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

            Assert.Equal(
                new[] { "Third", "Second", "First" },
                items.Select(item => item.GetProperty("name").GetString()).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, items.Select(item => item.GetProperty("sortOrder").GetInt32()).ToArray());

            csrf = await GetCsrfTokenAsync();
            using var subset = await PutJsonAsync(
                "/admin/content-categories/order",
                new { contentType = "VideoStream", orderedIds = new[] { first, second } },
                csrf);
            await AssertCodeAsync(subset, HttpStatusCode.BadRequest, "validation_failed");
            using var subsetBody = await ReadJsonAsync(subset);
            Assert.True(subsetBody.RootElement.GetProperty("errors").TryGetProperty("orderedIds", out _));

            csrf = await GetCsrfTokenAsync();
            using var duplicate = await PutJsonAsync(
                "/admin/content-categories/order",
                new { contentType = "VideoStream", orderedIds = new[] { first, first, second } },
                csrf);
            await AssertCodeAsync(duplicate, HttpStatusCode.BadRequest, "validation_failed");

            csrf = await GetCsrfTokenAsync();
            using var foreign = await PutJsonAsync(
                "/admin/content-categories/order",
                new
                {
                    contentType = "VideoStream",
                    orderedIds = new[] { first, second, third, Guid.NewGuid().ToString() }
                },
                csrf);
            await AssertCodeAsync(foreign, HttpStatusCode.BadRequest, "validation_failed");

            using var unknownType = await PutJsonAsync(
                "/admin/content-categories/order",
                new { contentType = "Podcast", orderedIds = new[] { first } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(unknownType, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task CategoryWritesRequireAntiforgeryAndTheAdminRole()
        {
            using var anonymous = Fixture.CreateClient();
            using var anonymousList = await anonymous.GetAsync(
                "/admin/content-categories?type=VideoStream",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAdminAndSignInAsync();
            using var noCsrf = await PostJsonAsync(
                "/admin/content-categories",
                new { name = "No Token", type = "VideoStream" });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "client", client: client);
            using var forbidden = await client.GetAsync("/admin/content-categories?type=VideoStream", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        private async Task<string> CreateCategoryAsync(string name, string type)
        {
            using var response = await PostCategoryAsync(name, type);
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> PostCategoryAsync(string name, string type)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync(
                "/admin/content-categories",
                new { name, type },
                csrf);
        }
    }
}
