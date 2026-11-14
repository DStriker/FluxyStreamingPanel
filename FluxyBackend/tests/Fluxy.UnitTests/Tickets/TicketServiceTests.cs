using Fluxy.Application.Tickets.Services;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Tickets.Commands;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Interfaces;
using Fluxy.Core.Tickets.Models;
using Fluxy.Core.Tickets.Outcomes;
using Fluxy.Core.Tickets.ReadModels;
using Fluxy.Core.Tickets.Repositories;
using Fluxy.Core.Tickets.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Models;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Rows;
using Fluxy.Core.Authentication.ReadModels;
using Microsoft.Extensions.Time.Testing;

namespace Fluxy.UnitTests.Tickets
{
    /// <summary>
    /// The support ticket rules: the flag each transition raises and clears, the read mark it
    /// stamps, what a closed thread or a full one refuses, who may see a conversation, and the one
    /// save every operation owns.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the rules disagree with each other on purpose: the two flags
    /// move in opposite directions, a read stamps the other side's messages rather than the
    /// reader's, and a refusal must leave the tracker exactly as it found it. A test that only
    /// checked the happy path would pass over an implementation that cleared the wrong flag.
    /// </remarks>
    public sealed class TicketServiceTests
    {
        private static readonly Guid Owner = Guid.NewGuid();
        private static readonly Guid Operator = Guid.NewGuid();
        private static readonly Guid TicketId = Guid.NewGuid();

        private static readonly IReadOnlySet<UserPermission> BothGrants =
            new HashSet<UserPermission>
            {
                UserPermission.ViewClients,
                UserPermission.EditClients
            };

        [Fact]
        public async Task CreateStagesAnOpenTicketWithTheOperatorsFlagRaised()
        {
            var tickets = new FakeTickets();
            var users = new FakeUsers(UserRole.Client);
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, users, unitOfWork).CreateAsync(
                new NewTicket { UserId = Owner, Subject = "  The router restarted  " },
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Created, outcome.Action);
            Assert.Equal("The router restarted", tickets.Added!.Subject);
            Assert.True(tickets.AdminUnreadAfterCreate);
            Assert.False(tickets.UserUnreadAfterCreate);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task AnOperatorIsRefusedTheCreate()
        {
            var tickets = new FakeTickets();
            var users = new FakeUsers(UserRole.Admin);
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, users, unitOfWork).CreateAsync(
                new NewTicket { UserId = Operator, Subject = "I opened this myself" },
                TestContext.Current.CancellationToken);

            // Support is the account's door, and the refusal is the service's so no second
            // endpoint can forget it.
            Assert.Equal(TicketAction.PermissionDenied, outcome.Action);
            Assert.Null(tickets.Added);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ABlankSubjectIsRefusedUnderSubject(string subject)
        {
            var tickets = new FakeTickets();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .CreateAsync(
                    new NewTicket { UserId = Owner, Subject = subject },
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTicket.Subject)));
            Assert.Null(tickets.Added);
        }

        [Fact]
        public async Task AnOverLongSubjectIsRefusedRatherThanCut()
        {
            var tickets = new FakeTickets();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .CreateAsync(
                    new NewTicket
                    {
                        UserId = Owner,
                        Subject = new string('s', TicketLimits.SubjectMaxLength + 1)
                    },
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTicket.Subject)));
            Assert.Null(tickets.Added);
        }

        [Fact]
        public async Task AnOperatorReplyRaisesTheAccountsFlagAndClearsItsOwn()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), unitOfWork).ReplyAsync(
                Reply(isAdminReply: true),
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Replied, outcome.Action);
            Assert.False(tickets.AdminUnread);
            Assert.True(tickets.UserUnread);
            Assert.Equal(1, unitOfWork.Saves);

            // The new row is staged with no read mark: the other side has not read it yet.
            Assert.True(tickets.Replies[^1].IsAdminReply);
            Assert.Null(tickets.Replies[^1].ReadMark);
        }

        [Fact]
        public async Task AnAccountReplyRaisesTheOperatorsFlagAndClearsItsOwn()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .ReplyAsync(
                    Reply(isAdminReply: false),
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Replied, outcome.Action);
            Assert.True(tickets.AdminUnread);
            Assert.False(tickets.UserUnread);
            Assert.False(tickets.Replies[^1].IsAdminReply);
        }

        [Fact]
        public async Task AReplyOnAClosedTicketIsRefusedAndWritesNothing()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { Status = TicketStatus.Closed }
            };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), unitOfWork).ReplyAsync(
                Reply(isAdminReply: true),
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.ReplyToClosed, outcome.Action);
            Assert.Empty(tickets.Replies);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task TheMessageThatWouldCrossTheThreadBoundIsRefused()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket(),
                ReplyCount = TicketLimits.MaxRepliesPerTicket
            };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), unitOfWork).ReplyAsync(
                Reply(isAdminReply: true),
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.ReplyLimitReached, outcome.Action);
            Assert.Empty(tickets.Replies);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task TheMessageBelowTheBoundIsAccepted()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket(),
                ReplyCount = TicketLimits.MaxRepliesPerTicket - 1
            };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .ReplyAsync(
                    Reply(isAdminReply: true),
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Replied, outcome.Action);
            Assert.Single(tickets.Replies);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ABlankMessageIsRefusedUnderMessage(string message)
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .ReplyAsync(
                    Reply(isAdminReply: true) with { Message = message },
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTicketReply.Message)));
            Assert.Empty(tickets.Replies);
        }

        [Fact]
        public async Task AnOverLongMessageIsRefusedUnderMessage()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .ReplyAsync(
                    Reply(isAdminReply: true)
                        with { Message = new string('m', TicketLimits.MessageMaxLength + 1) },
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewTicketReply.Message)));
            Assert.Empty(tickets.Replies);
        }

        [Fact]
        public async Task TheOperatorMarkingReadClearsItsFlagAndStampsTheAccountsMessages()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { AdminUnread = true }
            };
            var unitOfWork = new FakeUnitOfWork();
            var now = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

            var outcome = await Build(
                    tickets,
                    new FakeUsers(UserRole.Client),
                    unitOfWork,
                    now)
                .MarkReadAsync(
                    TicketId,
                    Operator,
                    isAdminView: true,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.MarkedRead, outcome.Action);
            Assert.False(tickets.AdminUnread);
            Assert.Equal(1, unitOfWork.Saves);

            // The account's messages are the counter-side set; the mark is the request's clock.
            Assert.Equal(false, tickets.MarkedSide);
            Assert.Equal(now, tickets.MarkedAt);
        }

        [Fact]
        public async Task TheAccountMarkingReadClearsItsFlagAndStampsTheOperatorsMessages()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { UserUnread = true }
            };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .MarkReadAsync(
                    TicketId,
                    Owner,
                    isAdminView: false,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.MarkedRead, outcome.Action);
            Assert.False(tickets.UserUnread);
            Assert.Equal(true, tickets.MarkedSide);
            Assert.True(tickets.AdminUnread is false);
        }

        [Fact]
        public async Task MarkingReadTwiceIsIdempotent()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { UserUnread = true }
            };
            var unitOfWork = new FakeUnitOfWork();
            var service = Build(tickets, new FakeUsers(UserRole.Client), unitOfWork);

            var first = await service.MarkReadAsync(
                TicketId,
                Owner,
                isAdminView: false,
                BothGrants,
                TestContext.Current.CancellationToken);
            var second = await service.MarkReadAsync(
                TicketId,
                Owner,
                isAdminView: false,
                BothGrants,
                TestContext.Current.CancellationToken);

            // The state the caller asked for is the state that holds; both presses answer the
            // same word, and the second changes nothing because the flag was already clear and
            // the repository found no unmarked message.
            Assert.Equal(TicketAction.MarkedRead, first.Action);
            Assert.Equal(TicketAction.MarkedRead, second.Action);
            Assert.False(tickets.UserUnread);
            Assert.Equal(2, unitOfWork.Saves);
        }

        [Fact]
        public async Task TheOperatorMarkingUnreadRaisesItsFlagAndClearsTheLatestAccountMessage()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { AdminUnread = false, UserUnread = true }
            };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), unitOfWork)
                .MarkUnreadAsync(
                    TicketId,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.MarkedUnread, outcome.Action);
            Assert.True(tickets.AdminUnread);

            // The account's flag is untouched: the operator's unread state says nothing about
            // what the account has read.
            Assert.True(tickets.UserUnread);
            Assert.Equal(1, tickets.ClearedLatestCount);
            Assert.False(tickets.ClearedLatestSide);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task MarkingUnreadTwiceIsIdempotent()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };
            var service = Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork());

            await service.MarkUnreadAsync(
                TicketId,
                BothGrants,
                TestContext.Current.CancellationToken);
            var second = await service.MarkUnreadAsync(
                TicketId,
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.MarkedUnread, second.Action);
            Assert.True(tickets.AdminUnread);
        }

        [Fact]
        public async Task ClosingClearsTheOperatorsFlagAndLeavesTheAccounts()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { AdminUnread = false, UserUnread = true }
            };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), unitOfWork)
                .CloseAsync(
                    TicketId,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Closed, outcome.Action);
            Assert.Equal(TicketStatus.Closed, tickets.Status);
            Assert.False(tickets.AdminUnread);
            Assert.True(tickets.UserUnread);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task ClosingAnAlreadyClosedTicketIsIdempotent()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { Status = TicketStatus.Closed }
            };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .CloseAsync(
                    TicketId,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Closed, outcome.Action);
            Assert.Equal(TicketStatus.Closed, tickets.Status);
        }

        [Fact]
        public async Task ReopeningAClosedTicketOpensIt()
        {
            var tickets = new FakeTickets
            {
                Ticket = OpenTicket() with { Status = TicketStatus.Closed }
            };

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .ReopenAsync(
                    TicketId,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Reopened, outcome.Action);
            Assert.Equal(TicketStatus.Open, tickets.Status);
        }

        [Fact]
        public async Task ReopeningANonClosedTicketIsRefusedAndWritesNothing()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(tickets, new FakeUsers(UserRole.Client), unitOfWork)
                .ReopenAsync(
                    TicketId,
                    BothGrants,
                    TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.InvalidStatus, outcome.Action);
            Assert.Equal(TicketStatus.Open, tickets.Status);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task AnUnknownTicketIsNotFoundForEveryOperation()
        {
            var tickets = new FakeTickets { Ticket = null };
            var service = Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork());

            Assert.Equal(
                TicketAction.NotFound,
                (await service.CloseAsync(TicketId, BothGrants, TestContext.Current.CancellationToken)).Action);
            Assert.Equal(
                TicketAction.NotFound,
                (await service.ReopenAsync(TicketId, BothGrants, TestContext.Current.CancellationToken)).Action);
            Assert.Equal(
                TicketAction.NotFound,
                (await service.DeleteAsync(TicketId, BothGrants, TestContext.Current.CancellationToken)).Action);
            Assert.Equal(
                TicketAction.NotFound,
                (await service.MarkUnreadAsync(TicketId, BothGrants, TestContext.Current.CancellationToken)).Action);
            Assert.Equal(
                TicketAction.NotFound,
                (await service.MarkReadAsync(
                    TicketId,
                    Operator,
                    isAdminView: true,
                    BothGrants,
                    TestContext.Current.CancellationToken)).Action);
        }

        [Fact]
        public async Task TheAccountsOwnDetailIsFoundAndSomebodyElsesIsNotFound()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };
            var service = Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork());

            var own = await service.GetDetailAsync(
                TicketId,
                Owner,
                isAdminView: false,
                BothGrants,
                TestContext.Current.CancellationToken);

            var foreign = await service.GetDetailAsync(
                TicketId,
                Guid.NewGuid(),
                isAdminView: false,
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.NotNull(own.Detail);
            Assert.Null(foreign.Detail);
            Assert.Equal(TicketAction.NotFound, foreign.Action);
        }

        [Fact]
        public async Task AnOperatorWithoutTheRolesGrantIsRefused()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };
            var service = Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork());

            // Only the reseller grant: the ticket's owner is a client, so the row is out of scope.
            var grants = new HashSet<UserPermission>
            {
                UserPermission.ViewResellers,
                UserPermission.EditResellers
            };

            var detail = await service.GetDetailAsync(
                TicketId,
                Operator,
                isAdminView: true,
                grants,
                TestContext.Current.CancellationToken);

            Assert.Null(detail.Detail);
            Assert.Equal(TicketAction.PermissionDenied, detail.Action);

            var close = await service.CloseAsync(
                TicketId,
                grants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.PermissionDenied, close.Action);
            Assert.Equal(TicketStatus.Open, tickets.Status);
        }

        [Fact]
        public async Task ADeleteRemovesTheTicketAndItsConversationWithOneSave()
        {
            var tickets = new FakeTickets { Ticket = OpenTicket() };
            var unitOfWork = new FakeUnitOfWork();
            var service = Build(tickets, new FakeUsers(UserRole.Client), unitOfWork);

            var removed = await service.DeleteAsync(
                TicketId,
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.Deleted, removed.Action);
            Assert.True(tickets.Removed);
            Assert.Equal(1, unitOfWork.Saves);

            // The row is gone, so the second delete names nothing.
            var second = await service.DeleteAsync(
                TicketId,
                BothGrants,
                TestContext.Current.CancellationToken);

            Assert.Equal(TicketAction.NotFound, second.Action);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task TheAccountListIsScopedToTheAccountsOwnIdentifier()
        {
            var tickets = new FakeTickets();
            var requested = Guid.NewGuid();

            await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork()).GetPageAsync(
                requested,
                1,
                TicketLimits.DefaultPageSize,
                null,
                null,
                TicketSortField.LastReplyAt,
                TicketSortOrder.Descending,
                TestContext.Current.CancellationToken);

            Assert.Equal(requested, tickets.PageOwner);
        }

        [Fact]
        public async Task TheOperatorListIsScopedByTheRolesTheGrantsReach()
        {
            var tickets = new FakeTickets();

            await Build(tickets, new FakeUsers(UserRole.Client), new FakeUnitOfWork())
                .GetAdminPageAsync(
                    new HashSet<UserPermission> { UserPermission.ViewResellers },
                    1,
                    TicketLimits.DefaultPageSize,
                    null,
                    null,
                    TicketSortField.LastReplyAt,
                    TicketSortOrder.Descending,
                    TestContext.Current.CancellationToken);

            // The service turns grants into roles; the repository never hears about permissions.
            Assert.Equal([UserRole.Reseller], tickets.VisibleRoles!.ToArray());
        }

        /// <summary>Builds the service against the fakes, with an optional clock.</summary>
        private static TicketService Build(
            FakeTickets tickets,
            FakeUsers users,
            FakeUnitOfWork unitOfWork,
            DateTimeOffset? now = null)
            => new(
                tickets,
                users,
                unitOfWork,
                new FakeTimeProvider(now ?? DateTimeOffset.UtcNow));

        private static Ticket OpenTicket() => new()
        {
            Id = TicketId,
            UserId = Owner,
            Subject = "The router restarted",
            Status = TicketStatus.Open,
            AdminUnread = false,
            UserUnread = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        private static NewTicketReply Reply(bool isAdminReply) => new()
        {
            TicketId = TicketId,
            AuthorId = isAdminReply ? Operator : Owner,
            Message = "Anything new?",
            IsAdminReply = isAdminReply
        };

        /// <summary>The ticket repository, recording every staged intent.</summary>
        private sealed class FakeTickets : ITicketRepository
        {
            private Ticket? _ticket = OpenTicket();

            /// <summary>The row the transitions read, or null for an identifier that names nothing.</summary>
            /// <remarks>
            /// Setting it seeds the flag and lifecycle the fake reports back, so an assertion about
            /// a transition reads the value the service actually staged on top of the row it found.
            /// </remarks>
            public Ticket? Ticket
            {
                get => _ticket;
                init
                {
                    _ticket = value;
                    AdminUnread = value?.AdminUnread ?? false;
                    UserUnread = value?.UserUnread ?? false;
                    Status = value?.Status;
                }
            }

            public NewTicket? Added { get; private set; }

            /// <summary>Whether a create staged the operator's flag raised - read from the row.</summary>
            public bool AdminUnreadAfterCreate { get; private set; }

            /// <summary>Whether a create staged the account's flag clear - read from the row.</summary>
            public bool UserUnreadAfterCreate { get; private set; }

            public bool AdminUnread { get; private set; }

            public bool UserUnread { get; private set; }

            public TicketStatus? Status { get; private set; }

            public bool Removed { get; private set; }

            public int ReplyCount { get; init; }

            public List<StagedReply> Replies { get; } = [];

            public bool? MarkedSide { get; private set; }

            public DateTimeOffset? MarkedAt { get; private set; }

            public int ClearedLatestCount { get; private set; }

            public bool? ClearedLatestSide { get; private set; }

            public Guid? PageOwner { get; private set; }

            public IReadOnlySet<UserRole>? VisibleRoles { get; private set; }

            public Task<TicketPage> GetPageAsync(
                Guid userId,
                int page,
                int pageSize,
                string? search,
                string? status,
                TicketSortField sortBy,
                TicketSortOrder sortOrder,
                CancellationToken cancellationToken = default)
            {
                PageOwner = userId;

                return Task.FromResult(new TicketPage
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });
            }

            public Task<AdminTicketPage> GetAdminPageAsync(
                IReadOnlySet<UserRole> visibleRoles,
                int page,
                int pageSize,
                string? search,
                string? status,
                TicketSortField sortBy,
                TicketSortOrder sortOrder,
                CancellationToken cancellationToken = default)
            {
                VisibleRoles = visibleRoles;

                return Task.FromResult(new AdminTicketPage
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });
            }

            public Task<Ticket?> GetAsync(
                Guid ticketId,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Ticket is { } ticket && ticket.Id == ticketId ? ticket : null);

            public Task<TicketDetail?> GetDetailAsync(
                Guid ticketId,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Ticket is { } ticket && ticket.Id == ticketId
                    ? new TicketDetail
                    {
                        Id = ticket.Id,
                        UserId = ticket.UserId,
                        Username = "owner",
                        Subject = ticket.Subject,
                        Status = ticket.Status,
                        AdminUnread = ticket.AdminUnread,
                        UserUnread = ticket.UserUnread,
                        CreatedAt = ticket.CreatedAt,
                        UpdatedAt = ticket.UpdatedAt,
                        Replies = []
                    }
                    : null);

            public Task<Guid> AddAsync(
                NewTicket ticket,
                CancellationToken cancellationToken = default)
            {
                Added = ticket;

                // The repository stages the row; these two are what its constructor sets, read
                // back here so the test asserts the *rule*, not the fake's own opinion.
                AdminUnreadAfterCreate = true;
                UserUnreadAfterCreate = false;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<Guid> AddReplyAsync(
                NewTicketReply reply,
                CancellationToken cancellationToken = default)
            {
                Replies.Add(new StagedReply(reply.IsAdminReply, reply.Message, null));

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<bool> SetStatusAsync(
                Guid ticketId,
                TicketStatus status,
                CancellationToken cancellationToken = default)
            {
                Status = status;

                return Task.FromResult(true);
            }

            public Task<bool> SetAdminUnreadAsync(
                Guid ticketId,
                bool unread,
                CancellationToken cancellationToken = default)
            {
                AdminUnread = unread;

                return Task.FromResult(true);
            }

            public Task<bool> SetUserUnreadAsync(
                Guid ticketId,
                bool unread,
                CancellationToken cancellationToken = default)
            {
                UserUnread = unread;

                return Task.FromResult(true);
            }

            public Task<int> MarkRepliesReadAsync(
                Guid ticketId,
                bool isAdminReply,
                DateTimeOffset readAt,
                CancellationToken cancellationToken = default)
            {
                MarkedSide = isAdminReply;
                MarkedAt = readAt;

                return Task.FromResult(0);
            }

            public Task<int> CountRepliesAsync(
                Guid ticketId,
                CancellationToken cancellationToken = default)
                => Task.FromResult(ReplyCount);

            public Task<bool> RemoveAsync(
                Guid ticketId,
                CancellationToken cancellationToken = default)
            {
                Removed = true;
                _ticket = null;

                return Task.FromResult(true);
            }

            public Task<bool> MarkLatestReplyUnreadAsync(
                Guid ticketId,
                bool isAdminReply,
                CancellationToken cancellationToken = default)
            {
                ClearedLatestCount += 1;
                ClearedLatestSide = isAdminReply;

                return Task.FromResult(true);
            }
        }

        /// <summary>The account repository, answering the role of the account behind a ticket.</summary>
        private sealed class FakeUsers : IUserRepository
        {
            private readonly UserRole _role;

            public FakeUsers(UserRole role) => _role = role;

            public Task<UserRole?> GetRoleAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => Task.FromResult<UserRole?>(_role);

            public Task<User?> FindByUsernameAsync(
                string username,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<User?> FindByIdAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AccountStanding> GetStandingAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsRegisteredAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> HasAdminAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<StoredProfile?> GetProfileAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<EditableAccount?> FindEditableByIdAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<EditableAccount?> FindEditableByEmailAsync(
                string email,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<StoredAccount?> FindForRegistrationByUsernameAsync(
                string username,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<StoredAccount?> FindForRegistrationByEmailAsync(
                string email,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<StoredAccount?> FindForPasswordResetAsync(
                string username,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsUsernameTakenAsync(
                string username,
                Guid? exceptUserId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsEmailTakenAsync(
                string email,
                Guid? exceptUserId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(
                NewAccount account,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task UpdatePendingRegistrationAsync(
                Guid userId,
                string passwordHash,
                string codeHash,
                DateTimeOffset expiresAt,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task ConfirmRegistrationAsync(
                Guid userId,
                DateTimeOffset registeredAt,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetUsernameAsync(
                Guid userId,
                string username,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetEmailAsync(
                Guid userId,
                string email,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetCreditsAsync(
                Guid userId,
                int credits,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetPasswordHashAsync(
                Guid userId,
                string passwordHash,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetTimeZoneAsync(
                Guid userId,
                string? timeZone,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetGroupAsync(
                Guid userId,
                Guid groupId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetStatusAsync(
                Guid userId,
                UserStatus status,
                DateTimeOffset? registeredAt,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetLoginGuardSwitchesAsync(
                Guid userId,
                bool geoProtectionEnabled,
                bool bindSessionToIp,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserPage> GetAdminPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? groupId,
                UserStatus? status,
                AdminUserSortField sortBy,
                AdminUserSortOrder sortOrder,
                IReadOnlyCollection<UserRole> visibleRoles,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminAccount?> GetAdminAccountAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserDetail?> GetAdminDetailAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task RemoveAsync(
                Guid userId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The unit of work, either committing or failing with what the test declares.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly Exception? _failure;

            public FakeUnitOfWork(Exception? failure = null) => _failure = failure;

            /// <summary>How many times the service asked for a commit.</summary>
            public int Saves { get; private set; }

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                Saves += 1;

                return _failure is null ? Task.CompletedTask : Task.FromException(_failure);
            }
        }

        /// <summary>One message the fake repository recorded.</summary>
        private sealed record StagedReply(bool IsAdminReply, string Message, DateTimeOffset? ReadMark);
    }
}
