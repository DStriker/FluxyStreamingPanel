namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// What the edit-source form sends. Every property is optional, and an absent one means
    /// "leave it alone".
    /// </summary>
    /// <remarks>
    /// None of the three fields has a "clear" state: a source without a name, an address or a
    /// retention is not a source. A property that is present but blank is refused rather than
    /// stored, so the null-versus-empty convention the other PATCH bodies use collapses to
    /// "null keeps, blank keeps, anything else is the new value" for the number and "blank is
    /// refused" for the two texts.
    /// </remarks>
    public sealed record UpdateEpgSourceRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New address, or null to keep the one on the row.</summary>
        public string? Url { get; init; }

        /// <summary>New retention as text, or null/blank to keep the one on the row.</summary>
        public string? DaysKeep { get; init; }
    }
}
