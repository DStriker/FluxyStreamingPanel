using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Utils;

namespace Fluxy.UnitTests.Tickets
{
    /// <summary>
    /// The derivation of the word a client reads from a ticket's stored lifecycle and one side's
    /// unread flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The matrix is the whole contract: a closed ticket reads as closed whatever either flag says,
    /// an open one reads as unread or read by the flag of the side being drawn, and the two sides
    /// never read each other's flag. A test that only checked one combination would pass over an
    /// implementation that ignored the lifecycle or swapped the sides.
    /// </para>
    /// <para>
    /// <c>ForViewer</c> is the rule; <c>ForAdmin</c> and <c>ForUser</c> name the side explicitly.
    /// Both are exercised, because the pair is what the two entrances call and a wrapper that
    /// passes the wrong flag would only show up here.
    /// </para>
    /// </remarks>
    public sealed class TicketStatusesTests
    {
        [Theory]
        [InlineData(false, TicketStatuses.Read)]
        [InlineData(true, TicketStatuses.Unread)]
        public void AnOpenTicketReadsByTheViewersOwnFlag(bool unread, string expected)
        {
            Assert.Equal(expected, TicketStatuses.ForViewer(TicketStatus.Open, unread));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AClosedTicketReadsClosedWhateverTheFlagSays(bool unread)
        {
            Assert.Equal(TicketStatuses.Closed, TicketStatuses.ForViewer(TicketStatus.Closed, unread));
        }

        [Fact]
        public void TheAdminSideReadsTheAdminFlag()
        {
            Assert.Equal(TicketStatuses.Unread, TicketStatuses.ForAdmin(TicketStatus.Open, true));
            Assert.Equal(TicketStatuses.Read, TicketStatuses.ForAdmin(TicketStatus.Open, false));
        }

        [Fact]
        public void TheUserSideReadsTheUserFlag()
        {
            Assert.Equal(TicketStatuses.Unread, TicketStatuses.ForUser(TicketStatus.Open, true));
            Assert.Equal(TicketStatuses.Read, TicketStatuses.ForUser(TicketStatus.Open, false));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClosedBeatsEitherSidesFlag(bool unread)
        {
            Assert.Equal(TicketStatuses.Closed, TicketStatuses.ForUser(TicketStatus.Closed, unread));
            Assert.Equal(TicketStatuses.Closed, TicketStatuses.ForAdmin(TicketStatus.Closed, unread));
        }

        [Fact]
        public void TheWordsAreTheWireContract()
        {
            Assert.Equal("unread", TicketStatuses.Unread);
            Assert.Equal("read", TicketStatuses.Read);
            Assert.Equal("closed", TicketStatuses.Closed);
            Assert.Equal([TicketStatuses.Unread, TicketStatuses.Read, TicketStatuses.Closed], TicketStatuses.All);
        }

        [Theory]
        [InlineData("unread")]
        [InlineData("UNREAD")]
        [InlineData(" read ")]
        [InlineData("Closed")]
        public void AKnownWordIsRecognisedInAnyCase(string value)
        {
            Assert.True(TicketStatuses.IsKnown(value));
        }

        [Theory]
        [InlineData("open")]
        [InlineData("archived")]
        [InlineData("")]
        [InlineData(null)]
        public void AnUnknownWordIsNotKnown(string? value)
        {
            Assert.False(TicketStatuses.IsKnown(value));
        }

        [Fact]
        public void TheSidesAreIndependent()
        {
            // An open ticket unread for the account is not unread for the operator: the two flags
            // answer two different questions about the same conversation.
            Assert.Equal(TicketStatuses.Unread, TicketStatuses.ForUser(TicketStatus.Open, true));
            Assert.Equal(TicketStatuses.Read, TicketStatuses.ForAdmin(TicketStatus.Open, false));
        }
    }
}
