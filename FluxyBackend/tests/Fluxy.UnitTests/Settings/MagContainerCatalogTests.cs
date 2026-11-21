using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The MAG container vocabulary: the canonical answer and the one alias the subscription
    /// formats already accept.
    /// </summary>
    public sealed class MagContainerCatalogTests
    {
        [Theory]
        [InlineData("mpeg-ts", MagContainer.MpegTs)]
        [InlineData("mpegts", MagContainer.MpegTs)]
        [InlineData("MPEG-TS", MagContainer.MpegTs)]
        [InlineData("  hls  ", MagContainer.Hls)]
        public void ANameParsesToItsMember(string value, MagContainer expected)
        {
            Assert.Equal(expected, MagContainerCatalog.Parse(value));
        }

        [Theory]
        [InlineData(MagContainer.MpegTs, "mpeg-ts")]
        [InlineData(MagContainer.Hls, "hls")]
        public void EveryMemberIsNamedByItsCanonicalSpelling(MagContainer container, string expected)
        {
            Assert.Equal(expected, MagContainerCatalog.NameOf(container));
        }

        [Fact]
        public void TheAliasAndThePolicySpellingBothAnswerTheCanonicalName()
        {
            var fromAlias = MagContainerCatalog.Parse("mpegts");

            Assert.NotNull(fromAlias);
            Assert.Equal("mpeg-ts", MagContainerCatalog.NameOf(fromAlias.Value));
        }

        [Theory]
        [InlineData("ts")]
        [InlineData("mpeg")]
        [InlineData("")]
        [InlineData(null)]
        public void ANameOutsideTheVocabularyIsRefused(string? value)
        {
            Assert.Null(MagContainerCatalog.Parse(value));
        }
    }
}
