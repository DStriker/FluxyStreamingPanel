using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.IntegrationTests.Infrastructure;
using Fluxy.IntegrationTests.Infrastructure.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Auth
{
    public sealed class RegistrationTests : IntegrationTestBase
    {
        public RegistrationTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task RegistrationStoresTheAccountAndMailsASixDigitCode()
        {
            const string email = "new.account@example.test";
            const string username = "new-account";

            var response = await RegisterAsync(username, email, "Password1");

            await AssertCodeAsync(response, HttpStatusCode.Created, "registration_submitted");
            Assert.False(response.Headers.Contains("Location"));

            var message = Fixture.Email.LastFor(email);
            Assert.NotNull(message);
            Assert.Matches(@"\b\d{6}\b", message!.Body);

            var code = Fixture.Email.CodeFor(email);
            Assert.NotNull(code);
            Assert.Equal(6, code!.Length);

            using var confirm = await ConfirmAsync(email, code);
            await AssertCodeAsync(confirm, HttpStatusCode.OK, "registration_confirmed");
            AssertCookie(confirm, AccessCookie);
            AssertCookie(confirm, RefreshCookie);

            using var me = await Client.GetAsync("/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            using var body = await ReadJsonAsync(me);
            Assert.Equal(username, body.RootElement.GetProperty("username").GetString());
            Assert.Equal(nameof(UserRole.Client), body.RootElement.GetProperty("role").GetString());
        }

        [Fact]
        public async Task RegistrationWithoutMailConfigurationIsRefusedBeforeTheRowIsStored()
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
            using var response = await PostJsonAsync(
                "/auth/registrations",
                new
                {
                    username = "unmailed",
                    email = "unmailed@example.test",
                    password = "Password1"
                },
                csrf,
                client);

            await AssertCodeAsync(response, HttpStatusCode.ServiceUnavailable, "registration_not_configured");

            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            Assert.Null(await users.FindByUsernameAsync(
                "unmailed",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task RegistrationWithoutAntiforgeryTokenIsRefusedBeforeAnythingIsStored()
        {
            var response = await PostJsonAsync(
                "/auth/registrations",
                new
                {
                    username = "new-account",
                    email = "new.account@example.test",
                    password = "Password1"
                });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
            Assert.Empty(Fixture.Email.Messages);
        }

        [Fact]
        public async Task InvalidRegistrationBodyListsTheOffendingFields()
        {
            var response = await RegisterAsync("ab", "not-an-email", "short");

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(response);
            var errors = body.RootElement.GetProperty("errors");
            Assert.True(errors.TryGetProperty("username", out _));
            Assert.True(errors.TryGetProperty("email", out _));
            Assert.True(errors.TryGetProperty("password", out _));
            Assert.Empty(Fixture.Email.Messages);
        }

        [Fact]
        public async Task ARegisteredUsernameOrEmailCannotBeRegisteredAgain()
        {
            await CreateUserAsync(
                UserRole.Client,
                "taken",
                "Password1",
                email: "taken@example.test");

            using var takenName = await RegisterAsync("taken", "another@example.test", "Password1");
            using var takenEmail = await RegisterAsync("another", "taken@example.test", "Password1");

            await AssertCodeAsync(takenName, HttpStatusCode.Conflict, "user_already_exists");
            await AssertCodeAsync(takenEmail, HttpStatusCode.Conflict, "user_already_exists");
            Assert.Empty(Fixture.Email.Messages);
        }

        [Fact]
        public async Task UnknownAddressAndWrongCodeAreTheSameRefusal()
        {
            var unknown = await ConfirmAsync("nobody@example.test", "123456");
            var response = await RegisterAsync("new-account", "new.account@example.test", "Password1");
            await AssertCodeAsync(response, HttpStatusCode.Created, "registration_submitted");

            var wrong = await ConfirmAsync("new.account@example.test", "000000");

            await AssertCodeAsync(unknown, HttpStatusCode.BadRequest, "invalid_code");
            await AssertCodeAsync(wrong, HttpStatusCode.BadRequest, "invalid_code");

            using var unknownBody = await ReadJsonAsync(unknown);
            using var wrongBody = await ReadJsonAsync(wrong);
            Assert.Equal(MessageOf(unknownBody), MessageOf(wrongBody));
        }

        [Fact]
        public async Task ACodePastItsWindowIsExpiredRatherThanInvalid()
        {
            var response = await RegisterAsync(
                "late-account",
                "late.account@example.test",
                "Password1");
            await AssertCodeAsync(response, HttpStatusCode.Created, "registration_submitted");

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

            using var confirm = await ConfirmAsync("late.account@example.test", code);
            await AssertCodeAsync(confirm, HttpStatusCode.BadRequest, "code_expired");
        }

        [Fact]
        public async Task RegistrationCanTakeOverAnUnregisteredAccount()
        {
            await CreateUserAsync(
                UserRole.Client,
                "pending",
                "OldPassword1",
                email: "pending@example.test",
                status: UserStatus.Unregistered);

            using var response = await RegisterAsync(
                "pending",
                "pending@example.test",
                "NewPassword1");
            await AssertCodeAsync(response, HttpStatusCode.Created, "registration_submitted");

            var code = Fixture.Email.CodeFor("pending@example.test");
            Assert.NotNull(code);

            using var confirm = await ConfirmAsync("pending@example.test", code);
            await AssertCodeAsync(confirm, HttpStatusCode.OK, "registration_confirmed");

            await SignInAsync(UserRole.Client, "pending", "NewPassword1");
        }

        [Fact]
        public async Task ConfirmationWithoutAntiforgeryTokenIsRefused()
        {
            var response = await PostJsonAsync(
                "/auth/registrations/confirm",
                new { email = "nobody@example.test", code = "123456" });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        private async Task<HttpResponseMessage> RegisterAsync(
            string username,
            string email,
            string password)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync(
                "/auth/registrations",
                new { username, email, password },
                csrf);
        }

        private async Task<HttpResponseMessage> ConfirmAsync(string email, string? code)
        {
            var csrf = await GetCsrfTokenAsync();

            return await PostJsonAsync(
                "/auth/registrations/confirm",
                new { email, code = code ?? string.Empty },
                csrf);
        }
    }
}
