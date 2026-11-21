namespace Fluxy.Core.Settings.Outcomes
{
    /// <summary>
    /// The result of a write to the MAG settings.
    /// </summary>
    /// <remarks>
    /// The rejected fields are keyed by the C# property names of the patch; the transport
    /// rewrites them to the camelCase names the wire uses, through the shared field-key
    /// normaliser.
    /// </remarks>
    public sealed record MagSettingsOutcome
    {
        /// <summary>What the service decided.</summary>
        public required MagSettingsAction Action { get; init; }

        /// <summary>Rejected fields, or null when nothing was rejected.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
