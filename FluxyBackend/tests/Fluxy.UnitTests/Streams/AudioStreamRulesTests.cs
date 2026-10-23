using Fluxy.Application.Streams.Utils;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The rules every write to an audio stream shares: what makes a name and a source list
    /// usable, and which fields are judged before the database is asked anything.
    /// </summary>
    /// <remarks>
    /// These are the sentences the operator reads beside the input that broke them, so they
    /// are worth a test of their own: a form that says "the name must be between 1 and 200
    /// characters" and a server that says something else is a form nobody can satisfy.
    /// </remarks>
    public sealed class AudioStreamRulesTests
    {
        [Fact]
        public void ANameIsUsableWhenItIsNotBlankAndFitsTheColumn()
        {
            Assert.True(AudioStreamRules.IsUsableName("Radio One"));
            Assert.True(AudioStreamRules.IsUsableName(new string('a', AudioStreamLimits.NameMaxLength)));

            Assert.False(AudioStreamRules.IsUsableName("   "));
            Assert.False(AudioStreamRules.IsUsableName(string.Empty));
            Assert.False(AudioStreamRules.IsUsableName(
                new string('a', AudioStreamLimits.NameMaxLength + 1)));
        }

        [Fact]
        public void ASourceListIsUsableWhenItHoldsOneEntryThatFits()
        {
            Assert.True(AudioStreamRules.HasUsableEntries(
                ["https://example.test/live"],
                AudioStreamLimits.SourceUrlMaxLength));

            Assert.False(AudioStreamRules.HasUsableEntries(
                [],
                AudioStreamLimits.SourceUrlMaxLength));

            Assert.False(AudioStreamRules.HasUsableEntries(
                ["  "],
                AudioStreamLimits.SourceUrlMaxLength));

            Assert.False(AudioStreamRules.HasUsableEntries(
                [new string('a', AudioStreamLimits.SourceUrlMaxLength + 1)],
                AudioStreamLimits.SourceUrlMaxLength));
        }

        [Fact]
        public void AWellFormedBodyPassesEveryCommonRule()
        {
            var errors = AudioStreamRules.ValidateCommon(
                AudioStreamType.OnDemand,
                AudioStreamStatus.Running,
                Guid.NewGuid(),
                "agent",
                "http://proxy.test:3128");

            Assert.Empty(errors);
        }

        [Fact]
        public void ATypeOutsideTheEnumIsRefusedOnTheTypeField()
        {
            var errors = AudioStreamRules.ValidateCommon(
                (AudioStreamType)99,
                AudioStreamStatus.Stopped,
                Guid.NewGuid(),
                null,
                null);

            Assert.True(errors.ContainsKey(nameof(NewAudioStream.Type)));
        }

        [Fact]
        public void AStatusOutsideTheEnumIsRefusedOnTheStatusField()
        {
            var errors = AudioStreamRules.ValidateCommon(
                AudioStreamType.Redirect,
                (AudioStreamStatus)7,
                Guid.NewGuid(),
                null,
                null);

            Assert.True(errors.ContainsKey(nameof(NewAudioStream.Status)));
        }

        [Fact]
        public void AnEmptyCategoryIsRefusedOnTheCategoryField()
        {
            var errors = AudioStreamRules.ValidateCommon(
                AudioStreamType.Redirect,
                AudioStreamStatus.Stopped,
                Guid.Empty,
                null,
                null);

            Assert.True(errors.ContainsKey(nameof(NewAudioStream.CategoryId)));
        }

        [Fact]
        public void RestreamSettingsLongerThanTheirColumnsAreRefused()
        {
            var errors = AudioStreamRules.ValidateCommon(
                AudioStreamType.Redirect,
                AudioStreamStatus.Stopped,
                Guid.NewGuid(),
                new string('a', AudioStreamLimits.RestreamUserAgentMaxLength + 1),
                new string('b', AudioStreamLimits.RestreamProxyMaxLength + 1));

            Assert.True(errors.ContainsKey(nameof(NewAudioStream.RestreamUserAgent)));
            Assert.True(errors.ContainsKey(nameof(NewAudioStream.RestreamProxy)));
        }

        [Fact]
        public void ABlankTextIsStoredAsNone()
        {
            Assert.Null(AudioStreamRules.NullIfBlank("   "));
            Assert.Null(AudioStreamRules.NullIfBlank(null));
            Assert.Equal("agent", AudioStreamRules.NullIfBlank("  agent  "));
        }
    }
}
