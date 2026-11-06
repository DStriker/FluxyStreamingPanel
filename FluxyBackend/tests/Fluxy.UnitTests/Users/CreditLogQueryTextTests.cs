using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The text-to-member table of the credit log query: what each spelling names, and what the
    /// two ISO 8601 instants it accepts mean.
    /// </summary>
    public sealed class CreditLogQueryTextTests
    {
        [Theory]
        [InlineData("createdAt", CreditLogSortField.CreatedAt)]
        [InlineData("createdat", CreditLogSortField.CreatedAt)]
        [InlineData(" AMOUNT ", CreditLogSortField.Amount)]
        public void SortFieldsMapBySpelling(string value, CreditLogSortField expected)
        {
            Assert.Equal(expected, CreditLogQueryText.ParseSortField(value));
        }

        [Theory]
        [InlineData("name")]
        [InlineData("amount desc")]
        [InlineData("")]
        [InlineData(null)]
        public void AFieldThatNamesNothingAnswersNull(string? value)
        {
            Assert.Null(CreditLogQueryText.ParseSortField(value));
        }

        [Theory]
        [InlineData("asc", CreditLogSortOrder.Ascending)]
        [InlineData("Asc", CreditLogSortOrder.Ascending)]
        [InlineData("desc", CreditLogSortOrder.Descending)]
        public void SortOrdersMapBySpelling(string value, CreditLogSortOrder expected)
        {
            Assert.Equal(expected, CreditLogQueryText.ParseSortOrder(value));
        }

        [Theory]
        [InlineData("ascending")]
        [InlineData("")]
        [InlineData(null)]
        public void AnOrderThatNamesNothingAnswersNull(string? value)
        {
            Assert.Null(CreditLogQueryText.ParseSortOrder(value));
        }

        [Theory]
        [InlineData("2026-05-01", "2026-05-01T00:00:00Z")]
        [InlineData("2026-05-01T10:00", "2026-05-01T10:00:00Z")]
        [InlineData("2026-05-01T10:00:00", "2026-05-01T10:00:00Z")]
        [InlineData("2026-05-01t10:00:00z", "2026-05-01T10:00:00Z")]
        [InlineData("2026-05-01T10:00:00+03:00", "2026-05-01T07:00:00Z")]
        [InlineData("2026-05-01T10:00:00.250Z", "2026-05-01T10:00:00.250Z")]
        public void TheIsoSpellingsAreReadAsTheInstantTheyName(string value, string expected)
        {
            Assert.Equal(
                DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
                CreditLogQueryText.ParseInstant(value));
        }

        [Theory]
        [InlineData("05/01/2026")]
        [InlineData("yesterday")]
        [InlineData("")]
        [InlineData(null)]
        public void AValueThatIsNotAnInstantAnswersNull(string? value)
        {
            Assert.Null(CreditLogQueryText.ParseInstant(value));
        }
    }
}
