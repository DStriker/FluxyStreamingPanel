using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Auth
{
    public sealed class AuthenticationTests : IntegrationTestBase
    {
        public AuthenticationTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task LoginWithoutAntiforgeryTokenIsRefused()
        {
            await CreateUserAsync(UserRole.Client, "client", "Password1");

            var response = await PostJsonAsync(
                "/auth/client-login",
                new { username = "client", password = "Password1" });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        [Fact]
        public async Task WrongPasswordAndUnknownAccountGetTheSameRefusal()
        {
            await CreateUserAsync(UserRole.Client, "client", "Password1");

            var csrf = await GetCsrfTokenAsync();
            using var wrongPassword = await PostJsonAsync(
                "/auth/client-login",
                new { username = "client", password = "WrongPassword1" },
                csrf);

            csrf = await GetCsrfTokenAsync();
            using var unknownAccount = await PostJsonAsync(
                "/auth/client-login",
                new { username = "nobody", password = "WrongPassword1" },
                csrf);

            await AssertCodeAsync(wrongPassword, HttpStatusCode.Unauthorized, "invalid_credentials");
            await AssertCodeAsync(unknownAccount, HttpStatusCode.Unauthorized, "invalid_credentials");

            using var wrongBody = await ReadJsonAsync(wrongPassword);
            using var unknownBody = await ReadJsonAsync(unknownAccount);

            Assert.Equal(MessageOf(wrongBody), MessageOf(unknownBody));
            AssertCookie(wrongPassword, AccessCookie, present: false);
            AssertCookie(unknownAccount, AccessCookie, present: false);
        }

        [Fact]
        public async Task AClientCannotUseTheAdminDoor()
        {
            await CreateUserAsync(UserRole.Client, "client", "Password1");

            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/auth/admin-login",
                new { username = "client", password = "Password1" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
        }

        [Fact]
        public async Task EachRoleSignsInAtItsOwnDoorAndMeReportsTheAccount()
        {
            foreach (var (role, path) in new[]
                     {
                         (UserRole.Client, "/auth/client-login"),
                         (UserRole.Reseller, "/auth/reseller-login"),
                         (UserRole.Admin, "/auth/admin-login")
                     })
            {
                var username = role.ToString().ToLowerInvariant();
                await CreateUserAsync(role, username, "Password1");

                using var client = Fixture.CreateClient();
                var csrf = await GetCsrfTokenAsync(client);

                using var login = await PostJsonAsync(
                    path,
                    new { username, password = "Password1" },
                    csrf,
                    client);

                await AssertCodeAsync(login, HttpStatusCode.OK, "authenticated");
                AssertCookie(login, AccessCookie);
                AssertCookie(login, RefreshCookie);

                using var me = await client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, me.StatusCode);

                using var body = await ReadJsonAsync(me);
                Assert.Equal(username, body.RootElement.GetProperty("username").GetString());
                Assert.Equal(role.ToString(), body.RootElement.GetProperty("role").GetString());
                Assert.Null(CodeOf(body));
            }
        }

        [Fact]
        public async Task MeWithoutSessionIsAuthRequired()
        {
            using var response = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "auth_required");
        }

        [Fact]
        public async Task LogoutRequiresSessionAndThenEndsIt()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync("/auth/logout", new { }, csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "signed_out");
            AssertCookie(response, AccessCookie, present: false);
            AssertCookie(response, RefreshCookie, present: false);

            using var me = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            await AssertCodeAsync(me, HttpStatusCode.Unauthorized, "auth_required");

            using var anonymous = Fixture.CreateClient();
            using var noSession = await anonymous.PostAsync("/auth/logout", content: null, TestContext.Current.CancellationToken);
            await AssertCodeAsync(noSession, HttpStatusCode.Unauthorized, "auth_required");
        }

        [Fact]
        public async Task LogoutWithoutAntiforgeryTokenIsRefused()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            using var response = await PostJsonAsync("/auth/logout", new { });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        [Fact]
        public async Task ReplayingASpentRefreshTokenEndsTheWholeSession()
        {
            var (client, cookies) = CreateCookieClient();
            using (client)
            {
                await CreateUserAsync(UserRole.Client, "client");
                await SignInAsync(UserRole.Client, "client", "Password1", client);

                var spentRefresh = CookieValue(cookies, RefreshCookie);
                Assert.False(string.IsNullOrEmpty(spentRefresh));

                var csrf = await GetCsrfTokenAsync(client);
                using var firstRefresh = await PostJsonAsync("/auth/refresh", new { }, csrf, client);
                await AssertCodeAsync(firstRefresh, HttpStatusCode.OK, "authenticated");

                var rotatedRefresh = CookieValue(cookies, RefreshCookie);
                Assert.NotEqual(spentRefresh, rotatedRefresh);

                var (replay, replayCookies) = CreateCookieClient();
                using (replay)
                {
                    replayCookies.Add(new Cookie(RefreshCookie, spentRefresh, "/", "localhost"));
                    var replayCsrf = await GetCsrfTokenAsync(replay);

                    using var replayResponse = await PostJsonAsync(
                        "/auth/refresh",
                        new { },
                        replayCsrf,
                        replay);

                    await AssertCodeAsync(
                        replayResponse,
                        HttpStatusCode.Unauthorized,
                        "session_expired");
                }

                var (afterReplay, afterReplayCookies) = CreateCookieClient();
                using (afterReplay)
                {
                    afterReplayCookies.Add(new Cookie(RefreshCookie, rotatedRefresh, "/", "localhost"));
                    var afterReplayCsrf = await GetCsrfTokenAsync(afterReplay);

                    using var response = await PostJsonAsync(
                        "/auth/refresh",
                        new { },
                        afterReplayCsrf,
                        afterReplay);

                    await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "session_expired");
                }
            }
        }

        [Fact]
        public async Task RefreshWithoutTokenIsRefusedWithoutSpendingTheWindow()
        {
            using var response = await PostJsonAsync(
                "/auth/refresh",
                new { },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "session_expired");
        }

        [Fact]
        public async Task MeReflectsAnAnonymousCallAfterTheAccessCookieExpires()
        {
            var (client, cookies) = CreateCookieClient();
            using (client)
            {
                await CreateUserAsync(UserRole.Client, "client");
                await SignInAsync(UserRole.Client, "client", "Password1", client);

                var access = cookies.GetCookies(new Uri("http://localhost"))[AccessCookie];
                Assert.NotNull(access);
                access.Expired = true;

                using var me = await client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
                await AssertCodeAsync(me, HttpStatusCode.Unauthorized, "auth_required");
            }
        }

        [Fact]
        public async Task SignedInAnswersCarryNoStore()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            using var me = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            using var profile = await Client.GetAsync("/auth/profile", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            Assert.Contains("no-store", me.Headers.CacheControl?.ToString());
            Assert.Contains("no-store", profile.Headers.CacheControl?.ToString());
        }

        [Fact]
        public async Task AuthenticatedAntiforgeryTokenIsBoundToTheAccount()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var authenticatedToken = await GetCsrfTokenAsync();

            using var anonymous = Fixture.CreateClient();
            var anonymousToken = await GetCsrfTokenAsync(anonymous);

            Assert.NotEqual(authenticatedToken, anonymousToken);

            using var response = await PostJsonAsync(
                "/auth/logout",
                new { },
                anonymousToken);

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }
    }
}
