using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fluxy.Core.Authentication.Options;
using Fluxy.Core.Users.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// The setup every integration test needs: a clean database, the base groups, and a client whose
    /// cookie container is its own.
    /// </summary>
    [Collection(FluxyCollection.Name)]
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected IntegrationTestBase(FluxyFixture fixture)
        {
            Fixture = fixture;
        }

        protected FluxyFixture Fixture { get; }

        protected HttpClient Client { get; private set; } = null!;

        public virtual async ValueTask InitializeAsync()
        {
            await Fixture.ResetAsync();
            Client = Fixture.CreateClient();
        }

        public virtual ValueTask DisposeAsync()
        {
            Client.Dispose();

            return ValueTask.CompletedTask;
        }

        protected async Task<string> GetCsrfTokenAsync(HttpClient? client = null)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var response = await (client ?? Client).GetAsync("/auth/csrf", cancellationToken);
            response.EnsureSuccessStatusCode();

            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return body.RootElement.GetProperty("token").GetString()
                ?? throw new InvalidOperationException("The CSRF response carried no token.");
        }

        protected async Task<Guid> CreateUserAsync(
            UserRole role,
            string username = "account",
            string password = "Password1",
            string? email = null,
            UserStatus status = UserStatus.Registered,
            string? timeZone = null,
            Guid? groupId = null)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                username,
                password,
                role,
                status,
                groupId: groupId,
                email: email,
                timeZone: timeZone,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        protected async Task<Guid> CreateGroupAsync(
            string name,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            params UserPermission[] permissions)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateGroupAsync(
                scope.ServiceProvider,
                name,
                role,
                status,
                TestContext.Current.CancellationToken,
                permissions);
        }

        protected async Task<Guid> CreateAndSignInAsync(
            UserRole role,
            string username = "account",
            string password = "Password1",
            HttpClient? client = null)
        {
            var id = await CreateUserAsync(role, username, password);
            await SignInAsync(role, username, password, client);

            return id;
        }

        protected async Task AssertCodeAsync(
            HttpResponseMessage response,
            System.Net.HttpStatusCode status,
            string code)
        {
            Assert.Equal(status, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal(code, CodeOf(body));
        }

        protected async Task<HttpResponseMessage> PostJsonAsync(
            string path,
            object body,
            string? csrfToken = null,
            HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            };

            AddCsrf(request, csrfToken);

            return await (client ?? Client).SendAsync(request, TestContext.Current.CancellationToken);
        }

        protected async Task<HttpResponseMessage> PatchJsonAsync(
            string path,
            object body,
            string? csrfToken = null,
            HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, path)
            {
                Content = JsonContent.Create(body)
            };

            AddCsrf(request, csrfToken);

            return await (client ?? Client).SendAsync(request, TestContext.Current.CancellationToken);
        }

        protected async Task<HttpResponseMessage> PutJsonAsync(
            string path,
            object body,
            string? csrfToken = null,
            HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(body)
            };

            AddCsrf(request, csrfToken);

            return await (client ?? Client).SendAsync(request, TestContext.Current.CancellationToken);
        }

        protected async Task<HttpResponseMessage> DeleteAsync(
            string path,
            string? csrfToken = null,
            HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, path);
            AddCsrf(request, csrfToken);

            return await (client ?? Client).SendAsync(request, TestContext.Current.CancellationToken);
        }

        protected async Task SignInAsync(
            UserRole role,
            string username,
            string password,
            HttpClient? client = null)
        {
            var target = client ?? Client;
            var csrf = await GetCsrfTokenAsync(target);
            var path = role switch
            {
                UserRole.Client => "/auth/client-login",
                UserRole.Reseller => "/auth/reseller-login",
                UserRole.Admin => "/auth/admin-login",
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };

            var response = await PostJsonAsync(
                path,
                new { username, password },
                csrf,
                target);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        protected async Task<Guid> CreateAdminAndSignInAsync(
            string username = "operator",
            string password = "Password1")
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var id = await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                username,
                password,
                UserRole.Admin,
                cancellationToken: TestContext.Current.CancellationToken);

            await SignInAsync(UserRole.Admin, username, password);

            return id;
        }

        protected async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return JsonDocument.Parse(text);
        }

        /// <summary>
        /// Creates a client whose cookie jar the test can inspect and edit, so a spent or foreign
        /// cookie can be replayed deliberately. The default client hides its jar inside the handler.
        /// </summary>
        protected (HttpClient Client, CookieContainer Cookies) CreateCookieClient()
        {
            var cookies = new CookieContainer();
            var handler = new CookieContainerHandler(FactoryServerHandler(), cookies);
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };

            return (client, cookies);
        }

        protected static string? CookieValue(CookieContainer cookies, string name)
            => cookies
                .GetCookies(new Uri("http://localhost"))
                .FirstOrDefault(cookie => cookie.Name == name)
                ?.Value;

        private HttpMessageHandler FactoryServerHandler()
            => Fixture.Factory.Server.CreateHandler();

        protected static string? CodeOf(JsonDocument document)
            => document.RootElement.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;

        protected static string? MessageOf(JsonDocument document)
            => document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;

        protected static string IdFromLocation(HttpResponseMessage response)
        {
            var location = response.Headers.Location?.ToString();
            var id = location?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            Assert.True(Guid.TryParse(id, out _), $"Location did not name a uuid: {location}");

            return id!;
        }

        protected static void AssertCookie(
            HttpResponseMessage response,
            string cookieName,
            bool present = true)
        {
            var raw = response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
                ? values.ToArray()
                : [];
            var cookies = raw.Where(value => value.StartsWith(cookieName + "=", StringComparison.Ordinal))
                .ToArray();

            var live = cookies.Where(value =>
                !value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.True(
                present ? live.Length > 0 : live.Length == 0,
                string.Join(" || ", raw));
        }

        protected static string AccessCookie => AuthenticationOptions.AccessCookieName;

        protected static string RefreshCookie => AuthenticationOptions.RefreshCookieName;

        private static void AddCsrf(HttpRequestMessage request, string? csrfToken)
        {
            if (!string.IsNullOrEmpty(csrfToken))
            {
                request.Headers.Add("X-CSRF-Token", csrfToken);
            }
        }
    }
}
