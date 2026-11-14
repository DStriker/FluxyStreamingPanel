using System.Net;
using System.Text.Json;
using Fluxy.Core.Tickets.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The operator's side of the support tickets end to end: the page and its default order, the
    /// search and the viewer-relative status filter, every sort field, the grant scope, the
    /// transitions with their idempotency and flag movement, the thread bound, the delete cascade,
    /// the no-store reads, the collection fence and the antiforgery pair on every write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Conversations are planted through the Core repository.</b>
    /// <see cref="TestDataSeeder.PlantTicketAsync"/> stages the ticket and, when asked, its first
    /// message through <c>ITicketRepository</c> and commits once - the same insert intent
    /// <c>ITicketService.CreateAsync</c> uses without the create endpoint's subject rules, so a
    /// test can plant a conversation for any account without a signed-in visitor of that account.
    /// </para>
    /// <para>
    /// <b>One save stamps one instant.</b> Rows planted in one call share <c>created_at</c>, which
    /// is what makes the identifier tie-break testable: only the id can order them. A test that
    /// needs two instants advances the fixture's <c>FakeTimeProvider</c> and plants again.
    /// </para>
    /// <para>
    /// <b>The account's own surface is reached beside the operator's</b> where the contract is about
    /// both ends of one flag: an operator's reply must read <c>unread</c> on the account's screen,
    /// and a closed conversation must stay readable from both sides.
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
        public async Task TheListDefaultsToLastReplyAtDescendingWithAnIdentifierTieBreak()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");

            var first = await PlantAsync(clientId, "first ticket");
            var second = await PlantAsync(clientId, "second ticket");
            var third = await PlantAsync(clientId, "third ticket");

            var expected = new[] { first, second, third }
                .Select(id => id.ToString())
                .OrderByDescending(text => text, StringComparer.Ordinal)
                .ToArray();

            // A page about accounts' conversations is not something a cache may keep.
            using (var response = await Client.GetAsync(
                "/admin/tickets?pageSize=50",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);

                using var body = await ReadJsonAsync(response);

                Assert.Equal(3, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(50, body.RootElement.GetProperty("pageSize").GetInt32());

                // One instant for every planted row - the fake clock is the context's too - so the
                // identifier is the only thing left to order by, descending.
                Assert.Equal(expected, Ids(body).Select(id => id.ToString()).ToArray());
            }

            // A newer message lifts its ticket to the top even when its identifier is the lowest:
            // the default order reads the computed last-reply instant, not the row's creation.
            var raised = Guid.Parse(expected[^1]);
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await PlantRepliesAsync(raised, 1, clientId);

            using var after = await ReadPageAsync("pageSize=50");
            Assert.Equal(raised.ToString(), Ids(after)[0].ToString());

            // The thread that is still empty keeps its creation instant as its last reply, so the
            // page draws a value for every row rather than a null the column cannot sort.
            var untouched = new[] { first, second, third }.First(id => id != raised);

            using (var detail = await GetAdminDetailAsync(untouched))
            {
                Assert.Equal(
                    detail.RootElement.GetProperty("createdAt").GetString(),
                    detail.RootElement.GetProperty("lastReplyAt").GetString());
            }
        }

        [Fact]
        public async Task PageTurnsAreDisjointAndTotalDescribesTheWholeList()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");

            var planted = new List<Guid>();

            for (var index = 0; index < 5; index++)
            {
                planted.Add(await PlantAsync(clientId, $"ticket {index + 1}"));
            }

            var seen = new List<Guid>();

            for (var page = 1; page <= 3; page++)
            {
                using var turn = await ReadPageAsync($"page={page}&pageSize=2");

                // The count describes the list, not the page, and every turn reports the same one.
                Assert.Equal(5, turn.RootElement.GetProperty("total").GetInt32());
                seen.AddRange(Ids(turn));
            }

            Assert.Equal(5, seen.Count);
            Assert.Equal(5, seen.Distinct().Count());

            // Five rows written in one instant: the identifier decides, and no page turn repeats
            // or skips one.
            Assert.Equal(
                planted
                    .Select(id => id.ToString())
                    .OrderByDescending(text => text, StringComparer.Ordinal)
                    .ToArray(),
                seen.Select(id => id.ToString()).ToArray());

            using var beyond = await ReadPageAsync("page=9&pageSize=2");
            Assert.Equal(5, beyond.RootElement.GetProperty("total").GetInt32());
            Assert.Empty(beyond.RootElement.GetProperty("items").EnumerateArray());
        }

        [Fact]
        public async Task SearchNarrowsTheListCaseInsensitivelyAndCoversTheAccountName()
        {
            await CreateAdminAndSignInAsync();
            var ownerId = await CreateUserAsync(UserRole.Client, "distinctive-owner");

            await PlantAsync(ownerId, "The Upstream Server Refused The Connection");
            await PlantAsync(ownerId, "a different sentence entirely");

            // Case-insensitive against the stored subject.
            using (var upper = await ReadPageAsync("search=UPSTREAM"))
            {
                Assert.Equal(1, upper.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "The Upstream Server Refused The Connection",
                    SingleRow(upper).GetProperty("subject").GetString());
            }

            using (var lower = await ReadPageAsync("search=upstream"))
            {
                Assert.Equal(1, lower.RootElement.GetProperty("total").GetInt32());
            }

            // The operator's search covers the account's current login name as well - the name is
            // resolved by the join, and a renamed account is searched by the name it carries now.
            using (var byOwner = await ReadPageAsync("search=distinctive-owner"))
            {
                Assert.Equal(2, byOwner.RootElement.GetProperty("total").GetInt32());
            }

            using (var nothing = await ReadPageAsync("search=nothing-matches-this"))
            {
                Assert.Equal(0, nothing.RootElement.GetProperty("total").GetInt32());
            }
        }

        [Fact]
        public async Task TheStatusFilterIsViewerRelativeAndClosedBeatsTheFlag()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");

            var unread = await PlantAsync(clientId, "never read");
            var read = await PlantAsync(clientId, "read by the operator");
            var closed = await PlantAsync(clientId, "closed by the operator");

            var csrf = await GetCsrfTokenAsync();

            using (var marked = await TransitionAsync(read, "read", csrf))
            {
                await AssertCodeAsync(marked, HttpStatusCode.OK, "ticket_marked_read");
            }

            using (var shut = await TransitionAsync(closed, "close", csrf))
            {
                await AssertCodeAsync(shut, HttpStatusCode.OK, "ticket_closed");
            }

            using (var all = await ReadPageAsync("pageSize=50"))
            {
                var rows = Rows(all);

                Assert.Equal(3, all.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(
                    "unread",
                    RowById(rows, unread).GetProperty("status").GetString());
                Assert.Equal("read", RowById(rows, read).GetProperty("status").GetString());
                Assert.Equal("closed", RowById(rows, closed).GetProperty("status").GetString());
            }

            // `unread` and `read` are the operator's own flag, and both exclude a closed row
            // whatever the flag left standing says; `closed` is the lifecycle both sides share.
            using (var onlyUnread = await ReadPageAsync("status=unread"))
            {
                Assert.Equal(1, onlyUnread.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(unread, Ids(onlyUnread).Single());
            }

            using (var onlyRead = await ReadPageAsync("status=read"))
            {
                Assert.Equal(1, onlyRead.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(read, Ids(onlyRead).Single());
            }

            using (var onlyClosed = await ReadPageAsync("status=closed"))
            {
                Assert.Equal(1, onlyClosed.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(closed, Ids(onlyClosed).Single());
            }

            // A word the contract does not know is refused under its own parameter, and `status`
            // is not a sort field either.
            using (var badStatus = await Client.GetAsync(
                "/admin/tickets?status=archived",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(badStatus, HttpStatusCode.BadRequest, "validation_failed");
            }
        }

        [Fact]
        public async Task EverySortFieldOrdersTheList()
        {
            await CreateAdminAndSignInAsync();

            var alpha = await CreateUserAsync(UserRole.Client, "alpha");
            var zeta = await CreateUserAsync(UserRole.Client, "zeta");

            var bravo = await PlantAsync(alpha, "Bravo subject");
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            var apple = await PlantAsync(zeta, "Apple subject");

            // Subject, ascending: the alphabetic column and nothing else.
            using (var bySubject = await ReadPageAsync("sortBy=subject&sortOrder=asc"))
            {
                Assert.Equal(
                    ["Apple subject", "Bravo subject"],
                    Rows(bySubject)
                        .Select(row => row.GetProperty("subject").GetString())
                        .ToArray());
            }

            // The account's name is resolved on read, so an order on it orders the joined value.
            using (var byUsername = await ReadPageAsync("sortBy=username&sortOrder=asc"))
            {
                Assert.Equal(
                    ["alpha", "zeta"],
                    Rows(byUsername)
                        .Select(row => row.GetProperty("username").GetString())
                        .ToArray());
            }

            // Creation instant, newest first: the second ticket was planted a second later.
            using (var byCreated = await ReadPageAsync("sortBy=createdAt&sortOrder=desc"))
            {
                Assert.Equal(apple, Ids(byCreated)[0]);
                Assert.Equal(bravo, Ids(byCreated)[1]);
            }

            // lastReplyAt falls back to the creation instant while a thread is empty, which is the
            // same value the default order reads.
            using (var byLastReply = await ReadPageAsync("sortBy=lastReplyAt&sortOrder=asc"))
            {
                Assert.Equal(bravo, Ids(byLastReply)[0]);
                Assert.Equal(apple, Ids(byLastReply)[1]);
            }

            // A field that names no column is refused under sortBy, never answered with the
            // default order; `status` is derived per viewer and cannot be a column.
            using (var byStatus = await Client.GetAsync(
                "/admin/tickets?sortBy=status",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(byStatus, HttpStatusCode.BadRequest, "validation_failed");

                using var body = await ReadJsonAsync(byStatus);
                Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));
            }
        }

        [Fact]
        public async Task TheGrantsScopeTheListAndTheDetailAndAnEmptyGrantSetIsRefused()
        {
            var viewerGroup = await CreateGroupAsync(
                "Reseller watchers",
                UserRole.Admin,
                permissions: UserPermission.ViewResellers);
            await CreateUserAsync(UserRole.Admin, "reseller-reader", Password, groupId: viewerGroup);

            var clientId = await CreateUserAsync(UserRole.Client, "client-owner");
            var resellerId = await CreateUserAsync(UserRole.Reseller, "reseller-owner");

            var clientTicket = await PlantAsync(clientId, "a client's conversation");
            var resellerTicket = await PlantAsync(resellerId, "a reseller's conversation");

            using var reader = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "reseller-reader", Password, reader);

            // The list is scoped by the roles the grants reach, and `total` is counted after that
            // scope: the pager must not advertise a ticket the reader cannot open.
            using (var list = await reader.GetAsync(
                "/admin/tickets",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);

                using var body = await ReadJsonAsync(list);

                Assert.Equal(1, body.RootElement.GetProperty("total").GetInt32());
                Assert.Equal(resellerTicket, Ids(body).Single());
                Assert.Equal(
                    "reseller-owner",
                    SingleRow(body).GetProperty("username").GetString());
            }

            using (var visible = await reader.GetAsync(
                $"/admin/tickets/{resellerTicket}",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            }

            // An existing conversation whose owner's role the grants do not reach is a permission
            // refusal, not the missing-row 404 - the reader may have seen the row in a colleague's
            // wider list.
            using (var hidden = await reader.GetAsync(
                $"/admin/tickets/{clientTicket}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(hidden, HttpStatusCode.Forbidden, "permission_denied");
            }

            // A group with no account grant reaches none of the routes, reads or writes.
            var grantlessGroup = await CreateGroupAsync("Grantless", UserRole.Admin);
            await CreateUserAsync(UserRole.Admin, "grantless", Password, groupId: grantlessGroup);

            using var grantless = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "grantless", Password, grantless);

            using (var refusedList = await grantless.GetAsync(
                "/admin/tickets",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedList, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var refusedDetail = await grantless.GetAsync(
                $"/admin/tickets/{resellerTicket}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(refusedDetail, HttpStatusCode.Forbidden, "permission_denied");
            }

            foreach (var action in new[] { "read", "unread", "close", "reopen" })
            {
                using var refusal = await TransitionAsync(
                    resellerTicket,
                    action,
                    await GetCsrfTokenAsync(grantless),
                    grantless);

                await AssertCodeAsync(refusal, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var refusedReply = await PostJsonAsync(
                $"/admin/tickets/{resellerTicket}/replies",
                new { message = "not allowed" },
                await GetCsrfTokenAsync(grantless),
                grantless))
            {
                await AssertCodeAsync(refusedReply, HttpStatusCode.Forbidden, "permission_denied");
            }

            using (var refusedDelete = await DeleteAsync(
                $"/admin/tickets/{resellerTicket}",
                await GetCsrfTokenAsync(grantless),
                grantless))
            {
                await AssertCodeAsync(refusedDelete, HttpStatusCode.Forbidden, "permission_denied");
            }

            // None of the refusals changed the row.
            Assert.Equal(0, await CountRepliesAsync(resellerTicket));
        }

        [Fact]
        public async Task TheTransitionsAreIdempotentAndMoveTheFlagsTheContractNames()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(clientId, "help me", "the first question");

            using var userClient = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, userClient);

            // The account's message raised the operator's flag; the account wrote it, so its own
            // flag is clear and it reads `read` on its own screen.
            using (var adminView = await GetAdminDetailAsync(ticketId))
            {
                Assert.Equal("unread", adminView.RootElement.GetProperty("status").GetString());
            }

            using (var userView = await GetUserDetailAsync(ticketId, userClient))
            {
                Assert.Equal("read", userView.RootElement.GetProperty("status").GetString());
            }

            var csrf = await GetCsrfTokenAsync();

            // Close is a state, and a second close answers the same word: the state the caller
            // asked for is the state that holds. The operator's flag is cleared; the account's is
            // untouched.
            using (var closed = await TransitionAsync(ticketId, "close", csrf))
            {
                await AssertCodeAsync(closed, HttpStatusCode.OK, "ticket_closed");
            }

            using (var closedAgain = await TransitionAsync(ticketId, "close", csrf))
            {
                await AssertCodeAsync(closedAgain, HttpStatusCode.OK, "ticket_closed");
            }

            using (var adminClosed = await GetAdminDetailAsync(ticketId))
            {
                Assert.Equal("closed", adminClosed.RootElement.GetProperty("status").GetString());
            }

            // A closed conversation is readable from both sides; closing it did not hide it.
            using (var userClosed = await GetUserDetailAsync(ticketId, userClient))
            {
                Assert.Equal("closed", userClosed.RootElement.GetProperty("status").GetString());
            }

            using (var reopened = await TransitionAsync(ticketId, "reopen", csrf))
            {
                await AssertCodeAsync(reopened, HttpStatusCode.OK, "ticket_reopened");
            }

            // Reopening a ticket that is already open has no state change to report.
            using (var reopenAgain = await TransitionAsync(ticketId, "reopen", csrf))
            {
                await AssertCodeAsync(reopenAgain, HttpStatusCode.Conflict, "invalid_status");
            }

            // An operator's reply raises the account's flag and clears the writer's own.
            using (var reply = await PostJsonAsync(
                $"/admin/tickets/{ticketId}/replies",
                new { message = "the operator's answer" },
                csrf))
            {
                await AssertCodeAsync(reply, HttpStatusCode.Created, "ticket_reply_created");
            }

            using (var adminAfterReply = await GetAdminDetailAsync(ticketId))
            {
                Assert.Equal("read", adminAfterReply.RootElement.GetProperty("status").GetString());

                var answer = ReplyWith(adminAfterReply, "the operator's answer");

                Assert.True(answer.GetProperty("isAdminReply").GetBoolean());
                Assert.Equal("operator", answer.GetProperty("authorName").GetString());
                Assert.Equal(JsonValueKind.Null, answer.GetProperty("readAt").ValueKind);
            }

            using (var userUnread = await GetUserDetailAsync(ticketId, userClient))
            {
                Assert.Equal("unread", userUnread.RootElement.GetProperty("status").GetString());
            }

            // The account's answer raises the operator's flag again, because the writer's own flag
            // is the only one a message clears. The clock steps so the two account messages have
            // distinct instants: written in one instant, only the identifier could name the newest,
            // and the assertion below is about "the latest account message" as the thread orders it.
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));

            using (var accountReply = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/replies",
                new { message = "the account's follow-up" },
                await GetCsrfTokenAsync(userClient),
                userClient))
            {
                await AssertCodeAsync(accountReply, HttpStatusCode.Created, "ticket_reply_created");
            }

            using (var adminUnreadAgain = await GetAdminDetailAsync(ticketId))
            {
                Assert.Equal("unread", adminUnreadAgain.RootElement.GetProperty("status").GetString());
            }

            // Reading clears the operator's flag and stamps both account messages; a second read
            // finds nothing left to stamp and answers the same word.
            using (var read = await TransitionAsync(ticketId, "read", csrf))
            {
                await AssertCodeAsync(read, HttpStatusCode.OK, "ticket_marked_read");
            }

            using (var readAgain = await TransitionAsync(ticketId, "read", csrf))
            {
                await AssertCodeAsync(readAgain, HttpStatusCode.OK, "ticket_marked_read");
            }

            using (var readDetail = await GetAdminDetailAsync(ticketId))
            {
                Assert.Equal("read", readDetail.RootElement.GetProperty("status").GetString());

                Assert.NotEqual(
                    JsonValueKind.Null,
                    ReplyWith(readDetail, "the first question").GetProperty("readAt").ValueKind);
                Assert.NotEqual(
                    JsonValueKind.Null,
                    ReplyWith(readDetail, "the account's follow-up")
                        .GetProperty("readAt").ValueKind);

                // The operator's own message is not stamped by the operator's read.
                Assert.Equal(
                    JsonValueKind.Null,
                    ReplyWith(readDetail, "the operator's answer").GetProperty("readAt").ValueKind);
            }

            // Marking unread raises the flag again and clears the read mark of the newest account
            // message only - "there is something new here", not "the whole thread is unread".
            using (var unread = await TransitionAsync(ticketId, "unread", csrf))
            {
                await AssertCodeAsync(unread, HttpStatusCode.OK, "ticket_marked_unread");
            }

            using (var unreadAgain = await TransitionAsync(ticketId, "unread", csrf))
            {
                await AssertCodeAsync(unreadAgain, HttpStatusCode.OK, "ticket_marked_unread");
            }

            using var afterUnread = await GetAdminDetailAsync(ticketId);

            Assert.Equal("unread", afterUnread.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                JsonValueKind.Null,
                ReplyWith(afterUnread, "the account's follow-up").GetProperty("readAt").ValueKind);
            Assert.NotEqual(
                JsonValueKind.Null,
                ReplyWith(afterUnread, "the first question").GetProperty("readAt").ValueKind);
        }

        [Fact]
        public async Task AReplyToAClosedTicketIsRefusedAndWritesNothing()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(clientId, "already answered", "the question");

            using var userClient = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, userClient);

            var csrf = await GetCsrfTokenAsync();

            using (var closed = await TransitionAsync(ticketId, "close", csrf))
            {
                await AssertCodeAsync(closed, HttpStatusCode.OK, "ticket_closed");
            }

            using (var operatorReply = await PostJsonAsync(
                $"/admin/tickets/{ticketId}/replies",
                new { message = "too late" },
                csrf))
            {
                await AssertCodeAsync(
                    operatorReply,
                    HttpStatusCode.Conflict,
                    "ticket_is_closed");
            }

            using (var accountReply = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/replies",
                new { message = "also too late" },
                await GetCsrfTokenAsync(userClient),
                userClient))
            {
                await AssertCodeAsync(
                    accountReply,
                    HttpStatusCode.Conflict,
                    "ticket_is_closed");
            }

            // Both refusals were decided before anything was staged.
            Assert.Equal(1, await CountRepliesAsync(ticketId));

            using var detail = await GetAdminDetailAsync(ticketId);
            Assert.Equal(1, detail.RootElement.GetProperty("replies").GetArrayLength());
            Assert.Equal("closed", detail.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task TheMessageBelowTheThreadBoundIsAcceptedAndTheNextOneIsRefused()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(clientId, "a long conversation");

            await PlantRepliesAsync(
                ticketId,
                TicketLimits.MaxRepliesPerTicket - 1,
                clientId);

            using var userClient = Fixture.CreateClient();
            await SignInAsync(UserRole.Client, "owner", Password, userClient);

            // The message that fills the thread to its bound is accepted.
            using (var last = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/replies",
                new { message = "the five hundredth message" },
                await GetCsrfTokenAsync(userClient),
                userClient))
            {
                await AssertCodeAsync(last, HttpStatusCode.Created, "ticket_reply_created");
            }

            Assert.Equal(TicketLimits.MaxRepliesPerTicket, await CountRepliesAsync(ticketId));

            // The one that would cross it is refused, and nothing is staged.
            using (var beyond = await PostJsonAsync(
                $"/auth/tickets/{ticketId}/replies",
                new { message = "the five hundred and first message" },
                await GetCsrfTokenAsync(userClient),
                userClient))
            {
                await AssertCodeAsync(
                    beyond,
                    HttpStatusCode.Conflict,
                    "ticket_reply_limit_reached");
            }

            Assert.Equal(TicketLimits.MaxRepliesPerTicket, await CountRepliesAsync(ticketId));
        }

        [Fact]
        public async Task DeletingATicketRemovesItsConversationAndASecondDeleteIsNotFound()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            var kept = await PlantAsync(clientId, "kept ticket");
            var removed = await PlantAsync(clientId, "removed ticket", "its only message");

            Assert.Equal(1, await CountRepliesAsync(removed));

            using (var deleted = await DeleteAsync(
                $"/admin/tickets/{removed}",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(deleted, HttpStatusCode.OK, "ticket_deleted");
            }

            // The messages went with the row through the cascade in the same save.
            Assert.Equal(0, await CountRepliesAsync(removed));
            Assert.Equal(0, await CountTicketsAsync(removed));
            Assert.Equal(1, await CountTicketsAsync(kept));

            using (var gone = await Client.GetAsync(
                $"/admin/tickets/{removed}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(gone, HttpStatusCode.NotFound, "ticket_not_found");
            }

            using (var secondDelete = await DeleteAsync(
                $"/admin/tickets/{removed}",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(secondDelete, HttpStatusCode.NotFound, "ticket_not_found");
            }
        }

        [Fact]
        public async Task ReadsCarryNoStoreAndTheCollectionRefusesTheCreate()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(clientId, "a conversation");

            using (var list = await Client.GetAsync(
                "/admin/tickets",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);
                Assert.True(list.Headers.CacheControl?.NoStore);
            }

            using (var detail = await Client.GetAsync(
                $"/admin/tickets/{ticketId}",
                TestContext.Current.CancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
                Assert.True(detail.Headers.CacheControl?.NoStore);
            }

            // Operators answer conversations; they never open one. There is no POST on the
            // collection, and the shared status-page envelope says so.
            using (var create = await PostJsonAsync(
                "/admin/tickets",
                new { subject = "an operator's own ticket" },
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(
                    create,
                    HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed");

                using var body = await ReadJsonAsync(create);
                Assert.False(string.IsNullOrWhiteSpace(MessageOf(body)));
            }

            Assert.Equal(1, await CountTicketsAsync(ticketId));
        }

        [Fact]
        public async Task EveryAdminWriteRefusesAMissingAntiforgeryPair()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            var ticketId = await PlantAsync(clientId, "a conversation", "the question");

            using (var reply = await PostJsonAsync(
                $"/admin/tickets/{ticketId}/replies",
                new { message = "no token" }))
            {
                await AssertCodeAsync(reply, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            foreach (var action in new[] { "read", "unread", "close", "reopen" })
            {
                using var refusal = await TransitionAsync(ticketId, action, csrf: null);

                await AssertCodeAsync(refusal, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            using (var delete = await DeleteAsync($"/admin/tickets/{ticketId}"))
            {
                await AssertCodeAsync(delete, HttpStatusCode.BadRequest, "csrf_invalid");
            }

            // None of the refused writes touched the row.
            using var detail = await GetAdminDetailAsync(ticketId);
            Assert.Equal(1, detail.RootElement.GetProperty("replies").GetArrayLength());
            Assert.NotEqual("closed", detail.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task DeletingTheAccountRemovesItsTicketsAndConversation()
        {
            await CreateAdminAndSignInAsync();
            var clientId = await CreateUserAsync(UserRole.Client, "leaving");
            var keptId = await CreateUserAsync(UserRole.Client, "staying");

            var removed = await PlantAsync(clientId, "the leaving account's ticket", "a message");
            await PlantRepliesAsync(removed, 1, clientId);
            await PlantAsync(keptId, "the staying account's ticket");

            using (var deleted = await DeleteAsync(
                $"/admin/users/{clientId}",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(deleted, HttpStatusCode.OK, "user_deleted");
            }

            // Both cascades ran: the ticket with the account, the messages with the ticket.
            Assert.Equal(0, await CountTicketsForUserAsync(clientId));
            Assert.Equal(0, await CountRepliesAsync(removed));
            Assert.Equal(1, await CountTicketsForUserAsync(keptId));

            using (var gone = await Client.GetAsync(
                $"/admin/tickets/{removed}",
                TestContext.Current.CancellationToken))
            {
                await AssertCodeAsync(gone, HttpStatusCode.NotFound, "ticket_not_found");
            }
        }

        [Fact]
        public async Task ADeletedOperatorsRepliesSurviveWithANullAuthorOnTheOperatorsSide()
        {
            await CreateAdminAndSignInAsync();

            var clientId = await CreateUserAsync(UserRole.Client, "owner");
            await CreateUserAsync(UserRole.Admin, "operator-two", Password);

            var ticketId = await PlantAsync(clientId, "a conversation", "the question");

            using (var departing = Fixture.CreateClient())
            {
                await SignInAsync(UserRole.Admin, "operator-two", Password, departing);

                using var reply = await PostJsonAsync(
                    $"/admin/tickets/{ticketId}/replies",
                    new { message = "said by the departing operator" },
                    await GetCsrfTokenAsync(departing),
                    departing);

                await AssertCodeAsync(reply, HttpStatusCode.Created, "ticket_reply_created");
            }

            var departingId = await UserIdAsync("operator-two");

            using (var deleted = await DeleteAsync(
                $"/admin/users/{departingId}",
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(deleted, HttpStatusCode.OK, "user_deleted");
            }

            // The message is part of the conversation and may not disappear with its author. The
            // side stays a fact of the row, and the name it no longer resolves to is null.
            using var detail = await GetAdminDetailAsync(ticketId);
            var surviving = ReplyWith(detail, "said by the departing operator");

            Assert.Equal(JsonValueKind.Null, surviving.GetProperty("authorId").ValueKind);
            Assert.Equal(JsonValueKind.Null, surviving.GetProperty("authorName").ValueKind);
            Assert.True(surviving.GetProperty("isAdminReply").GetBoolean());
        }

        /// <summary>Reads one page of the operator's list.</summary>
        /// <param name="query">Query string without its leading question mark.</param>
        private async Task<JsonDocument> ReadPageAsync(string query)
        {
            using var response = await Client.GetAsync(
                query.Length == 0 ? "/admin/tickets" : $"/admin/tickets?{query}",
                TestContext.Current.CancellationToken);

            response.EnsureSuccessStatusCode();

            return await ReadJsonAsync(response);
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

        /// <summary>Reads one conversation from the account's own side.</summary>
        /// <param name="ticketId">Ticket to read.</param>
        /// <param name="client">Signed-in account whose client is used.</param>
        private async Task<JsonDocument> GetUserDetailAsync(Guid ticketId, HttpClient client)
        {
            using var response = await client.GetAsync(
                $"/auth/tickets/{ticketId}",
                TestContext.Current.CancellationToken);

            response.EnsureSuccessStatusCode();

            return await ReadJsonAsync(response);
        }

        /// <summary>Runs one of the operator's transitions on a ticket.</summary>
        /// <param name="ticketId">Ticket to change.</param>
        /// <param name="action"><c>read</c>, <c>unread</c>, <c>close</c> or <c>reopen</c>.</param>
        /// <param name="csrf">Antiforgery token, or null to send none.</param>
        /// <param name="client">Client to send with; the signed-in one when null.</param>
        private Task<HttpResponseMessage> TransitionAsync(
            Guid ticketId,
            string action,
            string? csrf,
            HttpClient? client = null)
            => PostJsonAsync($"/admin/tickets/{ticketId}/{action}", new { }, csrf, client);

        /// <summary>Plants one conversation, optionally with its first account message.</summary>
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

        /// <summary>Stages messages of one side into a conversation and commits them once.</summary>
        /// <param name="ticketId">Conversation the messages are written into.</param>
        /// <param name="count">How many messages to stage.</param>
        /// <param name="authorId">Account the messages name as their author.</param>
        private async Task PlantRepliesAsync(Guid ticketId, int count, Guid authorId)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            await TestDataSeeder.PlantTicketRepliesAsync(
                scope.ServiceProvider,
                ticketId,
                count,
                authorId,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        /// <summary>Counts the messages one conversation holds.</summary>
        /// <param name="ticketId">Conversation to count.</param>
        private async Task<int> CountRepliesAsync(Guid ticketId)
        {
            await using var connection = await Fixture.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select count(*) from ticket_replies where ticket_id = @id",
                connection);
            command.Parameters.AddWithValue("id", ticketId);

            return Convert.ToInt32(await command.ExecuteScalarAsync(
                TestContext.Current.CancellationToken));
        }

        /// <summary>Counts the tickets one identifier still names.</summary>
        /// <param name="ticketId">Ticket to count.</param>
        private async Task<int> CountTicketsAsync(Guid ticketId)
        {
            await using var connection = await Fixture.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select count(*) from tickets where id = @id",
                connection);
            command.Parameters.AddWithValue("id", ticketId);

            return Convert.ToInt32(await command.ExecuteScalarAsync(
                TestContext.Current.CancellationToken));
        }

        /// <summary>Counts the tickets one account still owns.</summary>
        /// <param name="userId">Account whose tickets are counted.</param>
        private async Task<int> CountTicketsForUserAsync(Guid userId)
        {
            await using var connection = await Fixture.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select count(*) from tickets where user_id = @id",
                connection);
            command.Parameters.AddWithValue("id", userId);

            return Convert.ToInt32(await command.ExecuteScalarAsync(
                TestContext.Current.CancellationToken));
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

        /// <summary>One row of a page, by the ticket it carries.</summary>
        /// <param name="rows">Rows of the page.</param>
        /// <param name="ticketId">Ticket the row names.</param>
        private static JsonElement RowById(JsonElement[] rows, Guid ticketId)
            => rows.Single(row => row.GetProperty("id").GetString() == ticketId.ToString());

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
