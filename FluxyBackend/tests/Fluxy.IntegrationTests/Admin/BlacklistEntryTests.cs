using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The blacklist API: one list at a time, a value unique inside it, the counter and the
    /// added date never written, and the two permission gates.
    /// </summary>
    /// <remarks>
    /// The rows are planted through the API rather than through the repository, because the
    /// controller is what this suite is about: a contract that refused the wrong field or
    /// answered a duplicate with the wrong status would be invisible to a repository test.
    /// </remarks>
    public sealed class BlacklistEntryTests : IntegrationTestBase
    {
        public BlacklistEntryTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AnEntryIsCreatedChangedAndDeleted()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostEntryAsync(new
            {
                type = "ip",
                value = "203.0.113.5",
                note = "scanner"
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "blacklist_entry_created");
            Assert.NotNull(created.Headers.Location);
            var id = IdFromLocation(created);
            Assert.Equal($"/admin/blacklist-entries/{id}", created.Headers.Location?.ToString());

            using var detail = await Client.GetAsync(
                $"/admin/blacklist-entries/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal("ip", root.GetProperty("type").GetString());
            Assert.Equal("203.0.113.5", root.GetProperty("value").GetString());
            Assert.Equal("scanner", root.GetProperty("note").GetString());

            // A new entry has blocked nothing, and the added date is stamped on the row.
            Assert.Equal(0, root.GetProperty("blockedAttempts").GetInt32());
            var addedAt = root.GetProperty("createdAt").GetDateTimeOffset();

            // The change writes the value and the note - and nothing else, even when the body
            // names the counter or the added date.
            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/blacklist-entries/{id}",
                new
                {
                    value = "203.0.113.6",
                    note = "confirmed scanner",
                    blockedAttempts = 900,
                    createdAt = "1999-01-01T00:00:00Z"
                },
                csrf);
            await AssertCodeAsync(patched, HttpStatusCode.OK, "blacklist_entry_updated");

            using var afterPatch = await Client.GetAsync(
                $"/admin/blacklist-entries/{id}",
                TestContext.Current.CancellationToken);
            using var afterPatchBody = await ReadJsonAsync(afterPatch);
            var patchedRoot = afterPatchBody.RootElement;

            Assert.Equal("203.0.113.6", patchedRoot.GetProperty("value").GetString());
            Assert.Equal("confirmed scanner", patchedRoot.GetProperty("note").GetString());
            Assert.Equal(0, patchedRoot.GetProperty("blockedAttempts").GetInt32());
            Assert.Equal(addedAt, patchedRoot.GetProperty("createdAt").GetDateTimeOffset());

            // An empty note clears; an absent one keeps what the row holds.
            csrf = await GetCsrfTokenAsync();
            using var cleared = await PatchJsonAsync(
                $"/admin/blacklist-entries/{id}",
                new { note = "" },
                csrf);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "blacklist_entry_updated");

            using var afterClear = await Client.GetAsync(
                $"/admin/blacklist-entries/{id}",
                TestContext.Current.CancellationToken);
            using var afterClearBody = await ReadJsonAsync(afterClear);
            Assert.Equal(
                System.Text.Json.JsonValueKind.Null,
                afterClearBody.RootElement.GetProperty("note").ValueKind);
            Assert.Equal(
                "203.0.113.6",
                afterClearBody.RootElement.GetProperty("value").GetString());

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/blacklist-entries/{id}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "blacklist_entry_deleted");

            using var missing = await Client.GetAsync(
                $"/admin/blacklist-entries/{id}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "blacklist_entry_not_found");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/blacklist-entries/{id}", csrf);
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "blacklist_entry_not_found");
        }

        [Fact]
        public async Task AValueIsUniqueInsideItsOwnList()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostEntryAsync(new
            {
                type = "ip",
                value = "203.0.113.7"
            });
            await AssertCodeAsync(created, HttpStatusCode.Created, "blacklist_entry_created");

            using var duplicate = await PostEntryAsync(new
            {
                type = "ip",
                value = "203.0.113.7"
            });
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "blacklist_value_exists");
            using var duplicateBody = await ReadJsonAsync(duplicate);
            Assert.True(
                duplicateBody.RootElement.GetProperty("errors").TryGetProperty("value", out _));

            // The same text under another list is a different rule, not a conflict.
            using var otherList = await PostEntryAsync(new
            {
                type = "isp",
                value = "203.0.113.7"
            });
            await AssertCodeAsync(otherList, HttpStatusCode.Created, "blacklist_entry_created");

            // The user-agent list accepts the hyphenated spelling as well as the collapsed one.
            using var agent = await PostEntryAsync(new
            {
                type = "user-agent",
                value = "curl/8.0"
            });
            await AssertCodeAsync(agent, HttpStatusCode.Created, "blacklist_entry_created");

            using var agentDetail = await Client.GetAsync(
                $"/admin/blacklist-entries/{IdFromLocation(agent)}",
                TestContext.Current.CancellationToken);
            using var agentBody = await ReadJsonAsync(agentDetail);
            Assert.Equal("user-agent", agentBody.RootElement.GetProperty("type").GetString());

            // A change may not take a value another entry of the list already holds.
            using var second = await PostEntryAsync(new
            {
                type = "ip",
                value = "203.0.113.8"
            });
            await AssertCodeAsync(second, HttpStatusCode.Created, "blacklist_entry_created");

            var csrf = await GetCsrfTokenAsync();
            using var collision = await PatchJsonAsync(
                $"/admin/blacklist-entries/{IdFromLocation(second)}",
                new { value = "203.0.113.7" },
                csrf);
            await AssertCodeAsync(collision, HttpStatusCode.Conflict, "blacklist_value_exists");
            using var collisionBody = await ReadJsonAsync(collision);
            Assert.True(
                collisionBody.RootElement.GetProperty("errors").TryGetProperty("value", out _));
        }

        [Fact]
        public async Task EachFieldIsRefusedUnderItsOwnName()
        {
            await CreateAdminAndSignInAsync();

            using var blank = await PostEntryAsync(new { type = "isp", value = "   " });
            await AssertCodeAsync(blank, HttpStatusCode.BadRequest, "validation_failed");
            using var blankBody = await ReadJsonAsync(blank);
            Assert.True(blankBody.RootElement.GetProperty("errors").TryGetProperty("value", out _));

            using var invalidIp = await PostEntryAsync(new
            {
                type = "ip",
                value = "300.1.2.3"
            });
            await AssertCodeAsync(invalidIp, HttpStatusCode.BadRequest, "validation_failed");
            using var invalidIpBody = await ReadJsonAsync(invalidIp);
            Assert.True(
                invalidIpBody.RootElement.GetProperty("errors").TryGetProperty("value", out _));

            // The address rule is scoped to the IP list: the same text is a valid ISP value.
            using var ispText = await PostEntryAsync(new
            {
                type = "isp",
                value = "300.1.2.3"
            });
            await AssertCodeAsync(ispText, HttpStatusCode.Created, "blacklist_entry_created");

            using var longValue = await PostEntryAsync(new
            {
                type = "user-agent",
                value = new string('a', 513)
            });
            await AssertCodeAsync(longValue, HttpStatusCode.BadRequest, "validation_failed");
            using var longValueBody = await ReadJsonAsync(longValue);
            Assert.True(
                longValueBody.RootElement.GetProperty("errors").TryGetProperty("value", out _));

            using var longNote = await PostEntryAsync(new
            {
                type = "ip",
                value = "198.51.100.1",
                note = new string('a', 1001)
            });
            await AssertCodeAsync(longNote, HttpStatusCode.BadRequest, "validation_failed");
            using var longNoteBody = await ReadJsonAsync(longNote);
            Assert.True(
                longNoteBody.RootElement.GetProperty("errors").TryGetProperty("note", out _));

            using var unknownType = await PostEntryAsync(new
            {
                type = "country",
                value = "RU"
            });
            await AssertCodeAsync(unknownType, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownTypeBody = await ReadJsonAsync(unknownType);
            Assert.True(
                unknownTypeBody.RootElement.GetProperty("errors").TryGetProperty("type", out _));
        }

        [Fact]
        public async Task TheListIsOrderedNewestFirstByDefault()
        {
            await CreateAdminAndSignInAsync();

            await CreateEntryAsync("ip", "198.51.100.10");

            // The audit clock is frozen in the test host, so two creates a moment apart would
            // share a `created_at` and the order would fall to the identifier tie-break. One
            // second keeps this about the order without moving the clock the access tokens are
            // validated against past their skew.
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await CreateEntryAsync("ip", "198.51.100.11");
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await CreateEntryAsync("ip", "198.51.100.12");

            // Nothing but the type in the query: the default order is the added date, newest
            // first, and the list holds only this list's entries.
            using var page = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var body = await ReadJsonAsync(page);

            Assert.Equal(3, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                new[] { "198.51.100.12", "198.51.100.11", "198.51.100.10" },
                body.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("value").GetString())
                    .ToArray());
        }

        [Fact]
        public async Task TheListSearchesSortsAndPagesOnTheServer()
        {
            await CreateAdminAndSignInAsync();

            await CreateEntryAsync("ip", "10.0.0.1", "beta note");

            // One second between the creates so the added-date order has something to order by:
            // the host's clock is frozen, and without this the three rows share a `created_at`
            // and fall to the identifier tie-break.
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await CreateEntryAsync("ip", "10.0.0.2");
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await CreateEntryAsync("ip", "10.0.0.3", "alpha note");
            await CreateEntryAsync("isp", "Unrelated provider");

            using var searchedByValue = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&search=10.0.0.2",
                TestContext.Current.CancellationToken);
            using var searchedByValueBody = await ReadJsonAsync(searchedByValue);
            Assert.Equal(1, searchedByValueBody.RootElement.GetProperty("total").GetInt32());

            // Search covers the note, which is the one place an operator explains an entry.
            using var searchedByNote = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&search=ALPHA",
                TestContext.Current.CancellationToken);
            using var searchedByNoteBody = await ReadJsonAsync(searchedByNote);
            Assert.Equal(1, searchedByNoteBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                "10.0.0.3",
                searchedByNoteBody.RootElement.GetProperty("items")[0]
                    .GetProperty("value").GetString());

            using var byValue = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&sortBy=value&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var byValueBody = await ReadJsonAsync(byValue);
            Assert.Equal(
                new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3" },
                byValueBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("value").GetString())
                    .ToArray());

            // The counter is 0 on every entry, so the order is the tie-break and the answer is
            // still a page - the column is offered and does not throw.
            using var byCounter = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&sortBy=blockedAttempts&sortOrder=desc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, byCounter.StatusCode);
            using var byCounterBody = await ReadJsonAsync(byCounter);
            Assert.Equal(3, byCounterBody.RootElement.GetProperty("total").GetInt32());

            using var byCreatedAsc = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&sortBy=createdAt&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var byCreatedAscBody = await ReadJsonAsync(byCreatedAsc);
            Assert.Equal(
                new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3" },
                byCreatedAscBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("value").GetString())
                    .ToArray());

            using var paged = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&page=1&pageSize=2&sortBy=value&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var pagedBody = await ReadJsonAsync(paged);
            Assert.Equal(2, pagedBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(3, pagedBody.RootElement.GetProperty("total").GetInt32());

            using var unknownSort = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&sortBy=note",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownSort, HttpStatusCode.BadRequest, "validation_failed");

            using var unknownOrder = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&sortOrder=upwards",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownOrder, HttpStatusCode.BadRequest, "validation_failed");

            using var unknownType = await Client.GetAsync(
                "/admin/blacklist-entries?type=country",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownType, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownTypeBody = await ReadJsonAsync(unknownType);
            Assert.True(
                unknownTypeBody.RootElement.GetProperty("errors").TryGetProperty("type", out _));

            // A request that names no list at all is refused rather than answered with all
            // three lists folded together.
            using var missingType = await Client.GetAsync(
                "/admin/blacklist-entries",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(missingType, HttpStatusCode.BadRequest, "validation_failed");

            using var badPage = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&page=0",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badPage, HttpStatusCode.BadRequest, "validation_failed");

            using var badSize = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip&pageSize=1001",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badSize, HttpStatusCode.BadRequest, "validation_failed");

            using var longSearch = await Client.GetAsync(
                $"/admin/blacklist-entries?type=ip&search={new string('a', 201)}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(longSearch, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task AMissingEntryAnswersNotFoundUnderEveryWrite()
        {
            await CreateAdminAndSignInAsync();
            var unknown = Guid.NewGuid();

            using var read = await Client.GetAsync(
                $"/admin/blacklist-entries/{unknown}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(read, HttpStatusCode.NotFound, "blacklist_entry_not_found");

            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/blacklist-entries/{unknown}",
                new { value = "203.0.113.5" },
                csrf);
            await AssertCodeAsync(patched, HttpStatusCode.NotFound, "blacklist_entry_not_found");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/blacklist-entries/{unknown}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.NotFound, "blacklist_entry_not_found");
        }

        [Fact]
        public async Task WritesRequireCsrfAndThePermission()
        {
            await CreateAdminAndSignInAsync();

            using var noCsrf = await PostJsonAsync(
                "/admin/blacklist-entries",
                new { type = "ip", value = "203.0.113.9" });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            // An administrator whose group may run the streams but not read the lists: the role
            // requirement passes, so the refusal proves the permission half.
            var streamOperators = await CreateGroupAsync(
                "Stream operators",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewStreams,
                Fluxy.Core.Users.Enums.UserPermission.EditStreams);

            using var operatorClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "streamoperator", groupId: streamOperators);
            await SignInAsync(UserRole.Admin, "streamoperator", "Password1", operatorClient);

            using var forbiddenRead = await operatorClient.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(forbiddenRead, HttpStatusCode.Forbidden, "permission_denied");

            var csrf = await GetCsrfTokenAsync(operatorClient);
            using var forbiddenWrite = await PostJsonAsync(
                "/admin/blacklist-entries",
                new { type = "ip", value = "203.0.113.9" },
                csrf,
                operatorClient);
            await AssertCodeAsync(forbiddenWrite, HttpStatusCode.Forbidden, "permission_denied");

            // A view-only grant reads and is refused the writes.
            var readers = await CreateGroupAsync(
                "Blacklist readers",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewBlacklists);

            using var readerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "blacklistreader", groupId: readers);
            await SignInAsync(UserRole.Admin, "blacklistreader", "Password1", readerClient);

            using var allowedRead = await readerClient.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

            csrf = await GetCsrfTokenAsync(readerClient);
            using var refusedWrite = await PostJsonAsync(
                "/admin/blacklist-entries",
                new { type = "ip", value = "203.0.113.9" },
                csrf,
                readerClient);
            await AssertCodeAsync(refusedWrite, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task ClearingOneListLeavesTheOtherTwoUntouched()
        {
            await CreateAdminAndSignInAsync();

            await CreateEntryAsync("ip", "198.51.100.1");
            await CreateEntryAsync("ip", "198.51.100.2");
            await CreateEntryAsync("isp", "Provider one");
            await CreateEntryAsync("user-agent", "curl/8.0");

            var csrf = await GetCsrfTokenAsync();
            using var cleared = await DeleteAsync("/admin/blacklist-entries?type=ip", csrf);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "blacklist_entries_cleared");

            using var ip = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            using var ipBody = await ReadJsonAsync(ip);
            Assert.Equal(0, ipBody.RootElement.GetProperty("total").GetInt32());

            // The other two lists still hold what they held before the clear.
            using var isp = await Client.GetAsync(
                "/admin/blacklist-entries?type=isp",
                TestContext.Current.CancellationToken);
            using var ispBody = await ReadJsonAsync(isp);
            Assert.Equal(1, ispBody.RootElement.GetProperty("total").GetInt32());

            using var agent = await Client.GetAsync(
                "/admin/blacklist-entries?type=user-agent",
                TestContext.Current.CancellationToken);
            using var agentBody = await ReadJsonAsync(agent);
            Assert.Equal(1, agentBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task ClearingIgnoresTheSearchFilter()
        {
            await CreateAdminAndSignInAsync();

            await CreateEntryAsync("isp", "Alpha provider");
            await CreateEntryAsync("isp", "Beta provider");

            // The operator is looking at the search-narrowed view; the command is about the
            // list, so every row of the type goes - not only the one on screen.
            var csrf = await GetCsrfTokenAsync();
            using var cleared = await DeleteAsync(
                "/admin/blacklist-entries?type=isp&search=alpha",
                csrf);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "blacklist_entries_cleared");

            using var all = await Client.GetAsync(
                "/admin/blacklist-entries?type=isp",
                TestContext.Current.CancellationToken);
            using var allBody = await ReadJsonAsync(all);
            Assert.Equal(0, allBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task ClearingAnEmptyListIsStillASuccess()
        {
            await CreateAdminAndSignInAsync();

            // The command is already satisfied; a refusal would report a failure for having
            // worked, and the button would look broken on a list it had just emptied.
            var csrf = await GetCsrfTokenAsync();
            using var cleared = await DeleteAsync("/admin/blacklist-entries?type=isp", csrf);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "blacklist_entries_cleared");
        }

        [Fact]
        public async Task ClearingWithoutAListIsRefused()
        {
            await CreateAdminAndSignInAsync();
            await CreateEntryAsync("ip", "198.51.100.3");

            // A missing type must not be read as "all three": nothing has been asked, and a
            // clear that emptied the installation without naming a list is not a command.
            var csrf = await GetCsrfTokenAsync();
            using var refused = await DeleteAsync("/admin/blacklist-entries", csrf);
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            using var refusedBody = await ReadJsonAsync(refused);
            Assert.True(
                refusedBody.RootElement.GetProperty("errors").TryGetProperty("type", out _));

            using var unknown = await DeleteAsync("/admin/blacklist-entries?type=country", csrf);
            await AssertCodeAsync(unknown, HttpStatusCode.BadRequest, "validation_failed");

            // Nothing was removed by either refusal.
            using var ip = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            using var ipBody = await ReadJsonAsync(ip);
            Assert.Equal(1, ipBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task ClearRequiresCsrfAndTheEditPermission()
        {
            await CreateAdminAndSignInAsync();
            await CreateEntryAsync("ip", "198.51.100.4");

            using var noCsrf = await DeleteAsync("/admin/blacklist-entries?type=ip");
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            // The list survived the refused request.
            using var stillThere = await Client.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            using var stillThereBody = await ReadJsonAsync(stillThere);
            Assert.Equal(1, stillThereBody.RootElement.GetProperty("total").GetInt32());

            // A view-only grant reads the list and is refused the clear.
            var readers = await CreateGroupAsync(
                "Blacklist readers",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewBlacklists);

            using var readerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "clearreader", groupId: readers);
            await SignInAsync(UserRole.Admin, "clearreader", "Password1", readerClient);

            using var allowedRead = await readerClient.GetAsync(
                "/admin/blacklist-entries?type=ip",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

            var csrf = await GetCsrfTokenAsync(readerClient);
            using var refused = await DeleteAsync(
                "/admin/blacklist-entries?type=ip",
                csrf,
                readerClient);
            await AssertCodeAsync(refused, HttpStatusCode.Forbidden, "permission_denied");
        }

        private async Task<HttpResponseMessage> PostEntryAsync(object body)
            => await PostJsonAsync("/admin/blacklist-entries", body, await GetCsrfTokenAsync());

        private async Task<string> CreateEntryAsync(string type, string value, string? note = null)
        {
            var body = new Dictionary<string, object?>
            {
                ["type"] = type,
                ["value"] = value
            };

            if (note is not null)
            {
                body["note"] = note;
            }

            using var response = await PostEntryAsync(body);
            await AssertCodeAsync(response, HttpStatusCode.Created, "blacklist_entry_created");

            return IdFromLocation(response);
        }
    }
}
