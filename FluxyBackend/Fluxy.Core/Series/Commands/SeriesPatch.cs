using Fluxy.Core.Series.Enums;

namespace Fluxy.Core.Series.Commands
{
    /// <summary>
    /// What an administrator changes about an existing series. Every property is optional, and
    /// an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A nullable text field answers "keep" and "clear" without a second field: <c>null</c>
    /// keeps whatever the row holds, an empty string clears it, and anything else is the new
    /// value. <see cref="ReleaseDate"/> and <see cref="Rating"/> travel as text for the same
    /// reason - a null value would be ambiguous between "leave it" and "remove it".
    /// </para>
    /// <para>
    /// The lists carry one extra state. A null list keeps what the row holds, an empty list
    /// clears it, and a list with entries replaces the stored one wholesale.
    /// </para>
    /// </remarks>
    public sealed record SeriesPatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New status, or null to keep the one on the row.</summary>
        public SeriesStatus? Status { get; init; }

        /// <summary>New category, or null to keep the one on the row.</summary>
        public Guid? CategoryId { get; init; }

        /// <summary>New IMDb identifier; null keeps, empty clears.</summary>
        public string? ImdbId { get; init; }

        /// <summary>New poster address; null keeps, empty clears.</summary>
        public string? PosterUrl { get; init; }

        /// <summary>New backdrop address; null keeps, empty clears.</summary>
        public string? BackdropUrl { get; init; }

        /// <summary>New synopsis; null keeps, empty clears.</summary>
        public string? Plot { get; init; }

        /// <summary>New cast; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? Cast { get; init; }

        /// <summary>New director; null keeps, empty clears.</summary>
        public string? Director { get; init; }

        /// <summary>New genres; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? Genres { get; init; }

        /// <summary>New release date as <c>yyyy-MM-dd</c>; null keeps, empty clears.</summary>
        public string? ReleaseDate { get; init; }

        /// <summary>New rating as text; null keeps, empty clears.</summary>
        public string? Rating { get; init; }

        /// <summary>
        /// New set of bouquets the series belongs to; null keeps, empty leaves every bouquet.
        /// </summary>
        /// <remarks>
        /// A <b>set</b> rather than a list: the field answers "which packages is this series in"
        /// and says nothing about where it sits inside one. A bouquet the series joins takes it
        /// at the end of that bouquet's list.
        /// </remarks>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
