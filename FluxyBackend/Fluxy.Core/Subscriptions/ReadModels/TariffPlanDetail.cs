using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One tariff plan in full, as the edit form reads it: every setting plus the identifiers of
    /// the two relations, so the form's pickers open on what the plan actually has.
    /// </summary>
    public sealed record TariffPlanDetail
    {
        /// <summary>Identifier of the plan.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique across the installation in any spelling.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the plan is a free trial.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>Cost in credits.</summary>
        public required int Cost { get; init; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public required int DurationValue { get; init; }

        /// <summary>The unit the validity period is counted in.</summary>
        public required TariffPlanDurationUnit DurationUnit { get; init; }

        /// <summary>Whether the plan may be delivered as an M3U playlist.</summary>
        public required bool SupportsM3u { get; init; }

        /// <summary>Whether the plan may be delivered to a MAG set-top box.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the plan may be delivered to an Enigma2 receiver.</summary>
        public required bool SupportsEnigma2 { get; init; }

        /// <summary>Whether the plan may be delivered as an MPEG-TS stream.</summary>
        public required bool OutputMpegTs { get; init; }

        /// <summary>Whether the plan may be delivered as an HLS stream.</summary>
        public required bool OutputHls { get; init; }

        /// <summary>Whether a MAG device's STB identifier is checked strictly.</summary>
        public required bool StrictStbDeviceId { get; init; }

        /// <summary>Whether a subscriber may restream the subscription.</summary>
        public required bool AllowRestream { get; init; }

        /// <summary>How many devices may be connected at the same time.</summary>
        public required int MaxConnections { get; init; }

        /// <summary>Whether connections from different countries are allowed at the same time.</summary>
        public required bool AllowMultipleCountries { get; init; }

        /// <summary>
        /// Every group that may be sold the plan, in the groups' own name order. Empty means no
        /// group may be sold it yet.
        /// </summary>
        public required IReadOnlyList<Guid> UserGroupIds { get; init; }

        /// <summary>
        /// Every bouquet the plan delivers, in the bouquets' own order. Empty means a
        /// subscription with no content.
        /// </summary>
        public required IReadOnlyList<Guid> BouquetIds { get; init; }
    }
}
