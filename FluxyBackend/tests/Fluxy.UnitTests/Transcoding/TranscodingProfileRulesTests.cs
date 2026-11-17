using Fluxy.Application.Transcoding.Utils;
using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Utils;

namespace Fluxy.UnitTests.Transcoding
{
    /// <summary>
    /// The rules every write to a transcoding profile shares: the name, every numeric bound the
    /// model states, and the all-or-none redaction rectangle.
    /// </summary>
    /// <remarks>
    /// These are the sentences the operator reads beside the input that broke them, so they are
    /// worth a test of their own: a form that says "between 0 and 51" and a server that says
    /// something else is a form nobody can satisfy.
    /// </remarks>
    public sealed class TranscodingProfileRulesTests
    {
        [Fact]
        public void ANameIsUsableWhenItIsNotBlankAndFitsTheColumn()
        {
            Assert.True(TranscodingProfileRules.IsUsableName("Standard H264 AAC"));
            Assert.True(TranscodingProfileRules.IsUsableName(
                new string('a', TranscodingProfileLimits.NameMaxLength)));

            Assert.False(TranscodingProfileRules.IsUsableName(""));
            Assert.False(TranscodingProfileRules.IsUsableName(
                new string('a', TranscodingProfileLimits.NameMaxLength + 1)));
        }

        [Fact]
        public void AnEmptyProfileIsValidBecauseEveryOptionIsOptional()
        {
            var errors = TranscodingProfileRules.Validate(Write("Copy me"));

            Assert.Empty(errors);
        }

        [Fact]
        public void ABitrateBufferOrToleranceBelowZeroIsRefusedOnItsOwnField()
        {
            var errors = TranscodingProfileRules.Validate(Write(
                "Negative",
                videoBitrateAvg: -1,
                audioBitrateAvg: -1,
                minBitrateTolerance: -1,
                maxBitrateTolerance: -1,
                bufferSize: -1));

            Assert.Contains(nameof(TranscodingProfileWrite.VideoBitrateAvg), errors.Keys);
            Assert.Contains(nameof(TranscodingProfileWrite.AudioBitrateAvg), errors.Keys);
            Assert.Contains(nameof(TranscodingProfileWrite.MinBitrateTolerance), errors.Keys);
            Assert.Contains(nameof(TranscodingProfileWrite.MaxBitrateTolerance), errors.Keys);
            Assert.Contains(nameof(TranscodingProfileWrite.BufferSize), errors.Keys);
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, true)]
        [InlineData(51, true)]
        [InlineData(52, false)]
        public void TheCrfIsJudgedAgainstFfmpegsOwnWindow(int crf, bool accepted)
        {
            var errors = TranscodingProfileRules.Validate(Write("Crf", crf: crf));

            Assert.Equal(accepted, !errors.ContainsKey(nameof(TranscodingProfileWrite.Crf)));
        }

        [Theory]
        [InlineData(-2, false)]
        [InlineData(-1, true)]
        [InlineData(0, true)]
        [InlineData(1920, true)]
        public void AWidthOrHeightOfMinusOneIsValidAndAnythingBelowItIsNot(
            int value,
            bool accepted)
        {
            var errors = TranscodingProfileRules.Validate(Write("Size", width: value, height: value));

            Assert.Equal(
                accepted,
                !errors.ContainsKey(nameof(TranscodingProfileWrite.Width))
                && !errors.ContainsKey(nameof(TranscodingProfileWrite.Height)));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(8, true)]
        [InlineData(9, false)]
        public void TheChannelCountIsJudgedBetweenOneAndEight(int channels, bool accepted)
        {
            var errors = TranscodingProfileRules.Validate(Write("Channels", audioChannels: channels));

            Assert.Equal(
                accepted,
                !errors.ContainsKey(nameof(TranscodingProfileWrite.AudioChannels)));
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, true)]
        [InlineData(16, true)]
        public void AThreadCountOfZeroMeansAutomaticAndIsValid(int threads, bool accepted)
        {
            var errors = TranscodingProfileRules.Validate(Write("Threads", threads: threads));

            Assert.Equal(accepted, !errors.ContainsKey(nameof(TranscodingProfileWrite.Threads)));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        public void TheFramerateMustBeGreaterThanZero(decimal framerate, bool accepted)
        {
            var errors = TranscodingProfileRules.Validate(Write("Rate", framerate: framerate));

            Assert.Equal(accepted, !errors.ContainsKey(nameof(TranscodingProfileWrite.Framerate)));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(44100, true)]
        public void TheSampleRateMustBeGreaterThanZero(int rate, bool accepted)
        {
            var errors = TranscodingProfileRules.Validate(Write("Rate", audioSampleRate: rate));

            Assert.Equal(
                accepted,
                !errors.ContainsKey(nameof(TranscodingProfileWrite.AudioSampleRate)));
        }

        [Fact]
        public void TheRedactionRectangleIsAllOrNone()
        {
            var whole = TranscodingProfileRules.Validate(Write(
                "Rect",
                redactionX: 10,
                redactionY: 10,
                redactionWidth: 100,
                redactionHeight: 50));

            Assert.Empty(whole);

            var partial = TranscodingProfileRules.Validate(Write(
                "Rect",
                redactionX: 10,
                redactionY: 10));

            Assert.Contains(nameof(TranscodingProfileWrite.RedactionX), partial.Keys);

            var reversedPartial = TranscodingProfileRules.Validate(Write(
                "Rect",
                redactionWidth: 100,
                redactionHeight: 50));

            Assert.Contains(nameof(TranscodingProfileWrite.RedactionX), reversedPartial.Keys);
        }

        [Fact]
        public void AnAspectRatioOrLogoLongerThanItsColumnIsRefused()
        {
            var errors = TranscodingProfileRules.Validate(Write(
                "Text",
                aspectRatio: new string('a', TranscodingProfileLimits.AspectRatioMaxLength + 1),
                logoUrl: new string('a', TranscodingProfileLimits.LogoUrlMaxLength + 1)));

            Assert.Contains(nameof(TranscodingProfileWrite.AspectRatio), errors.Keys);
            Assert.Contains(nameof(TranscodingProfileWrite.LogoUrl), errors.Keys);
        }

        [Fact]
        public void TheCreateDefaultsAreTheThreeThatMeanDoNothing()
        {
            Assert.Equal(VideoCodec.Copy, TranscodingProfileRules.DefaultVideoCodec);
            Assert.Equal(AudioCodec.Copy, TranscodingProfileRules.DefaultAudioCodec);
            Assert.Equal(TranscodingPreset.Default, TranscodingProfileRules.DefaultPreset);
        }

        private static TranscodingProfileWrite Write(
            string name,
            int? videoBitrateAvg = null,
            int? audioBitrateAvg = null,
            int? minBitrateTolerance = null,
            int? maxBitrateTolerance = null,
            int? bufferSize = null,
            int? crf = null,
            int? width = null,
            int? height = null,
            string? aspectRatio = null,
            decimal? framerate = null,
            int? audioSampleRate = null,
            int? audioChannels = null,
            int? threads = null,
            string? logoUrl = null,
            int? redactionX = null,
            int? redactionY = null,
            int? redactionWidth = null,
            int? redactionHeight = null)
            => new()
            {
                Name = name,
                VideoCodec = VideoCodec.Copy,
                AudioCodec = AudioCodec.Copy,
                Preset = TranscodingPreset.Default,
                VideoBitrateAvg = videoBitrateAvg,
                AudioBitrateAvg = audioBitrateAvg,
                MinBitrateTolerance = minBitrateTolerance,
                MaxBitrateTolerance = maxBitrateTolerance,
                BufferSize = bufferSize,
                Crf = crf,
                Width = width,
                Height = height,
                AspectRatio = aspectRatio,
                Framerate = framerate,
                AudioSampleRate = audioSampleRate,
                AudioChannels = audioChannels,
                Threads = threads,
                LogoUrl = logoUrl,
                RedactionX = redactionX,
                RedactionY = redactionY,
                RedactionWidth = redactionWidth,
                RedactionHeight = redactionHeight
            };
    }
}
