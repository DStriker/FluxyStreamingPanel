namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// What the edit-plan form sends. Every property is optional, and an absent one means
    /// "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two relation lists follow the rule the content forms already state: an absent list
    /// keeps the links the plan has, while a list that is present - even an empty one - replaces
    /// them.
    /// </para>
    /// <para>
    /// The values that are not nullable on the row have no "clear" state, so a property that is
    /// present but blank is refused rather than stored: a plan without a name is not a plan.
    /// </para>
    /// </remarks>
    public sealed record UpdateTariffPlanRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New trial flag, or null to keep the one on the row.</summary>
        public bool? IsTrial { get; init; }

        /// <summary>New cost, or null to keep the one on the row.</summary>
        public int? Cost { get; init; }

        /// <summary>New validity length, or null to keep the one on the row.</summary>
        public int? DurationValue { get; init; }

        /// <summary>New unit as text, or null to keep the one on the row.</summary>
        public string? DurationUnit { get; init; }

        /// <summary>New playlist support, or null to keep the one on the row.</summary>
        public bool? SupportsM3u { get; init; }

        /// <summary>New MAG support, or null to keep the one on the row.</summary>
        public bool? SupportsMag { get; init; }

        /// <summary>New Enigma2 support, or null to keep the one on the row.</summary>
        public bool? SupportsEnigma2 { get; init; }

        /// <summary>New MPEG-TS output, or null to keep the one on the row.</summary>
        public bool? OutputMpegTs { get; init; }

        /// <summary>New HLS output, or null to keep the one on the row.</summary>
        public bool? OutputHls { get; init; }

        /// <summary>New strict STB check, or null to keep the one on the row.</summary>
        public bool? StrictStbDeviceId { get; init; }

        /// <summary>New restreaming permission, or null to keep the one on the row.</summary>
        public bool? AllowRestream { get; init; }

        /// <summary>New connection ceiling, or null to keep the one on the row.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>New multi-country allowance, or null to keep the one on the row.</summary>
        public bool? AllowMultipleCountries { get; init; }

        /// <summary>Every group that may be sold the plan, or null to keep the links it has.</summary>
        public IReadOnlyList<Guid>? UserGroupIds { get; init; }

        /// <summary>Every bouquet the plan delivers, or null to keep the links it has.</summary>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
