using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Profile
{
    public sealed class ProfileTests : IntegrationTestBase
    {
        public ProfileTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ProfileReportsTheAccountAndItsGuard()
        {
            await CreateUserAsync(
                UserRole.Client,
                "client",
                "Password1",
                email: "client@example.test",
                timeZone: "UTC");
            await SignInAsync(UserRole.Client, "client", "Password1");

            using var response = await Client.GetAsync("/auth/profile", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("client", body.RootElement.GetProperty("username").GetString());
            Assert.Equal("client@example.test", body.RootElement.GetProperty("email").GetString());
            Assert.Equal(nameof(UserRole.Client), body.RootElement.GetProperty("role").GetString());
            Assert.Equal("UTC", body.RootElement.GetProperty("timeZone").GetString());
            Assert.False(body.RootElement.GetProperty("geoProtectionEnabled").GetBoolean());
            Assert.False(body.RootElement.GetProperty("bindSessionToIp").GetBoolean());
            Assert.Empty(body.RootElement.GetProperty("allowedIps").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("allowedCountry").ValueKind);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        }

        [Fact]
        public async Task TimeZoneCanBeSetAndClearedWithoutTheCurrentPassword()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var set = await PatchJsonAsync(
                "/auth/profile",
                new { timeZone = "UTC" },
                csrf);
            await AssertCodeAsync(set, HttpStatusCode.OK, "profile_updated");

            using var withZone = await Client.GetAsync("/auth/profile", TestContext.Current.CancellationToken);
            using var withZoneBody = await ReadJsonAsync(withZone);
            Assert.Equal("UTC", withZoneBody.RootElement.GetProperty("timeZone").GetString());

            csrf = await GetCsrfTokenAsync();
            using var clear = await PatchJsonAsync(
                "/auth/profile",
                new { timeZone = string.Empty },
                csrf);
            await AssertCodeAsync(clear, HttpStatusCode.OK, "profile_updated");

            using var cleared = await Client.GetAsync("/auth/profile", TestContext.Current.CancellationToken);
            using var clearedBody = await ReadJsonAsync(cleared);
            Assert.Equal(JsonValueKind.Null, clearedBody.RootElement.GetProperty("timeZone").ValueKind);
        }

        [Fact]
        public async Task ASensitiveChangeNeedsTheCurrentPassword()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var missing = await PatchJsonAsync(
                "/auth/profile",
                new { username = "renamed" },
                csrf);
            await AssertCodeAsync(missing, HttpStatusCode.BadRequest, "validation_failed");

            using var missingBody = await ReadJsonAsync(missing);
            Assert.True(missingBody.RootElement.GetProperty("errors").TryGetProperty("currentPassword", out _));

            csrf = await GetCsrfTokenAsync();
            using var wrong = await PatchJsonAsync(
                "/auth/profile",
                new { currentPassword = "WrongPassword1", username = "renamed" },
                csrf);
            await AssertCodeAsync(wrong, HttpStatusCode.BadRequest, "invalid_current_password");
        }

        [Fact]
        public async Task TwoChangeKindsInOneBodyAreRefused()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var response = await PatchJsonAsync(
                "/auth/profile",
                new
                {
                    currentPassword = "Password1",
                    username = "renamed",
                    email = "renamed@example.test"
                },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task UsernameChangeIsMailedAndConfirmed()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var submitted = await PatchJsonAsync(
                "/auth/profile",
                new { currentPassword = "Password1", username = "renamed" },
                csrf);
            await AssertCodeAsync(submitted, HttpStatusCode.OK, "profile_change_submitted");

            var code = Fixture.Email.CodeFor("client@example.test");
            Assert.NotNull(code);

            using var stillOld = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            using var stillOldBody = await ReadJsonAsync(stillOld);
            Assert.Equal("client", stillOldBody.RootElement.GetProperty("username").GetString());

            csrf = await GetCsrfTokenAsync();
            using var confirmed = await PostJsonAsync(
                "/auth/profile/confirm",
                new { code },
                csrf);
            await AssertCodeAsync(confirmed, HttpStatusCode.OK, "profile_change_confirmed");

            using var me = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            using var meBody = await ReadJsonAsync(me);
            Assert.Equal("renamed", meBody.RootElement.GetProperty("username").GetString());

            using var signInOld = Fixture.CreateClient();
            var oldCsrf = await GetCsrfTokenAsync(signInOld);
            using var oldLogin = await PostJsonAsync(
                "/auth/client-login",
                new { username = "client", password = "Password1" },
                oldCsrf,
                signInOld);
            await AssertCodeAsync(oldLogin, HttpStatusCode.Unauthorized, "invalid_credentials");

            using var signInNew = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "renamed", "Password1", signInNew);
        }

        [Fact]
        public async Task LoginGuardIsStagedAndConfirmedAsAWhole()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var submitted = await PatchJsonAsync(
                "/auth/profile",
                new
                {
                    currentPassword = "Password1",
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

            using var profile = await Client.GetAsync("/auth/profile", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(profile);
            Assert.False(body.RootElement.GetProperty("geoProtectionEnabled").GetBoolean());
            Assert.True(body.RootElement.GetProperty("bindSessionToIp").GetBoolean());
            Assert.Equal(
                "192.0.2.0/24",
                body.RootElement.GetProperty("allowedIps")[0].GetString());
            Assert.Equal("US", body.RootElement.GetProperty("allowedCountry").GetString());
            Assert.Equal(
                15169,
                body.RootElement.GetProperty("allowedAutonomousSystemNumber").GetInt32());
        }

        [Fact]
        public async Task AProfileChangeWithAWrongCodeIsRefused()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var submitted = await PatchJsonAsync(
                "/auth/profile",
                new { currentPassword = "Password1", username = "renamed" },
                csrf);
            await AssertCodeAsync(submitted, HttpStatusCode.OK, "profile_change_submitted");

            csrf = await GetCsrfTokenAsync();
            using var wrong = await PostJsonAsync(
                "/auth/profile/confirm",
                new { code = "000000" },
                csrf);

            await AssertCodeAsync(wrong, HttpStatusCode.BadRequest, "invalid_code");
        }

        [Fact]
        public async Task ProfileNeedsASessionAndAntiforgeryForChanges()
        {
            using var anonymous = Fixture.CreateClient();
            using var profile = await anonymous.GetAsync("/auth/profile", TestContext.Current.CancellationToken);
            await AssertCodeAsync(profile, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAndSignInAsync(UserRole.Client, "client");

            using var noCsrf = await PatchJsonAsync(
                "/auth/profile",
                new { timeZone = "UTC" });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
        }
    }
}
