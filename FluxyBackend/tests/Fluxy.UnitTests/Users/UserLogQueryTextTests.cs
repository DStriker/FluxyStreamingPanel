using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The text-to-member table of the user log query: what each spelling names, and what the
    /// ISO 8601 instants it accepts mean.
    /// </summary>
    public sealed class UserLogQueryTextTests
    {
        [Theory]
        [InlineData("createdAt", UserLogSortField.CreatedAt)]
        [InlineData("createdat", UserLogSortField.CreatedAt)]
        [InlineData(" recordtype ", UserLogSortField.RecordType)]
        public void SortFieldsMapBySpelling(string value, UserLogSortField expected)
        {
            Assert.Equal(expected, UserLogQueryText.ParseSortField(value));
        }

        [Theory]
        [InlineData("amount")]
        [InlineData("recordType desc")]
        [InlineData("")]
        [InlineData(null)]
        public void AFieldThatNamesNothingAnswersNull(string? value)
        {
            Assert.Null(UserLogQueryText.ParseSortField(value));
        }

        [Theory]
        [InlineData("asc", UserLogSortOrder.Ascending)]
        [InlineData("Asc", UserLogSortOrder.Ascending)]
        [InlineData("desc", UserLogSortOrder.Descending)]
        public void SortOrdersMapBySpelling(string value, UserLogSortOrder expected)
        {
            Assert.Equal(expected, UserLogQueryText.ParseSortOrder(value));
        }

        [Theory]
        [InlineData("ascending")]
        [InlineData("")]
        [InlineData(null)]
        public void AnOrderThatNamesNothingAnswersNull(string? value)
        {
            Assert.Null(UserLogQueryText.ParseSortOrder(value));
        }

        [Theory]
        [InlineData("de", "DE")]
        [InlineData("DE", "DE")]
        [InlineData(" fr ", "FR")]
        public void ACountryIsCanonicalisedToUpperCase(string text, string expected)
        {
            Assert.Equal(expected, UserLogQueryText.NormalizeCountry(text));
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("Germany")]
        [InlineData("D")]
        [InlineData("")]
        [InlineData(null)]
        public void ACountryThatNamesNoCodeIsNull(string? text)
        {
            Assert.Null(UserLogQueryText.NormalizeCountry(text));
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
                UserLogQueryText.ParseInstant(value));
        }

        [Theory]
        [InlineData("05/01/2026")]
        [InlineData("yesterday")]
        [InlineData("")]
        [InlineData(null)]
        public void AValueThatIsNotAnInstantAnswersNull(string? value)
        {
            Assert.Null(UserLogQueryText.ParseInstant(value));
        }
    }
}
