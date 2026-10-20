using Fluxy.Application.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class TimeZonePolicyTests
    {
        [Fact]
        public void TryNormalizeTreatsEmptyValuesAsFollowTheBrowser()
        {
            Assert.True(TimeZonePolicy.TryNormalize(null, out var nullZone));
            Assert.Null(nullZone);

            Assert.True(TimeZonePolicy.TryNormalize("", out var emptyZone));
            Assert.Null(emptyZone);

            Assert.True(TimeZonePolicy.TryNormalize("   ", out var whitespaceZone));
            Assert.Null(whitespaceZone);
        }

        [Fact]
        public void TryNormalizeAcceptsAKnownZoneAndTrimsIt()
        {
            var known = TimeZoneInfo.Utc.Id;

            Assert.True(TimeZonePolicy.TryNormalize($" {known} ", out var zone));
            Assert.Equal(known, zone);
        }

        [Theory]
        [InlineData("Mars/OlympusMons")]
        [InlineData("Not/AZone")]
        public void TryNormalizeRefusesAnUnknownZone(string zone)
        {
            Assert.False(TimeZonePolicy.TryNormalize(zone, out var normalized));
            Assert.Null(normalized);
        }

        [Fact]
        public void TryNormalizeRefusesAnIdentifierLongerThanTheColumn()
        {
            Assert.False(TimeZonePolicy.TryNormalize(
                new string('a', TimeZonePolicy.MaxLength + 1),
                out var zone));
            Assert.Null(zone);
        }

        [Fact]
        public void TryNormalizeChecksTheLengthBeforeResolving()
        {
            var tooLong = TimeZoneInfo.Utc.Id + new string('a', TimeZonePolicy.MaxLength);

            Assert.False(TimeZonePolicy.TryNormalize(tooLong, out _));
        }
    }
}
