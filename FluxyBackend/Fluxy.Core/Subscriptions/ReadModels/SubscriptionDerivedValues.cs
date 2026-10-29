using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// What the attached tariff plan decides for one subscription, as the form draws it.
    /// </summary>
    /// <remarks>
    /// Read beside <see cref="SubscriptionDetail"/> and never stored: these are the plan's own
    /// values, which a save with that plan writes and a save without it drops. Sending them
    /// lets the edit form show a disabled control carrying the value the row would become,
    /// rather than an empty one the operator has to interpret.
    /// </remarks>
    public sealed record SubscriptionDerivedValues
    {
        /// <summary>Connection ceiling the plan allows, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>Whether the plan allows restreaming.</summary>
        public required bool IsRestreamer { get; init; }

        /// <summary>The formats the plan delivers.</summary>
        public required SubscriptionOutputFormat OutputFormats { get; init; }

        /// <summary>Whether the plan's MAG devices are checked strictly.</summary>
        public required bool IsStbLock { get; init; }

        /// <summary>Whether the plan pins the subscription to one country.</summary>
        public required bool IsCountryLock { get; init; }

        /// <summary>Whether the plan is a trial.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>Whether the plan delivers a playlist.</summary>
        public required bool SupportsPlaylist { get; init; }

        /// <summary>Whether the plan delivers to a MAG device.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the plan delivers to an Enigma2 device.</summary>
        public required bool SupportsEnigma2 { get; init; }

        /// <summary>The bouquets the plan delivers, in the bouquets' own order.</summary>
        public required IReadOnlyList<Guid> BouquetIds { get; init; }
    }
}
