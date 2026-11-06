using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;
using Xunit;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The list bounds of the credit log, and the fact that the enum values are schema.
    /// </summary>
    public sealed class CreditLogListLimitsTests
    {
        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, CreditLogListLimits.DefaultPageSize);
            Assert.Equal(1000, CreditLogListLimits.MaxPageSize);
            Assert.Equal(200, CreditLogListLimits.MaxSearchLength);
        }

        [Theory]
        [InlineData(CreditLogSortField.CreatedAt, 0)]
        [InlineData(CreditLogSortField.Amount, 1)]
        public void TheSortFieldNumbersAreTheSchema(CreditLogSortField field, int stored)
        {
            Assert.Equal(stored, (int)field);
        }

        [Theory]
        [InlineData(CreditLogSortOrder.Ascending, 0)]
        [InlineData(CreditLogSortOrder.Descending, 1)]
        public void TheSortOrderNumbersAreTheSchema(CreditLogSortOrder order, int stored)
        {
            Assert.Equal(stored, (int)order);
        }
    }
}
