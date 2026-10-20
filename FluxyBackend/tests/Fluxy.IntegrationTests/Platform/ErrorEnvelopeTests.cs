using System.Net;
using System.Text;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Platform
{
    public sealed class ErrorEnvelopeTests : IntegrationTestBase
    {
        public ErrorEnvelopeTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task BodylessFrameworkRefusalsAreWrappedInTheEnvelope()
        {
            using var missing = await Client.GetAsync("/this-route-does-not-exist", TestContext.Current.CancellationToken);
            await AssertCodeAsync(missing, HttpStatusCode.NotFound, "not_found");
            Assert.Contains("no-store", missing.Headers.CacheControl!.ToString());

            using var wrongMethod = await Client.GetAsync("/auth/client-login", TestContext.Current.CancellationToken);
            await AssertCodeAsync(wrongMethod, HttpStatusCode.MethodNotAllowed, "method_not_allowed");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/client-login")
            {
                Content = new StringContent(
                    """{"username":"client","password":"Password1"}""",
                    Encoding.UTF8,
                    "text/plain")
            };
            using var unsupported = await Client.SendAsync(request, TestContext.Current.CancellationToken);
            await AssertCodeAsync(unsupported, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        }

        [Fact]
        public async Task MalformedJsonIsAnsweredAsValidationFailed()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/registrations")
            {
                Content = new StringContent("{ not json", Encoding.UTF8, "application/json")
            };

            using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            using var body = await ReadJsonAsync(response);
            Assert.True(body.RootElement.TryGetProperty("errors", out _));
        }

        [Fact]
        public async Task CsrfEndpointWritesTheReadableCopyAndNoStore()
        {
            using var response = await Client.GetAsync("/auth/csrf", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var setCookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
            Assert.Contains("XSRF-TOKEN", setCookie);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        }

        [Fact]
        public async Task AnUnauthenticatedPrivateCallUsesTheAuthorizationEnvelope()
        {
            using var response = await Client.GetAsync("/auth/profile", TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "auth_required");
            using var body = await ReadJsonAsync(response);
            Assert.Equal("auth_required", CodeOf(body));
            Assert.False(string.IsNullOrWhiteSpace(MessageOf(body)));
        }

        [Fact]
        public async Task AClientIsRefusedTheAdminAreaWithTheAuthorizationEnvelope()
        {
            await CreateAndSignInAsync(UserRole.Client, "client");

            using var response = await Client.GetAsync("/admin/user-groups", TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.Forbidden, "auth_role_changed");
            using var body = await ReadJsonAsync(response);
            Assert.Equal("auth_role_changed", CodeOf(body));
        }
    }
}
