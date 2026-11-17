using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Utils;

namespace Fluxy.Application.Transcoding.Utils
{
    /// <summary>
    /// The rules and sentences every write to a transcoding profile shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A create and a patch reach the same row through different requests, so the rules that
    /// are a fact about the row rather than about the request live here once. What is not here
    /// is what a request alone decides: the create defaults and the "absent keeps, null clears"
    /// merge stay with the service.
    /// </para>
    /// <para>
    /// The bounds are ffmpeg's own where ffmpeg states one - a CRF outside 0..51 is a value the
    /// encoder refuses - and this installation's decision where it does not. The frontend
    /// mirrors them for instant feedback, and a request that gets past the mirror is judged
    /// again in this layer.
    /// </para>
    /// </remarks>
    public static class TranscodingProfileRules
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between " + TranscodingProfileLimits.NameMinLength + " and " +
                TranscodingProfileLimits.NameMaxLength + " characters."
        ];

        /// <summary>The sentence a bitrate, buffer or tolerance below zero gets.</summary>
        public static readonly string[] BitrateRule =
        [
            "The value must be zero or greater."
        ];

        /// <summary>The sentence a CRF outside 0..51 gets.</summary>
        public static readonly string[] CrfRule =
        [
            "The CRF must be between " + TranscodingProfileLimits.CrfMin + " and " +
                TranscodingProfileLimits.CrfMax + "."
        ];

        /// <summary>The sentence a width or height below -1 gets.</summary>
        public static readonly string[] DimensionRule =
        [
            "The value must be -1 (keep the aspect ratio) or greater."
        ];

        /// <summary>The sentence an aspect ratio that is too long gets.</summary>
        public static readonly string[] AspectRatioRule =
        [
            "The aspect ratio must be at most " +
                TranscodingProfileLimits.AspectRatioMaxLength + " characters."
        ];

        /// <summary>The sentence a framerate at or below zero gets.</summary>
        public static readonly string[] FramerateRule =
        [
            "The frame rate must be greater than zero."
        ];

        /// <summary>The sentence a sample rate at or below zero gets.</summary>
        public static readonly string[] SampleRateRule =
        [
            "The sample rate must be greater than zero."
        ];

        /// <summary>The sentence a channel count outside 1..8 gets.</summary>
        public static readonly string[] ChannelsRule =
        [
            "The channel count must be between " + TranscodingProfileLimits.AudioChannelsMin +
                " and " + TranscodingProfileLimits.AudioChannelsMax + "."
        ];

        /// <summary>The sentence a thread count below zero gets.</summary>
        public static readonly string[] ThreadsRule =
        [
            "The thread count must be zero (automatic) or greater."
        ];

        /// <summary>The sentence a watermark address that is too long gets.</summary>
        public static readonly string[] LogoRule =
        [
            "The logo address must be at most " +
                TranscodingProfileLimits.LogoUrlMaxLength + " characters."
        ];

        /// <summary>The sentence a redaction rectangle that is present only in part gets.</summary>
        public static readonly string[] RedactionRule =
        [
            "The redaction rectangle needs all four values: x, y, width and height."
        ];

        /// <summary>The default video encoder a create applies when the body names none.</summary>
        public const VideoCodec DefaultVideoCodec = VideoCodec.Copy;

        /// <summary>The default audio encoder a create applies when the body names none.</summary>
        public const AudioCodec DefaultAudioCodec = AudioCodec.Copy;

        /// <summary>The default preset a create applies when the body names none.</summary>
        public const TranscodingPreset DefaultPreset = TranscodingPreset.Default;

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge, already trimmed by the service.</param>
        public static bool IsUsableName(string name)
            => name.Length is >= TranscodingProfileLimits.NameMinLength
                and <= TranscodingProfileLimits.NameMaxLength;

        /// <summary>
        /// Checks everything about an already-resolved write that can be judged without asking
        /// the database.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names shared by the two commands for these fields - the
        /// transport normalizes them to camelCase for the client. The redaction rectangle is
        /// reported under <c>RedactionX</c>, which is the first of the four: the refusal is about
        /// the set, and the form draws the four in one control.
        /// </remarks>
        /// <param name="profile">Values the row is about to carry.</param>
        public static Dictionary<string, string[]> Validate(TranscodingProfileWrite profile)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsableName(profile.Name))
            {
                errors[nameof(TranscodingProfileWrite.Name)] = NameRule;
            }

            Bound(errors, nameof(TranscodingProfileWrite.VideoBitrateAvg), profile.VideoBitrateAvg);
            Bound(errors, nameof(TranscodingProfileWrite.AudioBitrateAvg), profile.AudioBitrateAvg);
            Bound(
                errors,
                nameof(TranscodingProfileWrite.MinBitrateTolerance),
                profile.MinBitrateTolerance);
            Bound(
                errors,
                nameof(TranscodingProfileWrite.MaxBitrateTolerance),
                profile.MaxBitrateTolerance);
            Bound(errors, nameof(TranscodingProfileWrite.BufferSize), profile.BufferSize);

            if (profile.Crf is { } crf
                && crf is < TranscodingProfileLimits.CrfMin or > TranscodingProfileLimits.CrfMax)
            {
                errors[nameof(TranscodingProfileWrite.Crf)] = CrfRule;
            }

            Dimension(errors, nameof(TranscodingProfileWrite.Width), profile.Width);
            Dimension(errors, nameof(TranscodingProfileWrite.Height), profile.Height);

            if (profile.AspectRatio is { Length: > TranscodingProfileLimits.AspectRatioMaxLength })
            {
                errors[nameof(TranscodingProfileWrite.AspectRatio)] = AspectRatioRule;
            }

            if (profile.Framerate is <= 0)
            {
                errors[nameof(TranscodingProfileWrite.Framerate)] = FramerateRule;
            }

            if (profile.AudioSampleRate is <= 0)
            {
                errors[nameof(TranscodingProfileWrite.AudioSampleRate)] = SampleRateRule;
            }

            if (profile.AudioChannels is { } channels
                && channels is < TranscodingProfileLimits.AudioChannelsMin
                    or > TranscodingProfileLimits.AudioChannelsMax)
            {
                errors[nameof(TranscodingProfileWrite.AudioChannels)] = ChannelsRule;
            }

            if (profile.Threads is < TranscodingProfileLimits.ThreadsMin)
            {
                errors[nameof(TranscodingProfileWrite.Threads)] = ThreadsRule;
            }

            if (profile.LogoUrl is { Length: > TranscodingProfileLimits.LogoUrlMaxLength })
            {
                errors[nameof(TranscodingProfileWrite.LogoUrl)] = LogoRule;
            }

            ValidateRedaction(
                errors,
                profile.RedactionX,
                profile.RedactionY,
                profile.RedactionWidth,
                profile.RedactionHeight);

            return errors;
        }

        /// <summary>
        /// Whether the four redaction values describe one rectangle: all present or all absent.
        /// </summary>
        /// <param name="x">Left edge, possibly null.</param>
        /// <param name="y">Top edge, possibly null.</param>
        /// <param name="width">Width, possibly null.</param>
        /// <param name="height">Height, possibly null.</param>
        public static bool IsWholeRectangle(int? x, int? y, int? width, int? height)
            => (x is null && y is null && width is null && height is null)
                || (x is not null && y is not null && width is not null && height is not null);

        private static void Bound(
            Dictionary<string, string[]> errors,
            string field,
            int? value)
        {
            if (value is < TranscodingProfileLimits.BitrateMin)
            {
                errors[field] = BitrateRule;
            }
        }

        private static void Dimension(
            Dictionary<string, string[]> errors,
            string field,
            int? value)
        {
            if (value is < TranscodingProfileLimits.DimensionMin)
            {
                errors[field] = DimensionRule;
            }
        }

        private static void ValidateRedaction(
            Dictionary<string, string[]> errors,
            int? x,
            int? y,
            int? width,
            int? height)
        {
            if (!IsWholeRectangle(x, y, width, height))
            {
                errors[nameof(TranscodingProfileWrite.RedactionX)] = RedactionRule;
                return;
            }

            if (x is null)
            {
                return;
            }

            // A whole rectangle is still judged: a negative edge names no region on any picture.
            if (x < 0)
            {
                errors[nameof(TranscodingProfileWrite.RedactionX)] = BitrateRule;
            }

            if (y < 0)
            {
                errors[nameof(TranscodingProfileWrite.RedactionY)] = BitrateRule;
            }

            if (width < 1)
            {
                errors[nameof(TranscodingProfileWrite.RedactionWidth)] = DimensionRule;
            }

            if (height < 1)
            {
                errors[nameof(TranscodingProfileWrite.RedactionHeight)] = DimensionRule;
            }
        }
    }
}
