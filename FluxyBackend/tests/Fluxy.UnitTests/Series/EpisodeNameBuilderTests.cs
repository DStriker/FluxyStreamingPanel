using Fluxy.Core.Series.Utils;

namespace Fluxy.UnitTests.Series
{
    public sealed class EpisodeNameBuilderTests
    {
        [Theory]
        [InlineData(true, true, "Breaking Bad S01E02")]
        [InlineData(true, false, "Breaking Bad S01")]
        [InlineData(false, true, "Breaking Bad E02")]
        [InlineData(false, false, "Breaking Bad")]
        public void BuildFollowsTheTwoCheckboxes(
            bool addSeason,
            bool addEpisode,
            string expected)
        {
            var name = EpisodeNameBuilder.Build("Breaking Bad", 1, 2, addSeason, addEpisode);

            Assert.Equal(expected, name);
        }

        [Fact]
        public void BuildPadsTheNumbersToTwoDigits()
        {
            var name = EpisodeNameBuilder.Build("Show", 3, 7, true, true);

            Assert.Equal("Show S03E07", name);
        }

        [Fact]
        public void BuildKeepsTheTagWhenTheSeriesNameHasToBeShortened()
        {
            var seriesName = new string('S', SeriesLimits.EpisodeNameMaxLength);

            var name = EpisodeNameBuilder.Build(seriesName, 1, 2, true, true);

            Assert.Equal(SeriesLimits.EpisodeNameMaxLength, name.Length);
            Assert.EndsWith(" S01E02", name, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildTrimsTheSeriesName()
        {
            var name = EpisodeNameBuilder.Build("  Show  ", 1, 1, true, true);

            Assert.Equal("Show S01E01", name);
        }
    }
}
