using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The MAG theme vocabulary: the owner's five names in both directions.
    /// </summary>
    public sealed class MagThemeCatalogTests
    {
        [Theory]
        [InlineData("default", MagTheme.Default)]
        [InlineData("DIGITAL", MagTheme.Digital)]
        [InlineData("  Emerald ", MagTheme.Emerald)]
        [InlineData("CAPPUCINO", MagTheme.Cappucino)]
        [InlineData("Ocean Blue", MagTheme.OceanBlue)]
        public void ATitleParsesToItsMember(string value, MagTheme expected)
        {
            Assert.Equal(expected, MagThemeCatalog.Parse(value));
        }

        [Fact]
        public void CappucinoKeepsTheOwnersSpellingAndIsNotCorrected()
        {
            Assert.Equal(MagTheme.Cappucino, MagThemeCatalog.Parse("cappucino"));
            Assert.Null(MagThemeCatalog.Parse("cappuccino"));
            Assert.Equal("cappucino", MagThemeCatalog.NameOf(MagTheme.Cappucino));
        }

        [Theory]
        [InlineData(MagTheme.Default, "default")]
        [InlineData(MagTheme.Digital, "digital")]
        [InlineData(MagTheme.Emerald, "emerald")]
        [InlineData(MagTheme.Cappucino, "cappucino")]
        [InlineData(MagTheme.OceanBlue, "ocean blue")]
        public void EveryMemberIsNamedByItsWireSpelling(MagTheme theme, string expected)
        {
            Assert.Equal(expected, MagThemeCatalog.NameOf(theme));
            Assert.Equal(theme, MagThemeCatalog.Parse(expected));
        }

        [Theory]
        [InlineData("ocean")]
        [InlineData("blue")]
        [InlineData("oceanblue")]
        [InlineData("")]
        [InlineData(null)]
        public void ANameOutsideTheVocabularyIsRefused(string? value)
        {
            Assert.Null(MagThemeCatalog.Parse(value));
        }
    }
}
