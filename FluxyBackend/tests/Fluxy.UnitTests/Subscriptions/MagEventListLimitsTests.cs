using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The column width and list bounds of the MAG event table, and the stored numbers of its
    /// enums.
    /// </summary>
    /// <remarks>
    /// The message width is shared by the table configuration and the write rule, so a test here
    /// is what keeps them from drifting apart silently. The enum values are pinned because they
    /// are already schema: renumbering one would reinterpret every stored row without a single
    /// query being made.
    /// </remarks>
    public sealed class MagEventListLimitsTests
    {
        [Fact]
        public void TheMessageWidthIsTheOneTheColumnDeclares()
        {
            Assert.Equal(1000, MagEventListLimits.MessageMaxLength);
        }

        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, MagEventListLimits.DefaultPageSize);
            Assert.Equal(1000, MagEventListLimits.MaxPageSize);
            Assert.Equal(200, MagEventListLimits.MaxSearchLength);
        }

        [Fact]
        public void TheDefaultPageSizeIsInsideTheCeiling()
        {
            Assert.InRange(
                MagEventListLimits.DefaultPageSize,
                1,
                MagEventListLimits.MaxPageSize);
        }

        [Theory]
        [InlineData(MagEventType.PlayChannel, 1)]
        [InlineData(MagEventType.ClosePortal, 2)]
        [InlineData(MagEventType.ReloadPortal, 3)]
        [InlineData(MagEventType.RebootDevice, 4)]
        [InlineData(MagEventType.SendMessage, 5)]
        [InlineData(MagEventType.ResetStbLock, 6)]
        public void TheEventTypeNumbersAreTheSchema(MagEventType type, int stored)
        {
            Assert.Equal(stored, (int)type);
        }

        [Theory]
        [InlineData(MagEventStatus.NotConfirmed, 0)]
        [InlineData(MagEventStatus.Confirmed, 1)]
        public void TheStatusNumbersAreTheSchema(MagEventStatus status, int stored)
        {
            Assert.Equal(stored, (int)status);
        }

        [Theory]
        [InlineData(MagEventSortField.CreatedAt, 0)]
        public void TheSortFieldNumbersAreTheContract(MagEventSortField field, int stored)
        {
            Assert.Equal(stored, (int)field);
        }

        [Theory]
        [InlineData(MagEventSortOrder.Asc, 0)]
        [InlineData(MagEventSortOrder.Desc, 1)]
        public void TheSortOrderNumbersAreTheContract(MagEventSortOrder order, int stored)
        {
            Assert.Equal(stored, (int)order);
        }
    }
}
