namespace Fluxy.API.Contracts.Transcoding
{
    /// <summary>
    /// One entry of the profile picker: the identifier and the name it is chosen by.
    /// </summary>
    /// <remarks>
    /// Deliberately the whole shape of the picker and nothing else. The endpoint that answers
    /// this list is gated by any of the four content-view grants rather than by the profile
    /// pair, because choosing a profile while editing a stream is part of editing the stream.
    /// </remarks>
    public sealed record AdminTranscodingProfileOptionResponse
    {
        /// <summary>Identifier of the profile, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }
    }
}
