using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Admin
{
    public sealed class UserGroupsTests : IntegrationTestBase
    {
        public UserGroupsTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheThreeBaseGroupsCarryTheirCatalogPermissions()
        {
            await CreateAdminAndSignInAsync();

            using var list = await Client.GetAsync("/admin/user-groups?pageSize=100", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var listBody = await ReadJsonAsync(list);
            Assert.Equal(3, listBody.RootElement.GetProperty("total").GetInt32());

            var items = listBody.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.All(items, item => Assert.True(item.GetProperty("isBase").GetBoolean()));

            using var adminDetail = await Client.GetAsync(
                $"/admin/user-groups/{BaseUserGroups.ForRole(UserRole.Admin)}",
                TestContext.Current.CancellationToken);
            using var adminBody = await ReadJsonAsync(adminDetail);
            var adminPermissions = adminBody.RootElement
                .GetProperty("permissions")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();

            Assert.Equal(14, adminPermissions.Length);
            Assert.Contains("viewClients", adminPermissions);
            Assert.Contains("editStreams", adminPermissions);
            Assert.Contains("viewAudioStreams", adminPermissions);
            Assert.Contains("editAudioStreams", adminPermissions);
            Assert.Contains("viewVideoOnDemand", adminPermissions);
            Assert.Contains("editVideoOnDemand", adminPermissions);
            Assert.Equal(1, adminBody.RootElement.GetProperty("members").GetInt32());

            using var clientDetail = await Client.GetAsync(
                $"/admin/user-groups/{BaseUserGroups.ForRole(UserRole.Client)}",
                TestContext.Current.CancellationToken);
            using var clientBody = await ReadJsonAsync(clientDetail);
            Assert.Empty(clientBody.RootElement.GetProperty("permissions").EnumerateArray());
            Assert.Equal(0, clientBody.RootElement.GetProperty("members").GetInt32());
        }

        [Fact]
        public async Task AGroupCanBeCreatedRenamedAndDeleted()
        {
            await CreateAdminAndSignInAsync();

            var groupId = await CreateGroupThroughApiAsync(
                "Operators",
                nameof(UserRole.Admin),
                "viewStreams",
                "editStreams");

            using var detail = await Client.GetAsync($"/admin/user-groups/{groupId}", TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            var permissions = detailBody.RootElement
                .GetProperty("permissions")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();

            Assert.Equal(2, permissions.Length);
            Assert.Contains("viewStreams", permissions);
            Assert.Contains("editStreams", permissions);
            Assert.False(detailBody.RootElement.GetProperty("isBase").GetBoolean());

            using var duplicate = await PostGroupAsync(
                "Operators",
                nameof(UserRole.Admin),
                "viewStreams");
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "user_group_already_exists");

            var csrf = await GetCsrfTokenAsync();
            using var renamed = await PatchJsonAsync(
                $"/admin/user-groups/{groupId}",
                new { name = "Operators Renamed" },
                csrf);
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "user_group_updated");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/user-groups/{groupId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "user_group_deleted");

            using var missing = await Client.GetAsync($"/admin/user-groups/{groupId}", TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "user_group_not_found");
        }

        [Fact]
        public async Task ABaseGroupMayOnlyBeRenamed()
        {
            await CreateAdminAndSignInAsync();
            var baseId = BaseUserGroups.ForRole(UserRole.Admin);

            var csrf = await GetCsrfTokenAsync();
            using var role = await PatchJsonAsync(
                $"/admin/user-groups/{baseId}",
                new { role = nameof(UserRole.Client) },
                csrf);
            await AssertCodeAsync(role, HttpStatusCode.Conflict, "user_group_immutable");

            csrf = await GetCsrfTokenAsync();
            using var status = await PatchJsonAsync(
                $"/admin/user-groups/{baseId}",
                new { status = nameof(UserStatus.Blocked) },
                csrf);
            await AssertCodeAsync(status, HttpStatusCode.Conflict, "user_group_immutable");

            csrf = await GetCsrfTokenAsync();
            using var permissions = await PatchJsonAsync(
                $"/admin/user-groups/{baseId}",
                new { permissions = Array.Empty<string>() },
                csrf);
            await AssertCodeAsync(permissions, HttpStatusCode.Conflict, "user_group_immutable");

            csrf = await GetCsrfTokenAsync();
            using var renamed = await PatchJsonAsync(
                $"/admin/user-groups/{baseId}",
                new { name = "Administrators Renamed" },
                csrf);
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "user_group_updated");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/user-groups/{baseId}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.Conflict, "user_group_immutable");
        }

        [Fact]
        public async Task AGroupWithMembersCannotBeDeleted()
        {
            await CreateAdminAndSignInAsync();
            var groupId = await CreateGroupThroughApiAsync(
                "Client Group",
                nameof(UserRole.Client));

            await CreateUserAsync(
                UserRole.Client,
                "member",
                groupId: Guid.Parse(groupId));

            var csrf = await GetCsrfTokenAsync();
            using var inUse = await DeleteAsync($"/admin/user-groups/{groupId}", csrf);
            await AssertCodeAsync(inUse, HttpStatusCode.Conflict, "user_group_in_use");
        }

        [Fact]
        public async Task UnknownPermissionsAreRefusedWhileWrongRolePermissionsAreDropped()
        {
            await CreateAdminAndSignInAsync();

            using var unknown = await PostGroupAsync(
                "Unknown Grant",
                nameof(UserRole.Admin),
                "viewEverything");
            await AssertCodeAsync(unknown, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownBody = await ReadJsonAsync(unknown);
            Assert.True(unknownBody.RootElement.GetProperty("errors").TryGetProperty("permissions", out _));

            var clientGroup = await CreateGroupThroughApiAsync(
                "Client Grant",
                nameof(UserRole.Client),
                "viewStreams");

            using var detail = await Client.GetAsync($"/admin/user-groups/{clientGroup}", TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Empty(detailBody.RootElement.GetProperty("permissions").EnumerateArray());
        }

        [Fact]
        public async Task ViewAndEditAreSeparatePermissions()
        {
            await CreateAdminAndSignInAsync();

            var viewOnly = await CreateGroupThroughApiAsync(
                "Stream Watchers",
                nameof(UserRole.Admin),
                "viewStreams");

            using var watcher = Fixture.CreateClient();
            await CreateUserAsync(
                UserRole.Admin,
                "watcher",
                groupId: Guid.Parse(viewOnly));
            await SignInAsync(UserRole.Admin, "watcher", "Password1", watcher);

            using var streams = await watcher.GetAsync("/admin/video-streams", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, streams.StatusCode);

            using var groups = await watcher.GetAsync("/admin/user-groups", TestContext.Current.CancellationToken);
            await AssertCodeAsync(groups, HttpStatusCode.Forbidden, "permission_denied");

            var csrf = await GetCsrfTokenAsync(watcher);
            using var createStream = await PostJsonAsync(
                "/admin/video-streams",
                new { },
                csrf,
                watcher);
            await AssertCodeAsync(createStream, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task BulkRunsRefuseBaseGroupsPerRowAndDeleteTheRest()
        {
            await CreateAdminAndSignInAsync();
            var customId = await CreateGroupAsync("Bulk Group", UserRole.Client);
            var baseId = BaseUserGroups.ForRole(UserRole.Client);

            var csrf = await GetCsrfTokenAsync();
            using var run = await PostJsonAsync(
                "/admin/user-groups/bulk",
                new
                {
                    action = "delete",
                    ids = new[] { baseId.ToString(), customId.ToString() }
                },
                csrf);

            Assert.Equal(HttpStatusCode.OK, run.StatusCode);
            using var body = await ReadJsonAsync(run);
            Assert.Equal("user_groups_bulk_completed", CodeOf(body));
            Assert.Equal(1, body.RootElement.GetProperty("succeeded").GetInt32());
            Assert.Equal(1, body.RootElement.GetProperty("failed").GetInt32());

            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(baseId.ToString(), items[0].GetProperty("id").GetString());
            Assert.Equal("user_group_immutable", items[0].GetProperty("code").GetString());
            Assert.Equal(customId.ToString(), items[1].GetProperty("id").GetString());
            Assert.Equal("user_group_deleted", items[1].GetProperty("code").GetString());

            using var custom = await Client.GetAsync(
                $"/admin/user-groups/{customId}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(custom, HttpStatusCode.NotFound, "user_group_not_found");

            using var baseGroup = await Client.GetAsync(
                $"/admin/user-groups/{baseId}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, baseGroup.StatusCode);
        }

        [Fact]
        public async Task GroupWritesRequireAntiforgery()
        {
            await CreateAdminAndSignInAsync();

            using var response = await PostJsonAsync(
                "/admin/user-groups",
                new
                {
                    name = "No Token",
                    role = nameof(UserRole.Admin),
                    status = nameof(UserStatus.Registered)
                });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        private async Task<string> CreateGroupThroughApiAsync(
            string name,
            string role,
            params string[] permissions)
        {
            using var response = await PostGroupAsync(name, role, permissions);
            await AssertCodeAsync(response, HttpStatusCode.Created, "user_group_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> PostGroupAsync(
            string name,
            string role,
            params string[] permissions)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync(
                "/admin/user-groups",
                new
                {
                    name,
                    role,
                    status = nameof(UserStatus.Registered),
                    permissions
                },
                csrf);
        }
    }
}
