using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.IntegrationTests.Infrastructure;
using Fluxy.IntegrationTests.Infrastructure.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The user audit log end to end: every flow that writes a row, the read surface, the clear,
    /// the permission gate and the fail-open write path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The log is read through the API and counted through SQL.</b> A page is what an operator
    /// sees, so the assertions go through <c>GET /admin/user-logs</c>; a "nothing was written"
    /// assertion has no page to look at and counts the table directly, through the documented SQL
    /// fixture in <see cref="TestDataSeeder"/>.
    /// </para>
    /// <para>
    /// <b>Setup writes rows of its own.</b> Signing an administrator in is a login and produces a
    /// row, which is the feature working correctly - so an assertion about a log always names the
    /// rows it planted rather than assuming a pristine table.
    /// </para>
    /// </remarks>
    public sealed class UserLogTests : IntegrationTestBase
    {
        private const string Password = "Password1";

        public UserLogTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ARegistrationIsRecordedWithTheAccountAndItsFailuresWithTheTypedName()
        {
            using var submitted = await RegisterAsync("new-account", "new.account@example.test");
            await AssertCodeAsync(submitted, HttpStatusCode.Created, "registration_submitted");

            var accountId = await FindUserIdAsync("new-account");
            Assert.NotNull(accountId);

            await CreateAdminAndSignInAsync();

            using var success = await ReadLogsAsync(
                "recordTypes=registration_submit&search=Registration%20submitted");
            var successRow = SingleRow(success);
            Assert.Equal("registration_submit", successRow.GetProperty("recordType").GetString());
            Assert.True(successRow.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(accountId!.Value.ToString(), successRow.GetProperty("accountId").GetString());
            Assert.Equal("new-account", successRow.GetProperty("username").GetString());
            Assert.Equal(
                "Registration submitted: new-account",
                successRow.GetProperty("description").GetString());

            // The same name cannot be registered again: one failure row naming the typed value.
            var csrf = await GetCsrfTokenAsync();
            using var duplicate = await PostJsonAsync(
                "/auth/registrations",
                new { username = "new-account", email = "other@example.test", password = Password },
                csrf);
            await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "user_already_exists");

            using var refused = await ReadLogsAsync(
                "recordTypes=registration_submit&search=already%20exists");
            var refusedRow = SingleRow(refused);
            Assert.False(refusedRow.GetProperty("isSuccess").GetBoolean());
            Assert.Equal("new-account", refusedRow.GetProperty("username").GetString());
            Assert.Equal(
                "Registration refused (already exists): new-account",
                refusedRow.GetProperty("description").GetString());
        }

        [Fact]
        public async Task ARefusedRegistrationWithNoMailServerIsRecordedAndNamesNobody()
        {
            using var factory = new FluxyApiFactory(
                Fixture.PostgresConnectionString,
                Fixture.RedisConnectionString,
                new RecordingEmailSender { IsConfigured = false },
                Fixture.Recaptcha,
                Fixture.GeoIp,
                Fixture.Clock);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var csrf = await GetCsrfTokenAsync(client);
            using var refused = await PostJsonAsync(
                "/auth/registrations",
                new { username = "unmailed", email = "unmailed@example.test", password = Password },
                csrf,
                client);
            await AssertCodeAsync(refused, HttpStatusCode.ServiceUnavailable, "registration_not_configured");

            Assert.Equal(1, await CountLogsAsync());

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("search=mail%20not%20configured");
            var row = SingleRow(logs);
            Assert.False(row.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("accountId").ValueKind);
            Assert.Equal("unmailed", row.GetProperty("username").GetString());
            Assert.Contains("mail not configured", row.GetProperty("description").GetString());
        }

        [Fact]
        public async Task AConfirmationRecordsEveryOutcomeAndNamesTheAccountWhenThereIsOne()
        {
            using var unknown = await ConfirmRegistrationAsync("nobody@example.test", "123456");
            await AssertCodeAsync(unknown, HttpStatusCode.BadRequest, "invalid_code");

            using var submitted = await RegisterAsync("pending-account", "pending@example.test");
            await AssertCodeAsync(submitted, HttpStatusCode.Created, "registration_submitted");

            using var wrong = await ConfirmRegistrationAsync("pending@example.test", "000000");
            await AssertCodeAsync(wrong, HttpStatusCode.BadRequest, "invalid_code");

            var code = Fixture.Email.CodeFor("pending@example.test");
            Assert.NotNull(code);

            using var confirmed = await ConfirmRegistrationAsync("pending@example.test", code);
            await AssertCodeAsync(confirmed, HttpStatusCode.OK, "registration_confirmed");

            var accountId = await FindUserIdAsync("pending-account");

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=registration_confirm");
            var rows = Rows(logs);

            Assert.Equal(3, rows.Length);

            // The successful confirmation names the account; the wrong code names the account it
            // was tried against; the unknown address names nobody. The order is the identifier
            // tie-break - the host's clock is frozen - so each row is found by its outcome and
            // its sentence rather than by position.
            var confirmedRow = Assert.Single(rows, row => row.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(accountId!.Value.ToString(), confirmedRow.GetProperty("accountId").GetString());
            Assert.Equal("pending-account", confirmedRow.GetProperty("username").GetString());
            Assert.Contains("Registration confirmed", confirmedRow.GetProperty("description").GetString());

            var wrongCodeRow = Assert.Single(rows, row =>
                !row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("username").GetString() == "pending-account");
            Assert.Equal(accountId!.Value.ToString(), wrongCodeRow.GetProperty("accountId").GetString());

            var unknownRow = Assert.Single(rows, row =>
                row.GetProperty("username").GetString() == "nobody@example.test");
            Assert.False(unknownRow.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(JsonValueKind.Null, unknownRow.GetProperty("accountId").ValueKind);
        }

        [Fact]
        public async Task AnExpiredRegistrationCodeIsRecordedAsExpired()
        {
            using var submitted = await RegisterAsync("late-account", "late.account@example.test");
            await AssertCodeAsync(submitted, HttpStatusCode.Created, "registration_submitted");

            var code = Fixture.Email.CodeFor("late.account@example.test");
            Assert.NotNull(code);

            await using (var connection = await Fixture.OpenConnectionAsync())
            await using (var command = new NpgsqlCommand(
                "update users set registration_code_expires_at = now() - interval '1 minute' " +
                "where email = @email",
                connection))
            {
                command.Parameters.AddWithValue("email", "late.account@example.test");
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            using var expired = await ConfirmRegistrationAsync("late.account@example.test", code);
            await AssertCodeAsync(expired, HttpStatusCode.BadRequest, "code_expired");

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync(
                "recordTypes=registration_confirm&search=expired");
            var row = SingleRow(logs);
            Assert.False(row.GetProperty("isSuccess").GetBoolean());
            Assert.Contains("code expired", row.GetProperty("description").GetString());
        }

        [Fact]
        public async Task ASignInRecordsTheTypedNameItsReasonAndItsSuccess()
        {
            await CreateUserAsync(UserRole.Client, "client", Password);

            var csrf = await GetCsrfTokenAsync();
            using var unknown = await PostJsonAsync(
                "/auth/client-login",
                new { username = "ghost", password = "WrongPassword1" },
                csrf);
            await AssertCodeAsync(unknown, HttpStatusCode.Unauthorized, "invalid_credentials");

            csrf = await GetCsrfTokenAsync();
            using var wrong = await PostJsonAsync(
                "/auth/client-login",
                new { username = "client", password = "WrongPassword1" },
                csrf);
            await AssertCodeAsync(wrong, HttpStatusCode.Unauthorized, "invalid_credentials");

            await SignInAsync(UserRole.Client, "client", Password);

            var clientId = await FindUserIdAsync("client");
            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=login&search=login%20failed");
            var failed = Rows(logs);
            Assert.Equal(2, failed.Length);
            Assert.All(failed, row => Assert.False(row.GetProperty("isSuccess").GetBoolean()));

            var wrongPasswordRow = Assert.Single(failed, row =>
                row.GetProperty("description").GetString()!.Contains("wrong password"));
            Assert.Equal("client", wrongPasswordRow.GetProperty("username").GetString());
            Assert.Equal(clientId!.Value.ToString(), wrongPasswordRow.GetProperty("accountId").GetString());

            var unknownRow = Assert.Single(failed, row =>
                row.GetProperty("description").GetString()!.Contains("no such account"));
            Assert.Equal("ghost", unknownRow.GetProperty("username").GetString());
            Assert.Equal(JsonValueKind.Null, unknownRow.GetProperty("accountId").ValueKind);

            // The successful sign-in of the account itself; the administrator's setup sign-in is
            // a row of its own and is excluded by the identifier filter.
            using var succeeded = await ReadLogsAsync(
                $"recordTypes=login&userIds={clientId}&search=login%20succeeded");
            var successRow = SingleRow(succeeded);
            Assert.True(successRow.GetProperty("isSuccess").GetBoolean());
            Assert.Equal("client", successRow.GetProperty("username").GetString());
        }

        [Fact]
        public async Task ASignInWithTheWrongDoorIsRecordedAsAWrongRoleWithoutChangingTheAnswer()
        {
            await CreateUserAsync(UserRole.Client, "client", Password);

            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/auth/admin-login",
                new { username = "client", password = Password },
                csrf);
            await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=login&search=wrong%20role");
            var row = SingleRow(logs);
            Assert.False(row.GetProperty("isSuccess").GetBoolean());
            Assert.Equal("client", row.GetProperty("username").GetString());
        }

        [Fact]
        public async Task APasswordResetRecordsTheRequestTheRefusalAndTheConfirmation()
        {
            await CreateUserAsync(
                UserRole.Client,
                "client",
                Password,
                email: "client@example.test");

            var csrf = await GetCsrfTokenAsync();
            using var unknownPair = await PostJsonAsync(
                "/auth/password-resets",
                new { username = "ghost", email = "ghost@example.test", password = "NewPassword1" },
                csrf);
            await AssertCodeAsync(unknownPair, HttpStatusCode.BadRequest, "credentials_mismatch");

            csrf = await GetCsrfTokenAsync();
            using var requested = await PostJsonAsync(
                "/auth/password-resets",
                new { username = "client", email = "client@example.test", password = "NewPassword1" },
                csrf);
            await AssertCodeAsync(requested, HttpStatusCode.OK, "password_reset_submitted");

            var code = Fixture.Email.CodeFor("client@example.test");
            Assert.NotNull(code);

            csrf = await GetCsrfTokenAsync();
            using var wrong = await PostJsonAsync(
                "/auth/password-resets/confirm",
                new { username = "client", code = "000000" },
                csrf);
            await AssertCodeAsync(wrong, HttpStatusCode.BadRequest, "invalid_code");

            csrf = await GetCsrfTokenAsync();
            using var confirmed = await PostJsonAsync(
                "/auth/password-resets/confirm",
                new { username = "client", code },
                csrf);
            await AssertCodeAsync(confirmed, HttpStatusCode.OK, "password_reset_confirmed");

            await CreateAdminAndSignInAsync();

            using var requests = await ReadLogsAsync("recordTypes=password_reset_request");
            var requestRows = Rows(requests);
            Assert.Equal(2, requestRows.Length);
            Assert.Single(requestRows, row =>
                row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString()!.Contains("Password reset requested"));
            Assert.Single(requestRows, row =>
                !row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString()!.Contains("unknown pair"));

            using var confirms = await ReadLogsAsync("recordTypes=password_reset_confirm");
            var confirmRows = Rows(confirms);
            Assert.Equal(2, confirmRows.Length);
            Assert.Single(confirmRows, row => row.GetProperty("isSuccess").GetBoolean());
            Assert.Single(confirmRows, row =>
                !row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString()!.Contains("invalid code"));

            // The guard clear inside the reset confirmation writes no login_guard_change row.
            using var guardRows = await ReadLogsAsync("recordTypes=login_guard_change");
            Assert.Empty(Rows(guardRows));
        }

        [Fact]
        public async Task AProfileChangeIsRecordedByKindAndATimeZoneChangeWritesNothing()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var submitted = await PatchJsonAsync(
                "/auth/profile",
                new { currentPassword = Password, username = "renamed" },
                csrf);
            await AssertCodeAsync(submitted, HttpStatusCode.OK, "profile_change_submitted");

            var code = Fixture.Email.CodeFor("client@example.test");
            Assert.NotNull(code);

            csrf = await GetCsrfTokenAsync();
            using var wrong = await PostJsonAsync(
                "/auth/profile/confirm",
                new { code = "000000" },
                csrf);
            await AssertCodeAsync(wrong, HttpStatusCode.BadRequest, "invalid_code");

            csrf = await GetCsrfTokenAsync();
            using var confirmed = await PostJsonAsync(
                "/auth/profile/confirm",
                new { code },
                csrf);
            await AssertCodeAsync(confirmed, HttpStatusCode.OK, "profile_change_confirmed");

            // A display preference is not worth an audit row.
            var before = await CountLogsAsync();
            csrf = await GetCsrfTokenAsync();
            using var timeZone = await PatchJsonAsync(
                "/auth/profile",
                new { timeZone = "UTC" },
                csrf);
            await AssertCodeAsync(timeZone, HttpStatusCode.OK, "profile_updated");
            Assert.Equal(before, await CountLogsAsync());

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=username_change");
            var rows = Rows(logs);

            // Three attempts: the request, the refused confirmation, the successful confirmation.
            Assert.Equal(3, rows.Length);

            var refusedRow = Assert.Single(rows, row => !row.GetProperty("isSuccess").GetBoolean());
            Assert.Contains(
                "confirmation failed (invalid code)",
                refusedRow.GetProperty("description").GetString());

            Assert.Contains(rows, row =>
                row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString() == "Username change requested: client");

            Assert.Contains(rows, row =>
                row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString()
                    == "Username changed: client | client \u2192 renamed");
        }

        [Fact]
        public async Task AProfileRefusalIsRecordedAndNeverCarriesAPassword()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var refused = await PatchJsonAsync(
                "/auth/profile",
                new { currentPassword = "WrongPassword1", newPassword = "NewPassword2" },
                csrf);
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "invalid_current_password");

            csrf = await GetCsrfTokenAsync();
            using var submitted = await PatchJsonAsync(
                "/auth/profile",
                new { currentPassword = Password, newPassword = "NewPassword2" },
                csrf);
            await AssertCodeAsync(submitted, HttpStatusCode.OK, "profile_change_submitted");

            var code = Fixture.Email.CodeFor("client@example.test");
            Assert.NotNull(code);

            csrf = await GetCsrfTokenAsync();
            using var confirmed = await PostJsonAsync(
                "/auth/profile/confirm",
                new { code },
                csrf);
            await AssertCodeAsync(confirmed, HttpStatusCode.OK, "profile_change_confirmed");

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=password_change");
            var rows = Rows(logs);
            Assert.Equal(3, rows.Length);

            // A wrong current password, the staged request, and the confirmation.
            Assert.Single(rows, row =>
                row.GetProperty("description").GetString() == "Password change refused (wrong password): client");
            Assert.Single(rows, row =>
                row.GetProperty("description").GetString() == "Password change requested: client");
            Assert.Single(rows, row =>
                row.GetProperty("description").GetString() == "Password changed: client");

            // Never a password: neither the one the account signed in with, nor the new one, nor
            // a hash, appears anywhere in the table.
            using var everything = await ReadLogsAsync(string.Empty);
            foreach (var row in Rows(everything))
            {
                var text = row.GetProperty("description").GetString()!;
                Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
                Assert.DoesNotContain("NewPassword2", text, StringComparison.Ordinal);
                Assert.DoesNotContain("$2", text, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task AProfileRequestThatLosesTheStagingRaceIsRecordedLikeTheOneThatWon()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            await using var connection = await Fixture.OpenConnectionAsync();
            const string trigger = "trg_pending_change_forced_race";

            try
            {
                // The unique index is what turns two simultaneous requests into one winner, and an
                // HTTP test cannot hold the window between the staged read and the save open. The
                // trigger raises the same SQLSTATE under the same constraint name, so the service
                // takes the branch a real loser takes and answers what it answers.
                await TestDataSeeder.InstallPendingChangeRaceTriggerAsync(
                    connection,
                    trigger,
                    TestContext.Current.CancellationToken);

                var csrf = await GetCsrfTokenAsync();
                using var lost = await PatchJsonAsync(
                    "/auth/profile",
                    new { currentPassword = Password, username = "renamed" },
                    csrf);
                await AssertCodeAsync(lost, HttpStatusCode.OK, "profile_change_submitted");
            }
            finally
            {
                await TestDataSeeder.DropPendingChangeRaceTriggerAsync(
                    connection,
                    trigger,
                    TestContext.Current.CancellationToken);
            }

            // No code was mailed by this request: the row it would have carried the hash for was
            // refused, so the winner's code is the only one that works.
            Assert.Null(Fixture.Email.CodeFor("client@example.test"));

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=username_change");
            var row = SingleRow(logs);

            Assert.True(row.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(
                "Username change requested: client",
                row.GetProperty("description").GetString());
        }

        [Fact]
        public async Task ALoginGuardChangeIsRecordedOnBothBranchesAndAnAdminEditWritesNothing()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            // A wrong current password is a refusal of this flow like any other.
            var csrf = await GetCsrfTokenAsync();
            using var refused = await PatchJsonAsync(
                "/auth/profile",
                new
                {
                    currentPassword = "WrongPassword1",
                    loginGuard = new
                    {
                        geoProtectionEnabled = false,
                        bindSessionToIp = true,
                        allowedIps = Array.Empty<string>(),
                        allowedCountry = (string?)null,
                        allowedAutonomousSystemNumber = (int?)null
                    }
                },
                csrf);
            await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "invalid_current_password");

            csrf = await GetCsrfTokenAsync();
            using var submitted = await PatchJsonAsync(
                "/auth/profile",
                new
                {
                    currentPassword = Password,
                    loginGuard = new
                    {
                        geoProtectionEnabled = false,
                        bindSessionToIp = true,
                        allowedIps = new[] { "192.0.2.0/24" },
                        allowedCountry = "US",
                        allowedAutonomousSystemNumber = 15169
                    }
                },
                csrf);
            await AssertCodeAsync(submitted, HttpStatusCode.OK, "profile_change_submitted");

            var code = Fixture.Email.CodeFor("client@example.test");
            Assert.NotNull(code);

            csrf = await GetCsrfTokenAsync();
            using var confirmed = await PostJsonAsync(
                "/auth/profile/confirm",
                new { code },
                csrf);
            await AssertCodeAsync(confirmed, HttpStatusCode.OK, "profile_change_confirmed");

            var clientId = await FindUserIdAsync("client");
            await CreateAdminAndSignInAsync();

            // An admin editing the same account's guard is not the account's own change: it
            // writes no row of the user-side audit log.
            var before = await CountGuardRowsAsync();
            csrf = await GetCsrfTokenAsync();
            using var adminEdit = await PatchJsonAsync(
                $"/admin/users/{clientId}",
                new
                {
                    loginGuard = new
                    {
                        geoProtectionEnabled = false,
                        bindSessionToIp = false,
                        allowedIps = Array.Empty<string>(),
                        allowedCountry = (string?)null,
                        allowedAutonomousSystemNumber = (int?)null
                    }
                },
                csrf);
            await AssertCodeAsync(adminEdit, HttpStatusCode.OK, "user_updated");
            Assert.Equal(before, await CountGuardRowsAsync());

            using var logs = await ReadLogsAsync("recordTypes=login_guard_change");
            var rows = Rows(logs);
            Assert.Equal(3, rows.Length);
            Assert.Single(rows, row =>
                !row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString() == "Login guard change refused (wrong password): client");
            Assert.Contains(rows, row =>
                row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString() == "Login guard change confirmed: client");
            Assert.Contains(rows, row =>
                row.GetProperty("isSuccess").GetBoolean()
                && row.GetProperty("description").GetString() == "Login guard change requested: client");
        }

        [Fact]
        public async Task LogoutIsAlwaysRecordedAsASuccessForTheAccountInTheToken()
        {
            var userId = await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync("/auth/logout", new { }, csrf);
            await AssertCodeAsync(response, HttpStatusCode.OK, "signed_out");

            await CreateAdminAndSignInAsync();

            using var logs = await ReadLogsAsync("recordTypes=logout");
            var row = SingleRow(logs);
            Assert.True(row.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(userId.ToString(), row.GetProperty("accountId").GetString());
            Assert.Equal("client", row.GetProperty("username").GetString());
            Assert.Equal("Logged out: client", row.GetProperty("description").GetString());
        }

        [Fact]
        public async Task ThePageDefaultsToNewestFirstAndIsCountedUnderItsFilter()
        {
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();
            var older = await TestDataSeeder.PlantUserLogAsync(
                connection,
                null,
                UserLogRecordType.Login,
                false,
                "Login failed (no such account): older",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                cancellationToken: TestContext.Current.CancellationToken);
            var newer = await TestDataSeeder.PlantUserLogAsync(
                connection,
                null,
                UserLogRecordType.Login,
                false,
                "Login failed (no such account): newer",
                new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                cancellationToken: TestContext.Current.CancellationToken);

            using var response = await ReadLogsAsync(string.Empty);
            Assert.True(response.RootElement.GetProperty("total").GetInt32() >= 2);
            Assert.Equal(20, response.RootElement.GetProperty("pageSize").GetInt32());
            Assert.True(response.RootElement.GetProperty("items")[0].GetProperty("createdAt").GetDateTimeOffset()
                >= response.RootElement.GetProperty("items")[1].GetProperty("createdAt").GetDateTimeOffset());

            using var filtered = await ReadLogsAsync("search=no%20such%20account");
            var body = filtered.RootElement;
            Assert.Equal(2, body.GetProperty("total").GetInt32());
            Assert.Equal([newer, older], Ids(body.GetProperty("items")));
        }

        [Fact]
        public async Task ThePageBoundsAndAFilterElementAreRefusedUnderTheirOwnNames()
        {
            await CreateAdminAndSignInAsync();

            await AssertRefusedAsync("page=0", "page");
            await AssertRefusedAsync("pageSize=1001", "pageSize");
            await AssertRefusedAsync($"search={new string('x', 201)}", "search");
            await AssertRefusedAsync("userIds=not-a-uuid", "userIds");
            await AssertRefusedAsync("groupIds=not-a-uuid", "groupIds");
            await AssertRefusedAsync("recordTypes=sign_in", "recordTypes");
            await AssertRefusedAsync($"ip={new string('1', 46)}", "ip");
            await AssertRefusedAsync($"isp={new string('i', 256)}", "isp");
            await AssertRefusedAsync("country=XX", "country");
            await AssertRefusedAsync("country=Germany", "country");
            await AssertRefusedAsync("dateFrom=yesterday", "dateFrom");
            await AssertRefusedAsync("dateTo=yesterday", "dateTo");
            await AssertRefusedAsync("sortBy=username", "sortBy");
            await AssertRefusedAsync("sortOrder=upwards", "sortOrder");
        }

        [Fact]
        public async Task AnInvertedRangeIsAValidQueryAndAnswersAnEmptyPage()
        {
            await CreateAdminAndSignInAsync();

            using var response = await GetLogsAsync(
                "dateFrom=2026-05-02T00:00:00Z&dateTo=2026-05-01T00:00:00Z");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal(0, body.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        }

        [Fact]
        public async Task TheTotalIsStableAcrossPageTurnsAndTheTieBreakKeepsPagesDisjoint()
        {
            await CreateAdminAndSignInAsync();

            await using var connection = await Fixture.OpenConnectionAsync();
            var wroteAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
            var ids = new[]
            {
                await TestDataSeeder.PlantUserLogAsync(
                    connection, null, UserLogRecordType.Login, false,
                    "Login failed (no such account): one", wroteAt,
                    cancellationToken: TestContext.Current.CancellationToken),
                await TestDataSeeder.PlantUserLogAsync(
                    connection, null, UserLogRecordType.Login, false,
                    "Login failed (no such account): two", wroteAt,
                    cancellationToken: TestContext.Current.CancellationToken),
                await TestDataSeeder.PlantUserLogAsync(
                    connection, null, UserLogRecordType.Login, false,
                    "Login failed (no such account): three", wroteAt,
                    cancellationToken: TestContext.Current.CancellationToken)
            };

            var seen = new List<Guid>();

            for (var page = 1; page <= 3; page++)
            {
                using var body = await ReadLogsAsync(
                    $"search=no%20such%20account&page={page}&pageSize=1");

                Assert.Equal(3, body.RootElement.GetProperty("total").GetInt32());
                seen.AddRange(Ids(body.RootElement.GetProperty("items")));
            }

            Assert.Equal(3, seen.Count);
            Assert.Equal(3, seen.Distinct().Count());
            Assert.Equal(ids.OrderBy(id => id).ToArray(), seen.OrderBy(id => id).ToArray());

            // A page past the end reports the true total and no rows.
            using var beyond = await ReadLogsAsync("search=no%20such%20account&page=9&pageSize=2");
            Assert.Equal(3, beyond.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(beyond.RootElement.GetProperty("items").EnumerateArray());
        }

        [Fact]
        public async Task TheFiltersComposeAndAGroupFilterFollowsTheCurrentGroupOnly()
        {
            var firstGroup = await CreateGroupAsync("First", UserRole.Client);
            var secondGroup = await CreateGroupAsync("Second", UserRole.Client);

            var movedId = await CreateUserAsync(
                UserRole.Client,
                "moved",
                Password,
                groupId: firstGroup);
            var otherId = await CreateUserAsync(
                UserRole.Client,
                "other",
                Password,
                groupId: secondGroup);

            using var movedClient = Fixture.CreateClient();
            using var otherClient = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "moved", Password, movedClient);
            await SignInAsync(UserRole.Client, "other", Password, otherClient);

            await CreateAdminAndSignInAsync();

            // A repeated filter: the two accounts by identifier.
            using var byUser = await ReadLogsAsync(
                $"recordTypes=login&userIds={movedId}&userIds={otherId}");
            Assert.Equal(2, byUser.RootElement.GetProperty("total").GetInt32());

            using var byFirstGroup = await ReadLogsAsync(
                $"recordTypes=login&groupIds={firstGroup}");
            Assert.Equal(1, byFirstGroup.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                movedId.ToString(),
                SingleRow(byFirstGroup).GetProperty("accountId").GetString());

            // Compose a record type with a search that only one of the two carries.
            using var composed = await ReadLogsAsync(
                "recordTypes=login&search=Login%20succeeded&userIds=" + otherId);
            Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(
                otherId.ToString(),
                SingleRow(composed).GetProperty("accountId").GetString()!);

            // Moving the account changes which group its rows belong to: the filter asks about
            // the account's current group, not about the group it held when the row was written.
            var csrf = await GetCsrfTokenAsync();
            using var moved = await PatchJsonAsync(
                $"/admin/users/{movedId}",
                new { groupId = secondGroup.ToString() },
                csrf);
            await AssertCodeAsync(moved, HttpStatusCode.OK, "user_updated");

            using var noLongerFirst = await ReadLogsAsync(
                $"recordTypes=login&groupIds={firstGroup}");
            Assert.Equal(0, noLongerFirst.RootElement.GetProperty("total").GetInt32());

            using var nowSecond = await ReadLogsAsync(
                $"recordTypes=login&groupIds={secondGroup}");
            Assert.Equal(2, nowSecond.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task TheNetworkFiltersAreExactCanonicalAndNeverMatchANullColumn()
        {
            await CreateAdminAndSignInAsync();

            var wroteAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

            await using var connection = await Fixture.OpenConnectionAsync();

            var german = await TestDataSeeder.PlantUserLogAsync(
                connection,
                null,
                UserLogRecordType.Login,
                false,
                "Login failed (no such account): german",
                wroteAt,
                ip: "198.51.100.1",
                isp: "Alpha Net",
                countryCode: "DE",
                cancellationToken: TestContext.Current.CancellationToken);
            var french = await TestDataSeeder.PlantUserLogAsync(
                connection,
                null,
                UserLogRecordType.Login,
                false,
                "Login failed (no such account): french",
                wroteAt.AddMinutes(1),
                ip: "198.51.100.2",
                isp: "Beta Net",
                countryCode: "FR",
                cancellationToken: TestContext.Current.CancellationToken);
            var unknownNetwork = await TestDataSeeder.PlantUserLogAsync(
                connection,
                null,
                UserLogRecordType.Login,
                false,
                "Login failed (no such account): unknown network",
                wroteAt.AddMinutes(2),
                ip: null,
                isp: null,
                countryCode: null,
                cancellationToken: TestContext.Current.CancellationToken);

            // The address is compared exactly, case included.
            using (var byIp = await ReadLogsAsync("ip=198.51.100.1"))
            {
                Assert.Equal(1, byIp.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(german, Assert.Single(Ids(byIp.RootElement.GetProperty("items"))));
            }

            // The provider is compared exactly, too.
            using (var byIsp = await ReadLogsAsync("isp=Beta%20Net"))
            {
                Assert.Equal(1, byIsp.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(french, Assert.Single(Ids(byIsp.RootElement.GetProperty("items"))));
            }

            // Case is part of the value: the stored provider is not folded, so neither is the
            // filter - the same rule the connection log's `isp` and `userAgent` follow.
            using (var otherCase = await ReadLogsAsync("isp=beta%20net"))
            {
                Assert.Equal(0, otherCase.RootElement.GetProperty("total").GetInt32());
            }

            // The country is canonicalised: a typed lower-case code finds the stored upper-case
            // one, and it never matches the row whose column is null.
            using (var byCountry = await ReadLogsAsync("country=de"))
            {
                Assert.Equal(1, byCountry.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(german, Assert.Single(Ids(byCountry.RootElement.GetProperty("items"))));
            }

            // A null network column never matches a bound: the unknown-network row is not in any
            // filtered answer, and the three filters composed with the record type still narrow
            // to the row that carries all three facts.
            using (var composed = await ReadLogsAsync(
                "recordTypes=login&ip=198.51.100.2&isp=Beta%20Net&country=fr"))
            {
                Assert.Equal(1, composed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(french, Assert.Single(Ids(composed.RootElement.GetProperty("items"))));
            }

            using (var nullNeverMatches = await ReadLogsAsync(
                "search=unknown%20network&country=DE"))
            {
                Assert.Equal(0, nullNeverMatches.RootElement.GetProperty("total").GetInt32());
                Assert.Empty(nullNeverMatches.RootElement.GetProperty("items").EnumerateArray());
            }

            // The row itself exists and is reachable when no network filter is named, so the
            // zero above is the null column rather than a missing row.
            using (var unfiltered = await ReadLogsAsync("search=unknown%20network"))
            {
                Assert.Equal(1, unfiltered.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    unknownNetwork,
                    Assert.Single(Ids(unfiltered.RootElement.GetProperty("items"))));
            }

            // A blank value means "no filter", the same rule the other filters follow.
            using (var blank = await ReadLogsAsync("ip=&isp=%20&country="))
            {
                Assert.True(blank.RootElement.GetProperty("total").GetInt32() >= 3);
            }
        }

        [Fact]
        public async Task ClearingWipesEverythingIgnoresTheFiltersAndNeedsTheCsrfPair()
        {
            await CreateAdminAndSignInAsync();
            await CreateUserAsync(UserRole.Client, "client", Password);
            using var client = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "client", Password, client);

            var before = await CountLogsAsync();
            Assert.True(before > 0);

            using var noCsrf = await Client.DeleteAsync(
                "/admin/user-logs",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
            Assert.Equal(before, await CountLogsAsync());

            using var cleared = await DeleteAsync(
                "/admin/user-logs?recordTypes=login&search=nomatch",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "user_logs_cleared");
            using (var body = await ReadJsonAsync(cleared))
            {
                Assert.Equal(
                    $"Cleared the user log: {before} row(s) deleted.",
                    body.RootElement.GetProperty("message").GetString());
            }

            Assert.Equal(0, await CountLogsAsync());

            using var again = await DeleteAsync("/admin/user-logs", await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.OK, "user_logs_cleared");
            using var againBody = await ReadJsonAsync(again);
            Assert.Equal(
                "Cleared the user log: 0 row(s) deleted.",
                againBody.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task TheEndpointsAreRefusedWithoutTheGrantAndToEveryOtherRole()
        {
            // An administrator holding only the client grants may neither read nor wipe the log.
            var readersGroup = await CreateGroupAsync(
                "Readers",
                UserRole.Admin,
                permissions: UserPermission.ViewClients);
            await CreateUserAsync(UserRole.Admin, "reader", Password, groupId: readersGroup);

            using var reader = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "reader", Password, reader);

            using (var list = await reader.GetAsync(
                "/admin/user-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(list, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var clear = await DeleteAsync(
                "/admin/user-logs",
                await GetCsrfTokenAsync(reader),
                reader))
            {
                await AssertCodeAsync(clear, HttpStatusCode.Forbidden, "permission_denied");
            }

            // The grant on the base Administrators group reads the log.
            await CreateAdminAndSignInAsync();
            using var allowed = await ReadLogsAsync(string.Empty);
            Assert.True(allowed.RootElement.GetProperty("total").GetInt32() > 0);

            // A client and a reseller are refused by the role gate ahead of any permission.
            using var client = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Client, "viewer", client: client);
            using (var refusedClient = await client.GetAsync(
                "/admin/user-logs",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedClient, HttpStatusCode.Forbidden, "auth_role_changed");
            }

            using var reseller = Fixture.CreateClient();
            await CreateAndSignInAsync(UserRole.Reseller, "seller", client: reseller);
            using var refusedReseller = await reseller.GetAsync(
                "/admin/user-logs",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(refusedReseller, HttpStatusCode.Forbidden, "auth_role_changed");
        }

        [Fact]
        public async Task AFailedAuditWriteLeavesTheLoginAndTheRegistrationAnswerUnchanged()
        {
            await CreateUserAsync(UserRole.Client, "client", Password);

            await using var connection = await Fixture.OpenConnectionAsync();
            const string trigger = "trg_user_log_forced_failure";

            try
            {
                await TestDataSeeder.InstallUserLogFailureTriggerAsync(
                    connection,
                    trigger,
                    TestContext.Current.CancellationToken);

                var csrf = await GetCsrfTokenAsync();
                using var login = await PostJsonAsync(
                    "/auth/client-login",
                    new { username = "client", password = Password },
                    csrf);
                await AssertCodeAsync(login, HttpStatusCode.OK, "authenticated");
                AssertCookie(login, AccessCookie);

                csrf = await GetCsrfTokenAsync();
                using var registration = await PostJsonAsync(
                    "/auth/registrations",
                    new
                    {
                        username = "new-account",
                        email = "new.account@example.test",
                        password = Password
                    },
                    csrf);
                await AssertCodeAsync(registration, HttpStatusCode.Created, "registration_submitted");

                // The profile flow saves again after the service answers - it mints a fresh token
                // pair - so this is the case the "record last" rule used to make unsafe: the log
                // row has to leave the change tracker with the failed save, or the next save of the
                // same request retries the insert and turns a success into a 500.
                csrf = await GetCsrfTokenAsync();
                using var profile = await PatchJsonAsync(
                    "/auth/profile",
                    new { currentPassword = Password, username = "renamed" },
                    csrf);
                await AssertCodeAsync(profile, HttpStatusCode.OK, "profile_change_submitted");
            }
            finally
            {
                await TestDataSeeder.DropUserLogFailureTriggerAsync(
                    connection,
                    trigger,
                    TestContext.Current.CancellationToken);
            }

            // The account and the staged registration are facts; only the audit rows are lost.
            Assert.NotNull(await FindUserIdAsync("client"));
            Assert.NotNull(await FindUserIdAsync("new-account"));
        }

        private async Task<HttpResponseMessage> RegisterAsync(string username, string email)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync(
                "/auth/registrations",
                new { username, email, password = Password },
                csrf);
        }

        private async Task<HttpResponseMessage> ConfirmRegistrationAsync(string email, string? code)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync(
                "/auth/registrations/confirm",
                new { email, code = code ?? string.Empty },
                csrf);
        }

        private Task<HttpResponseMessage> GetLogsAsync(string query)
            => Client.GetAsync(
                query.Length == 0 ? "/admin/user-logs" : $"/admin/user-logs?{query}",
                TestContext.Current.CancellationToken);

        private async Task<JsonDocument> ReadLogsAsync(string query)
        {
            using var response = await GetLogsAsync(query);
            response.EnsureSuccessStatusCode();

            return await ReadJsonAsync(response);
        }

        private async Task AssertRefusedAsync(string query, string parameter)
        {
            using var response = await GetLogsAsync(query);
            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(response);
            Assert.True(
                body.RootElement.GetProperty("errors").TryGetProperty(parameter, out _),
                $"no errors.{parameter}: {body.RootElement}");
        }

        private async Task<int> CountLogsAsync()
        {
            await using var connection = await Fixture.OpenConnectionAsync();

            return await TestDataSeeder.CountUserLogsAsync(
                connection,
                TestContext.Current.CancellationToken);
        }

        private async Task<int> CountGuardRowsAsync()
        {
            await using var connection = await Fixture.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select count(*) from user_logs where record_type = @type",
                connection);
            command.Parameters.AddWithValue("type", (short)UserLogRecordType.LoginGuardChange);

            return Convert.ToInt32(await command.ExecuteScalarAsync(
                TestContext.Current.CancellationToken));
        }

        private async Task<Guid?> FindUserIdAsync(string username)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var account = await users.FindByUsernameAsync(
                username,
                TestContext.Current.CancellationToken);

            return account?.Id;
        }

        private static JsonElement SingleRow(JsonDocument document)
        {
            var rows = Rows(document);

            Assert.Single(rows);

            return rows[0];
        }

        private static JsonElement[] Rows(JsonDocument document)
            => document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        private static Guid[] Ids(JsonElement items)
            => items
                .EnumerateArray()
                .Select(row => Guid.Parse(row.GetProperty("id").GetString()!))
                .ToArray();
    }
}
