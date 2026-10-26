namespace Fluxy.Core.Epg.ReadModels
{
    /// <summary>
    /// A programme guide source as the API answers with it: everything an operator may read
    /// and nothing the updater owns.
    /// </summary>
    /// <remarks>
    /// The raw guide and the moment it was last refreshed's payload are deliberately absent -
    /// the raw file is what the updater parses, and a page of twenty sources would otherwise
    /// carry twenty files. <see cref="LastEpgUpdatedAt"/> is present because it is a fact the
    /// table draws: "when was this guide refreshed last".
    /// </remarks>
    public sealed record EpgSourceView
    {
        /// <summary>Identifier of the source.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Absolute http(s) address the guide is fetched from.</summary>
        public required string Url { get; init; }

        /// <summary>How many days of programmes are kept for this source.</summary>
        public required int DaysKeep { get; init; }

        /// <summary>When the guide was last refreshed, or null when it never has been.</summary>
        public DateTimeOffset? LastEpgUpdatedAt { get; init; }
    }
}
