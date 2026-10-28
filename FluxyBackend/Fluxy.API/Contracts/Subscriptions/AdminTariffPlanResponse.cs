namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// One tariff plan as a row of the table: every setting, plus the counts of the two
    /// relations the table draws beside them.
    /// </summary>
    /// <remarks>
    /// The identifiers of the relations are deliberately absent. A page of plans never draws
    /// them - it draws how many there are - and the edit form reads them from the detail
    /// endpoint instead.
    /// </remarks>
    public sealed record AdminTariffPlanResponse
    {
        /// <summary>Identifier of the plan, as text. The table shows it shortened.</summary>
        public required string Id { get; init; }

        /// <summary>Display name, unique across the installation in any spelling.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the plan is a free trial.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>Cost in credits.</summary>
        public required int Cost { get; init; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public required int DurationValue { get; init; }

        /// <summary>Unit of the validity period: hours, days, months or years.</summary>
        public required string DurationUnit { get; init; }

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

        /// <summary>How many user groups may be sold the plan.</summary>
        public required int UserGroupCount { get; init; }

        /// <summary>How many bouquets the plan delivers.</summary>
        public required int BouquetCount { get; init; }
    }
}
