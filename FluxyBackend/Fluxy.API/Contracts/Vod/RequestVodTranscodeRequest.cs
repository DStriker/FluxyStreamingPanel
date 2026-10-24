namespace Fluxy.API.Contracts.Vod
{
    /// <summary>
    /// What a request to convert one video carries: the container wanted, as text.
    /// </summary>
    /// <remarks>
    /// As text rather than an enum, for the reason every other body of this API parses its
    /// enums by hand: model binding would refuse the conventional lower-case spelling before
    /// the antiforgery check could run.
    /// </remarks>
    public sealed record RequestVodTranscodeRequest
    {
        /// <summary>Container to produce: <c>mp4</c>, <c>avi</c>, <c>mkv</c>, <c>mpg</c> or <c>flv</c>.</summary>
        public string? Format { get; init; }
    }
}
