using Fluxy.Core.Backups.Utils;

namespace Fluxy.UnitTests.Backups
{
    /// <summary>
    /// What a client-supplied name may be: the traversal matrix, the Windows traps and the
    /// belt-and-braces path check.
    /// </summary>
    /// <remarks>
    /// Every case here is one the API answers with the same <c>404</c>, so the pattern is the only
    /// thing standing between a request and the host's filesystem.
    /// </remarks>
    public sealed class BackupFileNameRulesTests
    {
        [Theory]
        [InlineData("fluxy-20261123T101500Z.dump")]
        [InlineData("fluxy-20261123T101500Z-2.dump")]
        [InlineData("a_b-c.9.dump")]
        [InlineData("0.dump")]
        public void AWellFormedNameIsAccepted(string fileName)
        {
            Assert.True(BackupFileNameRules.IsValid(fileName));
        }

        [Theory]
        [InlineData("")]
        [InlineData("../x.dump")]
        [InlineData("..\\x.dump")]
        [InlineData("C:\\x.dump")]
        [InlineData("sub/x.dump")]
        [InlineData("sub\\x.dump")]
        [InlineData("con.dump")]
        [InlineData("CON.dump")]
        [InlineData("nul.dump")]
        [InlineData("com1.dump")]
        [InlineData("LPT9.dump")]
        [InlineData("x.DUMP")]
        [InlineData("x.dump.")]
        [InlineData("x .dump")]
        [InlineData(".dump")]
        [InlineData("x.dump ")]
        [InlineData("-x.dump")]
        [InlineData("x:y.dump")]
        [InlineData("x\u0000y.dump")]
        [InlineData("x\ty.dump")]
        public void EverythingElseIsRefused(string fileName)
        {
            Assert.False(BackupFileNameRules.IsValid(fileName));
        }

        [Fact]
        public void ANamePastTheLengthLimitIsRefused()
        {
            var atLimit = string.Concat(new string('a', BackupFileNameRules.MaxLength - 5), ".dump");
            var overLimit = string.Concat(new string('a', BackupFileNameRules.MaxLength - 4), ".dump");

            Assert.True(BackupFileNameRules.IsValid(atLimit));
            Assert.False(BackupFileNameRules.IsValid(overLimit));
        }

        [Fact]
        public void ANonDumpExtensionIsRefused()
        {
            Assert.False(BackupFileNameRules.IsValid("fluxy-20261123T101500Z.tar"));
            Assert.False(BackupFileNameRules.IsValid("fluxy-20261123T101500Z"));
        }

        [Fact]
        public void TheResolvedPathOfAValidNameSitsInsideTheDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "fluxy-backups-rules");

            Assert.True(
                BackupFileNameRules.IsInsideDirectory(directory, "fluxy-20261123T101500Z.dump"));
        }

        [Fact]
        public void ATraversalNameResolvesOutsideTheDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "fluxy-backups-rules");

            Assert.False(BackupFileNameRules.IsInsideDirectory(directory, "..\\escape.dump"));
            Assert.False(BackupFileNameRules.IsInsideDirectory(directory, "../escape.dump"));
        }

        [Fact]
        public void AnAbsoluteNameResolvesOutsideTheDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "fluxy-backups-rules");
            var absolute = Path.Combine(Path.GetTempPath(), "elsewhere.dump");

            Assert.False(BackupFileNameRules.IsInsideDirectory(directory, absolute));
        }
    }
}
