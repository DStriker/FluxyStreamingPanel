namespace Fluxy.Core.Epg.Models
{
    /// <summary>
    /// Immutable business representation of one programme guide source: where a guide is
    /// fetched from, and how much of what it carries is kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two of the fields on this model are written by the system rather than by an operator.
    /// <see cref="LastEpgUpdatedAt"/> is stamped by whatever updates the guide, and
    /// <see cref="RawData"/> holds the file as it arrived - which is why the read side of the
    /// API answers with <see cref="ReadModels.EpgSourceView"/> instead of this model: a page of
    /// twenty sources has no business carrying twenty raw guides over the wire.
    /// </para>
    /// <para>
    /// The individual programmes a guide carries are <see cref="EpgData"/> rows filed under
    /// this source, created and deleted by the same updater. They are not children of this
    /// model in the usual aggregate sense - nothing here enumerates them - because no request
    /// reads a source and its whole schedule in one answer.
    /// </para>
    /// </remarks>
    public sealed record EpgSource
    {
        /// <summary>Identifier of the source.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, at most 200 characters, unique across the installation and compared
        /// case sensitively - the same rule the categories and the streams are held to.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Absolute http(s) address the guide is fetched from, at most
        /// <see cref="Utils.EpgSourceLimits.UrlMaxLength"/> characters.
        /// </summary>
        public required string Url { get; init; }

        /// <summary>
        /// How many days of programmes are kept for this source. At least
        /// <see cref="Utils.EpgSourceLimits.DaysKeepMin"/> and at most
        /// <see cref="Utils.EpgSourceLimits.DaysKeepMax"/>.
        /// </summary>
        public required int DaysKeep { get; init; }

        /// <summary>
        /// When the guide was last refreshed, or null when it never has been. Written by the
        /// update process; no endpoint in this pass changes it.
        /// </summary>
        public DateTimeOffset? LastEpgUpdatedAt { get; init; }

        /// <summary>
        /// The guide exactly as the last update received it, or null when it has never been
        /// fetched. Stored for the updater to parse; never sent to a client.
        /// </summary>
        public string? RawData { get; init; }
    }
}
