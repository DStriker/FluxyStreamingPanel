namespace Fluxy.API.Contracts.Series
{
    /// <summary>
    /// What a request to convert one episode carries: the container wanted, as text.
    /// </summary>
    public sealed record RequestEpisodeTranscodeRequest
    {
        /// <summary>Container to produce: <c>mp4</c>, <c>avi</c>, <c>mkv</c>, <c>mpg</c> or <c>flv</c>.</summary>
        public string? Format { get; init; }
    }
}
