using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    /// <summary>
    /// The bouquet endpoints: the packages themselves, the content each one holds, and the
    /// bouquet picker that reaches them from the four content forms.
    /// </summary>
    /// <remarks>
    /// The rule these tests exist to pin is the one the screens cannot show: <b>the order the
    /// content manager writes is the order the next read answers</b>, and <b>a tick in a
    /// content form's picker appends rather than inserts</b>. Both are about positions, and a
    /// position is exactly the kind of fact that survives every type-check while being wrong.
    /// </remarks>
    public sealed class BouquetTests : IntegrationTestBase
    {
        public BouquetTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ANameIsUniqueWithoutRegardToCase()
        {
            await CreateAdminAndSignInAsync();

            _ = await CreateBouquetAsync("Cinema");
            _ = await CreateBouquetAsync("Sport");

            // The same word in another spelling is the same bouquet: an operator scanning the
            // list cannot tell the two apart, and a package delivered under two spellings is one
            // package.
            using var lowerCase = await PostBouquetAsync("cinema");
            await AssertCodeAsync(lowerCase, HttpStatusCode.Conflict, "bouquet_already_exists");
            using var lowerCaseBody = await ReadJsonAsync(lowerCase);
            Assert.True(lowerCaseBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var upperCase = await PostBouquetAsync("CINEMA");
            await AssertCodeAsync(upperCase, HttpStatusCode.Conflict, "bouquet_already_exists");

            using var blank = await PostBouquetAsync("   ");
            await AssertCodeAsync(blank, HttpStatusCode.BadRequest, "validation_failed");

            // Renaming to the row's own spelling in another case is not a conflict with itself.
            using var list = await Client.GetAsync("/admin/bouquets", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            var first = body.RootElement.GetProperty("items")[0].GetProperty("id").GetString()!;

            using var renamed = await PatchJsonAsync(
                $"/admin/bouquets/{first}",
                new { name = "CINEMA" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "bouquet_updated");
        }

        [Fact]
        public async Task ABouquetJoinsTheEndOfTheListAndTheOrderIsRewrittenWholesale()
        {
            await CreateAdminAndSignInAsync();

            var first = await CreateBouquetAsync("First");
            var second = await CreateBouquetAsync("Second");
            var third = await CreateBouquetAsync("Third");

            using var list = await Client.GetAsync("/admin/bouquets", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

            Assert.Equal(
                new[] { "First", "Second", "Third" },
                items.Select(item => item.GetProperty("name").GetString()).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, items.Select(item => item.GetProperty("sortOrder").GetInt32()).ToArray());

            using var reordered = await PutJsonAsync(
                "/admin/bouquets/order",
                new { orderedIds = new[] { third, first, second } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(reordered, HttpStatusCode.OK, "bouquets_reordered");

            using var after = await Client.GetAsync("/admin/bouquets", TestContext.Current.CancellationToken);
            using var afterBody = await ReadJsonAsync(after);
            var afterItems = afterBody.RootElement.GetProperty("items").EnumerateArray().ToArray();

            Assert.Equal(
                new[] { "Third", "First", "Second" },
                afterItems.Select(item => item.GetProperty("name").GetString()).ToArray());

            // A subset, a duplicate and a foreign identifier are each refused whole: half an
            // order is not an order, it is two rows sharing a position.
            using var subset = await PutJsonAsync(
                "/admin/bouquets/order",
                new { orderedIds = new[] { first, second } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(subset, HttpStatusCode.BadRequest, "validation_failed");

            using var duplicate = await PutJsonAsync(
                "/admin/bouquets/order",
                new { orderedIds = new[] { first, first, second, third } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(duplicate, HttpStatusCode.BadRequest, "validation_failed");

            using var foreign = await PutJsonAsync(
                "/admin/bouquets/order",
                new { orderedIds = new[] { first, second, third, Guid.NewGuid().ToString() } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(foreign, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task AnEmptyBouquetIsDeletableAndAFullOneIsNot()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Packages", "VideoStream");
            var stream = await CreateStreamAsync(category, "News");
            var bouquet = await CreateBouquetAsync("Empty");

            var csrf = await GetCsrfTokenAsync();
            using var content = await PutJsonAsync(
                $"/admin/bouquets/{bouquet}/content",
                new { contentType = "videostream", contentIds = new[] { stream } },
                csrf);
            await AssertCodeAsync(content, HttpStatusCode.OK, "bouquet_content_set");

            csrf = await GetCsrfTokenAsync();
            using var refused = await DeleteAsync($"/admin/bouquets/{bouquet}", csrf);
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "bouquet_not_empty");
            using var refusedBody = await ReadJsonAsync(refused);
            Assert.Contains("1 item", refusedBody.RootElement.GetProperty("message").GetString());

            // Emptied, the very same bouquet becomes deletable - and the deletion leaves the
            // positions of the surviving bouquets alone.
            csrf = await GetCsrfTokenAsync();
            using var emptied = await PutJsonAsync(
                $"/admin/bouquets/{bouquet}/content",
                new { contentType = "videostream", contentIds = Array.Empty<string>() },
                csrf);
            await AssertCodeAsync(emptied, HttpStatusCode.OK, "bouquet_content_set");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/bouquets/{bouquet}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "bouquet_deleted");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/bouquets/{bouquet}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "bouquet_not_found");
        }

        [Fact]
        public async Task TheContentManagerOrderIsWhatTheNextReadAnswers()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Ordered", "VideoStream");
            var alpha = await CreateStreamAsync(category, "Alpha");
            var beta = await CreateStreamAsync(category, "Beta");
            var gamma = await CreateStreamAsync(category, "Gamma");
            var bouquet = await CreateBouquetAsync("Ordered");

            using var written = await PutJsonAsync(
                $"/admin/bouquets/{bouquet}/content",
                new { contentType = "videostream", contentIds = new[] { gamma, alpha, beta } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(written, HttpStatusCode.OK, "bouquet_content_set");

            // The order the manager saved is the order it opens with - that is the whole
            // promise of the drag, and the one thing a set would quietly lose.
            using var read = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);

            using var body = await ReadJsonAsync(read);
            Assert.Equal(
                new[] { "Gamma", "Alpha", "Beta" },
                Names(body.RootElement.GetProperty("members")));
            Assert.Empty(body.RootElement.GetProperty("available").EnumerateArray());

            // A second save replaces both membership and order in one statement.
            using var rewritten = await PutJsonAsync(
                $"/admin/bouquets/{bouquet}/content",
                new { contentType = "videostream", contentIds = new[] { beta, gamma } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(rewritten, HttpStatusCode.OK, "bouquet_content_set");

            using var reread = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            using var rereadBody = await ReadJsonAsync(reread);

            Assert.Equal(new[] { "Beta", "Gamma" }, Names(rereadBody.RootElement.GetProperty("members")));
            Assert.Equal(new[] { "Alpha" }, Names(rereadBody.RootElement.GetProperty("available")));

            // A duplicate, an identifier of no content and an unknown kind are refused, and the
            // refusal names the field rather than silently skipping the entry.
            foreach (var broken in new object[]
            {
                new { contentType = "videostream", contentIds = new[] { beta, beta } },
                new { contentType = "videostream", contentIds = new[] { Guid.NewGuid().ToString() } },
                new { contentType = "podcast", contentIds = new[] { beta } }
            })
            {
                using var refused = await PutJsonAsync(
                    $"/admin/bouquets/{bouquet}/content",
                    broken,
                    await GetCsrfTokenAsync());

                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }

            using var missing = await Client.GetAsync(
                $"/admin/bouquets/{Guid.NewGuid()}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "bouquet_not_found");
        }

        [Fact]
        public async Task OneItemMayLiveInSeveralBouquetsAtOnce()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Shared", "VideoStream");
            var stream = await CreateStreamAsync(category, "Shared");
            var first = await CreateBouquetAsync("First");
            var second = await CreateBouquetAsync("Second");

            using var firstWrite = await PutJsonAsync(
                $"/admin/bouquets/{first}/content",
                new { contentType = "videostream", contentIds = new[] { stream } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(firstWrite, HttpStatusCode.OK, "bouquet_content_set");

            using var secondWrite = await PutJsonAsync(
                $"/admin/bouquets/{second}/content",
                new { contentType = "videostream", contentIds = new[] { stream } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(secondWrite, HttpStatusCode.OK, "bouquet_content_set");

            using var list = await Client.GetAsync("/admin/bouquets", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

            Assert.All(items, item => Assert.Equal(1, item.GetProperty("videoStreamCount").GetInt32()));
        }

        [Fact]
        public async Task TheContentPickerAppendsWhereverTheItemJoins()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Forms", "VideoStream");
            var alpha = await CreateStreamAsync(category, "Alpha");
            var beta = await CreateStreamAsync(category, "Beta");
            var gamma = await CreateStreamAsync(category, "Gamma");
            var bouquet = await CreateBouquetAsync("Form-filled");

            // The bouquet screen arranged Alpha then Beta.
            using var arranged = await PutJsonAsync(
                $"/admin/bouquets/{bouquet}/content",
                new { contentType = "videostream", contentIds = new[] { alpha, beta } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(arranged, HttpStatusCode.OK, "bouquet_content_set");

            // A stream's own form now puts Gamma into the bouquet. A tick in a picker says
            // "this stream is in that package", never "move everything else along", so Gamma
            // arrives at the end and the two positions above it do not move.
            using var patched = await PatchJsonAsync(
                $"/admin/video-streams/{gamma}",
                new { bouquetIds = new[] { bouquet } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(patched, HttpStatusCode.OK, "video_stream_updated");

            using var read = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(read);
            Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, Names(body.RootElement.GetProperty("members")));

            // The stream's detail answers the picker with its packages, which is what the form
            // opens with.
            using var detail = await Client.GetAsync(
                $"/admin/video-streams/{gamma}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(
                new[] { bouquet },
                detailBody.RootElement.GetProperty("bouquetIds").EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            // Ticking again changes nothing: the stream is already in, so its position stays.
            using var again = await PatchJsonAsync(
                $"/admin/video-streams/{gamma}",
                new { bouquetIds = new[] { bouquet } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.OK, "video_stream_updated");

            using var after = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            using var afterBody = await ReadJsonAsync(after);
            Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, Names(afterBody.RootElement.GetProperty("members")));

            // An empty list is a statement: the stream leaves every bouquet.
            using var cleared = await PatchJsonAsync(
                $"/admin/video-streams/{gamma}",
                new { bouquetIds = Array.Empty<string>() },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "video_stream_updated");

            using var withoutGamma = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            using var withoutGammaBody = await ReadJsonAsync(withoutGamma);
            Assert.Equal(
                new[] { "Alpha", "Beta" },
                Names(withoutGammaBody.RootElement.GetProperty("members")));
            Assert.Contains("Gamma", Names(withoutGammaBody.RootElement.GetProperty("available")));

            // A body naming a bouquet that does not exist is refused with the field named.
            using var unknown = await PatchJsonAsync(
                $"/admin/video-streams/{gamma}",
                new { bouquetIds = new[] { Guid.NewGuid().ToString() } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(unknown, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownBody = await ReadJsonAsync(unknown);
            Assert.True(unknownBody.RootElement.GetProperty("errors").TryGetProperty("bouquetIds", out _));

            // An absent field keeps every link, which is the patch convention of this API.
            using var untouched = await PatchJsonAsync(
                $"/admin/video-streams/{alpha}",
                new { name = "Alpha Renamed" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(untouched, HttpStatusCode.OK, "video_stream_updated");

            using var still = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            using var stillBody = await ReadJsonAsync(still);
            Assert.Equal(
                new[] { "Alpha Renamed", "Beta" },
                Names(stillBody.RootElement.GetProperty("members")));
        }

        [Fact]
        public async Task DeletingContentTakesItsMembershipsWithIt()
        {
            await CreateAdminAndSignInAsync();

            var category = await CreateCategoryAsync("Cascade", "VideoStream");
            var stream = await CreateStreamAsync(category, "Doomed");
            var keeper = await CreateStreamAsync(category, "Keeper");
            var bouquet = await CreateBouquetAsync("Cascade");

            using var content = await PutJsonAsync(
                $"/admin/bouquets/{bouquet}/content",
                new { contentType = "videostream", contentIds = new[] { stream, keeper } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(content, HttpStatusCode.OK, "bouquet_content_set");

            using var deleted = await DeleteAsync(
                $"/admin/video-streams/{stream}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "video_stream_deleted");

            // The foreign key cascades, so the membership left with the stream and the bouquet
            // no longer counts it - a count that lied would make the delete button lie too.
            using var list = await Client.GetAsync("/admin/bouquets", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            Assert.Equal(1, body.RootElement.GetProperty("items")[0].GetProperty("videoStreamCount").GetInt32());

            using var read = await Client.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            using var readBody = await ReadJsonAsync(read);
            Assert.Equal(new[] { "Keeper" }, Names(readBody.RootElement.GetProperty("members")));
        }

        [Fact]
        public async Task BouquetWritesRequireAntiforgeryAndTheBouquetPermissions()
        {
            using var anonymous = Fixture.CreateClient();
            using var anonymousList = await anonymous.GetAsync(
                "/admin/bouquets",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAdminAndSignInAsync();
            using var noCsrf = await PostJsonAsync("/admin/bouquets", new { name = "No Token" });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            // The two bouquet grants are one pair per resource, like every other content kind:
            // an administrator whose group holds only the read grant may read the list and a
            // bouquet's content but may change nothing.
            var bouquet = await CreateBouquetAsync("Guarded");

            var readerClient = Fixture.CreateClient();
            var readerGroup = await CreateGroupAsync(
                "bouquet-readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewBouquets);
            var readerId = await CreateUserAsync(
                UserRole.Admin,
                "bouquet-reader",
                groupId: readerGroup);
            Assert.NotEqual(Guid.Empty, readerId);
            await SignInAsync(UserRole.Admin, "bouquet-reader", "Password1", readerClient);

            using var readable = await readerClient.GetAsync(
                "/admin/bouquets",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

            using var readableContent = await readerClient.GetAsync(
                $"/admin/bouquets/{bouquet}/content?contentType=videostream",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readableContent.StatusCode);

            using var refusedWrite = await PatchJsonAsync(
                $"/admin/bouquets/{bouquet}",
                new { name = "Not Allowed" },
                await GetCsrfTokenAsync(readerClient),
                readerClient);
            Assert.Equal(HttpStatusCode.Forbidden, refusedWrite.StatusCode);

            var editorClient = Fixture.CreateClient();
            var editorGroup = await CreateGroupAsync(
                "bouquet-editors",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewBouquets,
                UserPermission.EditBouquets);
            _ = await CreateUserAsync(UserRole.Admin, "bouquet-editor", groupId: editorGroup);
            await SignInAsync(UserRole.Admin, "bouquet-editor", "Password1", editorClient);

            using var allowed = await PatchJsonAsync(
                $"/admin/bouquets/{bouquet}",
                new { name = "Allowed" },
                await GetCsrfTokenAsync(editorClient),
                editorClient);
            await AssertCodeAsync(allowed, HttpStatusCode.OK, "bouquet_updated");
        }

        private static string?[] Names(JsonElement items)
            => items.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();

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
            using var response = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name,
                    type = "permanent",
                    categoryId,
                    sources = new[] { "https://example.test/source" }
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "video_stream_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateBouquetAsync(string name)
        {
            using var response = await PostBouquetAsync(name);
            await AssertCodeAsync(response, HttpStatusCode.Created, "bouquet_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> PostBouquetAsync(string name)
            => await PostJsonAsync("/admin/bouquets", new { name }, await GetCsrfTokenAsync());
    }
}
