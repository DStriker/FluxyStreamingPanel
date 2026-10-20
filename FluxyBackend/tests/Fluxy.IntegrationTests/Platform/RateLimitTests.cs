using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fluxy.IntegrationTests.Platform
{
    public sealed class RateLimitTests : IntegrationTestBase
    {
        public RateLimitTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheRegistrationWindowIsSpentByTheFirstStoredRow()
        {
            using var factory = CreateFactory(new Dictionary<string, string?>
            {
                ["RateLimit:RegisterLimit"] = "1"
            });
            using var client = CreateClient(factory);

            using var first = await RegisterAsync(client, "first", "first@example.test");
            await AssertCodeAsync(first, HttpStatusCode.Created, "registration_submitted");

            using var second = await RegisterAsync(client, "second", "second@example.test");
            await AssertCodeAsync(second, HttpStatusCode.TooManyRequests, "registration_rate_limited");
            Assert.Equal(900, RetryAfterSeconds(second));
        }

        [Fact]
        public async Task ConfirmationAttemptsAreCountedBeforeTheCodeIsChecked()
        {
            using var factory = CreateFactory(new Dictionary<string, string?>
            {
                ["RateLimit:RegisterLimit"] = "100",
                ["RateLimit:ConfirmLimit"] = "2"
            });
            using var client = CreateClient(factory);

            using var registered = await RegisterAsync(client, "confirm", "confirm@example.test");
            await AssertCodeAsync(registered, HttpStatusCode.Created, "registration_submitted");

            using var first = await ConfirmAsync(client, "confirm@example.test", "000000");
            await AssertCodeAsync(first, HttpStatusCode.BadRequest, "invalid_code");

            using var second = await ConfirmAsync(client, "confirm@example.test", "111111");
            await AssertCodeAsync(second, HttpStatusCode.BadRequest, "invalid_code");

            using var third = await ConfirmAsync(client, "confirm@example.test", "222222");
            await AssertCodeAsync(third, HttpStatusCode.TooManyRequests, "confirmation_rate_limited");
            Assert.Equal(900, RetryAfterSeconds(third));
        }

        [Fact]
        public async Task SignInAttemptsAreLimitedPerRoleAndAddress()
        {
            using var factory = CreateFactory(new Dictionary<string, string?>
            {
                ["RateLimit:LoginLimit"] = "2"
            });
            using var client = CreateClient(factory);

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var csrf = await GetCsrfTokenAsync(client);
                using var response = await PostJsonAsync(
                    "/auth/client-login",
                    new { username = "unknown", password = "Password1" },
                    csrf,
                    client);

                await AssertCodeAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
            }

            var thirdCsrf = await GetCsrfTokenAsync(client);
            using var throttled = await PostJsonAsync(
                "/auth/client-login",
                new { username = "unknown", password = "Password1" },
                thirdCsrf,
                client);

            await AssertCodeAsync(throttled, HttpStatusCode.TooManyRequests, "login_rate_limited");
            Assert.Equal(900, RetryAfterSeconds(throttled));
        }

        private static double RetryAfterSeconds(HttpResponseMessage response)
        {
            Assert.NotNull(response.Headers.RetryAfter);

            return response.Headers.RetryAfter!.Delta!.Value.TotalSeconds;
        }

        private FluxyApiFactory CreateFactory(IReadOnlyDictionary<string, string?> settings)
            => new(
                Fixture.PostgresConnectionString,
                Fixture.RedisConnectionString,
                Fixture.Email,
                Fixture.Recaptcha,
                Fixture.GeoIp,
                Fixture.Clock,
                settings);

        private static HttpClient CreateClient(FluxyApiFactory factory)
            => factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        private async Task<HttpResponseMessage> RegisterAsync(
            HttpClient client,
            string username,
            string email)
        {
            var csrf = await GetCsrfTokenAsync(client);

            return await PostJsonAsync(
                "/auth/registrations",
                new { username, email, password = "Password1" },
                csrf,
                client);
        }

        private async Task<HttpResponseMessage> ConfirmAsync(
            HttpClient client,
            string email,
            string code)
        {
            var csrf = await GetCsrfTokenAsync(client);

            return await PostJsonAsync(
                "/auth/registrations/confirm",
                new { email, code },
                csrf,
                client);
        }
    }
}
