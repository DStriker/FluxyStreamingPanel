using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Admin
{
    public sealed class AdminUsersTests : IntegrationTestBase
    {
        public AdminUsersTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AdminCanCreateEditBlockUnblockAndDeleteAnAccount()
        {
            var adminId = await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            using var created = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "managed",
                    email = "managed@example.test",
                    password = "Password1",
                    groupId = BaseUserGroups.ForRole(UserRole.Client).ToString(),
                    status = nameof(UserStatus.Registered)
                },
                csrf);

            await AssertCodeAsync(created, HttpStatusCode.Created, "user_created");
            Assert.NotNull(created.Headers.Location);

            var managedId = IdFromLocation(created);

            using var detail = await Client.GetAsync($"/admin/users/{managedId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal("managed", detailBody.RootElement.GetProperty("username").GetString());
            Assert.Equal(nameof(UserRole.Client), detailBody.RootElement.GetProperty("role").GetString());
            Assert.Equal(
                nameof(UserStatus.Registered),
                detailBody.RootElement.GetProperty("effectiveStatus").GetString());

            csrf = await GetCsrfTokenAsync();
            using var updated = await PatchJsonAsync(
                $"/admin/users/{managedId}",
                new { username = "managed-renamed", timeZone = "UTC" },
                csrf);
            await AssertCodeAsync(updated, HttpStatusCode.OK, "user_updated");

            using var renamed = await Client.GetAsync($"/admin/users/{managedId}", TestContext.Current.CancellationToken);
            using var renamedBody = await ReadJsonAsync(renamed);
            Assert.Equal("managed-renamed", renamedBody.RootElement.GetProperty("username").GetString());
            Assert.Equal("UTC", renamedBody.RootElement.GetProperty("timeZone").GetString());

            using var victim = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "managed-renamed", "Password1", victim);

            csrf = await GetCsrfTokenAsync();
            using var blocked = await PostJsonAsync(
                $"/admin/users/{managedId}/block",
                new { },
                csrf);
            await AssertCodeAsync(blocked, HttpStatusCode.OK, "user_blocked");

            using var victimMe = await victim.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, victimMe.StatusCode);

            csrf = await GetCsrfTokenAsync();
            using var unblocked = await PostJsonAsync(
                $"/admin/users/{managedId}/unblock",
                new { },
                csrf);
            await AssertCodeAsync(unblocked, HttpStatusCode.OK, "user_unblocked");

            using var signIn = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "managed-renamed", "Password1", signIn);

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/users/{managedId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "user_deleted");

            using var missing = await Client.GetAsync($"/admin/users/{managedId}", TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "user_not_found");

            Assert.NotEqual(Guid.Empty, adminId);
        }

        [Fact]
        public async Task DuplicateAndInvalidAccountsAreRefusedWithFields()
        {
            await CreateAdminAndSignInAsync();
            await CreateUserAsync(UserRole.Client, "existing", email: "existing@example.test");

            var csrf = await GetCsrfTokenAsync();
            using var duplicate = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "existing",
                    email = "another@example.test",
                    password = "Password1",
                    groupId = BaseUserGroups.ForRole(UserRole.Client).ToString(),
                    status = nameof(UserStatus.Registered)
                },
                csrf);
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "user_already_exists");

            csrf = await GetCsrfTokenAsync();
            using var invalidReferences = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "x",
                    email = "not-an-email",
                    password = "short",
                    groupId = "not-a-guid",
                    status = "Nowhere"
                },
                csrf);
            await AssertCodeAsync(invalidReferences, HttpStatusCode.BadRequest, "validation_failed");

            using var referencesBody = await ReadJsonAsync(invalidReferences);
            var referenceErrors = referencesBody.RootElement.GetProperty("errors");
            Assert.True(referenceErrors.TryGetProperty("groupId", out _));
            Assert.True(referenceErrors.TryGetProperty("status", out _));

            csrf = await GetCsrfTokenAsync();
            using var invalidFields = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "x",
                    email = "not-an-email",
                    password = "short",
                    groupId = BaseUserGroups.ForRole(UserRole.Client).ToString(),
                    status = nameof(UserStatus.Registered)
                },
                csrf);
            await AssertCodeAsync(invalidFields, HttpStatusCode.BadRequest, "validation_failed");

            using var fieldsBody = await ReadJsonAsync(invalidFields);
            var fieldErrors = fieldsBody.RootElement.GetProperty("errors");
            Assert.True(fieldErrors.TryGetProperty("username", out _));
            Assert.True(fieldErrors.TryGetProperty("email", out _));
            Assert.True(fieldErrors.TryGetProperty("password", out _));
        }

        [Fact]
        public async Task TheAccountsListFiltersPagesAndSortsOnTheServer()
        {
            await CreateAdminAndSignInAsync();
            await CreateUserAsync(UserRole.Client, "alpha", email: "alpha@example.test");
            await CreateUserAsync(UserRole.Client, "beta", email: "beta@example.test");
            await CreateUserAsync(UserRole.Reseller, "gamma", email: "gamma@example.test");

            using var page = await Client.GetAsync(
                "/admin/users?page=1&pageSize=2&sortBy=username&sortOrder=asc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageBody = await ReadJsonAsync(page);
            Assert.Equal(4, pageBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, pageBody.RootElement.GetProperty("items").GetArrayLength());

            using var filtered = await Client.GetAsync(
                $"/admin/users?groupId={BaseUserGroups.ForRole(UserRole.Reseller)}&search=gamma",
                TestContext.Current.CancellationToken);
            using var filteredBody = await ReadJsonAsync(filtered);
            Assert.Equal(1, filteredBody.RootElement.GetProperty("total").GetInt32());
            var item = filteredBody.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("gamma", item.GetProperty("username").GetString());

            using var invalidGroup = await Client.GetAsync(
                "/admin/users?groupId=not-a-guid",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidGroup, HttpStatusCode.BadRequest, "validation_failed");
            using var invalidGroupBody = await ReadJsonAsync(invalidGroup);
            Assert.True(invalidGroupBody.RootElement.GetProperty("errors").TryGetProperty("groupId", out _));

            using var lastSeen = await Client.GetAsync(
                "/admin/users?sortBy=lastSeenAt&sortOrder=desc",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, lastSeen.StatusCode);

            using var invalidSort = await Client.GetAsync(
                "/admin/users?sortBy=password",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidSort, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task AnAdministratorCannotRemoveTheirOwnAccess()
        {
            var adminId = await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            using var delete = await DeleteAsync($"/admin/users/{adminId}", csrf);
            await AssertCodeAsync(delete, HttpStatusCode.BadRequest, "cannot_delete_self");

            csrf = await GetCsrfTokenAsync();
            using var block = await PostJsonAsync(
                $"/admin/users/{adminId}/block",
                new { },
                csrf);
            await AssertCodeAsync(block, HttpStatusCode.BadRequest, "cannot_block_self");

            csrf = await GetCsrfTokenAsync();
            using var demote = await PatchJsonAsync(
                $"/admin/users/{adminId}",
                new { groupId = BaseUserGroups.ForRole(UserRole.Client).ToString() },
                csrf);
            await AssertCodeAsync(demote, HttpStatusCode.BadRequest, "cannot_demote_self");
        }

        [Fact]
        public async Task WritesRequireAntiforgeryAndReadsRequireTheAdminRole()
        {
            using var anonymous = Fixture.CreateClient();
            using var anonymousList = await anonymous.GetAsync("/admin/users", TestContext.Current.CancellationToken);
            await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAdminAndSignInAsync();
            using var noCsrf = await PostJsonAsync(
                "/admin/users",
                new
                {
                    username = "managed",
                    email = "managed@example.test",
                    password = "Password1",
                    groupId = BaseUserGroups.ForRole(UserRole.Client).ToString(),
                    status = nameof(UserStatus.Registered)
                });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "client", client: client);
            using var forbidden = await client.GetAsync("/admin/users", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        [Fact]
        public async Task BulkRunsReportEachAccountAndKeepTheSelfRefusalToItsOwnRow()
        {
            var adminId = await CreateAdminAndSignInAsync();
            var targetId = await CreateUserAsync(UserRole.Client, "bulk-target");

            var csrf = await GetCsrfTokenAsync();
            using var run = await PostJsonAsync(
                "/admin/users/bulk",
                new
                {
                    action = "block",
                    ids = new[] { adminId.ToString(), targetId.ToString() }
                },
                csrf);

            Assert.Equal(HttpStatusCode.OK, run.StatusCode);
            using var body = await ReadJsonAsync(run);
            Assert.Equal("users_bulk_completed", CodeOf(body));
            Assert.Equal(1, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("failed").GetInt32());

            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(2, items.Length);
            Assert.Equal(adminId.ToString(), items[0].GetProperty("id").GetString());
            Assert.Equal("cannot_block_self", items[0].GetProperty("code").GetString());
            Assert.False(items[0].GetProperty("ok").GetBoolean());
            Assert.Equal(targetId.ToString(), items[1].GetProperty("id").GetString());
            Assert.Equal("user_blocked", items[1].GetProperty("code").GetString());
            Assert.True(items[1].GetProperty("ok").GetBoolean());

            using var target = await Client.GetAsync(
                $"/admin/users/{targetId}",
                TestContext.Current.CancellationToken);
            using var targetBody = await ReadJsonAsync(target);
            Assert.Equal(
                nameof(UserStatus.Blocked),
                targetBody.RootElement.GetProperty("effectiveStatus").GetString());
        }

        [Fact]
        public async Task ADeletionThatDoesNotExistIsNotFound()
        {
            await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            using var response = await DeleteAsync($"/admin/users/{Guid.NewGuid()}", csrf);

            await AssertCodeAsync(response, HttpStatusCode.NotFound, "user_not_found");
        }
    }
}
