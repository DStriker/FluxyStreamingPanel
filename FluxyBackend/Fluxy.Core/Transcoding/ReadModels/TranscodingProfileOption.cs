namespace Fluxy.Core.Transcoding.ReadModels
{
    /// <summary>
    /// One entry of the picker the four content forms draw: a profile's identifier and the name
    /// it is chosen by.
    /// </summary>
    /// <remarks>
    /// The picker needs nothing else. It is answered by a different endpoint with a different
    /// grant from the full list, because choosing a profile while editing a stream is not the
    /// same job as managing the profiles themselves: an operator who may run the streams gets
    /// the names to choose from and no table to browse.
    /// </remarks>
    public sealed record TranscodingProfileOption
    {
        /// <summary>Identifier of the profile.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }
    }
}
