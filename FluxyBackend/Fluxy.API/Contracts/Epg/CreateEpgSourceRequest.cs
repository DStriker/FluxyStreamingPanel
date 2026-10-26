namespace Fluxy.API.Contracts.Epg
{
    /// <summary>
    /// What the add-source form sends to create a programme guide source.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted. <see cref="DaysKeep"/> arrives as text
    /// so that a value that is not a number is refused where the refusal can name the field.
    /// </remarks>
    public sealed record CreateEpgSourceRequest
    {
        /// <summary>Display name of the source. Unique across the installation.</summary>
        public string? Name { get; init; }

        /// <summary>Absolute http(s) address the guide is fetched from.</summary>
        public string? Url { get; init; }

        /// <summary>Days of programmes to keep, or none to keep the default of seven.</summary>
        public string? DaysKeep { get; init; }
    }
}
