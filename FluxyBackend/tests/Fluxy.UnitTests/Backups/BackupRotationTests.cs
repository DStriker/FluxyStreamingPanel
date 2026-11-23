using Fluxy.Application.Backups.Utils;

namespace Fluxy.UnitTests.Backups
{
    /// <summary>
    /// The rotation decision: what is kept, what is removed and what is never touched.
    /// </summary>
    public sealed class BackupRotationTests
    {
        private static readonly string[] Names =
        [
            "fluxy-20260101T000000Z.dump",
            "fluxy-20260102T000000Z.dump",
            "fluxy-20260103T000000Z.dump",
            "fluxy-20260104T000000Z.dump"
        ];

        [Fact]
        public void ZeroKeepsEveryArchive()
        {
            Assert.Empty(BackupRotation.SelectForDeletion(Names, 0));
        }

        [Fact]
        public void ANegativeCountKeepsEveryArchiveToo()
        {
            Assert.Empty(BackupRotation.SelectForDeletion(Names, -1));
        }

        [Fact]
        public void ACountAtOrAboveTheFileCountRemovesNothing()
        {
            Assert.Empty(BackupRotation.SelectForDeletion(Names, Names.Length));
            Assert.Empty(BackupRotation.SelectForDeletion(Names, Names.Length + 10));
        }

        [Fact]
        public void TheOldestFilesPastTheCountAreRemoved()
        {
            var removed = BackupRotation.SelectForDeletion(Names, 2);

            Assert.Equal(
                ["fluxy-20260101T000000Z.dump", "fluxy-20260102T000000Z.dump"],
                removed);
        }

        [Fact]
        public void KeepOneRemovesEveryOlderArchive()
        {
            var removed = BackupRotation.SelectForDeletion(Names, 1);

            Assert.Equal(Names[..^1], removed);
        }

        [Fact]
        public void NonMatchingNamesAreNeverTouched()
        {
            var mixed = new[]
            {
                "fluxy-20260101T000000Z.dump",
                "fluxy-20260102T000000Z.dump",
                "operator-copy.dump",
                "fluxy-20260103T000000Z.dump.tmp",
                "notes.txt"
            };

            var removed = BackupRotation.SelectForDeletion(mixed, 1);

            Assert.Equal(["fluxy-20260101T000000Z.dump"], removed);
        }

        [Fact]
        public void ACollisionSuffixIsOrderedByTheSameRuleAsEveryOtherName()
        {
            var names = new[]
            {
                "fluxy-20260101T000000Z.dump",
                "fluxy-20260101T000000Z-2.dump",
                "fluxy-20260101T000000Z-3.dump"
            };

            // Descending ordinal: the plain name (`.` > `-`) is newest-looking, then -3, then -2.
            var removed = BackupRotation.SelectForDeletion(names, 2);

            Assert.Equal(["fluxy-20260101T000000Z-2.dump"], removed);
        }
    }
}
