using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Models
{
    /// <summary>
    /// Immutable business representation of one video available on demand: the address it is
    /// played from, how it is delivered, and the metadata the catalogue shows around it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One address rather than a list. A stream needs several because a live broadcast falls
    /// over; an on-demand file does not, and a list here would be a failover chain nobody
    /// asked for. A playlist import supplies one address per entry, which is exactly this
    /// shape.
    /// </para>
    /// <para>
    /// <see cref="Cast"/> and <see cref="Genres"/> are plain ordered strings rather than rows
    /// of their own. They are metadata read with the item and never queried by position; a
    /// table per list would add two joins and the "unloaded collection reads as empty" bug to
    /// buy an order the array already keeps.
    /// </para>
    /// </remarks>
    public sealed record VodItem
    {
        /// <summary>Identifier of the item.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, at most 200 characters, unique within its <see cref="CategoryId"/> and
        /// compared case sensitively.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>Whether the item is published or kept disabled.</summary>
        public required VodStatus Status { get; init; }

        /// <summary>How the item is delivered: served by this installation or redirected to.</summary>
        public required VodType Type { get; init; }

        /// <summary>Category the item is filed under, always a category of on-demand video.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Display name of that category, which is what the table column draws.</summary>
        public required string CategoryName { get; init; }

        /// <summary>URL or file path the video is played from.</summary>
        public required string Source { get; init; }

        /// <summary>URL or file path of the subtitles, or null when the item carries none.</summary>
        public string? SubtitleUrl { get; init; }

        /// <summary>IMDb identifier (<c>tt</c> and digits), or null when none is recorded.</summary>
        public string? ImdbId { get; init; }

        /// <summary>Address of the portrait cover, or null.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>Address of the wide background picture, or null.</summary>
        public string? BackdropUrl { get; init; }

        /// <summary>Synopsis, or null when none is recorded.</summary>
        public string? Plot { get; init; }

        /// <summary>Cast, in billing order.</summary>
        public required IReadOnlyList<string> Cast { get; init; }

        /// <summary>Director, or null when none is recorded.</summary>
        public string? Director { get; init; }

        /// <summary>Genres, in the order the operator entered them.</summary>
        public required IReadOnlyList<string> Genres { get; init; }

        /// <summary>Release date, or null when the item does not record one.</summary>
        public DateOnly? ReleaseDate { get; init; }

        /// <summary>Rating from 0 to 10, or null when the item is not rated.</summary>
        public decimal? Rating { get; init; }

        /// <summary>Upper case ISO 3166-1 alpha-2 country code, or null.</summary>
        public string? CountryCode { get; init; }
    }
}
