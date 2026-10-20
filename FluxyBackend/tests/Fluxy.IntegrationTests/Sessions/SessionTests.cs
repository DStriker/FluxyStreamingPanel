using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Sessions
{
    public sealed class SessionTests : IntegrationTestBase
    {
        public SessionTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task ActiveSessionsListOneCardPerSignInAndOnlyOneCurrent()
        {
            await CreateUserAsync(UserRole.Client, "client");

            using var first = Fixture.CreateClient();
            using var second = Fixture.CreateClient();

            await SignInAsync(UserRole.Client, "client", "Password1", first);
            await SignInAsync(UserRole.Client, "client", "Password1", second);

            using var response = await first.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

            Assert.Equal(2, items.Length);
            Assert.Single(items, item => item.GetProperty("isCurrent").GetBoolean());
            Assert.Equal(2, items.Select(item => item.GetProperty("id").GetString()).Distinct().Count());
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        }

        [Fact]
        public async Task TheCurrentSessionCannotBeRevoked()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var currentId = await CurrentSessionIdAsync();
            var csrf = await GetCsrfTokenAsync();
            using var response = await DeleteAsync(
                $"/auth/active-sessions/{currentId}",
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "cannot_revoke_current");

            using var list = await Client.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        }

        [Fact]
        public async Task RevokingAnotherSessionStopsItsAccessTokensAtOnce()
        {
            await CreateUserAsync(UserRole.Client, "client");

            using var first = Fixture.CreateClient();
            using var second = Fixture.CreateClient();

            await SignInAsync(UserRole.Client, "client", "Password1", first);
            await SignInAsync(UserRole.Client, "client", "Password1", second);

            using var list = await first.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            var otherId = body.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Single(item => !item.GetProperty("isCurrent").GetBoolean())
                .GetProperty("id")
                .GetString();

            Assert.NotNull(otherId);

            var csrf = await GetCsrfTokenAsync(first);
            using var revoke = await DeleteAsync(
                $"/auth/active-sessions/{otherId}",
                csrf,
                first);

            await AssertCodeAsync(revoke, HttpStatusCode.OK, "session_revoked");

            using var secondMe = await second.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, secondMe.StatusCode);

            using var firstMe = await first.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstMe.StatusCode);
        }

        [Fact]
        public async Task RevokingOthersKeepsOnlyTheCurrentSession()
        {
            await CreateUserAsync(UserRole.Client, "client");

            using var first = Fixture.CreateClient();
            using var second = Fixture.CreateClient();

            await SignInAsync(UserRole.Client, "client", "Password1", first);
            await SignInAsync(UserRole.Client, "client", "Password1", second);

            var csrf = await GetCsrfTokenAsync(first);
            using var response = await DeleteAsync("/auth/active-sessions/others", csrf, first);

            await AssertCodeAsync(response, HttpStatusCode.OK, "other_sessions_revoked");

            using var body = await ReadJsonAsync(response);
            Assert.Equal(1, body.RootElement.GetProperty("revokedCount").GetInt32());

            using var secondMe = await second.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, secondMe.StatusCode);

            using var list = await first.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);
            Assert.Single(listBody.RootElement.GetProperty("items").EnumerateArray());

            csrf = await GetCsrfTokenAsync(first);
            using var again = await DeleteAsync("/auth/active-sessions/others", csrf, first);
            await AssertCodeAsync(again, HttpStatusCode.OK, "other_sessions_revoked");
            using var againBody = await ReadJsonAsync(again);
            Assert.Equal(0, againBody.RootElement.GetProperty("revokedCount").GetInt32());
        }

        [Fact]
        public async Task AForeignUnknownOrExpiredSessionIdIsTheSameNotFound()
        {
            await CreateUserAsync(UserRole.Client, "owner");
            using var owner = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", "Password1", owner);

            await CreateUserAsync(UserRole.Client, "stranger");
            using var stranger = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "stranger", "Password1", stranger);

            using var strangerList = await stranger.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            using var strangerBody = await ReadJsonAsync(strangerList);
            var foreignId = strangerBody.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .First()
                .GetProperty("id")
                .GetString();

            var csrf = await GetCsrfTokenAsync(owner);

            using var foreign = await DeleteAsync($"/auth/active-sessions/{foreignId}", csrf, owner);
            using var unknown = await DeleteAsync(
                $"/auth/active-sessions/{Guid.NewGuid()}",
                csrf,
                owner);

            await AssertCodeAsync(foreign, HttpStatusCode.NotFound, "session_not_found");
            await AssertCodeAsync(unknown, HttpStatusCode.NotFound, "session_not_found");

            using var strangerMe = await stranger.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, strangerMe.StatusCode);

            using var nonGuid = await DeleteAsync("/auth/active-sessions/not-a-guid", csrf, owner);
            await AssertCodeAsync(nonGuid, HttpStatusCode.NotFound, "not_found");
        }

        [Fact]
        public async Task SessionRevokesAndReadsRequireAntiforgeryAndASession()
        {
            using var anonymous = Fixture.CreateClient();
            using var anonymousList = await anonymous.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAndSignInAsync(UserRole.Client, "client");

            using var missingCsrf = await DeleteAsync(
                $"/auth/active-sessions/{Guid.NewGuid()}");
            await AssertCodeAsync(missingCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            using var missingOthersCsrf = await DeleteAsync("/auth/active-sessions/others");
            await AssertCodeAsync(missingOthersCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        [Fact]
        public async Task VisitHistoryIsPagedAndCountsBeforeSlicing()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            var csrf = await GetCsrfTokenAsync();
            using var rotated = await PostJsonAsync("/auth/refresh", new { }, csrf);
            await AssertCodeAsync(rotated, HttpStatusCode.OK, "authenticated");

            using var firstPage = await Client.GetAsync("/auth/sessions?page=1&pageSize=1", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
            using var firstBody = await ReadJsonAsync(firstPage);

            Assert.Equal(2, firstBody.RootElement.GetProperty("total").GetInt32());
            Assert.Single(firstBody.RootElement.GetProperty("items").EnumerateArray());

            using var secondPage = await Client.GetAsync("/auth/sessions?page=2&pageSize=1", TestContext.Current.CancellationToken);
            using var secondBody = await ReadJsonAsync(secondPage);
            Assert.Single(secondBody.RootElement.GetProperty("items").EnumerateArray());

            using var emptyPage = await Client.GetAsync("/auth/sessions?page=3&pageSize=1", TestContext.Current.CancellationToken);
            using var emptyBody = await ReadJsonAsync(emptyPage);
            Assert.Empty(emptyBody.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(2, emptyBody.RootElement.GetProperty("total").GetInt32());
        }

        [Fact]
        public async Task VisitHistorySearchAndSortAreServerSide()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("FluxyIntegration/1.0");
            await CreateAndSignInAsync(UserRole.Client, "client");

            using var matching = await Client.GetAsync("/auth/sessions?search=FluxyIntegration", TestContext.Current.CancellationToken);
            using var matchingBody = await ReadJsonAsync(matching);
            Assert.Equal(1, matchingBody.RootElement.GetProperty("total").GetInt32());

            using var none = await Client.GetAsync("/auth/sessions?search=nothing-matches", TestContext.Current.CancellationToken);
            using var noneBody = await ReadJsonAsync(none);
            Assert.Equal(0, noneBody.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(noneBody.RootElement.GetProperty("items").EnumerateArray());

            using var ascending = await Client.GetAsync("/auth/sessions?sortBy=ip&sortOrder=asc", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ascending.StatusCode);

            using var invalidPage = await Client.GetAsync("/auth/sessions?page=0", TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidPage, HttpStatusCode.BadRequest, "validation_failed");
            using var invalidBody = await ReadJsonAsync(invalidPage);
            Assert.True(invalidBody.RootElement.GetProperty("errors").TryGetProperty("page", out _));

            using var invalidSort = await Client.GetAsync("/auth/sessions?sortBy=organisation", TestContext.Current.CancellationToken);
            await AssertCodeAsync(invalidSort, HttpStatusCode.BadRequest, "validation_failed");
        }

        private async Task<string> CurrentSessionIdAsync()
        {
            using var list = await Client.GetAsync("/auth/active-sessions", TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);

            return body.RootElement
                .GetProperty("items")
                .EnumerateArray()
                .Single(item => item.GetProperty("isCurrent").GetBoolean())
                .GetProperty("id")
                .GetString()
                ?? throw new InvalidOperationException("The active session had no identifier.");
        }
    }
}
