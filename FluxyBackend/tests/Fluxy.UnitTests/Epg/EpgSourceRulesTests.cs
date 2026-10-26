using Fluxy.Application.Epg.Utils;
using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Utils;

namespace Fluxy.UnitTests.Epg
{
    /// <summary>
    /// The rules every write to a programme guide source shares: what makes a name, an address
    /// and a retention usable, and which fields are judged before the database is asked
    /// anything.
    /// </summary>
    /// <remarks>
    /// These are the sentences the operator reads beside the input that broke them, so they
    /// are worth a test of their own: a form that says "the address must be an absolute http or
    /// https URL" and a server that says something else is a form nobody can satisfy.
    /// </remarks>
    public sealed class EpgSourceRulesTests
    {
        [Fact]
        public void ANameIsUsableWhenItIsNotBlankAndFitsTheColumn()
        {
            Assert.True(EpgSourceRules.IsUsableName("Main guide"));
            Assert.True(EpgSourceRules.IsUsableName(
                new string('a', EpgSourceLimits.NameMaxLength)));

            Assert.False(EpgSourceRules.IsUsableName("   "));
            Assert.False(EpgSourceRules.IsUsableName(string.Empty));
            Assert.False(EpgSourceRules.IsUsableName(
                new string('a', EpgSourceLimits.NameMaxLength + 1)));
        }

        [Fact]
        public void AnAddressIsUsableOnlyWhenItIsAnAbsoluteHttpOrHttpsUrl()
        {
            Assert.True(EpgSourceRules.IsUsableUrl("https://example.test/guide.xml"));
            Assert.True(EpgSourceRules.IsUsableUrl("http://example.test/guide.xml"));
            Assert.True(EpgSourceRules.IsUsableUrl("  https://example.test/guide.xml  "));

            Assert.False(EpgSourceRules.IsUsableUrl("example.test/guide.xml"));
            Assert.False(EpgSourceRules.IsUsableUrl("/var/guides/guide.xml"));
            Assert.False(EpgSourceRules.IsUsableUrl("ftp://example.test/guide.xml"));
            Assert.False(EpgSourceRules.IsUsableUrl("   "));
            Assert.False(EpgSourceRules.IsUsableUrl(
                "https://example.test/" + new string('a', EpgSourceLimits.UrlMaxLength)));
        }

        [Fact]
        public void ARetentionIsUsableInsideItsRange()
        {
            Assert.True(EpgSourceRules.IsUsableDaysKeep(EpgSourceLimits.DaysKeepMin));
            Assert.True(EpgSourceRules.IsUsableDaysKeep(EpgSourceLimits.DaysKeepDefault));
            Assert.True(EpgSourceRules.IsUsableDaysKeep(EpgSourceLimits.DaysKeepMax));

            Assert.False(EpgSourceRules.IsUsableDaysKeep(EpgSourceLimits.DaysKeepMin - 1));
            Assert.False(EpgSourceRules.IsUsableDaysKeep(EpgSourceLimits.DaysKeepMax + 1));
            Assert.False(EpgSourceRules.IsUsableDaysKeep(0));
        }

        [Fact]
        public void AWellFormedSourcePassesEveryRule()
        {
            var errors = EpgSourceRules.Validate(
                "Main guide",
                "https://example.test/guide.xml",
                EpgSourceLimits.DaysKeepDefault);

            Assert.Empty(errors);
        }

        [Fact]
        public void ABrokenFieldIsRefusedUnderItsOwnName()
        {
            var errors = EpgSourceRules.Validate(
                "   ",
                "not-a-url",
                0);

            Assert.True(errors.ContainsKey(nameof(NewEpgSource.Name)));
            Assert.True(errors.ContainsKey(nameof(NewEpgSource.Url)));
            Assert.True(errors.ContainsKey(nameof(NewEpgSource.DaysKeep)));
            Assert.Equal(3, errors.Count);
        }
    }
}
