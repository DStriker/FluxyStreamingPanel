namespace Fluxy.Core.Epg.Commands
{
    /// <summary>
    /// The values a create or an update writes to a guide source row.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Models.EpgSource"/> because a write does not name what only
    /// the updater owns - the raw guide and the moment it was last refreshed - and because the
    /// service has already trimmed and defaulted every value by the time a row is written.
    /// </remarks>
    public sealed record EpgSourceWrite
    {
        /// <summary>Display name, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Absolute http(s) address the guide is fetched from.</summary>
        public required string Url { get; init; }

        /// <summary>How many days of programmes are kept.</summary>
        public required int DaysKeep { get; init; }
    }
}
