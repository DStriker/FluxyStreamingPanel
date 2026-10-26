namespace Fluxy.Core.Epg.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a programme guide source.
    /// </summary>
    /// <remarks>
    /// <see cref="DaysKeep"/> is optional because the form offers a default: an absent value
    /// is resolved to <see cref="Utils.EpgSourceLimits.DaysKeepDefault"/> by the service, not
    /// by the column - this project declares no database defaults, so a value the application
    /// forgot would surface as an error rather than be invented silently.
    /// </remarks>
    public sealed record NewEpgSource
    {
        /// <summary>Display name. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>Absolute http(s) address the guide is fetched from.</summary>
        public required string Url { get; init; }

        /// <summary>
        /// How many days of programmes are kept, or null to keep the default.
        /// </summary>
        public int? DaysKeep { get; init; }
    }
}
