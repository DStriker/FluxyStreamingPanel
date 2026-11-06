using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The reseller credit log and the write path that feeds it: the balance on the account, the
    /// one ledger row per operation, the refusals, and the two endpoints that read and clear it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules these tests exist to pin are the ones a form cannot show from its own state: that
    /// an account created with no credit still carries a row saying so, that an unchanged balance
    /// writes nothing, that a balance and its row commit together or not at all, and that
    /// <c>total</c> is counted under the filter the page was cut from.
    /// </para>
    /// <para>
    /// <b>Rows are planted directly only where the production path cannot reach.</b> The write path
    /// stamps <c>created_at</c> with the request clock, so a test that has to place rows at chosen
    /// instants - or force a save to fail between staging and commit - uses the documented SQL
    /// fixtures in <see cref="TestDataSeeder"/>; everything the API can produce is produced through
    /// the API.
    /// </para>
    /// </remarks>
    public sealed class CreditLogTests : IntegrationTestBase
    {
        public CreditLogTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task CreatingAResellerWritesExactlyOneRowEvenWithNoCredit()
        {
            await CreateAdminAndSignInAsync();

            var resellerId = await CreateResellerThroughApiAsync("reseller-one");

            await using var connection = await Fixture.OpenConnectionAsync();
            Assert.Equal(1, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            using var list = await GetAsync("/admin/credit-logs");
            using var body = await ReadJsonAsync(list);
            Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());

            var row = body.RootElement.GetProperty("items")[0];
            Assert.Equal(resellerId.ToString(), row.GetProperty("resellerId").GetString());
            Assert.Equal("reseller-one", row.GetProperty("resellerUsername").GetString());
            Assert.Equal(0, row.GetProperty("amount").GetInt32());
            Assert.Equal(
                "Reseller created with initial credits: 0",
                row.GetProperty("description").GetString());
            Assert.NotEqual(JsonValueKind.Null, row.GetProperty("issuedById").ValueKind);
            Assert.Equal("operator", row.GetProperty("issuedByUsername").GetString());

            // The balance is on the account and survives reload.
            using var detail = await Client.GetAsync(
                $"/admin/users/{resellerId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(0, detailBody.RootElement.GetProperty("credits").GetInt32());
        }

        [Fact]
        public async Task CreatingAResellerWithCreditStoresTheBalanceAndTheRowTogether()
        {
            await CreateAdminAndSignInAsync();

            var resellerId = await CreateResellerThroughApiAsync("reseller-funded", credits: 25);

            using var list = await GetAsync($"/admin/credit-logs?resellerId={resellerId}");
            using var body = await ReadJsonAsync(list);
            var row = body.RootElement.GetProperty("items")[0];
            Assert.Equal(25, row.GetProperty("amount").GetInt32());
            Assert.Equal(
                "Reseller created with initial credits: 25",
                row.GetProperty("description").GetString());

            using var detail = await Client.GetAsync(
                $"/admin/users/{resellerId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(25, detailBody.RootElement.GetProperty("credits").GetInt32());

            // The list row carries the same balance.
            using var users = await GetAsync("/admin/users?search=reseller-funded");
            using var usersBody = await ReadJsonAsync(users);
            Assert.Equal(
                25,
                usersBody.RootElement.GetProperty("items")[0].GetProperty("credits").GetInt32());
        }

        [Fact]
        public async Task ACustomGroupWithTheResellerRoleBehavesAsAReseller()
        {
            await CreateAdminAndSignInAsync();

            // The role, not the base group identifier, decides what an account is.
            var groupId = await CreateGroupAsync("Partners", UserRole.Reseller);

            var csrf = await GetCsrfTokenAsync();
            using var created = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "partner",
                    email = "partner@example.test",
                    password = "Password1",
                    groupId = groupId.ToString(),
                    status = nameof(UserStatus.Registered),
                    credits = 7
                },
                csrf);
            await AssertCodeAsync(created, HttpStatusCode.Created, "user_created");

            var resellerId = Guid.Parse(IdFromLocation(created));

            using var list = await GetAsync($"/admin/credit-logs?resellerId={resellerId}");
            using var body = await ReadJsonAsync(list);
            Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(7, body.RootElement.GetProperty("items")[0].GetProperty("amount").GetInt32());
        }

        [Fact]
        public async Task AnUnchangedBalanceWritesNoRowAndAChangeWritesExactlyOne()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller", credits: 10);
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();

            // A save that names the same value is not a change: no second row.
            var csrf = await GetCsrfTokenAsync();
            using var unchanged = await PatchJsonAsync(
                $"/admin/users/{resellerId}",
                new { credits = 10 },
                csrf);
            await AssertCodeAsync(unchanged, HttpStatusCode.OK, "user_updated");
            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // A change is one balance write and exactly one row carrying the delta.
            csrf = await GetCsrfTokenAsync();
            using var increased = await PatchJsonAsync(
                $"/admin/users/{resellerId}",
                new { credits = 15 },
                csrf);
            await AssertCodeAsync(increased, HttpStatusCode.OK, "user_updated");

            Assert.Equal(1, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            using var list = await GetAsync($"/admin/credit-logs?resellerId={resellerId}");
            using var body = await ReadJsonAsync(list);
            var row = body.RootElement.GetProperty("items")[0];
            Assert.Equal(5, row.GetProperty("amount").GetInt32());
            Assert.Equal(
                "Credits changed: 10 \u2192 15",
                row.GetProperty("description").GetString());
            Assert.NotEqual(JsonValueKind.Null, row.GetProperty("issuedById").ValueKind);

            using var detail = await Client.GetAsync(
                $"/admin/users/{resellerId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(15, detailBody.RootElement.GetProperty("credits").GetInt32());

            // One operation, one row: the second save above did not add a second entry.
            using var all = await GetAsync("/admin/credit-logs");
            using var allBody = await ReadJsonAsync(all);
            Assert.Equal(1, allBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task SpendingCreditRecordsTheActingAdminAsTheIssuer()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller", credits: 20);
            await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            using var spent = await PatchJsonAsync(
                $"/admin/users/{resellerId}",
                new { credits = 8 },
                csrf);
            await AssertCodeAsync(spent, HttpStatusCode.OK, "user_updated");

            using var list = await GetAsync($"/admin/credit-logs?resellerId={resellerId}");
            using var body = await ReadJsonAsync(list);
            var row = body.RootElement.GetProperty("items")[0];

            Assert.Equal(-12, row.GetProperty("amount").GetInt32());

            // A decrease is an admin-panel change like any other: the row names the administrator
            // who made it, not the reseller whose balance moved. Only a reseller-initiated spend
            // (a write path that does not exist yet) would carry no issuer.
            Assert.NotEqual(JsonValueKind.Null, row.GetProperty("issuedById").ValueKind);
            Assert.Equal("operator", row.GetProperty("issuedByUsername").GetString());
            Assert.Equal(
                "Credits changed: 20 \u2192 8",
                row.GetProperty("description").GetString());
        }

        [Fact]
        public async Task NamingCreditsForANonResellerIsRefusedOnCreateAndOnUpdate()
        {
            var clientId = await CreateUserAsync(UserRole.Client, "client");
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();

            var csrf = await GetCsrfTokenAsync();
            using var created = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "would-be-reseller",
                    email = "would-be-reseller@example.test",
                    password = "Password1",
                    groupId = BaseUserGroups.ForRole(UserRole.Client).ToString(),
                    status = nameof(UserStatus.Registered),
                    credits = 5
                },
                csrf);
            await AssertCodeAsync(created, HttpStatusCode.BadRequest, "validation_failed");
            using var createdBody = await ReadJsonAsync(created);
            var createErrors = createdBody.RootElement.GetProperty("errors");
            Assert.Equal(
                "Credits apply only to reseller accounts.",
                createErrors.GetProperty("credits")[0].GetString());

            csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/users/{clientId}",
                new { credits = 5 },
                csrf);
            await AssertCodeAsync(patched, HttpStatusCode.BadRequest, "validation_failed");
            using var patchedBody = await ReadJsonAsync(patched);
            var patchErrors = patchedBody.RootElement.GetProperty("errors");
            Assert.Equal(
                "Credits apply only to reseller accounts.",
                patchErrors.GetProperty("credits")[0].GetString());

            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            using var detail = await Client.GetAsync(
                $"/admin/users/{clientId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(0, detailBody.RootElement.GetProperty("credits").GetInt32());
        }

        [Fact]
        public async Task ANegativeBalanceIsRefusedAndWritesNothing()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller", credits: 5);
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();

            var csrf = await GetCsrfTokenAsync();
            using var negative = await PatchJsonAsync(
                $"/admin/users/{resellerId}",
                new { credits = -1 },
                csrf);
            await AssertCodeAsync(negative, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(negative);
            Assert.Equal(
                "Credits cannot be negative.",
                body.RootElement.GetProperty("errors").GetProperty("credits")[0].GetString());

            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            using var detail = await Client.GetAsync(
                $"/admin/users/{resellerId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(5, detailBody.RootElement.GetProperty("credits").GetInt32());

            // The same rule on the create path: a body that names a negative balance is refused
            // under the same field, and no account is written for it.
            csrf = await GetCsrfTokenAsync();
            using var created = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "negative-reseller",
                    email = "negative-reseller@example.test",
                    password = "Password1",
                    groupId = BaseUserGroups.ForRole(UserRole.Reseller).ToString(),
                    status = nameof(UserStatus.Registered),
                    credits = -1
                },
                csrf);
            await AssertCodeAsync(created, HttpStatusCode.BadRequest, "validation_failed");
            using var createdBody = await ReadJsonAsync(created);
            Assert.Equal(
                "Credits cannot be negative.",
                createdBody.RootElement.GetProperty("errors").GetProperty("credits")[0].GetString());

            // Neither the account nor a ledger row was written by the refused create.
            using var missing = await Client.GetAsync(
                "/admin/users?search=negative-reseller",
                TestContext.Current.CancellationToken);
            using var missingBody = await ReadJsonAsync(missing);
            Assert.Equal(0, missingBody.RootElement.GetProperty("total").GetInt32());

            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task MovingAResellerToAClientKeepsTheBalanceAndWritesNoRow()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller", credits: 9);
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();

            var csrf = await GetCsrfTokenAsync();
            using var moved = await PatchJsonAsync(
                $"/admin/users/{resellerId}",
                new { groupId = BaseUserGroups.ForRole(UserRole.Client).ToString() },
                csrf);
            await AssertCodeAsync(moved, HttpStatusCode.OK, "user_updated");

            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            using var detail = await Client.GetAsync(
                $"/admin/users/{resellerId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(9, detailBody.RootElement.GetProperty("credits").GetInt32());
            Assert.Equal(nameof(UserRole.Client), detailBody.RootElement.GetProperty("role").GetString());
        }

        [Fact]
        public async Task DeletingTheIssuingAdminLeavesTheRowWithNoIssuerWhateverTheSign()
        {
            var issuerId = await CreateAdminAndSignInAsync();

            var resellerId = await CreateResellerThroughApiAsync("funded", credits: 11);

            // Both signs are written by the first administrator: the create row (11) and a
            // decrease to 5. Both carry them as issuer under the admin-panel rule.
            var csrf = await GetCsrfTokenAsync();
            using var spent = await PatchJsonAsync(
                $"/admin/users/{resellerId}",
                new { credits = 5 },
                csrf);
            await AssertCodeAsync(spent, HttpStatusCode.OK, "user_updated");

            // A second administrator deletes the first; the credit itself must survive.
            await CreateUserAsync(UserRole.Admin, "successor");
            using var successor = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "successor", "Password1", successor);

            var successorCsrf = await GetCsrfTokenAsync(successor);
            using var deleted = await DeleteAsync(
                $"/admin/users/{issuerId}",
                successorCsrf,
                successor);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "user_deleted");

            using var list = await successor.GetAsync(
                $"/admin/credit-logs?resellerId={resellerId}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using var body = await ReadJsonAsync(list);
            var rows = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

            // A create row and a decrease row, and both lost their issuer with the account.
            Assert.Equal(2, rows.Length);
            Assert.Contains(rows, row => row.GetProperty("amount").GetInt32() == 11);
            Assert.Contains(rows, row => row.GetProperty("amount").GetInt32() == -6);
            Assert.All(rows, row =>
            {
                Assert.Equal(JsonValueKind.Null, row.GetProperty("issuedById").ValueKind);
                Assert.Equal(JsonValueKind.Null, row.GetProperty("issuedByUsername").ValueKind);
            });
        }

        [Fact]
        public async Task ThePageFiltersSearchesAndSortsOnTheServer()
        {
            await CreateAdminAndSignInAsync();

            var first = await CreateResellerThroughApiAsync("alice", credits: 30);

            // The host's audit clock is frozen, so two creates a moment apart would share a
            // `created_at` and the "newest first" assertion below would fall to the identifier
            // tie-break - which says nothing about the created-at order this test is about. One
            // second keeps the order meaningful; the tie-break itself is pinned separately.
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            var second = await CreateResellerThroughApiAsync("bobby", credits: 10);

            using var all = await GetAsync("/admin/credit-logs");
            using var allBody = await ReadJsonAsync(all);
            Assert.Equal(2, allBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(20, allBody.RootElement.GetProperty("pageSize").GetInt32());
            Assert.True(all.Headers.CacheControl?.NoStore);

            using var searched = await GetAsync("/admin/credit-logs?search=ali");
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                first.ToString(),
                searchedBody.RootElement.GetProperty("items")[0].GetProperty("resellerId").GetString());

            // The search matches the description too, not only the name.
            using var byDescription = await GetAsync("/admin/credit-logs?search=initial%20credits");
            using var byDescriptionBody = await ReadJsonAsync(byDescription);
            Assert.Equal(2, byDescriptionBody.RootElement.GetProperty("total").GetInt32());

            using var filtered = await GetAsync($"/admin/credit-logs?resellerId={second}");
            using var filteredBody = await ReadJsonAsync(filtered);
            Assert.Equal(1, filteredBody.RootElement.GetProperty("total").GetInt32());

            using var byAmount = await GetAsync("/admin/credit-logs?sortBy=amount&sortOrder=asc");
            using var byAmountBody = await ReadJsonAsync(byAmount);
            var amounts = byAmountBody.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Select(row => row.GetProperty("amount").GetInt32())
                .ToArray();
            Assert.Equal([10, 30], amounts);

            // The untouched page is newest first: bobby was created after alice.
            using var untouched = await GetAsync("/admin/credit-logs");
            using var untouchedBody = await ReadJsonAsync(untouched);
            Assert.Equal(
                "bobby",
                untouchedBody.RootElement.GetProperty("items")[0].GetProperty("resellerUsername").GetString());
        }

        [Fact]
        public async Task TheDateRangeIsTwoInclusiveBoundsAndAnUnparseableOneIsRefused()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller");
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();

            var oldest = await TestDataSeeder.PlantCreditLogAsync(
                connection,
                resellerId,
                null,
                -1,
                "Oldest",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            var middle = await TestDataSeeder.PlantCreditLogAsync(
                connection,
                resellerId,
                null,
                -2,
                "Middle",
                new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            var newest = await TestDataSeeder.PlantCreditLogAsync(
                connection,
                resellerId,
                null,
                -3,
                "Newest",
                new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);

            // Both ends inclusive: the bounds name the rows' own instants.
            using var bounded = await GetAsync(
                "/admin/credit-logs?dateFrom=2026-01-01T00:00:00Z&dateTo=2026-02-01T00:00:00Z");
            using var boundedBody = await ReadJsonAsync(bounded);
            Assert.Equal(2, boundedBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                [middle, oldest],
                Ids(boundedBody.RootElement.GetProperty("items")));

            using var fromOnly = await GetAsync("/admin/credit-logs?dateFrom=2026-02-01T00:00:00Z");
            using var fromOnlyBody = await ReadJsonAsync(fromOnly);
            Assert.Equal(
                [newest, middle],
                Ids(fromOnlyBody.RootElement.GetProperty("items")));

            using var toOnly = await GetAsync("/admin/credit-logs?dateTo=2026-02-01T00:00:00Z");
            using var toOnlyBody = await ReadJsonAsync(toOnly);
            Assert.Equal(
                [middle, oldest],
                Ids(toOnlyBody.RootElement.GetProperty("items")));

            foreach (var parameter in new[] { "dateFrom", "dateTo" })
            {
                using var refused = await GetAsync($"/admin/credit-logs?{parameter}=yesterday");
                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

                using var refusedBody = await ReadJsonAsync(refused);
                Assert.True(
                    refusedBody.RootElement.GetProperty("errors").TryGetProperty(parameter, out _),
                    $"no errors.{parameter}: {refusedBody.RootElement}");
            }
        }

        [Fact]
        public async Task TheTotalIsCountedUnderThePagePredicateAndTheTieBreakKeepsPagesDisjoint()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller");
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();

            // Three rows written in the same instant: without the identifier tie-break a page
            // turn could show one twice and skip another.
            var wroteAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
            var ids = new[]
            {
                await TestDataSeeder.PlantCreditLogAsync(
                    connection, resellerId, null, 1, "One", wroteAt, TestContext.Current.CancellationToken),
                await TestDataSeeder.PlantCreditLogAsync(
                    connection, resellerId, null, 2, "Two", wroteAt, TestContext.Current.CancellationToken),
                await TestDataSeeder.PlantCreditLogAsync(
                    connection, resellerId, null, 3, "Three", wroteAt, TestContext.Current.CancellationToken)
            };

            var seen = new List<Guid>();

            for (var page = 1; page <= 3; page++)
            {
                using var response = await GetAsync(
                    $"/admin/credit-logs?resellerId={resellerId}&page={page}&pageSize=1&sortBy=createdAt&sortOrder=desc");
                using var body = await ReadJsonAsync(response);

                Assert.Equal(3, body.RootElement.GetProperty("total").GetInt32());
                seen.AddRange(Ids(body.RootElement.GetProperty("items")));
            }

            Assert.Equal(3, seen.Count);
            Assert.Equal(3, seen.Distinct().Count());
            Assert.Equal(ids.OrderBy(id => id).ToArray(), seen.OrderBy(id => id).ToArray());

            // The tie-break direction is pinned literally: rows sharing an instant come out by
            // identifier, descending, when the request asks for descending. The comparison is on
            // the canonical text form, which is the byte order PostgreSQL applies to a `uuid`.
            using var tied = await GetAsync(
                $"/admin/credit-logs?resellerId={resellerId}&sortBy=createdAt&sortOrder=desc");
            using var tiedBody = await ReadJsonAsync(tied);

            Assert.Equal(
                ids
                    .Select(id => id.ToString("D"))
                    .OrderByDescending(text => text, StringComparer.Ordinal)
                    .ToArray(),
                Ids(tiedBody.RootElement.GetProperty("items"))
                    .Select(id => id.ToString("D"))
                    .ToArray());

            // A page past the end reports the true total and an empty list.
            using var beyond = await GetAsync(
                $"/admin/credit-logs?resellerId={resellerId}&page=9&pageSize=2");
            using var beyondBody = await ReadJsonAsync(beyond);
            Assert.Equal(3, beyondBody.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(beyondBody.RootElement.GetProperty("items").EnumerateArray());

            using var filteredBeyond = await GetAsync(
                $"/admin/credit-logs?resellerId={resellerId}&page=9&pageSize=2&search=nomatch");
            using var filteredBeyondBody = await ReadJsonAsync(filteredBeyond);
            Assert.Equal(0, filteredBeyondBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task ThePageBoundsAreRefusedRatherThanClamped()
        {
            await CreateAdminAndSignInAsync();

            using var overCeiling = await GetAsync("/admin/credit-logs?pageSize=1001");
            await AssertCodeAsync(overCeiling, HttpStatusCode.BadRequest, "validation_failed");
            using var overCeilingBody = await ReadJsonAsync(overCeiling);
            Assert.True(overCeilingBody.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));

            using var zeroPage = await GetAsync("/admin/credit-logs?page=0");
            await AssertCodeAsync(zeroPage, HttpStatusCode.BadRequest, "validation_failed");

            using var badSort = await GetAsync("/admin/credit-logs?sortBy=resellerUsername");
            await AssertCodeAsync(badSort, HttpStatusCode.BadRequest, "validation_failed");
            using var badSortBody = await ReadJsonAsync(badSort);
            Assert.True(badSortBody.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));

            using var badOrder = await GetAsync("/admin/credit-logs?sortOrder=upwards");
            await AssertCodeAsync(badOrder, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task ClearingWipesEverythingIsIdempotentAndNeedsTheCsrfPair()
        {
            await CreateAdminAndSignInAsync();

            var resellerId = await CreateResellerThroughApiAsync("reseller", credits: 4);

            await using var connection = await Fixture.OpenConnectionAsync();

            Assert.Equal(1, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // No antiforgery pair: refused before anything is deleted.
            using var noCsrf = await Client.DeleteAsync(
                "/admin/credit-logs",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            Assert.Equal(1, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // With the pair the whole table goes, whatever the query string names.
            using var cleared = await DeleteAsync(
                $"/admin/credit-logs?resellerId={resellerId}&search=nomatch",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "credit_logs_cleared");
            using var clearedBody = await ReadJsonAsync(cleared);
            Assert.Equal(
                "Cleared the credit log: 1 row(s) deleted.",
                clearedBody.RootElement.GetProperty("message").GetString());

            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            // An empty table is success with a count of zero, not a refusal.
            using var again = await DeleteAsync(
                "/admin/credit-logs",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.OK, "credit_logs_cleared");
            using var againBody = await ReadJsonAsync(again);
            Assert.Equal(
                "Cleared the credit log: 0 row(s) deleted.",
                againBody.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task TheEndpointsAreRefusedWithoutTheResellerGrants()
        {
            // An administrator holding only the client grants may read neither the log nor wipe it.
            var readersGroup = await CreateGroupAsync(
                "Readers",
                UserRole.Admin,
                permissions: UserPermission.ViewClients);
            await CreateUserAsync(UserRole.Admin, "reader", groupId: readersGroup);

            using var reader = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "reader", "Password1", reader);

            using var list = await reader.GetAsync(
                "/admin/credit-logs",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(list, HttpStatusCode.Forbidden, "permission_denied");

    // The read grant reaches the list but not the wipe.
            var editorsGroup = await CreateGroupAsync(
                "Editors",
                UserRole.Admin,
                permissions: UserPermission.ViewResellers);
            await CreateUserAsync(UserRole.Admin, "editor", groupId: editorsGroup);

            using var editor = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "editor", "Password1", editor);

            using var readable = await editor.GetAsync(
                "/admin/credit-logs",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

            using var refusedClear = await DeleteAsync(
                "/admin/credit-logs",
                await GetCsrfTokenAsync(editor),
                editor);
            await AssertCodeAsync(refusedClear, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task TheEndpointsAreRefusedToAClientAndToAnonymous()
        {
            using var anonymous = Fixture.CreateClient();

            using (var anonymousList = await anonymous.GetAsync(
                "/admin/credit-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");
            }

            using (var anonymousClear = await anonymous.DeleteAsync(
                "/admin/credit-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(anonymousClear, HttpStatusCode.Unauthorized, "auth_required");
            }

            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);

            // A Client is refused by the role gate ahead of the permission gate, which is the
            // envelope every admin route of this API answers.
            using (var refusedList = await client.GetAsync(
                "/admin/credit-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedList, HttpStatusCode.Forbidden, "auth_role_changed");
            }
        }

        [Fact]
        public async Task ABalanceAndItsRowCommitTogetherOrNotAtAll()
        {
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller", credits: 3);
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();
            const string trigger = "trg_credit_log_forced_failure";

            try
            {
                await TestDataSeeder.InstallCreditLogFailureTriggerAsync(
                    connection,
                    trigger,
                    TestContext.Current.CancellationToken);

                var csrf = await GetCsrfTokenAsync();
                using var failed = await PatchJsonAsync(
                    $"/admin/users/{resellerId}",
                    new { credits = 40 },
                    csrf);

                // The write fails as a server error: the service has no answer for a constraint
                // it does not recognise, and the production envelope is what a client sees.
                await AssertCodeAsync(failed, HttpStatusCode.InternalServerError, "server_error");
            }
            finally
            {
                await TestDataSeeder.DropCreditLogFailureTriggerAsync(
                    connection,
                    trigger,
                    TestContext.Current.CancellationToken);
            }

            // Neither half of the operation survived: no balance change and no log row.
            Assert.Equal(0, await TestDataSeeder.CountCreditLogsAsync(
                connection,
                TestContext.Current.CancellationToken));

            using var detail = await Client.GetAsync(
                $"/admin/users/{resellerId}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal(3, detailBody.RootElement.GetProperty("credits").GetInt32());
        }

        [Fact]
        public async Task TheProfileCarriesCreditsAndMeStaysTokenClaimsOnly()
        {
            await CreateUserAsync(UserRole.Reseller, "reseller", email: "reseller@example.test", credits: 12);
            await SignInAsync(UserRole.Reseller, "reseller", "Password1");

            using var profile = await Client.GetAsync(
                "/auth/profile",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

            using var profileBody = await ReadJsonAsync(profile);
            Assert.Equal(12, profileBody.RootElement.GetProperty("credits").GetInt32());

            using var me = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);

            using var meBody = await ReadJsonAsync(me);
            Assert.False(meBody.RootElement.TryGetProperty("credits", out _));
        }

        /// <summary>
        /// Creates a reseller through <c>POST /admin/users</c>, which is the only path that writes
        /// the creation row, and answers the new account's identifier.
        /// </summary>
        private async Task<Guid> CreateResellerThroughApiAsync(string username, int? credits = null)
        {
            var csrf = await GetCsrfTokenAsync();
            using var created = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username,
                    email = $"{username}@example.test",
                    password = "Password1",
                    groupId = BaseUserGroups.ForRole(UserRole.Reseller).ToString(),
                    status = nameof(UserStatus.Registered),
                    credits
                },
                csrf);

            await AssertCodeAsync(created, HttpStatusCode.Created, "user_created");

            return Guid.Parse(IdFromLocation(created));
        }

        private Task<HttpResponseMessage> GetAsync(string path)
            => Client.GetAsync(path, TestContext.Current.CancellationToken);

        private static Guid[] Ids(JsonElement items)
            => items
                .EnumerateArray()
                .Select(row => Guid.Parse(row.GetProperty("id").GetString()!))
                .ToArray();
    }
}
