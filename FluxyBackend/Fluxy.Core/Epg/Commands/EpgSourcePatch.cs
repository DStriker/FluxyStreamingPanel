namespace Fluxy.Core.Epg.Commands
{
    /// <summary>
    /// What an administrator changes about an existing programme guide source. Every property
    /// is optional, and an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// All three fields are required of a source, so none of them has a "clear" state: an
    /// absent property keeps, and a property that is present but blank is refused rather than
    /// stored.
    /// </remarks>
    public sealed record EpgSourcePatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New address, or null to keep the one on the row.</summary>
        public string? Url { get; init; }

        /// <summary>New retention in days, or null to keep the one on the row.</summary>
        public int? DaysKeep { get; init; }
    }
}
