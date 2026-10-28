namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// What the add-plan form sends to create a tariff plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted.
    /// </para>
    /// <para>
    /// <see cref="DurationUnit"/> arrives as text for the reason <c>sortOrder</c> on every list
    /// does: enum binding takes a member name spelled in full and would refuse the conventional
    /// lower-case <c>hours</c>, and a refusal of a request spelled the obvious way teaches its
    /// callers to spell things oddly.
    /// </para>
    /// </remarks>
    public sealed record CreateTariffPlanRequest
    {
        /// <summary>Display name of the plan. Unique across the installation in any spelling.</summary>
        public string? Name { get; init; }

        /// <summary>Whether the plan is a free trial.</summary>
        public bool IsTrial { get; init; }

        /// <summary>Cost in credits.</summary>
        public int Cost { get; init; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public int DurationValue { get; init; }

        /// <summary>Unit of the validity period: hours, days, months or years.</summary>
        public string? DurationUnit { get; init; }

        /// <summary>Whether the plan may be delivered as an M3U playlist.</summary>
        public bool SupportsM3u { get; init; }

        /// <summary>Whether the plan may be delivered to a MAG set-top box.</summary>
        public bool SupportsMag { get; init; }

        /// <summary>Whether the plan may be delivered to an Enigma2 receiver.</summary>
        public bool SupportsEnigma2 { get; init; }

        /// <summary>Whether the plan may be delivered as an MPEG-TS stream.</summary>
        public bool OutputMpegTs { get; init; }

        /// <summary>Whether the plan may be delivered as an HLS stream.</summary>
        public bool OutputHls { get; init; }

        /// <summary>Whether a MAG device's STB identifier is checked strictly.</summary>
        public bool StrictStbDeviceId { get; init; }

        /// <summary>Whether a subscriber may restream the subscription.</summary>
        public bool AllowRestream { get; init; }

        /// <summary>How many devices may be connected at the same time.</summary>
        public int MaxConnections { get; init; }

        /// <summary>Whether connections from different countries are allowed at the same time.</summary>
        public bool AllowMultipleCountries { get; init; } = true;

        /// <summary>Groups that may be sold the plan, or none to offer it to nobody yet.</summary>
        public IReadOnlyList<Guid>? UserGroupIds { get; init; }

        /// <summary>Bouquets the plan delivers, or none for a subscription with no content.</summary>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
