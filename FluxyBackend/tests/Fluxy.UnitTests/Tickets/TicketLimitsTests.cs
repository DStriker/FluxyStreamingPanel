using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Utils;

namespace Fluxy.UnitTests.Tickets
{
    /// <summary>
    /// The column widths and list bounds of the support ticket surface, and the numeric vocabulary
    /// of its status and sort choices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The widths are shared by the table configurations and the write boundary, so a test here is
    /// what keeps them from drifting apart silently. Only <see cref="TicketStatus"/> is schema: its
    /// values are already written into rows, so renumbering one would reinterpret every stored
    /// ticket as a different state without a single query being made.
    /// </para>
    /// <para>
    /// <see cref="TicketSortField"/> and <see cref="TicketSortOrder"/> are query-contract only and
    /// are never persisted; their numbers are pinned so the choices a client sends and the members
    /// this API resolves stay paired. <see cref="TicketAction"/> is not persisted either, and its
    /// numbers are pinned for the same reason.
    /// </para>
    /// </remarks>
    public sealed class TicketLimitsTests
    {
        [Fact]
        public void TheStoredWidthsAreTheOnesTheColumnsDeclare()
        {
            Assert.Equal(200, TicketLimits.SubjectMaxLength);
            Assert.Equal(4000, TicketLimits.MessageMaxLength);
        }

        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, TicketLimits.DefaultPageSize);
            Assert.Equal(1000, TicketLimits.MaxPageSize);
            Assert.Equal(200, TicketLimits.MaxSearchLength);
        }

        [Fact]
        public void TheThreadBoundIsTheRatifiedOne()
        {
            Assert.Equal(500, TicketLimits.MaxRepliesPerTicket);
        }

        [Fact]
        public void TheDefaultPageSizeIsInsideTheCeiling()
        {
            Assert.InRange(TicketLimits.DefaultPageSize, 1, TicketLimits.MaxPageSize);
        }

        [Theory]
        [InlineData(TicketStatus.Open, 1)]
        [InlineData(TicketStatus.Closed, 2)]
        public void TheStatusNumbersAreTheSchema(TicketStatus status, int stored)
        {
            Assert.Equal(stored, (int)status);
        }

        [Theory]
        [InlineData(TicketSortField.Subject, 0)]
        [InlineData(TicketSortField.Username, 1)]
        [InlineData(TicketSortField.CreatedAt, 2)]
        [InlineData(TicketSortField.LastReplyAt, 3)]
        public void TheSortFieldNumbersAreTheContract(TicketSortField field, int stored)
        {
            Assert.Equal(stored, (int)field);
        }

        [Theory]
        [InlineData(TicketSortOrder.Ascending, 0)]
        [InlineData(TicketSortOrder.Descending, 1)]
        public void TheSortOrderNumbersAreTheContract(TicketSortOrder order, int stored)
        {
            Assert.Equal(stored, (int)order);
        }

        [Theory]
        [InlineData(TicketAction.Created, 0)]
        [InlineData(TicketAction.Replied, 1)]
        [InlineData(TicketAction.Closed, 2)]
        [InlineData(TicketAction.Reopened, 3)]
        [InlineData(TicketAction.NotFound, 4)]
        [InlineData(TicketAction.Invalid, 5)]
        public void TheActionNumbersAreTheContract(TicketAction action, int stored)
        {
            Assert.Equal(stored, (int)action);
        }

        [Fact]
        public void TheTwoSidesAreNamed()
        {
            Assert.Equal("admin", TicketReplySides.Admin);
            Assert.Equal("user", TicketReplySides.User);
            Assert.Equal(TicketReplySides.Admin, TicketReplySides.Of(isAdminReply: true));
            Assert.Equal(TicketReplySides.User, TicketReplySides.Of(isAdminReply: false));
        }

        [Fact]
        public void AMessageRaisesTheOtherSidesFlagAndOnlyThatOne()
        {
            Assert.True(TicketReplySides.RaisesAdminUnread(isAdminReply: false));
            Assert.False(TicketReplySides.RaisesUserUnread(isAdminReply: false));

            Assert.True(TicketReplySides.RaisesUserUnread(isAdminReply: true));
            Assert.False(TicketReplySides.RaisesAdminUnread(isAdminReply: true));
        }
    }
}
