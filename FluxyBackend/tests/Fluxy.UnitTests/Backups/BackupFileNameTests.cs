using Fluxy.Core.Backups.Utils;

namespace Fluxy.UnitTests.Backups
{
    /// <summary>
    /// The one naming rule: the instant in the name, the collision suffixes and the parse back.
    /// </summary>
    /// <remarks>
    /// The format is the record - there is no metadata row - so these assertions are the contract
    /// rotation and the scheduler both stand on: lexicographic order must equal chronological
    /// order, and a suffixed name must still report its own instant.
    /// </remarks>
    public sealed class BackupFileNameTests
    {
        private static readonly DateTimeOffset Instant =
            new(2026, 11, 23, 10, 15, 0, TimeSpan.Zero);

        [Fact]
        public void BuildWritesTheUtcInstantInTheDocumentedShape()
        {
            Assert.Equal("fluxy-20261123T101500Z.dump", BackupFileName.Build(Instant));
        }

        [Fact]
        public void BuildConvertsANonUtcOffsetToUtc()
        {
            var berlin = new DateTimeOffset(2026, 11, 23, 11, 15, 0, TimeSpan.FromHours(1));

            Assert.Equal("fluxy-20261123T101500Z.dump", BackupFileName.Build(berlin));
        }

        [Fact]
        public void BuildDropsSubSecondPrecision()
        {
            var instant = Instant.AddMilliseconds(789);

            Assert.Equal("fluxy-20261123T101500Z.dump", BackupFileName.Build(instant));
        }

        [Theory]
        [InlineData(2, "fluxy-20261123T101500Z-2.dump")]
        [InlineData(9, "fluxy-20261123T101500Z-9.dump")]
        [InlineData(1, "fluxy-20261123T101500Z-2.dump")]
        [InlineData(10, "fluxy-20261123T101500Z-9.dump")]
        public void BuildCollisionAppendsTheBoundedSuffix(int attempt, string expected)
        {
            Assert.Equal(expected, BackupFileName.BuildCollision(Instant, attempt));
        }

        [Theory]
        [InlineData("fluxy-20261123T101500Z.dump")]
        [InlineData("fluxy-20261123T101500Z-2.dump")]
        [InlineData("fluxy-20261123T101500Z-9.dump")]
        public void TryParseReadsTheInstantAndIgnoresTheSuffix(string fileName)
        {
            Assert.True(BackupFileName.TryParse(fileName, out var utc));
            Assert.Equal(Instant, utc);
            Assert.Equal(TimeSpan.Zero, utc.Offset);
        }

        [Theory]
        [InlineData("")]
        [InlineData("fluxy-20261123T101500Z")]
        [InlineData("fluxy-20261123T101500Z.dump.tmp")]
        [InlineData("fluxy-20261123T101500Z-1.dump")]
        [InlineData("fluxy-20261123T101500Z-10.dump")]
        [InlineData("other-20261123T101500Z.dump")]
        [InlineData("fluxy-nonsense.dump")]
        [InlineData("fluxy-20261323T101500Z.dump")]
        public void TryParseRefusesEverythingElse(string fileName)
        {
            Assert.False(BackupFileName.TryParse(fileName, out _));
        }

        [Fact]
        public void LexicographicOrderEqualsChronologicalOrder()
        {
            var names = new[]
            {
                BackupFileName.Build(Instant.AddHours(3)),
                BackupFileName.Build(Instant),
                BackupFileName.Build(Instant.AddDays(1))
            };

            var ordered = names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.Equal(
                [
                    "fluxy-20261123T101500Z.dump",
                    "fluxy-20261123T131500Z.dump",
                    "fluxy-20261124T101500Z.dump"
                ],
                ordered);
        }

        [Fact]
        public void ASuffixedNameSortsBeforeThePlainNameOfItsOwnSecond()
        {
            var plain = BackupFileName.Build(Instant);
            var suffixed = BackupFileName.BuildCollision(Instant, 2);

            // `.` (0x2E) compares higher than `-` (0x2D), so the unsuffixed name is the greater
            // of the two. The important part is that the comparison is total and ordinal: nothing
            // about a suffixed name is unorderable.
            Assert.True(string.CompareOrdinal(suffixed, plain) < 0);
        }
    }
}
