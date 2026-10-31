using Fluxy.Application.Blacklists.Utils;
using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Utils;

namespace Fluxy.UnitTests.Blacklists
{
    /// <summary>
    /// The rules a blacklist entry must satisfy, and which field a refusal names.
    /// </summary>
    /// <remarks>
    /// These are the sentences an operator reads beside the input that broke them, so they are
    /// worth a test of their own: a form whose mirror of a rule is stricter or looser than the
    /// server's is a form nobody can satisfy.
    /// </remarks>
    public sealed class BlacklistEntryRulesTests
    {
        [Fact]
        public void AValueIsUsableWhenItIsNotBlankAndFitsTheColumn()
        {
            Assert.True(BlacklistEntryRules.IsUsableValue("203.0.113.5"));
            Assert.True(BlacklistEntryRules.IsUsableValue(
                new string('a', BlacklistEntryLimits.ValueMaxLength)));

            Assert.False(BlacklistEntryRules.IsUsableValue("   "));
            Assert.False(BlacklistEntryRules.IsUsableValue(string.Empty));
            Assert.False(BlacklistEntryRules.IsUsableValue(
                new string('a', BlacklistEntryLimits.ValueMaxLength + 1)));
        }

        [Fact]
        public void AnAddressIsUsableOnlyWhenItParsesAsOne()
        {
            Assert.True(BlacklistEntryRules.IsAddress("203.0.113.5"));
            Assert.True(BlacklistEntryRules.IsAddress("2001:db8::1"));
            Assert.True(BlacklistEntryRules.IsAddress("  203.0.113.5  "));

            Assert.False(BlacklistEntryRules.IsAddress("300.1.2.3"));
            Assert.False(BlacklistEntryRules.IsAddress("203.0.113.0/24"));
            Assert.False(BlacklistEntryRules.IsAddress("curl/8.0"));
            Assert.False(BlacklistEntryRules.IsAddress(string.Empty));
        }

        [Fact]
        public void ANoteIsUsableUpToItsLimitOrAbsent()
        {
            Assert.True(BlacklistEntryRules.IsUsableNote(null));
            Assert.True(BlacklistEntryRules.IsUsableNote(
                new string('a', BlacklistEntryLimits.NoteMaxLength)));

            Assert.False(BlacklistEntryRules.IsUsableNote(
                new string('a', BlacklistEntryLimits.NoteMaxLength + 1)));
        }

        [Fact]
        public void AWellFormedEntryPassesEveryRule()
        {
            var errors = BlacklistEntryRules.Validate(BlacklistType.Ip, "203.0.113.5", "scanner");

            Assert.Empty(errors);
        }

        [Fact]
        public void AnAddressRuleAppliesOnlyToTheIpList()
        {
            // The same text is a valid ISP and user-agent value: it is the literal string the
            // enforcement matches on, not an address to parse.
            Assert.Empty(BlacklistEntryRules.Validate(BlacklistType.Isp, "not-an-address", null));
            Assert.Empty(BlacklistEntryRules.Validate(
                BlacklistType.UserAgent, "not-an-address", null));

            Assert.True(BlacklistEntryRules
                .Validate(BlacklistType.Ip, "not-an-address", null)
                .ContainsKey(nameof(NewBlacklistEntry.Value)));
        }

        [Fact]
        public void ABrokenFieldIsRefusedUnderItsOwnName()
        {
            var errors = BlacklistEntryRules.Validate(
                BlacklistType.Ip,
                "300.1.2.3",
                new string('a', BlacklistEntryLimits.NoteMaxLength + 1));

            Assert.True(errors.ContainsKey(nameof(NewBlacklistEntry.Value)));
            Assert.True(errors.ContainsKey(nameof(NewBlacklistEntry.Note)));
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void AnEmptyValueIsRefusedBeforeTheAddressRuleIsAsked()
        {
            // Validity first: a blank value breaks the length rule, and the sentence it gets
            // names the length rather than the address, which would be a confusing half-truth.
            var errors = BlacklistEntryRules.Validate(BlacklistType.Ip, "   ", null);

            Assert.Same(BlacklistEntryRules.ValueRule, errors[nameof(NewBlacklistEntry.Value)]);
        }
    }
}
