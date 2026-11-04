using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The text a connection log query accepts, and the default each parameter takes when it names
    /// nothing at all.
    /// </summary>
    /// <remarks>
    /// These are the spellings the screen and a hand-written client both use, and every one of
    /// them is refused rather than dropped when it names nothing: a filter that silently widened
    /// to "everything" would answer a request the caller did not make. The parsing lives in Core
    /// rather than in the controller so exactly this can be pinned without a running host.
    /// </remarks>
    public sealed class ConnectionLogQueryTextTests
    {
        [Theory]
        [InlineData("video", ConnectionStreamKind.VideoStream)]
        [InlineData("audio", ConnectionStreamKind.AudioStream)]
        [InlineData("VIDEO", ConnectionStreamKind.VideoStream)]
        [InlineData("Audio", ConnectionStreamKind.AudioStream)]
        [InlineData("  audio  ", ConnectionStreamKind.AudioStream)]
        public void AKindIsReadWithoutRegardToCaseOrSurroundingSpace(
            string text,
            ConnectionStreamKind expected)
        {
            Assert.Equal(expected, ConnectionLogQueryText.ParseStreamKind(text));
        }

        [Theory]
        [InlineData("videostream")]
        [InlineData("stream")]
        [InlineData("1")]
        [InlineData("")]
        [InlineData(null)]
        public void AKindThatNamesNoMemberIsNull(string? text)
        {
            Assert.Null(ConnectionLogQueryText.ParseStreamKind(text));
        }

        [Theory]
        [InlineData("live", true)]
        [InlineData("all", false)]
        [InlineData("LIVE", true)]
        [InlineData("All", false)]
        public void AStateIsReadWithoutRegardToCase(string text, bool expected)
        {
            Assert.Equal(expected, ConnectionLogQueryText.ParseLiveOnly(text));
        }

        [Theory]
        [InlineData("ended")]
        [InlineData("finished")]
        [InlineData("0")]
        [InlineData("")]
        [InlineData(null)]
        public void AStateThatNamesNeitherIsNull(string? text)
        {
            Assert.Null(ConnectionLogQueryText.ParseLiveOnly(text));
        }

        [Theory]
        [InlineData("startedAt", ConnectionLogSortField.StartedAt)]
        [InlineData("STARTEDAT", ConnectionLogSortField.StartedAt)]
        [InlineData("endedAt", ConnectionLogSortField.EndedAt)]
        [InlineData(" endedat ", ConnectionLogSortField.EndedAt)]
        public void ASortFieldIsReadWithoutRegardToCase(
            string text,
            ConnectionLogSortField expected)
        {
            Assert.Equal(expected, ConnectionLogQueryText.ParseSortField(text));
        }

        [Theory]
        [InlineData("streamTitle")]
        [InlineData("username")]
        [InlineData("started")]
        [InlineData("1")]
        [InlineData(null)]
        public void ASortFieldThatNamesNoColumnIsNull(string? text)
        {
            Assert.Null(ConnectionLogQueryText.ParseSortField(text));
        }

        [Theory]
        [InlineData("asc", ConnectionLogSortOrder.Ascending)]
        [InlineData("ASC", ConnectionLogSortOrder.Ascending)]
        [InlineData("desc", ConnectionLogSortOrder.Descending)]
        [InlineData(" Desc ", ConnectionLogSortOrder.Descending)]
        public void ASortOrderIsReadWithoutRegardToCase(
            string text,
            ConnectionLogSortOrder expected)
        {
            Assert.Equal(expected, ConnectionLogQueryText.ParseSortOrder(text));
        }

        [Theory]
        [InlineData("downwards")]
        [InlineData("")]
        [InlineData(null)]
        public void ASortOrderThatNamesNeitherIsNull(string? text)
        {
            Assert.Null(ConnectionLogQueryText.ParseSortOrder(text));
        }

        [Theory]
        [InlineData("de", "DE")]
        [InlineData("DE", "DE")]
        [InlineData(" fr ", "FR")]
        public void ACountryIsCanonicalisedToUpperCase(string text, string expected)
        {
            Assert.Equal(expected, ConnectionLogQueryText.NormalizeCountry(text));
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("Germany")]
        [InlineData("D")]
        [InlineData("")]
        [InlineData(null)]
        public void ACountryThatNamesNoCodeIsNull(string? text)
        {
            Assert.Null(ConnectionLogQueryText.NormalizeCountry(text));
        }

        [Theory]
        [InlineData(ConnectionStreamKind.VideoStream, "video")]
        [InlineData(ConnectionStreamKind.AudioStream, "audio")]
        public void TheNameOfAKindIsTheSpellingTheFilterAccepts(
            ConnectionStreamKind kind,
            string expected)
        {
            // One vocabulary in both directions: a client that reads a name back must be able to
            // send it again unchanged.
            Assert.Equal(expected, ConnectionLogQueryText.NameOf(kind));
            Assert.Equal(kind, ConnectionLogQueryText.ParseStreamKind(expected));
        }
    }
}
