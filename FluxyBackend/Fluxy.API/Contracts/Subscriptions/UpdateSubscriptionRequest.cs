namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// What the edit-subscription form sends. Every property is optional, and an absent one means
    /// "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A nullable text field answers "keep" and "clear" without a second field: <c>null</c>
    /// keeps, an empty string clears, anything else is the new value. <see cref="ExpireAt"/>
    /// follows the same shape as <c>yyyy-MM-dd</c>, which is what lets "leave the expiry" and
    /// "make it never expire" be two different requests.
    /// </para>
    /// <para>
    /// <see cref="TariffPlanId"/> needs one extra state, so it carries
    /// <see cref="ClearTariffPlan"/>: a value attaches that plan, <c>true</c> nulls the reference
    /// and re-activates the subscription's own parameters, and neither keeps the current plan.
    /// The same pair exists for <see cref="MaxConnections"/>, whose "unlimited" state a nullable
    /// number cannot express on its own.
    /// </para>
    /// <para>
    /// <see cref="UserId"/> is accepted and ignored: the owner never changes after a subscription
    /// is issued. It is here so a form that posts the whole row is not refused for a field the
    /// server simply does not obey.
    /// </para>
    /// </remarks>
    public sealed record UpdateSubscriptionRequest
    {
        /// <summary>Owner, accepted and ignored - the owner never changes.</summary>
        public string? UserId { get; init; }

        /// <summary>New playlist id, empty to clear, null to keep.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>New expiry as <c>yyyy-MM-dd</c>, empty to clear, null to keep.</summary>
        public string? ExpireAt { get; init; }

        /// <summary>The plan to attach, as a uuid, or null to leave the reference alone.</summary>
        public string? TariffPlanId { get; init; }

        /// <summary>True to null the plan reference and re-activate the own parameters.</summary>
        public bool ClearTariffPlan { get; init; }

        /// <summary>New connection ceiling, or null to keep the current one.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>True to store unlimited connections rather than a ceiling.</summary>
        public bool ClearMaxConnections { get; init; }

        /// <summary>New restreaming flag, or null to keep.</summary>
        public bool? IsRestreamer { get; init; }

        /// <summary>New addresses, empty to clear, null to keep.</summary>
        public IReadOnlyList<string>? IpWhitelist { get; init; }

        /// <summary>New user agents, empty to clear, null to keep.</summary>
        public IReadOnlyList<string>? UserAgentWhitelist { get; init; }

        /// <summary>New provider lock flag, or null to keep.</summary>
        public bool? IsIspLock { get; init; }

        /// <summary>New country lock flag, or null to keep.</summary>
        public bool? IsCountryLock { get; init; }

        /// <summary>New trial flag, or null to keep.</summary>
        public bool? IsTrial { get; init; }

        /// <summary>New formats, as enum member names; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? OutputFormats { get; init; }

        /// <summary>New MAG MAC, empty to clear, null to keep.</summary>
        public string? MagMac { get; init; }

        /// <summary>New STB lock flag, or null to keep.</summary>
        public bool? IsStbLock { get; init; }

        /// <summary>New Enigma2 MAC, empty to clear, null to keep.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>New bouquets as uuids; null keeps, empty clears.</summary>
        public IReadOnlyList<string>? BouquetIds { get; init; }
    }
}
