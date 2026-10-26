using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Content
{
    public sealed class EpgSourceTests : IntegrationTestBase
    {
        public EpgSourceTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ASourceIsCreatedChangedAndDeleted()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostSourceAsync(new
            {
                name = "Main guide",
                url = "https://example.test/guide.xml",
                daysKeep = "5"
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "epg_source_created");
            Assert.NotNull(created.Headers.Location);
            var id = IdFromLocation(created);
            Assert.Equal($"/admin/epg-sources/{id}", created.Headers.Location?.ToString());

            using var detail = await Client.GetAsync(
                $"/admin/epg-sources/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal("Main guide", root.GetProperty("name").GetString());
            Assert.Equal("https://example.test/guide.xml", root.GetProperty("url").GetString());
            Assert.Equal(5, root.GetProperty("daysKeep").GetInt32());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, root.GetProperty("lastEpgUpdatedAt").ValueKind);

            // The retention defaults to seven when a create names none, and the raw guide is
            // never part of an answer.
            using var withDefault = await PostSourceAsync(new
            {
                name = "Default guide",
                url = "https://example.test/default.xml"
            });
            await AssertCodeAsync(withDefault, HttpStatusCode.Created, "epg_source_created");
            var defaultId = IdFromLocation(withDefault);

            using var defaultDetail = await Client.GetAsync(
                $"/admin/epg-sources/{defaultId}",
                TestContext.Current.CancellationToken);
            using var defaultBody = await ReadJsonAsync(defaultDetail);
            Assert.Equal(7, defaultBody.RootElement.GetProperty("daysKeep").GetInt32());
            Assert.False(defaultBody.RootElement.TryGetProperty("rawData", out _));

            // A body that names one field keeps the other two.
            var csrf = await GetCsrfTokenAsync();
            using var renamed = await PatchJsonAsync(
                $"/admin/epg-sources/{id}",
                new { name = "Renamed guide" },
                csrf);
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "epg_source_updated");

            using var afterRename = await Client.GetAsync(
                $"/admin/epg-sources/{id}",
                TestContext.Current.CancellationToken);
            using var afterRenameBody = await ReadJsonAsync(afterRename);
            Assert.Equal("Renamed guide", afterRenameBody.RootElement.GetProperty("name").GetString());
            Assert.Equal(
                "https://example.test/guide.xml",
                afterRenameBody.RootElement.GetProperty("url").GetString());
            Assert.Equal(5, afterRenameBody.RootElement.GetProperty("daysKeep").GetInt32());

            csrf = await GetCsrfTokenAsync();
            using var retention = await PatchJsonAsync(
                $"/admin/epg-sources/{id}",
                new { daysKeep = "30" },
                csrf);
            await AssertCodeAsync(retention, HttpStatusCode.OK, "epg_source_updated");

            using var afterRetention = await Client.GetAsync(
                $"/admin/epg-sources/{id}",
                TestContext.Current.CancellationToken);
            using var afterRetentionBody = await ReadJsonAsync(afterRetention);
            Assert.Equal(30, afterRetentionBody.RootElement.GetProperty("daysKeep").GetInt32());

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/epg-sources/{id}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "epg_source_deleted");

            using var missing = await Client.GetAsync(
                $"/admin/epg-sources/{id}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "epg_source_not_found");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/epg-sources/{id}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "epg_source_not_found");
        }

        [Fact]
        public async Task NamesAreUniqueAndEachFieldIsRefusedUnderItsOwnName()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostSourceAsync(new
            {
                name = "Taken",
                url = "https://example.test/taken.xml"
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "epg_source_created");
            var id = IdFromLocation(created);

            using var duplicate = await PostSourceAsync(new
            {
                name = "Taken",
                url = "https://example.test/other.xml"
            });
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "epg_source_already_exists");
            using var duplicateBody = await ReadJsonAsync(duplicate);
            Assert.True(
                duplicateBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var blankName = await PostSourceAsync(new
            {
                name = "   ",
                url = "https://example.test/blank.xml"
            });
            await AssertCodeAsync(blankName, HttpStatusCode.BadRequest, "validation_failed");
            using var blankBody = await ReadJsonAsync(blankName);
            Assert.True(blankBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var badUrl = await PostSourceAsync(new
            {
                name = "Bad url",
                url = "not-an-address"
            });
            await AssertCodeAsync(badUrl, HttpStatusCode.BadRequest, "validation_failed");
            using var badUrlBody = await ReadJsonAsync(badUrl);
            Assert.True(badUrlBody.RootElement.GetProperty("errors").TryGetProperty("url", out _));

            using var notANumber = await PostSourceAsync(new
            {
                name = "Not a number",
                url = "https://example.test/number.xml",
                daysKeep = "seven"
            });
            await AssertCodeAsync(notANumber, HttpStatusCode.BadRequest, "validation_failed");
            using var notANumberBody = await ReadJsonAsync(notANumber);
            Assert.True(
                notANumberBody.RootElement.GetProperty("errors").TryGetProperty("daysKeep", out _));

            using var outOfRange = await PostSourceAsync(new
            {
                name = "Out of range",
                url = "https://example.test/range.xml",
                daysKeep = "366"
            });
            await AssertCodeAsync(outOfRange, HttpStatusCode.BadRequest, "validation_failed");
            using var outOfRangeBody = await ReadJsonAsync(outOfRange);
            Assert.True(
                outOfRangeBody.RootElement.GetProperty("errors").TryGetProperty("daysKeep", out _));

            // A change may not take a name another row already holds.
            using var second = await PostSourceAsync(new
            {
                name = "Second",
                url = "https://example.test/second.xml"
            });
            await AssertCodeAsync(second, HttpStatusCode.Created, "epg_source_created");
            var secondId = IdFromLocation(second);

            var csrf = await GetCsrfTokenAsync();
            using var collision = await PatchJsonAsync(
                $"/admin/epg-sources/{secondId}",
                new { name = "Taken" },
                csrf);
            await AssertCodeAsync(collision, HttpStatusCode.Conflict, "epg_source_already_exists");
        }

        [Fact]
        public async Task TheListSearchesSortsAndPagesOnTheServer()
        {
            await CreateAdminAndSignInAsync();

            await CreateSourceAsync("Alpha", "https://example.test/a.xml", "10");
            await CreateSourceAsync("Beta", "https://example.test/b.xml", "3");
            await CreateSourceAsync("Gamma", "https://example.test/g.xml");

            using var page = await Client.GetAsync(
                "/admin/epg-sources?page=1&pageSize=2&sortBy=name&sortOrder=asc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(3, pageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, pageBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(
                "Alpha",
                pageBody.RootElement.GetProperty("items")[0].GetProperty("name").GetString());

            using var second = await Client.GetAsync(
                "/admin/epg-sources?page=2&pageSize=2&sortBy=name&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var secondBody = await ReadJsonAsync(second);
            Assert.Equal(1, secondBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(
                "Gamma",
                secondBody.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
            Assert.Equal(3, secondBody.RootElement.GetProperty("total").GetInt32());

            using var searched = await Client.GetAsync(
                "/admin/epg-sources?search=BETA",
                TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());

            using var searchedByUrl = await Client.GetAsync(
                "/admin/epg-sources?search=g.xml",
                TestContext.Current.CancellationToken);
            using var searchedByUrlBody = await ReadJsonAsync(searchedByUrl);
            Assert.Equal(1, searchedByUrlBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                "Gamma",
                searchedByUrlBody.RootElement.GetProperty("items")[0].GetProperty("name").GetString());

            using var byRetention = await Client.GetAsync(
                "/admin/epg-sources?sortBy=daysKeep&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var byRetentionBody = await ReadJsonAsync(byRetention);
            Assert.Equal(
                new[] { "Beta", "Gamma", "Alpha" },
                byRetentionBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var byUrl = await Client.GetAsync(
                "/admin/epg-sources?sortBy=url&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var byUrlBody = await ReadJsonAsync(byUrl);
            Assert.Equal(
                new[] { "Gamma", "Beta", "Alpha" },
                byUrlBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            // Nothing has been refreshed, so every row groups as a null and the order is the
            // tie-break. The column is offered and the answer is still a page.
            using var byRefresh = await Client.GetAsync(
                "/admin/epg-sources?sortBy=lastEpgUpdatedAt&sortOrder=asc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, byRefresh.StatusCode);
            using var byRefreshBody = await ReadJsonAsync(byRefresh);
            Assert.Equal(3, byRefreshBody.RootElement.GetProperty("total").GetInt32());

            using var unknownSort = await Client.GetAsync(
                "/admin/epg-sources?sortBy=lastrefresh",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownSort, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownSortBody = await ReadJsonAsync(unknownSort);
            Assert.True(unknownSortBody.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));

            using var unknownOrder = await Client.GetAsync(
                "/admin/epg-sources?sortOrder=upwards",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownOrder, HttpStatusCode.BadRequest, "validation_failed");

            using var badPage = await Client.GetAsync(
                "/admin/epg-sources?page=0",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badPage, HttpStatusCode.BadRequest, "validation_failed");

            using var badSize = await Client.GetAsync(
                "/admin/epg-sources?pageSize=1001",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badSize, HttpStatusCode.BadRequest, "validation_failed");

            using var longSearch = await Client.GetAsync(
                $"/admin/epg-sources?search={new string('a', 201)}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(longSearch, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ASourceAStreamUsesCannotBeDeleted()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Live");

            using var created = await PostSourceAsync(new
            {
                name = "In use",
                url = "https://example.test/in-use.xml"
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "epg_source_created");
            var sourceId = IdFromLocation(created);

            var streamId = await CreateStreamAsync(categoryId, "News", sourceId, "news-1");

            var csrf = await GetCsrfTokenAsync();
            using var refused = await DeleteAsync($"/admin/epg-sources/{sourceId}", csrf);
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "epg_source_in_use");
            using var refusedBody = await ReadJsonAsync(refused);
            Assert.Contains("1 stream", MessageOf(refusedBody));

            // Once the stream stops naming the source, the source is free again.
            csrf = await GetCsrfTokenAsync();
            using var cleared = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new { epgSourceId = "", epgChannelKey = "" },
                csrf);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "video_stream_updated");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/epg-sources/{sourceId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "epg_source_deleted");
        }

        [Fact]
        public async Task AStreamCanOnlyNameASourceThatExists()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Live");

            using var unknown = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Dangling",
                    type = "permanent",
                    categoryId,
                    sources = new[] { "https://example.test/dangling" },
                    epgSourceId = Guid.NewGuid().ToString(),
                    epgChannelKey = "news-1"
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(unknown, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownBody = await ReadJsonAsync(unknown);
            Assert.True(
                unknownBody.RootElement.GetProperty("errors").TryGetProperty("epgSourceId", out _));

            // The same check guards a change, not only a create.
            var streamId = await CreateStreamAsync(categoryId, "Plain");
            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new
                {
                    epgSourceId = Guid.NewGuid().ToString(),
                    epgChannelKey = "news-1"
                },
                csrf);

            await AssertCodeAsync(patched, HttpStatusCode.BadRequest, "validation_failed");
            using var patchedBody = await ReadJsonAsync(patched);
            Assert.True(
                patchedBody.RootElement.GetProperty("errors").TryGetProperty("epgSourceId", out _));
        }

        [Fact]
        public async Task TheChannelsReadListsTheStreamsThatUseASource()
        {
            await CreateAdminAndSignInAsync();
            var categoryId = await CreateCategoryAsync("Live");

            using var created = await PostSourceAsync(new
            {
                name = "Channels",
                url = "https://example.test/channels.xml"
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "epg_source_created");
            var sourceId = IdFromLocation(created);

            var news = await CreateStreamAsync(categoryId, "News", sourceId, "news-1");
            _ = news;
            await CreateStreamAsync(categoryId, "Sport", sourceId, "sport-1");
            await CreateStreamAsync(categoryId, "Unrelated");

            using var page = await Client.GetAsync(
                $"/admin/epg-sources/{sourceId}/channels",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(2, pageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                new[] { "News", "Sport" },
                pageBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("streamName").GetString())
                    .Order()
                    .ToArray());
            Assert.Contains(
                "news-1",
                pageBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("epgChannelKey").GetString()));

            using var searched = await Client.GetAsync(
                $"/admin/epg-sources/{sourceId}/channels?search=sport",
                TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());

            using var byKey = await Client.GetAsync(
                $"/admin/epg-sources/{sourceId}/channels?sortBy=channelKey&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var byKeyBody = await ReadJsonAsync(byKey);
            Assert.Equal(
                new[] { "sport-1", "news-1" },
                byKeyBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("epgChannelKey").GetString())
                    .ToArray());

            using var unknownSort = await Client.GetAsync(
                $"/admin/epg-sources/{sourceId}/channels?sortBy=name",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownSort, HttpStatusCode.BadRequest, "validation_failed");

            using var missing = await Client.GetAsync(
                $"/admin/epg-sources/{Guid.NewGuid()}/channels",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "epg_source_not_found");
        }

        [Fact]
        public async Task WritesRequireCsrfAndThePermission()
        {
            await CreateAdminAndSignInAsync();

            using var noCsrf = await PostJsonAsync(
                "/admin/epg-sources",
                new
                {
                    name = "No token",
                    url = "https://example.test/no-token.xml"
                });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            // An administrator whose group may run the streams but not read the guides: the
            // role requirement passes, so the refusal proves the permission half.
            var streamOperators = await CreateGroupAsync(
                "Stream operators",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewStreams,
                Fluxy.Core.Users.Enums.UserPermission.EditStreams);

            using var operatorClient = Fixture.CreateClient();
            await CreateUserAsync(
                UserRole.Admin,
                "streamoperator",
                groupId: streamOperators);
            await SignInAsync(UserRole.Admin, "streamoperator", "Password1", operatorClient);

            using var forbiddenRead = await operatorClient.GetAsync(
                "/admin/epg-sources",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(forbiddenRead, HttpStatusCode.Forbidden, "permission_denied");

            var csrf = await GetCsrfTokenAsync(operatorClient);
            using var forbiddenWrite = await PostJsonAsync(
                "/admin/epg-sources",
                new
                {
                    name = "Not allowed",
                    url = "https://example.test/not-allowed.xml"
                },
                csrf,
                operatorClient);
            await AssertCodeAsync(forbiddenWrite, HttpStatusCode.Forbidden, "permission_denied");

            // A view-only grant reads and is refused the writes.
            var readers = await CreateGroupAsync(
                "Guide readers",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewEpgSources);

            using var readerClient = Fixture.CreateClient();
            await CreateUserAsync(
                UserRole.Admin,
                "guidereader",
                groupId: readers);
            await SignInAsync(UserRole.Admin, "guidereader", "Password1", readerClient);

            using var allowedRead = await readerClient.GetAsync(
                "/admin/epg-sources",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

            csrf = await GetCsrfTokenAsync(readerClient);
            using var refusedWrite = await PostJsonAsync(
                "/admin/epg-sources",
                new
                {
                    name = "Reader write",
                    url = "https://example.test/reader-write.xml"
                },
                csrf,
                readerClient);
            await AssertCodeAsync(refusedWrite, HttpStatusCode.Forbidden, "permission_denied");
        }

        private async Task<HttpResponseMessage> PostSourceAsync(object body)
            => await PostJsonAsync("/admin/epg-sources", body, await GetCsrfTokenAsync());

        private async Task<string> CreateSourceAsync(
            string name,
            string url,
            string? daysKeep = null)
        {
            var body = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["url"] = url
            };

            if (daysKeep is not null)
            {
                body["daysKeep"] = daysKeep;
            }

            using var response = await PostSourceAsync(body);
            await AssertCodeAsync(response, HttpStatusCode.Created, "epg_source_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateCategoryAsync(string name)
        {
            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type = "VideoStream" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        private async Task<string> CreateStreamAsync(
            string categoryId,
            string name,
            string? epgSourceId = null,
            string? epgChannelKey = null)
        {
            var body = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["type"] = "permanent",
                ["categoryId"] = categoryId,
                ["sources"] = new[] { $"https://example.test/{name.ToLowerInvariant()}" }
            };

            if (epgSourceId is not null)
            {
                body["epgSourceId"] = epgSourceId;
            }

            if (epgChannelKey is not null)
            {
                body["epgChannelKey"] = epgChannelKey;
            }

            using var response = await PostJsonAsync(
                "/admin/video-streams",
                body,
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.Created, "video_stream_created");

            return IdFromLocation(response);
        }
    }
}
