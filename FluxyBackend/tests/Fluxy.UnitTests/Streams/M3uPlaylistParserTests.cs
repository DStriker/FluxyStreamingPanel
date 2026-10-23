using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The lines of a playlist that become streams, and the lines that become problems.
    /// </summary>
    /// <remarks>
    /// The parser is the one place a file becomes rows, so these tests exercise the file shapes
    /// an operator actually uploads: a title with a comma, a name only in <c>tvg-name</c>, a
    /// directive with no address after it, and a file that is an HLS playlist rather than a
    /// list of channels. A parser that threw on the first bad line would turn a five thousand
    /// line file into a one-upload-per-line chore.
    /// </remarks>
    public sealed class M3uPlaylistParserTests
    {
        [Fact]
        public void ATitleAfterTheCommaNamesTheEntry()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTM3U\r\n"
                + "#EXTINF:-1 tvg-id=\"news\" group-title=\"General\",News HD\r\n"
                + "https://example.test/news\r\n");

            Assert.Equal(M3uPlaylistStatus.Parsed, outcome.Status);
            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("News HD", entry.Name);
            Assert.Equal("https://example.test/news", entry.Source);
            Assert.Equal(2, entry.LineNumber);
            Assert.Empty(outcome.Problems);
        }

        [Fact]
        public void TvgNameIsUsedWhenTheTitleIsEmpty()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1 tvg-name=\"Sport, live\",\n"
                + "https://example.test/sport\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("Sport, live", entry.Name);
        }

        [Fact]
        public void AnEntryWithNoNameGetsTheFallback()
        {
            var outcome = M3uPlaylistParser.Parse(
                "https://example.test/one\n"
                + "#EXTINF:-1,\n"
                + "https://example.test/two\n");

            Assert.Equal(
                new[] { "Stream 1", "Stream 2" },
                outcome.Entries.Select(entry => entry.Name));
        }

        [Fact]
        public void ACommaInsideTheTitleIsKept()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1,News, weather and sport\n"
                + "https://example.test/three\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("News, weather and sport", entry.Name);
        }

        [Fact]
        public void CommentsBetweenTheDirectiveAndTheAddressDoNotBreakThePair()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1,News\n"
                + "#EXTVLCOPT:http-user-agent=test\n"
                + "https://example.test/news\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("News", entry.Name);
            Assert.Empty(outcome.Problems);
        }

        [Fact]
        public void BadLinesAreReportedWithoutLosingTheGoodOnes()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1,No address\n"
                + "#EXTINF:-1,Bad address\n"
                + "not a url\n"
                + "#EXTINF:-1,Good\n"
                + "https://example.test/good\n");

            Assert.Equal(M3uPlaylistStatus.Parsed, outcome.Status);

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("Good", entry.Name);

            Assert.Equal(2, outcome.Problems.Count);
            Assert.Equal("No address", outcome.Problems[0].Name);
            Assert.Equal("Bad address", outcome.Problems[1].Name);
        }

        [Fact]
        public void ADanglingDirectiveAtTheEndIsReported()
        {
            var outcome = M3uPlaylistParser.Parse("#EXTINF:-1,Missing\n");

            Assert.Empty(outcome.Entries);
            var problem = Assert.Single(outcome.Problems);
            Assert.Equal(1, problem.LineNumber);
            Assert.Equal("Missing", problem.Name);
        }

        [Fact]
        public void AnHlsPlaylistIsRefusedWhole()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTM3U\n"
                + "#EXT-X-STREAM-INF:BANDWIDTH=1280000\n"
                + "https://example.test/low.m3u8\n");

            Assert.Equal(M3uPlaylistStatus.Hls, outcome.Status);
            Assert.Empty(outcome.Entries);
        }

        [Fact]
        public void AnEmptyFileIsEmpty()
        {
            Assert.Equal(M3uPlaylistStatus.Empty, M3uPlaylistParser.Parse(" \r\n\t").Status);
            Assert.Equal(M3uPlaylistStatus.Empty, M3uPlaylistParser.Parse(null).Status);
        }

        [Fact]
        public void AFileWithOnlyCommentsIsUnreadable()
        {
            var outcome = M3uPlaylistParser.Parse("#EXTM3U\n#PLAYLIST:Something\n");

            Assert.Equal(M3uPlaylistStatus.Unreadable, outcome.Status);
        }

        [Fact]
        public void MoreEntriesThanTheLimitAreRefusedWhole()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1,One\n"
                + "https://example.test/one\n"
                + "#EXTINF:-1,Two\n"
                + "https://example.test/two\n",
                maxEntries: 1);

            Assert.Equal(M3uPlaylistStatus.TooManyEntries, outcome.Status);
        }

        [Fact]
        public void ABomAndCrlfDoNotBecomePartOfTheFirstEntry()
        {
            var outcome = M3uPlaylistParser.Parse(
                "\uFEFF#EXTM3U\r\n"
                + "#EXTINF:-1,First\r\n"
                + "https://example.test/first\r\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("First", entry.Name);
            Assert.Equal("https://example.test/first", entry.Source);
        }

        [Fact]
        public void TvgLogoIsCarriedOnTheEntry()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1 tvg-logo=\"https://example.test/logo.png\",News\n"
                + "https://example.test/news\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("News", entry.Name);
            Assert.Equal("https://example.test/logo.png", entry.LogoUrl);
            Assert.Equal("https://example.test/news", entry.Source);
        }

        [Fact]
        public void MissingTvgLogoLeavesNull()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1,News\n"
                + "https://example.test/news\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Null(entry.LogoUrl);
        }

        [Fact]
        public void QuotedTvgLogoWithCommaIsKept()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1 tvg-logo=\"https://example.test/logo,a.png\",News\n"
                + "https://example.test/news\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Equal("https://example.test/logo,a.png", entry.LogoUrl);
        }

        [Fact]
        public void BlankTvgLogoLeavesNull()
        {
            var outcome = M3uPlaylistParser.Parse(
                "#EXTINF:-1 tvg-logo=\"   \",News\n"
                + "https://example.test/news\n");

            var entry = Assert.Single(outcome.Entries);
            Assert.Null(entry.LogoUrl);
        }
    }
}
