using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The column widths and list bounds of the stream problem journal, and the numeric vocabulary
    /// of its sort choices.
    /// </summary>
    /// <remarks>
    /// The message width is shared by the table configuration and the write boundary, so a test
    /// here is what keeps them from drifting apart silently. The sort field and order are
    /// query-contract only and are never persisted; their numbers are pinned so the choices a
    /// client sends and the members this API resolves stay paired.
    /// </remarks>
    public sealed class StreamProblemListLimitsTests
    {
        [Fact]
        public void TheStoredWidthIsTheOneTheColumnDeclares()
        {
            Assert.Equal(1000, StreamProblemListLimits.ErrorMessageMaxLength);
        }

        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, StreamProblemListLimits.DefaultPageSize);
            Assert.Equal(1000, StreamProblemListLimits.MaxPageSize);
            Assert.Equal(200, StreamProblemListLimits.MaxSearchLength);
        }

        [Fact]
        public void TheDefaultPageSizeIsInsideTheCeiling()
        {
            Assert.InRange(
                StreamProblemListLimits.DefaultPageSize,
                1,
                StreamProblemListLimits.MaxPageSize);
        }

        [Theory]
        [InlineData(StreamProblemSortField.CreatedAt, 0)]
        public void TheSortFieldNumbersAreTheContract(StreamProblemSortField field, int stored)
        {
            Assert.Equal(stored, (int)field);
        }

        [Theory]
        [InlineData(StreamProblemSortOrder.Ascending, 0)]
        [InlineData(StreamProblemSortOrder.Descending, 1)]
        public void TheSortOrderNumbersAreTheContract(StreamProblemSortOrder order, int stored)
        {
            Assert.Equal(stored, (int)order);
        }
    }
}
