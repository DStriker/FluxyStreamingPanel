using System.Net;
using System.Net.Http.Json;
using Fluxy.Core.Backups.Utils;
using Fluxy.Core.Persistence.Outcomes;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Npgsql;

namespace Fluxy.IntegrationTests.Backups
{
    /// <summary>
    /// The backup surface: the list, the three writes, every refusal, the two fences and the
    /// reused settings grants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here restores against Testcontainers.</b> The database is shared by every test in
    /// the collection, and a real <c>pg_restore --clean</c> would replace its schema mid-run. The
    /// tool behind <c>IDatabaseBackupTool</c> is replaced by a fake that writes the file the
    /// service renames, so the directory rules run for real and the database is never touched.
    /// </para>
    /// <para>
    /// <b>The six-table existence guard is the important one.</b> <c>pg_dump</c> silently ignores
    /// an <c>--exclude-table-data</c> name that matches no table and still exits zero, so a typo in
    /// the catalogue would produce a backup that quietly carries the log it promised to leave out.
    /// This suite asks the live schema whether the six names exist.
    /// </para>
    /// </remarks>
    public sealed class BackupsTests : IntegrationTestBase
    {
        public BackupsTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheBackupsAreRefusedToAnAnonymousCaller()
        {
            using var read = await Client.GetAsync(
                "/admin/backups",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(read, HttpStatusCode.Unauthorized, "auth_required");

            using var write = await PostJsonAsync("/admin/backups", new { });
            await AssertCodeAsync(write, HttpStatusCode.Unauthorized, "auth_required");

            using var delete = await DeleteAsync("/admin/backups/fluxy-20260101T000000Z.dump");
            await AssertCodeAsync(delete, HttpStatusCode.Unauthorized, "auth_required");
        }

        [Fact]
        public async Task TheListIsEmptyUntilABackupIsCreatedAndAnswersNewestFirst()
        {
            await CreateAdminAndSignInAsync();

            using (var empty = await Client.GetAsync(
                "/admin/backups",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
                Assert.True(empty.Headers.CacheControl?.NoStore ?? false);

                using var body = await ReadJsonAsync(empty);
                Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
            }

            using (var created = await PostJsonAsync("/admin/backups", new { }, await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(created, HttpStatusCode.Created, "backup_created");

                var location = created.Headers.Location?.ToString();

                Assert.NotNull(location);
                Assert.StartsWith("/admin/backups/fluxy-", location);

                using var body = await ReadJsonAsync(created);
                Assert.Equal(
                    location!.Split('/').Last(),
                    body.RootElement.GetProperty("fileName").GetString());
            }

            // A second, older archive is placed on disk directly rather than by advancing the shared
            // clock: the token service mints from that clock while the JWT validator reads the wall
            // clock, so moving it forward would leave every later sign-in refused as `auth_required`.
            // The list reads the directory, which is the fact under test here.
            var older = Path.Combine(Fixture.BackupDirectory, "fluxy-20260101T000000Z.dump");
            await File.WriteAllBytesAsync(older, new byte[4096], TestContext.Current.CancellationToken);

            // A file that is not this feature's is not in the list.
            await File.WriteAllTextAsync(
                Path.Combine(Fixture.BackupDirectory, "operator-copy.dump"),
                "not ours",
                TestContext.Current.CancellationToken);

            using var list = await Client.GetAsync(
                "/admin/backups",
                TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);

            var items = listBody.RootElement.GetProperty("items").EnumerateArray().ToList();

            Assert.Equal(2, items.Count);
            Assert.True(
                string.CompareOrdinal(
                    items[0].GetProperty("fileName").GetString(),
                    items[1].GetProperty("fileName").GetString()) > 0);
            Assert.Equal("fluxy-20260101T000000Z.dump", items[1].GetProperty("fileName").GetString());
            Assert.Equal(4096, items[1].GetProperty("sizeBytes").GetInt64());
            Assert.Equal(
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                items[1].GetProperty("createdAtUtc").GetDateTimeOffset());
            Assert.True(items[0].GetProperty("sizeBytes").GetInt64() > 0);
        }

        [Fact]
        public async Task ARestoreOfAnExistingArchiveAnswersBackupRestored()
        {
            await CreateAdminAndSignInAsync();

            var fileName = await CreateBackupAsync();

            using var restored = await PostJsonAsync(
                $"/admin/backups/{Uri.EscapeDataString(fileName)}/restore",
                new { },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(restored, HttpStatusCode.OK, "backup_restored");

            using var body = await ReadJsonAsync(restored);
            Assert.Equal(fileName, body.RootElement.GetProperty("fileName").GetString());
            Assert.Equal(1, Fixture.BackupTool.RestoreCalls);
        }

        [Fact]
        public async Task ADeleteAnswersTwoHundredWithBackupDeletedAndTheFileGoes()
        {
            await CreateAdminAndSignInAsync();

            var fileName = await CreateBackupAsync();

            using (var deleted = await DeleteAsync(
                $"/admin/backups/{Uri.EscapeDataString(fileName)}",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(deleted, HttpStatusCode.OK, "backup_deleted");

                using var body = await ReadJsonAsync(deleted);
                Assert.Equal(fileName, body.RootElement.GetProperty("fileName").GetString());
            }

            Assert.False(File.Exists(Path.Combine(Fixture.BackupDirectory, fileName)));
        }

        [Fact]
        public async Task AWellFormedFileThisFeatureDidNotWriteIsNotAddressable()
        {
            await CreateAdminAndSignInAsync();

            // Well formed by the path-safety rule but not a name the feature writes: it is not
            // listed, not rotated, and therefore not restorable or deletable either. One accept-set
            // for the read and the two writes, so an operator's own copy is never touched by a
            // route that could not have produced it.
            var foreign = Path.Combine(Fixture.BackupDirectory, "operator-copy.dump");
            await File.WriteAllTextAsync(foreign, "not ours", TestContext.Current.CancellationToken);

            using (var restored = await PostJsonAsync(
                "/admin/backups/operator-copy.dump/restore",
                new { },
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(restored, HttpStatusCode.NotFound, "backup_not_found");
            }

            using (var deleted = await DeleteAsync(
                "/admin/backups/operator-copy.dump",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(deleted, HttpStatusCode.NotFound, "backup_not_found");
            }

            Assert.True(File.Exists(foreign));
            Assert.Equal(0, Fixture.BackupTool.RestoreCalls);
        }

        [Theory]
        [InlineData("fluxy-20260101T000000Z.dump")]
        [InlineData("nope.dump")]
        [InlineData("..%2Fx.dump")]
        [InlineData("..%5Cx.dump")]
        [InlineData("C%3A%5Cx.dump")]
        [InlineData("x.DUMP")]
        [InlineData("con.dump")]
        [InlineData("x.dump.")]
        [InlineData("sub%5Cx.dump")]
        public async Task AnInvalidOrUnknownNameAnswersOneBackupNotFound(string encoded)
        {
            await CreateAdminAndSignInAsync();

            using var restored = await PostJsonAsync(
                $"/admin/backups/{encoded}/restore",
                new { },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(restored, HttpStatusCode.NotFound, "backup_not_found");

            using var deleted = await DeleteAsync(
                $"/admin/backups/{encoded}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.NotFound, "backup_not_found");
        }

        [Fact]
        public async Task ASecondWriteWhileOneRunsAnswersBackupBusy()
        {
            await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            var hold = Fixture.BackupTool.Hold(out var entered);

            var running = PostJsonAsync("/admin/backups", new { }, csrf);

            // Bounded rather than open-ended: a request that never reaches the tool would
            // otherwise hang the run instead of failing it with the reason.
            var reached = await Task.WhenAny(
                entered,
                Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            Assert.True(
                ReferenceEquals(reached, entered),
                "The first create never reached the backup tool, so the gate was never held.");

            using (var busy = await PostJsonAsync("/admin/backups", new { }, await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(busy, HttpStatusCode.Conflict, "backup_busy");
            }

            hold.SetResult();

            using var first = await running;
            await AssertCodeAsync(first, HttpStatusCode.Created, "backup_created");
        }

        [Fact]
        public async Task AToolThatCannotStartAnswersBackupToolUnavailable()
        {
            await CreateAdminAndSignInAsync();

            Fixture.BackupTool.CreateResult = BackupToolResult.Unavailable;

            using var response = await PostJsonAsync("/admin/backups", new { }, await GetCsrfTokenAsync());

            await AssertCodeAsync(
                response,
                HttpStatusCode.InternalServerError,
                "backup_tool_unavailable");
        }

        [Fact]
        public async Task AToolThatFailsAnswersBackupFailed()
        {
            await CreateAdminAndSignInAsync();

            Fixture.BackupTool.CreateResult = BackupToolResult.Failed;

            using var response = await PostJsonAsync("/admin/backups", new { }, await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.InternalServerError, "backup_failed");

            // Nothing pattern-matching survived a failed run: the temporary file is gone and the
            // final name was never used.
            Assert.Empty(
                Directory.Exists(Fixture.BackupDirectory)
                    ? Directory.GetFiles(Fixture.BackupDirectory)
                    : []);
        }

        [Fact]
        public async Task EveryWriteIsRefusedWithoutTheAntiforgeryPair()
        {
            await CreateAdminAndSignInAsync();

            var fileName = await CreateBackupAsync();

            using (var create = await PostJsonAsync("/admin/backups", new { }))
            {
                await AssertCodeAsync(create, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            using (var restore = await PostJsonAsync(
                $"/admin/backups/{Uri.EscapeDataString(fileName)}/restore",
                new { }))
            {
                await AssertCodeAsync(restore, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            using (var delete = await DeleteAsync($"/admin/backups/{Uri.EscapeDataString(fileName)}"))
            {
                await AssertCodeAsync(delete, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            // Nothing ran: the file is still there and the tool was never asked for a restore.
            Assert.True(File.Exists(Path.Combine(Fixture.BackupDirectory, fileName)));
            Assert.Equal(0, Fixture.BackupTool.RestoreCalls);
        }

        [Fact]
        public async Task TheCollectionAndTheFilePathsAnswerMethodNotAllowed()
        {
            await CreateAdminAndSignInAsync();

            foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch })
            {
                using var response = await SendAsync(method, "/admin/backups", new { });
                await AssertCodeAsync(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            var fileName = await CreateBackupAsync();
            var path = $"/admin/backups/{Uri.EscapeDataString(fileName)}";

            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Patch })
            {
                using var response = await SendAsync(method, path, null);
                await AssertCodeAsync(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            // A POST on the bare file names no transition - the restore adds `/restore`.
            using (var post = await SendAsync(HttpMethod.Post, path, new { }))
            {
                await AssertCodeAsync(post, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }
        }

        [Fact]
        public async Task TheReadNeedsViewSettingsAndEveryWriteNeedsEditSettings()
        {
            await CreateAdminAndSignInAsync();

            var readers = await CreateGroupAsync(
                "Backup readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSettings);

            using var reader = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "backup-reader", groupId: readers);
            await SignInAsync(UserRole.Admin, "backup-reader", "Password1", reader);

            using (var allowed = await reader.GetAsync(
                "/admin/backups",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            }

            using (var refusedCreate = await PostJsonAsync(
                "/admin/backups",
                new { },
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(
                    refusedCreate,
                    HttpStatusCode.Forbidden,
                    "permission_denied");
            }

            using (var refusedDelete = await DeleteAsync(
                "/admin/backups/fluxy-20260101T000000Z.dump",
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(
                    refusedDelete,
                    HttpStatusCode.Forbidden,
                    "permission_denied");
            }

            using (var refusedRestore = await PostJsonAsync(
                "/admin/backups/fluxy-20260101T000000Z.dump/restore",
                new { },
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(
                    refusedRestore,
                    HttpStatusCode.Forbidden,
                    "permission_denied");
            }

            // A group with neither grant reads nothing.
            var strangers = await CreateGroupAsync(
                "Backup strangers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewStreams);

            using var stranger = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "backup-stranger", groupId: strangers);
            await SignInAsync(UserRole.Admin, "backup-stranger", "Password1", stranger);

            using var refusedRead = await stranger.GetAsync(
                "/admin/backups",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(refusedRead, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task TheSixExcludedTablesExistInTheLiveSchema()
        {
            // pg_dump ignores an --exclude-table-data name that matches nothing and still exits
            // zero, so the catalogue has to be checked against the schema it claims to name.
            await using var connection = new NpgsqlConnection(Fixture.PostgresConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            foreach (var table in BackupTableExclusions.Tables)
            {
                await using var command = new NpgsqlCommand(
                    "select count(*) from information_schema.tables " +
                    "where table_schema = 'public' and table_name = @name",
                    connection);

                command.Parameters.AddWithValue("name", table);

                var count = (long)(await command.ExecuteScalarAsync(
                    TestContext.Current.CancellationToken))!;

                Assert.True(count == 1, $"The excluded table '{table}' is not in the live schema.");
            }
        }

        [Fact]
        public async Task ACreatedArchiveIsNamedByTheDocumentedPattern()
        {
            await CreateAdminAndSignInAsync();

            var fileName = await CreateBackupAsync();

            Assert.True(BackupFileName.TryParse(fileName, out var utc));
            Assert.Equal(TimeSpan.Zero, utc.Offset);
            Assert.Equal(Fixture.Clock.GetUtcNow().UtcDateTime.Second, utc.UtcDateTime.Second);
        }

        private async Task<string> CreateBackupAsync()
        {
            using var created = await PostJsonAsync("/admin/backups", new { }, await GetCsrfTokenAsync());

            await AssertCodeAsync(created, HttpStatusCode.Created, "backup_created");

            using var body = await ReadJsonAsync(created);

            return body.RootElement.GetProperty("fileName").GetString()!;
        }

        private async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string path,
            object? body)
        {
            using var request = new HttpRequestMessage(method, path);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            request.Headers.Add("X-CSRF-Token", await GetCsrfTokenAsync());

            return await Client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
