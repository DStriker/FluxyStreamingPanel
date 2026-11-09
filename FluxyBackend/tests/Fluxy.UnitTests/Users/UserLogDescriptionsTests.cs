using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The sentences a user log row carries: the fixed phrase, the sanitized subject, the
    /// optional detail, and the round trip from the sentence back to its subject.
    /// </summary>
    /// <remarks>
    /// The description is both the auditable record and the text the search box matches, so
    /// the wording and the sanitization are pinned here rather than left to a caller's memory.
    /// The inverse is tested beside the builder because a separator that one side understands
    /// and the other does not would silently answer half a sentence as a subject.
    /// </remarks>
    public sealed class UserLogDescriptionsTests
    {
        [Fact]
        public void ADescriptionIsThePhraseThenTheSubject()
        {
            Assert.Equal(
                "Logged out: alice",
                UserLogDescriptions.Describe("Logged out", "alice"));

            Assert.Equal(
                "Login failed (no such account): ghost",
                UserLogDescriptions.Describe("Login failed (no such account)", "ghost"));
        }

        [Fact]
        public void ADescriptionWithDetailCarriesItAfterASeparator()
        {
            Assert.Equal(
                "Password change requested: alice | confirmed",
                UserLogDescriptions.Describe("Password change requested", "alice", "confirmed"));
        }

        [Fact]
        public void ARenameReadsAsTheOldValueAnArrowAndTheNewValue()
        {
            Assert.Equal(
                "Username change requested: alice | bob \u2192 carol",
                UserLogDescriptions.Renamed("Username change requested", "alice", "bob", "carol"));

            var description = UserLogDescriptions.Renamed(
                "Email change requested",
                "alice",
                "bob@example.com",
                "carol@example.com");

            Assert.Contains('\u2192', description);
            Assert.DoesNotContain("->", description);
        }

        [Fact]
        public void TryGetSubjectIsTheExactInverseOfTheBuilder()
        {
            string?[] subjects = ["alice", "a: b", "  spaced  ", "x|y", "line\nbreak"];

            foreach (var subject in subjects)
            {
                var plain = UserLogDescriptions.Describe("Phrase", subject);
                Assert.True(UserLogDescriptions.TryGetSubject(plain, out var parsedPlain));
                Assert.Equal(UserLogDescriptions.Sanitize(subject), parsedPlain);

                var detailed = UserLogDescriptions.Describe("Phrase", subject, "detail");
                Assert.True(UserLogDescriptions.TryGetSubject(detailed, out var parsedDetailed));
                Assert.Equal(UserLogDescriptions.Sanitize(subject), parsedDetailed);
            }
        }

        [Fact]
        public void TheSubjectStopsAtTheFirstDetailSeparator()
        {
            Assert.True(UserLogDescriptions.TryGetSubject(
                "Phrase: subject | detail | more",
                out var subject));

            Assert.Equal("subject", subject);
        }

        [Fact]
        public void ADescriptionWithoutTheSeparatorHasNoSubject()
        {
            Assert.False(UserLogDescriptions.TryGetSubject("Logged out", out var subject));
            Assert.Null(subject);

            Assert.False(UserLogDescriptions.TryGetSubject(null, out _));
            Assert.False(UserLogDescriptions.TryGetSubject(string.Empty, out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\u0000\t\r\n")]
        public void AnEmptySubjectIsNamedRatherThanLeftBlank(string? value)
        {
            Assert.Equal(UserLogDescriptions.EmptySubject, UserLogDescriptions.Sanitize(value));

            Assert.Equal(
                $"Phrase: {UserLogDescriptions.EmptySubject}",
                UserLogDescriptions.Describe("Phrase", value));
        }

        [Fact]
        public void SanitizeStripsControlCharactersAndTheSeparator()
        {
            Assert.Equal("ab", UserLogDescriptions.Sanitize("a|b"));
            Assert.Equal("ab", UserLogDescriptions.Sanitize("a\u0000b"));
            Assert.Equal("linebreak", UserLogDescriptions.Sanitize("line\nbreak"));
            Assert.Equal("tab", UserLogDescriptions.Sanitize("ta\tb"));
        }

        [Fact]
        public void SanitizeCapsTheSubjectAtSixtyFourCharacters()
        {
            var sanitized = UserLogDescriptions.Sanitize(new string('a', 100));

            Assert.Equal(UserLogDescriptions.SubjectMaxLength, sanitized.Length);
            Assert.Equal(new string('a', UserLogDescriptions.SubjectMaxLength), sanitized);
        }

        [Fact]
        public void TheDescriptionIsCappedByTruncatingTheDetailFirst()
        {
            var subject = new string('s', UserLogDescriptions.SubjectMaxLength);
            var detail = new string('d', 1000);

            var description = UserLogDescriptions.Describe("Phrase", subject, detail);

            Assert.Equal(UserLogDescriptions.DescriptionMaxLength, description.Length);
            Assert.StartsWith($"Phrase: {subject} | ", description);
            Assert.EndsWith("ddd", description);
        }

        [Fact]
        public void TheSubjectSurvivesWhenTheDetailIsTooLongToKeep()
        {
            var subject = new string('s', UserLogDescriptions.SubjectMaxLength);
            var detail = new string('d', UserLogDescriptions.DescriptionMaxLength);

            var description = UserLogDescriptions.Describe("Phrase", subject, detail);

            Assert.True(description.Length <= UserLogDescriptions.DescriptionMaxLength);
            Assert.StartsWith($"Phrase: {subject}", description);
        }

        [Fact]
        public void APhraseThatContainsTheSeparatorIsRefused()
        {
            Assert.Throws<ArgumentException>(
                () => UserLogDescriptions.Describe("Login: failed", "alice"));

            Assert.Throws<ArgumentException>(
                () => UserLogDescriptions.Describe(" ", "alice"));
        }
    }
}
