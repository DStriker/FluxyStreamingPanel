using Fluxy.Core.Settings.Utils;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The set-top-box catalogue: its size, its order, and the canon the two lists are stored in.
    /// </summary>
    public sealed class StbTypeCatalogTests
    {
        [Fact]
        public void TheCatalogHoldsExactlyThirtyOneEntriesInTheOwnersOrder()
        {
            Assert.Equal(31, StbTypeCatalog.All.Count);
            Assert.Equal(
                "Aura HD, Aura HD2, Aura HD3, Aura HD4, Aura HD5, Aura HD6, Aura HD7, Aura HD8, " +
                "Aura HD9, MAG200, MAG245, MAG245D, MAG250, MAG254, MAG255, MAG256, MAG257, " +
                "MAG260, MAG270, MAG275, MAG322, MAG323, MAG324, MAG325, MAG349, MAG350, " +
                "MAG351, MAG352, MAG420, MAG520, WR320",
                string.Join(", ", StbTypeCatalog.All));
        }

        [Fact]
        public void EveryEntryIsUnique()
        {
            Assert.Equal(StbTypeCatalog.All.Count, StbTypeCatalog.All.Distinct().Count());
        }

        [Theory]
        [InlineData("MAG255", "MAG255")]
        [InlineData("mag255", "MAG255")]
        [InlineData("  Mag255  ", "MAG255")]
        [InlineData("aura hd9", "Aura HD9")]
        [InlineData("WR320", "WR320")]
        public void ANameIsCanonicalizedCaseInsensitively(string value, string expected)
        {
            Assert.True(StbTypeCatalog.TryCanonicalize(value, out var canonical));
            Assert.Equal(expected, canonical);
        }

        [Theory]
        [InlineData("MAG999")]
        [InlineData("MAG")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ANameOutsideTheCatalogIsRefused(string? value)
        {
            Assert.False(StbTypeCatalog.TryCanonicalize(value, out var canonical));
            Assert.Equal(string.Empty, canonical);
        }

        [Fact]
        public void NormalizeAnswersCanonicalSpellingsInFirstOccurrenceOrderWithoutRepetition()
        {
            var normalized = StbTypeCatalog.Normalize(["mag255", "MAG256", "MAG255", "aura hd"]);

            Assert.Equal(["MAG255", "MAG256", "Aura HD"], normalized);
        }

        [Fact]
        public void NormalizeAnswersAnEmptyListWhenNothingWasNamed()
        {
            Assert.Empty(StbTypeCatalog.Normalize(null));
            Assert.Empty(StbTypeCatalog.Normalize([]));
        }
    }
}
