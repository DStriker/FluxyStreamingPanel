using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Auth
{
    /// <summary>
    /// The account's own side of the support tickets end to end: the create with its
    /// <c>Location</c>, the own-only list and detail, the foreign identifier answered as not found,
    /// the reply, the read mark and the receipt it stamps, the operator's refusal of the create, the
    /// collection and row fences, the antiforgery pair on every write, and the no-store reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every identifier comes from the token.</b> No body or query names an owner, so the only
    /// way this side could read somebody else's conversation is a ticket identifier it guessed -
    /// which the tests try deliberately and expect to be answered as missing.
    /// </para>
    /// <para>
    /// <b>The account's flag and the operator's are read from two places on purpose.</b> A receipt
    /// the account's read stamps is a fact about an operator message, so it is asserted through the
    /// operator's own detail - the only read whose contract says which side's messages a mark
    /// covers.
    /// </para>
    /// </remarks>
    public sealed class TicketsTests : IntegrationTestBase
    {
        private const string Password = "Password1";

        /// <summary>
        /// Initializes a new instance of the <see cref="TicketsTests"/> class.
        /// </summary>
        /// <param name="fixture">Shared host and containers, reset before every test.</param>
        public TicketsTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheCreateAnswers201WithALocationThatReadsBack()
        {
            await CreateUserAsync(UserRole.Client, "owner");

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            using var created = await PostJsonAsync(
                "/auth/tickets",
                new { subject = "  my printer is on fire  " },
                await GetCsrfTokenAsync(account),
                account);

            await AssertCodeAsync(created, HttpStatusCode.Created, "ticket_created");

            var location = created.Headers.Location?.ToString();

            Assert.NotNull(location);
            Assert.StartsWith("/auth/tickets/", location, StringComparison.Ordinal);

            var ticketId = Guid.Parse(IdFromLocation(created));

            using (var detail = await account.GetAsync(
                location,
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
                Assert.True(detail.Headers.CacheControl?.NoStore);

                using var body = await ReadJsonAsync(detail);

                Assert.Equal(ticketId.ToString(), body.RootElement.GetProperty("id").GetString());

                // The subject is trimmed at the service boundary; the thread starts empty, and the
                // list's last-reply value falls back to the creation instant rather than a null.
                Assert.Equal("my printer is on fire", body.RootElement.GetProperty("subject").GetString());
                Assert.Equal(0, body.RootElement.GetProperty("replies").GetArrayLength());
                Assert.Equal("read", body.RootElement.GetProperty("status").GetString());
                Assert.Equal(
                    body.RootElement.GetProperty("createdAt").GetString(),
                    body.RootElement.GetProperty("lastReplyAt").GetString());
            }

            // The list holds the one row, and its status is the account's own view: the account
            // wrote the subject and has nothing waiting for it.
            using (var list = await account.GetAsync(
                "/auth/tickets",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);
                Assert.True(list.Headers.CacheControl?.NoStore);

                using var body = await ReadJsonAsync(list);

                Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(ticketId, Ids(body).Single());
                Assert.Equal("read", SingleRow(body).GetProperty("status").GetString());
            }
        }

        [Fact]
        public async Task ABlankOrOversizeSubjectAndMessageAreRefusedWithTheirField()
        {
            await CreateUserAsync(UserRole.Client, "owner");

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            var csrf = await GetCsrfTokenAsync(account);

            using (var blank = await PostJsonAsync(
                "/auth/tickets",
                new { subject = "   " },
                csrf,
                account))
            {
                await AssertCodeAsync(blank, HttpStatusCode.BadRequest, "validation_failed");

                using var body = await ReadJsonAsync(blank);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("subject", out _));
            }

            using (var tooLong = await PostJsonAsync(
                "/auth/tickets",
                new { subject = new string('s', 201) },
                csrf,
                account))
            {
                await AssertCodeAsync(tooLong, HttpStatusCode.BadRequest, "validation_failed");

                using var body = await ReadJsonAsync(tooLong);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("subject", out _));
            }

            // Nothing was created by either refusal.
            using (var empty = await account.GetAsync(
                "/auth/tickets",
                TestContext.Current.CancellationToken))
            {
                using var body = await ReadJsonAsync(empty);
                Assert.Equal(0, body.RootElement.GetProperty("total").GetInt32());
            }

            var ticketId = await CreateTicketAsync(account, "a real ticket");

            foreach (var message in new[] { "   ", new string('m', 4001) })
            {
                using var refused = await PostJsonAsync(
                    $"/auth/tickets/{ticketId}/replies",
                    new { message },
                    await GetCsrfTokenAsync(account),
                    account);

                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

                using var body = await ReadJsonAsync(refused);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("message", out _));
            }
        }

        [Fact]
        public async Task TheListAndTheDetailHoldOnlyTheCallersOwnTickets()
        {
            var ownId = await CreateUserAsync(UserRole.Client, "owner");
            var otherId = await CreateUserAsync(UserRole.Client, "other");

            var mine = await PlantAsync(ownId, "my own ticket");
            var theirs = await PlantAsync(otherId, "somebody else's ticket");

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            using (var list = await account.GetAsync(
                "/auth/tickets",
                TestContext.Current.CancellationToken))
            {
                using var body = await ReadJsonAsync(list);

                Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(mine, Ids(body).Single());
            }

            // A foreign identifier is answered exactly like one that names no row: the identifier
            // is the only thing this side can name, and telling it the row exists would turn the
            // route into a probe for other accounts' conversations.
            using (var foreign = await account.GetAsync(
                $"/auth/tickets/{theirs}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(foreign, HttpStatusCode.NotFound, "ticket_not_found");
            }

            using (var unknown = await account.GetAsync(
                $"/auth/tickets/{Guid.NewGuid()}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(unknown, HttpStatusCode.NotFound, "ticket_not_found");
            }

            using (var foreignReply = await PostJsonAsync(
                $"/auth/tickets/{theirs}/replies",
                new { message = "writing into somebody else's thread" },
                await GetCsrfTokenAsync(account),
                account))
            {
                await AssertCodeAsync(
                    foreignReply,
                    HttpStatusCode.NotFound,
                    "ticket_not_found");
            }

            using (var foreignRead = await PostJsonAsync(
                $"/auth/tickets/{theirs}/read",
                new { },
                await GetCsrfTokenAsync(account),
                account))
            {
                await AssertCodeAsync(foreignRead, HttpStatusCode.NotFound, "ticket_not_found");
            }
        }

        [Fact]
        public async Task AResellerNeverSeesAClientsTickets()
        {
            var clientId = await CreateUserAsync(UserRole.Client, "client-owner");
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller-owner");

            var clientTicket = await PlantAsync(clientId, "a client's conversation");
            var resellerTicket = await PlantAsync(resellerId, "a reseller's conversation");

            using var reseller = Fixture.CreateClient();
            await SignInAsync(UserRole.Reseller, "reseller-owner", Password, reseller);

            using (var list = await reseller.GetAsync(
                "/auth/tickets",
                TestContext.Current.CancellationToken))
            {
                using var body = await ReadJsonAsync(list);

                Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(resellerTicket, Ids(body).Single());
            }

            using (var foreign = await reseller.GetAsync(
                $"/auth/tickets/{clientTicket}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(foreign, HttpStatusCode.NotFound, "ticket_not_found");
            }

            // The reseller's own detail is readable: the same route serves both non-operator roles.
            using (var own = await reseller.GetAsync(
                $"/auth/tickets/{resellerTicket}",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            }
        }

        [Fact]
        public async Task TheReadClearsOnlyTheAccountsFlagAndStampsTheOperatorsMessages()
        {
            await CreateAdminAndSignInAsync();

            var ownerId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(ownerId, "a conversation", "the account's question");

            // The operator answers, which raises the account's flag and clears the operator's.
            using (var reply = await PostJsonAsync(
                $"/admin/tickets/{ticketId}/replies",
                new { message = "the operator's answer" },
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(reply, HttpStatusCode.Created, "ticket_reply_created");
            }

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            using (var before = await account.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken))
            {
                using var body = await ReadJsonAsync(before);
                Assert.Equal("unread", body.RootElement.GetProperty("status").GetString());
            }

            // The operator's read raised no receipt of its own; its message is still unread by the
            // account, which is the fact the mark is about to change.
            using (var operatorView = await GetAdminDetailAsync(ticketId))
            {
                Assert.Equal(
                    JsonValueKind.Null,
                    ReplyWith(operatorView, "the operator's answer")
                        .GetProperty("readAt").ValueKind);
            }

            using (var read = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/read",
                new { },
                await GetCsrfTokenAsync(account),
                account))
            {
                await AssertCodeAsync(read, HttpStatusCode.OK, "ticket_marked_read");
            }

            using (var after = await account.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken))
            {
                using var body = await ReadJsonAsync(after);

                // The account's flag cleared; the operator's flag is a different fact and the
                // account's read says nothing about it.
                Assert.Equal("read", body.RootElement.GetProperty("status").GetString());
            }

            using (var afterOperator = await GetAdminDetailAsync(ticketId))
            {
                // The operator's message now carries the account's receipt...
                Assert.NotEqual(
                    JsonValueKind.Null,
                    ReplyWith(afterOperator, "the operator's answer")
                        .GetProperty("readAt").ValueKind);

                // ...and the account's own message does not: a read stamps the other side only.
                Assert.Equal(
                    JsonValueKind.Null,
                    ReplyWith(afterOperator, "the account's question")
                        .GetProperty("readAt").ValueKind);

                // The operator wrote the newest message, so the operator's own flag is clear: a
                // message is read by the side that wrote it. The account's read did not change it.
                Assert.Equal("read", afterOperator.RootElement.GetProperty("status").GetString());
            }

            // A second read finds the flag already clear and no unmarked operator message, and
            // answers the same word.
            using (var readAgain = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/read",
                new { },
                await GetCsrfTokenAsync(account),
                account))
            {
                await AssertCodeAsync(readAgain, HttpStatusCode.OK, "ticket_marked_read");
            }

            using var stillRead = await account.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken);

            using var stillReadBody = await ReadJsonAsync(stillRead);
            Assert.Equal("read", stillReadBody.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task AReplyToAClosedTicketIsRefusedAndWritesNothing()
        {
            await CreateAdminAndSignInAsync();

            var ownerId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(ownerId, "a conversation", "the only message");

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            using (var closed = await PostJsonAsync(
                $"/admin/tickets/{ticketId}/close",
                new { },
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(closed, HttpStatusCode.OK, "ticket_closed");
            }

            using (var refused = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/replies",
                new { message = "trying to answer a finished conversation" },
                await GetCsrfTokenAsync(account),
                account))
            {
                await AssertCodeAsync(refused, HttpStatusCode.Conflict, "ticket_is_closed");
            }

            // The refused message left no row behind, and the closed conversation is still
            // readable by its owner - closing ends the answering, not the reading.
            using (var detail = await account.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

                using var body = await ReadJsonAsync(detail);

                Assert.Equal(1, body.RootElement.GetProperty("replies").GetArrayLength());
                Assert.Equal("closed", body.RootElement.GetProperty("status").GetString());
            }
        }

        [Fact]
        public async Task AnOperatorIsRefusedTheCreateButNotTheReads()
        {
            await CreateAdminAndSignInAsync();
            await CreateUserAsync(UserRole.Client, "owner");
            var someoneElses = await PlantAsync(
                await UserIdAsync("owner"),
                "an account's conversation");

            // The self list is the caller's own queue, and an operator's own queue is empty - it
            // is not a second reading of every account's tickets.
            using (var list = await Client.GetAsync(
                "/auth/tickets",
                TestContext.Current.CancellationToken))
            {
                using var body = await ReadJsonAsync(list);
                Assert.Equal(0, body.RootElement.GetProperty("total").GetInt32());
            }

            // Support is the account's door, and the refusal is the service's so a second endpoint
            // reaching the create cannot forget it.
            using (var create = await PostJsonAsync(
                "/auth/tickets",
                new { subject = "an operator's own ticket" },
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(create, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var foreign = await Client.GetAsync(
                $"/auth/tickets/{someoneElses}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(foreign, HttpStatusCode.NotFound, "ticket_not_found");
            }
        }

        [Fact]
        public async Task TheAccountOwnsNoCloseReopenDeleteOrUnreadAndThosePathsAnswer405()
        {
            var ownerId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(ownerId, "a conversation", "the question");

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            var csrf = await GetCsrfTokenAsync(account);

            foreach (var action in new[] { "close", "reopen", "unread" })
            {
                using var refusal = await PostJsonAsync(
                    $"/auth/tickets/{ticketId}/{action}",
                    new { },
                    csrf,
                    account);

                await AssertCodeAsync(
                    refusal,
                    HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed");

                using var body = await ReadJsonAsync(refusal);
                Assert.False(string.IsNullOrWhiteSpace(MessageOf(body)));
            }

            using (var delete = await DeleteAsync(
                $"/auth/tickets/{ticketId}",
                csrf,
                account))
            {
                await AssertCodeAsync(delete, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            // A verb no route of the row owns is the same refusal, and none of them wrote a row.
            using (var postRow = await PostJsonAsync(
                $"/auth/tickets/{ticketId}",
                new { },
                csrf,
                account))
            {
                await AssertCodeAsync(
                    postRow,
                    HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed");
            }

            using (var putRow = await PutJsonAsync(
                $"/auth/tickets/{ticketId}",
                new { },
                csrf,
                account))
            {
                await AssertCodeAsync(putRow, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
            }

            using var detail = await account.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken);

            using var detailBody = await ReadJsonAsync(detail);

            Assert.Equal(1, detailBody.RootElement.GetProperty("replies").GetArrayLength());
            Assert.Equal("read", detailBody.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task EverySelfWriteRefusesAMissingAntiforgeryPair()
        {
            var ownerId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(ownerId, "a conversation");

            using var account = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, account);

            using (var create = await PostJsonAsync(
                "/auth/tickets",
                new { subject = "no token" },
                client: account))
            {
                await AssertCodeAsync(create, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            using (var reply = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/replies",
                new { message = "no token" },
                client: account))
            {
                await AssertCodeAsync(reply, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            using (var read = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/read",
                new { },
                client: account))
            {
                await AssertCodeAsync(read, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            // Nothing was staged by any of the three.
            using var detail = await account.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken);

            using var body = await ReadJsonAsync(detail);

            Assert.Equal(0, body.RootElement.GetProperty("replies").GetArrayLength());
            Assert.Equal("read", body.RootElement.GetProperty("status").GetString());
        }

        /// <summary>Opens one ticket over the wire and answers the identifier it names.</summary>
        /// <param name="account">Signed-in account that opens it.</param>
        /// <param name="subject">Subject line.</param>
        private async Task<Guid> CreateTicketAsync(HttpClient account, string subject)
        {
            using var response = await PostJsonAsync(
                "/auth/tickets",
                new { subject },
                await GetCsrfTokenAsync(account),
                account);

            response.EnsureSuccessStatusCode();

            return Guid.Parse(IdFromLocation(response));
        }

        /// <summary>Reads one conversation from the operator's side.</summary>
        /// <param name="ticketId">Ticket to read.</param>
        private async Task<JsonDocument> GetAdminDetailAsync(Guid ticketId)
        {
            using var response = await Client.GetAsync(
                $"/admin/tickets/{ticketId}",
                TestContext.Current.CancellationToken);

            response.EnsureSuccessStatusCode();

            return await ReadJsonAsync(response);
        }

        /// <summary>Plants one conversation through the Core repository.</summary>
        /// <param name="userId">Account that opens it.</param>
        /// <param name="subject">Subject line.</param>
        /// <param name="firstMessage">Opening message, or null for an empty thread.</param>
        private async Task<Guid> PlantAsync(
            Guid userId,
            string subject,
            string? firstMessage = null)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.PlantTicketAsync(
                scope.ServiceProvider,
                userId,
                subject,
                firstMessage,
                TestContext.Current.CancellationToken);
        }

        /// <summary>Reads the identifier of an account that is already there.</summary>
        /// <param name="username">Login name the account was created with.</param>
        private async Task<Guid> UserIdAsync(string username)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            var users = scope.ServiceProvider
                .GetRequiredService<Fluxy.Core.Users.Repositories.IUserRepository>();

            var account = await users.FindByUsernameAsync(
                username,
                TestContext.Current.CancellationToken);

            return account?.Id
                ?? throw new InvalidOperationException($"No account is named {username}.");
        }

        /// <summary>The message carrying one text, out of a detail's thread.</summary>
        /// <param name="detail">Detail body.</param>
        /// <param name="message">Text to look for.</param>
        private static JsonElement ReplyWith(JsonDocument detail, string message)
            => detail.RootElement
                .GetProperty("replies")
                .EnumerateArray()
                .Single(reply => reply.GetProperty("message").GetString() == message);

        /// <summary>The rows of one page.</summary>
        /// <param name="document">Page body.</param>
        private static JsonElement[] Rows(JsonDocument document)
            => document.RootElement.GetProperty("items").EnumerateArray().ToArray();

        /// <summary>The one row of a page that must hold exactly one.</summary>
        /// <param name="document">Page body.</param>
        private static JsonElement SingleRow(JsonDocument document)
        {
            var rows = Rows(document);

            Assert.Single(rows);

            return rows[0];
        }

        /// <summary>The identifiers of one page's rows, in the order they arrived.</summary>
        /// <param name="document">Page body.</param>
        private static Guid[] Ids(JsonDocument document)
            => Rows(document)
                .Select(row => Guid.Parse(row.GetProperty("id").GetString()!))
                .ToArray();
    }
}
