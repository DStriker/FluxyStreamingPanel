namespace Fluxy.Core.Epg.Models
{
    /// <summary>
    /// Immutable business representation of one programme in a guide: which channel it is on,
    /// what it is called, and when it runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rows of this type are created and deleted by the guide updater and by nothing else -
    /// there is no endpoint, no form and no service that writes one in this pass. It is
    /// modelled now because the table, its indexes and the shape of a programme are part of
    /// the domain the updater will be written against.
    /// </para>
    /// <para>
    /// <see cref="ChannelId"/> is the identifier the <i>guide file</i> uses for the channel,
    /// not the identifier of a stream in this database. A stream points at this source plus
    /// this key through <c>VideoStream.EpgSourceId</c> and <c>VideoStream.EpgChannelKey</c>,
    /// which is why the two are strings rather than a foreign key: two guides may file the
    /// same channel under different keys.
    /// </para>
    /// </remarks>
    public sealed record EpgData
    {
        /// <summary>Identifier of the programme row.</summary>
        public required Guid Id { get; init; }

        /// <summary>Source this programme belongs to.</summary>
        public required Guid SourceId { get; init; }

        /// <summary>Channel key inside the guide file, at most 200 characters.</summary>
        public required string ChannelId { get; init; }

        /// <summary>Programme title.</summary>
        public required string Title { get; init; }

        /// <summary>
        /// Language of the title and the description as the guide stated it, or null when it
        /// stated none.
        /// </summary>
        public string? Language { get; init; }

        /// <summary>Moment the programme starts.</summary>
        public required DateTimeOffset StartDate { get; init; }

        /// <summary>Moment the programme ends.</summary>
        public required DateTimeOffset EndDate { get; init; }

        /// <summary>Description the guide carries, or null when it carries none.</summary>
        public string? Description { get; init; }
    }
}
